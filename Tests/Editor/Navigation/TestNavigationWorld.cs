using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Legacy fixture projection retained only for tests that inspect occupancy diagnostics.</summary>
    internal enum NavigationCell { Empty, OneWay, Solid }
}

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Small immutable geometry-backed contract fixture for planner tests.</summary>
    internal sealed class TestNavigationWorld : NavigationWorld
    {
        private readonly NavigationCell[] cells;
        private readonly IReadOnlyDictionary<Vector2Int, float> supportSurfaceHeights;
        private readonly NavigationWorldSnapshot snapshot;

        public override Vector2 Origin { get; }
        public override float CellSize { get; }
        public override RectInt CellBounds { get; }

        public TestNavigationWorld(RectInt bounds, IEnumerable<Vector2Int> solidCells,
            IEnumerable<Vector2Int> oneWayCells, float cellSize = 1f)
            : this(bounds, solidCells, oneWayCells, null, cellSize) { }

        public TestNavigationWorld(RectInt bounds, IEnumerable<Vector2Int> solidCells,
            IEnumerable<Vector2Int> oneWayCells,
            IReadOnlyDictionary<Vector2Int, float> supportSurfaceHeights, float cellSize = 1f)
        {
            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (solidCells == null) throw new ArgumentNullException(nameof(solidCells));
            if (oneWayCells == null) throw new ArgumentNullException(nameof(oneWayCells));
            CellBounds = bounds;
            CellSize = cellSize;
            Origin = Vector2.zero;
            cells = new NavigationCell[bounds.width * bounds.height];
            foreach (Vector2Int cell in oneWayCells) Set(cell, NavigationCell.OneWay);
            foreach (Vector2Int cell in solidCells) Set(cell, NavigationCell.Solid);
            this.supportSurfaceHeights = supportSurfaceHeights ?? CreateGeometricSupportHeights();

            List<NavigationShapeData> shapes = new();
            int sourceId = 0;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    NavigationCell kind = GetCell(cell);
                    if (kind == NavigationCell.Empty) continue;
                    float minX = Origin.x + x * CellSize;
                    float maxX = minX + CellSize;
                    float supportY = this.supportSurfaceHeights.TryGetValue(cell, out float authoredY)
                        ? authoredY : Origin.y + (y + 1) * CellSize;
                    if (kind == NavigationCell.OneWay)
                        shapes.Add(new NavigationShapeData(sourceId++, 0, NavigationShapeType.Edge,
                            new[] { new Vector2(minX, supportY), new Vector2(maxX, supportY) }, 0f,
                            NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, 1f));
                    else
                    {
                        float minY = Origin.y + y * CellSize;
                        shapes.Add(new NavigationShapeData(sourceId++, 0, NavigationShapeType.Polygon,
                            new[] { new Vector2(minX, minY), new Vector2(maxX, minY),
                            new Vector2(maxX, supportY), new Vector2(minX, supportY) },
                            0f, NavigationSurfaceKind.Solid, true));
                    }
                }
            snapshot = NavigationWorldSnapshot.Create(Origin, CellSize, bounds, shapes,
                bounds.width > 0 && bounds.height > 0
                    ? new[] { new NavigationRegionData(bounds, 0) }
                    : Array.Empty<NavigationRegionData>());
        }

        /// <summary>Gets a test-only occupancy projection; planning uses the geometry contract below.</summary>
        public NavigationCell GetCell(Vector2Int cell)
            => CellBounds.Contains(cell)
                ? cells[(cell.y - CellBounds.yMin) * CellBounds.width + cell.x - CellBounds.xMin]
                : NavigationCell.Solid;
        public bool IsSolid(Vector2Int cell) => GetCell(cell) == NavigationCell.Solid;
        public bool IsOneWay(Vector2Int cell) => GetCell(cell) == NavigationCell.OneWay;

        public bool TryGetSupportSurfaceY(Vector2Int cell, out float surfaceY)
        {
            surfaceY = default;
            return supportSurfaceHeights != null && supportSurfaceHeights.TryGetValue(cell, out surfaceY);
        }

        public override bool IsBodyClear(Rect bodyBounds, float tolerance)
        {
            Rect world = new(Origin.x + CellBounds.xMin * CellSize, Origin.y + CellBounds.yMin * CellSize,
                CellBounds.width * CellSize, CellBounds.height * CellSize);
            if (bodyBounds.xMin < world.xMin || bodyBounds.xMax > world.xMax
                || bodyBounds.yMin < world.yMin || bodyBounds.yMax > world.yMax) return false;
            Rect tested = bodyBounds;
            tested.yMin += tolerance;
            for (int y = CellBounds.yMin; y < CellBounds.yMax; y++)
                for (int x = CellBounds.xMin; x < CellBounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (GetCell(cell) != NavigationCell.Solid) continue;
                    float solidMinY = Origin.y + y * CellSize;
                    float solidMaxY = supportSurfaceHeights.TryGetValue(cell, out float authoredSupportY)
                        ? authoredSupportY : Origin.y + (y + 1) * CellSize;
                    float overlapX = Mathf.Min(tested.xMax, Origin.x + (x + 1) * CellSize)
                        - Mathf.Max(tested.xMin, Origin.x + x * CellSize);
                    float overlapY = Mathf.Min(tested.yMax, solidMaxY) - Mathf.Max(tested.yMin, solidMinY);
                    if (overlapX > 0.0001f && overlapY > 0.0001f) return false;
                }
            return true;
        }
        public override bool IsBodyPathClear(Rect bodyBounds, Vector2 displacement, float tolerance)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / Mathf.Max(0.0001f, CellSize * 0.25f)));
            for (int index = 0; index <= samples; index++)
            {
                Rect body = bodyBounds;
                body.position += displacement * (index / (float)samples);
                if (!IsBodyClear(body, tolerance)) return false;
            }
            return true;
        }
        public override bool IsLineOfSightClear(Vector2 start, Vector2 end) => snapshot.IsLineOfSightClear(start, end);
        public override bool TryGetSupportBelow(Vector2 position, out NavigationSupport support)
            => snapshot.TryGetSupportBelow(position, out support);
        public override bool TryResolveSupport(Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
        {
            support = default;
            bool found = false;
            float bestDistance = float.PositiveInfinity;
            for (int y = CellBounds.yMin; y < CellBounds.yMax; y++)
                for (int x = CellBounds.xMin; x < CellBounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    NavigationCell cellKind = GetCell(cell);
                    if (cellKind == NavigationCell.Empty || feet.x < x * CellSize - 0.0001f
                        || feet.x > (x + 1) * CellSize + 0.0001f
                        || !supportSurfaceHeights.TryGetValue(cell, out float supportY)
                        || supportY > feet.y + snapDistance || supportY < feet.y - snapDistance
                        || !IsBodyClear(new Rect(feet.x - bodySize.x * 0.5f, supportY, bodySize.x, bodySize.y), snapDistance))
                        continue;
                    NavigationSupport candidate = new(new NavigationSurfaceId(
                        (y - CellBounds.yMin) * CellBounds.width + x - CellBounds.xMin, 0),
                        cellKind == NavigationCell.OneWay ? NavigationSurfaceKind.OneWay : NavigationSurfaceKind.Solid,
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
        protected override void CollectSupportCandidatesCore(Rect anchorBounds, Vector2 bodySize,
            List<NavigationSupportCandidate> results)
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
        public override void CollectOneWayCrossings(Vector2 previousFeet, Vector2 currentFeet, float bodyWidth,
            List<NavigationSurfaceCrossing> results)
            => snapshot.CollectOneWayCrossings(previousFeet, currentFeet, bodyWidth, results);
        public override bool AreInSameRegion(Vector2 first, Vector2 second) => snapshot.AreInSameRegion(first, second);

        private void Set(Vector2Int cell, NavigationCell value)
        {
            if (!CellBounds.Contains(cell)) return;
            cells[(cell.y - CellBounds.yMin) * CellBounds.width + cell.x - CellBounds.xMin] = value;
        }

        private Dictionary<Vector2Int, float> CreateGeometricSupportHeights()
        {
            Dictionary<Vector2Int, float> heights = new();
            for (int y = CellBounds.yMin; y < CellBounds.yMax; y++)
                for (int x = CellBounds.xMin; x < CellBounds.xMax; x++)
                {
                    Vector2Int cell = new(x, y);
                    if (GetCell(cell) != NavigationCell.Empty)
                        heights[cell] = Origin.y + (cell.y + 1) * CellSize;
                }
            return heights;
        }
    }

    /// <summary>Adapts the new world-space contract for legacy occupancy assertions while tests migrate.</summary>
    internal static class NavigationTestWorldExtensions
    {
        public static NavigationCell GetCell(this INavigationWorld world, Vector2Int cell)
        {
            if (world == null || !world.CellBounds.Contains(cell)) return NavigationCell.Solid;
            Vector2 min = world.Origin + Vector2.Scale(cell, Vector2.one) * world.CellSize;
            Rect interior = new(min.x + world.CellSize * 0.1f, min.y + world.CellSize * 0.1f,
                world.CellSize * 0.8f, world.CellSize * 0.8f);
            if (!world.IsBodyClear(interior, 0f)) return NavigationCell.Solid;
            return world.TryResolveSupport(
                new Vector2(min.x + world.CellSize * 0.5f, min.y + world.CellSize),
                new Vector2(world.CellSize * 0.1f, world.CellSize * 0.1f),
                world.CellSize * 0.25f,
                out NavigationSupport support)
                && support.Kind == NavigationSurfaceKind.OneWay
                    ? NavigationCell.OneWay
                    : NavigationCell.Empty;
        }

        public static bool TryGetSupportSurfaceY(this INavigationWorld world, Vector2Int cell, out float surfaceY)
        {
            surfaceY = default;
            if (world == null || !world.CellBounds.Contains(cell)) return false;
            Vector2 min = world.Origin + Vector2.Scale(cell, Vector2.one) * world.CellSize;
            return world.TryResolveSupport(
                new Vector2(min.x + world.CellSize * 0.5f, min.y + world.CellSize),
                new Vector2(world.CellSize * 0.1f, world.CellSize * 0.1f),
                world.CellSize * 0.25f,
                out NavigationSupport support)
                && (surfaceY = support.Position.y) == support.Position.y;
        }

        public static bool TryResolveGroundSupport(this INavigationWorld world, Vector2 observedLowerCenter,
            Vector2 bodySize, float snapDistance, out Vector2 snappedLowerCenter,
            out Vector2Int supportCell, out NavigationCell supportKind)
        {
            snappedLowerCenter = default;
            supportCell = default;
            supportKind = NavigationCell.Empty;
            if (!world.TryResolveSupport(observedLowerCenter, bodySize, snapDistance,
                out NavigationSupport support)) return false;
            snappedLowerCenter = support.Position;
            supportCell = NavigationWorldQueries.WorldToCell(world, support.Position);
            supportKind = support.Kind == NavigationSurfaceKind.OneWay
                ? NavigationCell.OneWay : NavigationCell.Solid;
            return true;
        }

        public static bool TryResolveGroundSupport(this INavigationWorld world, Vector2 observedLowerCenter,
            Vector2 bodySize, out Vector2 snappedLowerCenter, out Vector2Int supportCell,
            out NavigationCell supportKind)
            => TryResolveGroundSupport(world, observedLowerCenter, bodySize,
                NavigationWorldQueries.SupportSnapDistance, out snappedLowerCenter, out supportCell, out supportKind);
    }
}
