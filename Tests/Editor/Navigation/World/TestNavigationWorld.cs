using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Small immutable geometry-backed contract fixture for planner tests. The double owns a
    /// cell-authored terrain model of its own; the world contract it implements is continuous.
    /// </summary>
    internal sealed class TestNavigationWorld : NavigationWorld
    {
        private readonly NavigationSurfaceKind?[] surfaces;
        private readonly IReadOnlyDictionary<Vector2Int, float> supportSurfaceHeights;
        private readonly NavigationWorldSnapshot snapshot;
        private readonly Vector2 origin;
        private readonly float terrainCell;
        private readonly RectInt terrainBounds;

        public override Rect WorldBounds { get; }

        public TestNavigationWorld(RectInt bounds, IEnumerable<Vector2Int> solidCells, IEnumerable<Vector2Int> oneWayCells, float cellSize = 1f)
            : this(bounds, solidCells, oneWayCells, null, cellSize) { }

        public TestNavigationWorld(RectInt bounds, IEnumerable<Vector2Int> solidCells, IEnumerable<Vector2Int> oneWayCells, IReadOnlyDictionary<Vector2Int, float> supportSurfaceHeights, float cellSize = 1f)
        {
            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (solidCells == null) throw new ArgumentNullException(nameof(solidCells));
            if (oneWayCells == null) throw new ArgumentNullException(nameof(oneWayCells));
            terrainBounds = bounds;
            terrainCell = cellSize;
            origin = Vector2.zero;
            WorldBounds = new Rect(bounds.x * cellSize, bounds.y * cellSize,
                bounds.width * cellSize, bounds.height * cellSize);
            surfaces = new NavigationSurfaceKind?[bounds.width * bounds.height];
            foreach (Vector2Int cell in oneWayCells) Set(cell, NavigationSurfaceKind.OneWay);
            foreach (Vector2Int cell in solidCells) Set(cell, NavigationSurfaceKind.Solid);
            this.supportSurfaceHeights = supportSurfaceHeights ?? CreateGeometricSupportHeights();

            List<NavigationShapeData> shapes = new();
            int sourceId = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    NavigationSurfaceKind? kind = GetSurfaceKind(cell);
                    if (!kind.HasValue) continue;
                    float minX = origin.x + x * terrainCell;
                    float maxX = minX + terrainCell;
                    float supportY = this.supportSurfaceHeights.TryGetValue(cell, out float authoredY)
                        ? authoredY : origin.y + (y + 1) * terrainCell;
                    if (kind == NavigationSurfaceKind.OneWay)
                        shapes.Add(new NavigationShapeData(sourceId++, 0, NavigationShapeType.Edge,
                            new[] { new Vector2(minX, supportY), new Vector2(maxX, supportY) }, 0f,
                            NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, directedNormalSign: 0f));
                    else
                    {
                        float minY = origin.y + y * terrainCell;
                        shapes.Add(new NavigationShapeData(sourceId++, 0, NavigationShapeType.Polygon,
                            new[] { new Vector2(minX, minY), new Vector2(maxX, minY),
                            new Vector2(maxX, supportY), new Vector2(minX, supportY) },
                            0f, NavigationSurfaceKind.Solid, true));
                    }
                }
            snapshot = NavigationWorldSnapshot.Create(WorldBounds, shapes,
                bounds.width > 0 && bounds.height > 0
                    ? new[] { new NavigationRegionData(WorldBounds, 0) }
                    : Array.Empty<NavigationRegionData>(),
                terrainCell, SupportCandidateCache.DefaultEntryLimit, SupportCandidateCache.DefaultCandidateLimit);
        }

        public override bool IsBodyClear(Rect bodyBounds, float tolerance)
        {
            Rect world = WorldBounds;
            if (bodyBounds.xMin < world.xMin || bodyBounds.xMax > world.xMax
                || bodyBounds.yMin < world.yMin || bodyBounds.yMax > world.yMax) return false;
            Rect tested = bodyBounds;
            tested.yMin += tolerance;
            for (int y = terrainBounds.yMin; y < terrainBounds.yMax; y++)
                for (int x = terrainBounds.xMin; x < terrainBounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (GetSurfaceKind(cell) != NavigationSurfaceKind.Solid) continue;
                    float solidMinY = origin.y + y * terrainCell;
                    float solidMaxY = supportSurfaceHeights.TryGetValue(cell, out float authoredSupportY)
                        ? authoredSupportY : origin.y + (y + 1) * terrainCell;
                    float overlapX = Mathf.Min(tested.xMax, origin.x + (x + 1) * terrainCell)
                        - Mathf.Max(tested.xMin, origin.x + x * terrainCell);
                    float overlapY = Mathf.Min(tested.yMax, solidMaxY) - Mathf.Max(tested.yMin, solidMinY);
                    if (overlapX > 0.0001f && overlapY > 0.0001f) return false;
                }
            return true;
        }

        public override bool IsBodyPathClear(Rect bodyBounds, Vector2 displacement, float tolerance)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / Mathf.Max(0.0001f, NavigationConstant.BodySweepSampleSpacing)));
            for (int index = 0; index <= samples; index++)
            {
                Rect body = bodyBounds;
                body.position += displacement * (index / (float)samples);
                if (!IsBodyClear(body, tolerance)) return false;
            }
            return true;
        }

        public override bool IsLineOfSightClear(Vector2 start, Vector2 end) => snapshot.IsLineOfSightClear(start, end);

        public override bool TryGetSupportBelow(Vector2 position, out NavigationSupport support) => snapshot.TryGetSupportBelow(position, out support);

        public override bool TryResolveSupport(Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
        {
            support = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            for (int y = terrainBounds.yMin; y < terrainBounds.yMax; y++)
                for (int x = terrainBounds.xMin; x < terrainBounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    NavigationSurfaceKind? cellKind = GetSurfaceKind(cell);
                    if (!cellKind.HasValue || feet.x < x * terrainCell - 0.0001f
                        || feet.x > (x + 1) * terrainCell + 0.0001f
                        || !supportSurfaceHeights.TryGetValue(cell, out float supportY)
                        || supportY > feet.y + snapDistance || supportY < feet.y - snapDistance
                        || !IsBodyClear(new Rect(feet.x - bodySize.x * 0.5f, supportY, bodySize.x, bodySize.y), snapDistance))
                        continue;
                    NavigationSupport candidate = new(new NavigationSurfaceId(
                        (y - terrainBounds.yMin) * terrainBounds.width + x - terrainBounds.xMin, 0),
                        cellKind.Value,
                        new Vector2(feet.x, supportY), Vector2.up);
                    float distance = Mathf.Abs(feet.y - supportY);
                    if (!found || distance < bestDistance - 0.0001f)
                    {
                        support = candidate;
                        bestDistance = distance;
                        found = true;
                    }
                }
            return found;
        }

        protected override void CollectSupportCandidatesCore(Rect anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results)
        {
            IReadOnlyList<NavigationSupportCandidate> candidates = snapshot.GetSupportCandidates(anchorBounds, bodySize);
            for (int index = 0; index < candidates.Count; index++)
            {
                NavigationSupportCandidate candidate = candidates[index];
                Vector2 position = candidate.Support.Position;
                if (IsBodyClear(new Rect(position.x - bodySize.x * 0.5f, position.y,
                    bodySize.x, bodySize.y), 0.0001f)) results.Add(candidate);
            }
        }

        public override void CollectOneWayCrossings(Vector2 previousFeet, Vector2 currentFeet, float bodyWidth, List<NavigationSurfaceCrossing> results)
            => snapshot.CollectOneWayCrossings(previousFeet, currentFeet, bodyWidth, results);

        public override bool AreInSameRegion(Vector2 first, Vector2 second) => snapshot.AreInSameRegion(first, second);

        private NavigationSurfaceKind? GetSurfaceKind(Vector2Int cell)
            => terrainBounds.Contains(cell)
                ? surfaces[(cell.y - terrainBounds.yMin) * terrainBounds.width + cell.x - terrainBounds.xMin]
                : NavigationSurfaceKind.Solid;

        private void Set(Vector2Int cell, NavigationSurfaceKind value)
        {
            if (!terrainBounds.Contains(cell)) return;
            surfaces[(cell.y - terrainBounds.yMin) * terrainBounds.width + cell.x - terrainBounds.xMin] = value;
        }

        private Dictionary<Vector2Int, float> CreateGeometricSupportHeights()
        {
            Dictionary<Vector2Int, float> heights = new();
            for (int y = terrainBounds.yMin; y < terrainBounds.yMax; y++)
                for (int x = terrainBounds.xMin; x < terrainBounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (GetSurfaceKind(cell).HasValue)
                        heights[cell] = origin.y + (cell.y + 1) * terrainCell;
                }
            return heights;
        }
    }

    /// <summary>
    /// Creates small composable navigation worlds for package-level planner contracts.
    /// </summary>
    internal static class NavigationTestWorlds
    {
        /// <summary>
        /// Creates a level solid floor with one-cell headroom around the requested span.
        /// </summary>
        public static TestNavigationWorld Ground(int firstX, int count, int height = 6)
        {
            return new(new RectInt(firstX, 0, count + 1, height), Floor(firstX, count), Array.Empty<Vector2Int>());
        }

        /// <summary>
        /// Creates mutable cell input for tests that need to add obstacles to an otherwise level floor.
        /// </summary>
        public static List<Vector2Int> Floor(int firstX, int count)
        {
            List<Vector2Int> cells = new();
            for (int x = firstX; x < firstX + count; x++) cells.Add(new Vector2Int(x, 0));
            return cells;
        }
    }
}
