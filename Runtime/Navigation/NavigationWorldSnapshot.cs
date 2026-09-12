using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Managed immutable navigation snapshot backed by captured geometry and spatial buckets.</summary>
    public sealed class NavigationWorldSnapshot : NavigationWorld
    {
        private const float Epsilon = 0.0001f;
        private readonly Vector2 origin;
        private readonly float cellSize;
        private readonly RectInt cellBounds;
        private readonly Shape[] shapes;
        private readonly Dictionary<Vector2Int, int[]> buckets;
        private readonly NavigationSupportCandidate[] supportCandidates;
        private readonly Dictionary<Vector2Int, int[]> supportCandidateBuckets;
        private readonly Dictionary<Vector2Int, int> regions;

        private NavigationWorldSnapshot(Vector2 origin,
            float cellSize,
            RectInt cellBounds,
            Shape[] shapes,
            Dictionary<Vector2Int, int[]> buckets,
            NavigationSupportCandidate[] supportCandidates,
            Dictionary<Vector2Int, int[]> supportCandidateBuckets,
            Dictionary<Vector2Int, int> regions,
            int supportCacheEntryLimit,
            int supportCacheCandidateLimit)
            : base(supportCacheEntryLimit, supportCacheCandidateLimit)
        {
            this.origin = origin;
            this.cellSize = cellSize;
            this.cellBounds = cellBounds;
            this.shapes = shapes;
            this.buckets = buckets;
            this.supportCandidates = supportCandidates;
            this.supportCandidateBuckets = supportCandidateBuckets;
            this.regions = regions;
        }

        public override Vector2 Origin => origin;
        public override float CellSize => cellSize;
        public override RectInt CellBounds => cellBounds;

        /// <summary>Copies detached geometry and builds immutable spatial and region indexes.</summary>
        public static NavigationWorldSnapshot Create(Vector2 origin, float cellSize, RectInt cellBounds, IReadOnlyList<NavigationShapeData> shapeData, IReadOnlyList<NavigationRegionData> regionData)
            => Create(origin, cellSize, cellBounds, shapeData, regionData, SupportCandidateCache.DefaultEntryLimit, SupportCandidateCache.DefaultCandidateLimit);

        /// <summary>Builds a snapshot with package-internal cache limits for focused cache validation.</summary>
        internal static NavigationWorldSnapshot Create(Vector2 origin, float cellSize, RectInt cellBounds, IReadOnlyList<NavigationShapeData> shapeData, IReadOnlyList<NavigationRegionData> regionData, int supportCacheEntryLimit, int supportCacheCandidateLimit)
        {
            Validate.Finite(origin, nameof(origin));
            if (!NavigationNumeric.IsFinite(cellSize) || cellSize <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            if (cellBounds.width <= 0 || cellBounds.height <= 0) throw new ArgumentException("Navigation bounds must be positive.", nameof(cellBounds));
            if (shapeData == null) throw new ArgumentNullException(nameof(shapeData));
            if (regionData == null) throw new ArgumentNullException(nameof(regionData));

            Shape[] shapes = new Shape[shapeData.Count];
            Dictionary<Vector2Int, List<int>> mutableBuckets = new();
            for (int index = 0; index < shapeData.Count; index++)
            {
                Shape shape = new(shapeData[index]);
                shapes[index] = shape;
                GetCellRange(shape.Min, shape.Max, origin, cellSize, cellBounds,
                    out int minX, out int maxX, out int minY, out int maxY);
                for (int y = minY; y <= maxY; y++)
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2Int key = new(x, y);
                        if (!mutableBuckets.TryGetValue(key, out List<int> entries))
                            mutableBuckets.Add(key, entries = new List<int>());
                        entries.Add(index);
                    }
            }

            Dictionary<Vector2Int, int[]> buckets = new();
            foreach (KeyValuePair<Vector2Int, List<int>> pair in mutableBuckets)
            {
                pair.Value.Sort((left, right) => CompareShape(shapes[left], shapes[right]));
                buckets.Add(pair.Key, pair.Value.ToArray());
            }

            Dictionary<Vector2Int, int> regions = new();
            for (int index = 0; index < regionData.Count; index++)
            {
                NavigationRegionData region = regionData[index];
                for (int y = region.CellBounds.yMin; y < region.CellBounds.yMax; y++)
                    for (int x = region.CellBounds.xMin; x < region.CellBounds.xMax; x++)
                    {
                        Vector2Int key = new(x, y);
                        if (regions.ContainsKey(key)) throw new ArgumentException("Navigation regions must not overlap.", nameof(regionData));
                        regions.Add(key, region.RegionId);
                    }
            }

            List<NavigationSupport> supports = new();
            for (int shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
                BuildSupportCandidates(shapes[shapeIndex], origin, cellSize, cellBounds, supports);

            supports.Sort(CompareSupport);
            NavigationSupportCandidate[] supportCandidates = new NavigationSupportCandidate[supports.Count];
            Dictionary<Vector2Int, List<int>> mutableCandidateBuckets = new();
            for (int index = 0; index < supports.Count; index++)
            {
                NavigationSupportCandidate candidate = new(index, supports[index]);
                supportCandidates[index] = candidate;
                Vector2Int cell = WorldToCell(candidate.Support.Position, origin, cellSize, cellBounds);
                if (!mutableCandidateBuckets.TryGetValue(cell, out List<int> entries))
                    mutableCandidateBuckets.Add(cell, entries = new List<int>());
                entries.Add(index);
            }

            Dictionary<Vector2Int, int[]> supportCandidateBuckets = new();
            foreach (KeyValuePair<Vector2Int, List<int>> pair in mutableCandidateBuckets)
                supportCandidateBuckets.Add(pair.Key, pair.Value.ToArray());

            return new NavigationWorldSnapshot(origin, cellSize, cellBounds, shapes, buckets, supportCandidates, supportCandidateBuckets, regions, supportCacheEntryLimit, supportCacheCandidateLimit);
        }

        public override bool IsBodyClear(Rect body, float surfaceContactTolerance)
        {
            Validate.PositiveRect(body, nameof(body));
            Validate.NonNegativeFinite(surfaceContactTolerance, nameof(surfaceContactTolerance));
            Rect world = GetWorldBounds();
            if (!world.Contains(body.min) || !world.Contains(body.max)) return false;
            Rect tested = body;
            tested.yMin += surfaceContactTolerance;
            if (tested.height <= Epsilon) return true;
            foreach (int index in QueryShapeIndexes(tested))
                if (shapes[index].Kind == NavigationSurfaceKind.Solid && IsShapeIntersection(shapes[index], tested)) return false;
            return true;
        }

        public override bool IsBodyPathClear(Rect startBody, Vector2 displacement, float surfaceContactTolerance)
        {
            Validate.PositiveRect(startBody, nameof(startBody));
            Validate.Finite(displacement, nameof(displacement));
            Validate.NonNegativeFinite(surfaceContactTolerance, nameof(surfaceContactTolerance));
            int samples = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / Mathf.Max(Epsilon, cellSize * 0.25f)));
            for (int index = 0; index <= samples; index++)
            {
                Rect body = startBody;
                body.position += displacement * (index / (float)samples);
                if (!IsBodyClear(body, surfaceContactTolerance)) return false;
            }
            return true;
        }

        public override bool IsLineOfSightClear(Vector2 start, Vector2 end)
        {
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            Rect world = GetWorldBounds();
            if (!world.Contains(start) || !world.Contains(end)) return false;
            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);
            Rect query = new(min, max - min);
            foreach (int index in QueryShapeIndexes(query))
                if (shapes[index].Kind == NavigationSurfaceKind.Solid && IsShapeSegmentIntersection(shapes[index], start, end)) return false;
            return true;
        }

        /// <summary>
        /// Resolves support under a body, preferring a center hit before considering an overlapping
        /// foot-edge contact. Returned anchors always retain the supplied body-center x coordinate.
        /// </summary>
        public override bool TryResolveSupport(Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
        {
            Validate.Finite(feet, nameof(feet));
            Validate.PositiveVector(bodySize, nameof(bodySize));
            Validate.NonNegativeFinite(snapDistance, nameof(snapDistance));
            Rect query = new(feet.x - bodySize.x * 0.5f, feet.y - snapDistance - Epsilon,
                bodySize.x, bodySize.y + snapDistance + Epsilon);

            if (TryFindCenterSupport(query, feet, bodySize, snapDistance, out support)) return true;
            return TryFindFootEdgeSupport(query, feet, bodySize, snapDistance, out support);
        }

        /// <inheritdoc />
        public override bool TryGetSupportBelow(Vector2 position, out NavigationSupport support)
        {
            Validate.Finite(position, nameof(position));
            support = default;
            Rect worldBounds = GetWorldBounds();
            if (position.x < worldBounds.xMin || position.x > worldBounds.xMax
                || position.y < worldBounds.yMin - Epsilon) return false;

            // Use a narrow spatial query, not the discretely sampled standing candidates.
            // A sloped or curved surface must be evaluated at the caller's exact X.
            Rect query = Rect.MinMaxRect(position.x - Epsilon, worldBounds.yMin - Epsilon,
                position.x + Epsilon, Mathf.Min(position.y + Epsilon, worldBounds.yMax));
            bool found = false;
            foreach (int index in QueryShapeIndexes(query))
            {
                Shape shape = shapes[index];
                if (!shape.HasSupport
                    || !TryGetSurfaceAtX(shape, position.x, out float y, out Vector2 normal, Mathf.Min(position.y, worldBounds.yMax), supportedOnly: true)
                    || y < worldBounds.yMin - Epsilon) continue;

                NavigationSupport candidate = new(new NavigationSurfaceId(shape.SourceId, shape.FeatureId),
                    shape.Kind, new Vector2(position.x, y), normal);
                if (!found || y > support.Position.y + Epsilon
                    || Mathf.Abs(y - support.Position.y) <= Epsilon && CompareSupport(candidate, support) < 0)
                {
                    support = candidate;
                    found = true;
                }
            }
            return found;
        }

        private bool TryFindCenterSupport(Rect query, Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
        {
            NavigationSupport best = default;
            float bestDistance = float.PositiveInfinity;
            bool found = false;
            foreach (int index in QueryShapeIndexes(query))
            {
                Shape shape = shapes[index];
                ConsiderSupportAtX(shape, feet.x, feet, bodySize, snapDistance,
                    ref found, ref best, ref bestDistance);
            }
            support = best;
            return found;
        }

        private bool TryFindFootEdgeSupport(Rect query, Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
        {
            NavigationSupport best = default;
            float bestDistance = float.PositiveInfinity;
            bool found = false;
            float footMinX = feet.x - bodySize.x * 0.5f;
            float footMaxX = feet.x + bodySize.x * 0.5f;
            foreach (int index in QueryShapeIndexes(query))
            {
                Shape shape = shapes[index];
                if (!shape.HasSupport) continue;

                if (shape.ShapeType == NavigationShapeType.Circle || shape.ShapeType == NavigationShapeType.Capsule)
                {
                    ConsiderSupportOnInterval(shape, footMinX, footMaxX, feet, bodySize, snapDistance,
                        ref found, ref best, ref bestDistance);
                    continue;
                }

                int edgeCount = shape.ShapeType == NavigationShapeType.Polygon ? shape.Vertices.Length : shape.Vertices.Length - 1;
                for (int edge = 0; edge < edgeCount; edge++)
                {
                    Vector2 first = shape.Vertices[edge];
                    Vector2 second = shape.Vertices[(edge + 1) % shape.Vertices.Length];
                    if (Mathf.Abs(second.x - first.x) <= Epsilon) continue;
                    ConsiderSupportOnInterval(shape, Mathf.Max(footMinX, Mathf.Min(first.x, second.x)),
                        Mathf.Min(footMaxX, Mathf.Max(first.x, second.x)), feet, bodySize, snapDistance,
                        ref found, ref best, ref bestDistance);
                }
            }

            support = best;
            return found;
        }

        private void ConsiderSupportOnInterval(Shape shape, float intervalMinX, float intervalMaxX, Vector2 feet, Vector2 bodySize, float snapDistance, ref bool found, ref NavigationSupport best, ref float bestDistance)
        {
            if (intervalMinX > intervalMaxX + Epsilon) return;
            float probeX = Mathf.Clamp(feet.x, intervalMinX, intervalMaxX);
            ConsiderSupportAtX(shape, probeX, feet, bodySize, snapDistance,
                ref found, ref best, ref bestDistance);
        }

        private void ConsiderSupportAtX(Shape shape, float probeX, Vector2 feet, Vector2 bodySize, float snapDistance, ref bool found, ref NavigationSupport best, ref float bestDistance)
        {
            if (!shape.HasSupport
                || !TryGetSurfaceAtX(shape, probeX, out float y, out Vector2 normal, feet.y + snapDistance)
                || !IsAllowedSupport(shape, normal)
                || y > feet.y + snapDistance || y < feet.y - snapDistance) return;

            Rect body = new(feet.x - bodySize.x * 0.5f, y, bodySize.x, bodySize.y);
            if (!IsBodyClear(body, snapDistance)) return;

            NavigationSupport candidate = new(new NavigationSurfaceId(shape.SourceId, shape.FeatureId),
                shape.Kind, new Vector2(feet.x, y), normal);
            float distance = Mathf.Abs(feet.y - y);
            if (!found || distance < bestDistance - Epsilon
                || distance <= bestDistance + Epsilon && CompareSupport(candidate, best) < 0)
            {
                best = candidate;
                bestDistance = distance;
                found = true;
            }
        }

        protected override void CollectSupportCandidatesCore(Rect anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            Validate.NonNegativeRect(anchorBounds, nameof(anchorBounds));
            Validate.PositiveVector(bodySize, nameof(bodySize));
            foreach (int candidateId in QuerySupportCandidateIds(anchorBounds))
            {
                NavigationSupportCandidate candidate = supportCandidates[candidateId];
                NavigationSupport support = candidate.Support;
                if (!anchorBounds.Contains(support.Position)) continue;
                Rect body = new(support.Position.x - bodySize.x * 0.5f, support.Position.y, bodySize.x, bodySize.y);
                if (IsBodyClear(body, Epsilon)) results.Add(candidate);
            }
            results.Sort((left, right) => left.Id.CompareTo(right.Id));
        }

        public override void CollectOneWayCrossings(Vector2 previousFeet, Vector2 currentFeet, float bodyWidth, List<NavigationSurfaceCrossing> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            Validate.Finite(previousFeet, nameof(previousFeet));
            Validate.Finite(currentFeet, nameof(currentFeet));
            if (!NavigationNumeric.IsFinite(bodyWidth) || bodyWidth <= 0f) throw new ArgumentOutOfRangeException(nameof(bodyWidth));
            results.Clear();
            Vector2 delta = currentFeet - previousFeet;
            if (delta.sqrMagnitude <= Epsilon * Epsilon) return;
            int steps = Mathf.Max(8, Mathf.CeilToInt(delta.magnitude / Mathf.Max(Epsilon, cellSize * 0.2f)));
            float halfWidth = bodyWidth * 0.5f;
            Rect sweptBounds = Rect.MinMaxRect(
                Mathf.Min(previousFeet.x, currentFeet.x) - halfWidth - Epsilon,
                Mathf.Min(previousFeet.y, currentFeet.y) - Epsilon,
                Mathf.Max(previousFeet.x, currentFeet.x) + halfWidth + Epsilon,
                Mathf.Max(previousFeet.y, currentFeet.y) + Epsilon);
            foreach (int shapeIndex in QueryShapeIndexes(sweptBounds))
            {
                Shape shape = shapes[shapeIndex];
                if (shape.Kind != NavigationSurfaceKind.OneWay
                    || shape.Max.x < sweptBounds.xMin - Epsilon || shape.Min.x > sweptBounds.xMax + Epsilon
                    || shape.Max.y < sweptBounds.yMin - Epsilon || shape.Min.y > sweptBounds.yMax + Epsilon)
                    continue;
                for (int offsetIndex = -1; offsetIndex <= 1; offsetIndex++)
                {
                    float offset = offsetIndex * halfWidth;
                    bool previousValid = TryGetSurfaceAtX(shape, previousFeet.x + offset, out _, out _);
                    float previousDifference = previousValid && TryGetSurfaceAtX(shape, previousFeet.x + offset, out float firstY, out _)
                        ? previousFeet.y - firstY : 0f;
                    for (int stepIndex = 1; stepIndex <= steps; stepIndex++)
                    {
                        float fraction = stepIndex / (float)steps;
                        Vector2 feet = Vector2.Lerp(previousFeet, currentFeet, fraction);
                        bool currentValid = TryGetSurfaceAtX(shape, feet.x + offset, out float currentY, out Vector2 currentNormal);
                        float currentDifference = currentValid ? feet.y - currentY : 0f;
                        bool crossedFromAbove = previousDifference >= -Epsilon && currentDifference < -Epsilon;
                        bool crossedFromBelow = previousDifference < -Epsilon && currentDifference >= -Epsilon;
                        if (previousValid && currentValid && IsAllowedSupport(shape, currentNormal)
                            && (crossedFromAbove || crossedFromBelow))
                        {
                            float denominator = previousDifference - currentDifference;
                            float local = denominator <= Epsilon ? 1f : Mathf.Clamp01(previousDifference / denominator);
                            float eventFraction = ((stepIndex - 1) + local) / steps;
                            float crossingX = Mathf.Lerp(previousFeet.x, currentFeet.x, eventFraction) + offset;
                            TryGetSurfaceAtX(shape, crossingX, out float crossingY, out Vector2 crossingNormal,
                                float.PositiveInfinity);
                            AddUnique(results, new NavigationSurfaceCrossing(
                                new NavigationSurfaceId(shape.SourceId, shape.FeatureId),
                                new Vector2(crossingX, crossingY), crossingNormal, eventFraction));
                        }
                        previousValid = currentValid;
                        previousDifference = currentDifference;
                    }
                }
            }
            results.Sort((left, right) => left.Fraction.CompareTo(right.Fraction) != 0
                ? left.Fraction.CompareTo(right.Fraction) : CompareSurface(left.Surface, right.Surface));
        }

        public override bool AreInSameRegion(Vector2 first, Vector2 second)
        {
            Validate.Finite(first, nameof(first));
            Validate.Finite(second, nameof(second));
            return TryGetRegion(first, out int firstRegion) && TryGetRegion(second, out int secondRegion)
                && firstRegion == secondRegion;
        }

        private bool TryGetRegion(Vector2 position, out int region)
        {
            Vector2 local = (position - origin) / cellSize;
            Vector2Int cell = new(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
            return regions.TryGetValue(cell, out region);
        }

        private IEnumerable<int> QueryShapeIndexes(Rect bounds)
        {
            HashSet<int> seen = new();
            GetCellRange(bounds.min, bounds.max, origin, cellSize, cellBounds,
                out int minX, out int maxX, out int minY, out int maxY);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (buckets.TryGetValue(new Vector2Int(x, y), out int[] entries))
                        for (int index = 0; index < entries.Length; index++)
                            if (seen.Add(entries[index])) yield return entries[index];
        }

        private IEnumerable<int> QuerySupportCandidateIds(Rect bounds)
        {
            GetCellRange(bounds.min, bounds.max, origin, cellSize, cellBounds, out int minX, out int maxX, out int minY, out int maxY);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (supportCandidateBuckets.TryGetValue(new Vector2Int(x, y), out int[] entries))
                        for (int index = 0; index < entries.Length; index++)
                            yield return entries[index];
        }

        private Rect GetWorldBounds() => new(origin + new Vector2(cellBounds.xMin, cellBounds.yMin) * cellSize, new Vector2(cellBounds.width, cellBounds.height) * cellSize);

        private static bool IsAllowedSupport(Shape shape, Vector2 normal)
            => normal.y > Epsilon && (shape.Kind != NavigationSurfaceKind.OneWay || Vector2.Dot(normal, shape.OneWayDirection) >= shape.OneWayCosHalfArc - Epsilon);

        private static bool TryGetSurfaceAtX(Shape shape, float x, out float y, out Vector2 normal, float maxSurfaceY = float.PositiveInfinity, bool supportedOnly = false)
        {
            y = float.NegativeInfinity;
            normal = Vector2.up;
            bool found = false;
            switch (shape.ShapeType)
            {
                case NavigationShapeType.Circle:
                    found = TryGetCircleSurface(shape.Vertices[0], shape.Radius, x, out y, out normal);
                    if (found && (y > maxSurfaceY + Epsilon || supportedOnly && !IsAllowedSupport(shape, normal))) found = false;
                    break;
                case NavigationShapeType.Capsule:
                    found = TryGetCapsuleSurface(shape, x, out y, out normal);
                    if (found && (y > maxSurfaceY + Epsilon || supportedOnly && !IsAllowedSupport(shape, normal))) found = false;
                    break;
                case NavigationShapeType.Edge:
                    for (int index = 1; index < shape.Vertices.Length; index++)
                        if (TryGetSegmentSurface(shape.Vertices[index - 1], shape.Vertices[index], x, out float edgeY, out Vector2 edgeNormal)
                            && edgeY <= maxSurfaceY + Epsilon
                            && (!supportedOnly || IsAllowedSupport(shape, edgeNormal))
                            && (!found || edgeY > y)) { y = edgeY; normal = edgeNormal; found = true; }
                    break;
                default:
                    float winding = PolygonWinding(shape.Vertices);
                    for (int index = 0; index < shape.Vertices.Length; index++)
                    {
                        Vector2 a = shape.Vertices[index];
                        Vector2 b = shape.Vertices[(index + 1) % shape.Vertices.Length];
                        if (TryGetSegmentSurface(a, b, x, out float polygonY, out Vector2 polygonNormal, winding >= 0f ? 1f : -1f, orientUp: !supportedOnly)
                            && polygonY <= maxSurfaceY + Epsilon
                            && (!supportedOnly || IsAllowedSupport(shape, polygonNormal))
                            && (!found || polygonY > y)) { y = polygonY; normal = polygonNormal; found = true; }
                    }
                    break;
            }
            return found;
        }

        private static bool TryGetCapsuleSurface(Shape shape, float x, out float y, out Vector2 normal)
        {
            y = float.NegativeInfinity; normal = Vector2.up; bool found = false;
            Vector2 a = shape.Vertices[0];
            Vector2 b = shape.Vertices[Mathf.Min(1, shape.Vertices.Length - 1)];
            Vector2 direction = b - a;
            float length = direction.magnitude;
            if (length <= Epsilon) return TryGetCircleSurface(a, shape.Radius, x, out y, out normal);
            Vector2 side = new Vector2(-direction.y, direction.x) / length;
            if (TryGetSegmentSurface(a + side * shape.Radius, b + side * shape.Radius, x, out float candidateY, out Vector2 candidateNormal)) { y = candidateY; normal = candidateNormal; found = true; }
            if (TryGetSegmentSurface(a - side * shape.Radius, b - side * shape.Radius, x, out candidateY, out candidateNormal) && (!found || candidateY > y)) { y = candidateY; normal = candidateNormal; found = true; }
            if (TryGetCircleSurface(a, shape.Radius, x, out candidateY, out candidateNormal) && (!found || candidateY > y)) { y = candidateY; normal = candidateNormal; found = true; }
            if (TryGetCircleSurface(b, shape.Radius, x, out candidateY, out candidateNormal) && (!found || candidateY > y)) { y = candidateY; normal = candidateNormal; found = true; }
            return found;
        }

        private static bool TryGetCircleSurface(Vector2 center, float radius, float x, out float y, out Vector2 normal)
        {
            y = float.NegativeInfinity; normal = Vector2.up;
            float dx = x - center.x;
            if (Mathf.Abs(dx) > radius + Epsilon || radius <= Epsilon) return false;
            float dy = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dx * dx));
            y = center.y + dy;
            normal = new Vector2(dx, dy).normalized;
            return true;
        }

        private static bool TryGetSegmentSurface(Vector2 a, Vector2 b, float x, out float y, out Vector2 normal, float polygonSign = 0f, bool orientUp = true)
        {
            y = 0f; normal = Vector2.up;
            float deltaX = b.x - a.x;
            if (Mathf.Abs(deltaX) <= Epsilon) return false;
            float parameter = (x - a.x) / deltaX;
            if (parameter < -Epsilon || parameter > 1f + Epsilon) return false;
            y = Mathf.Lerp(a.y, b.y, Mathf.Clamp01(parameter));
            Vector2 direction = b - a;
            Vector2 candidate = polygonSign == 0f
                ? new Vector2(-direction.y, direction.x).normalized
                : new Vector2(direction.y, -direction.x).normalized * polygonSign;
            // The downward query must distinguish a polygon's underside from its landing
            // surface. Existing overlap/standing queries retain their previous orientation.
            if (orientUp && candidate.y < 0f) candidate = -candidate;
            normal = candidate;
            return true;
        }

        private static bool IsShapeIntersection(Shape shape, Rect rect)
        {
            switch (shape.ShapeType)
            {
                case NavigationShapeType.Circle: return CircleIntersectsRect(shape.Vertices[0], shape.Radius, rect);
                case NavigationShapeType.Capsule: return SegmentIntersectsExpandedRect(shape.Vertices[0], shape.Vertices[1], shape.Radius, rect);
                case NavigationShapeType.Edge:
                    for (int index = 1; index < shape.Vertices.Length; index++)
                        if (SegmentIntersectsRectInterior(shape.Vertices[index - 1], shape.Vertices[index], rect)) return true;
                    return false;
                default: return PolygonIntersectsRect(shape.Vertices, rect);
            }
        }

        private static bool IsShapeSegmentIntersection(Shape shape, Vector2 start, Vector2 end)
        {
            switch (shape.ShapeType)
            {
                case NavigationShapeType.Circle: return DistancePointToSegment(shape.Vertices[0], start, end) <= shape.Radius + Epsilon;
                case NavigationShapeType.Capsule: return DistanceSegments(shape.Vertices[0], shape.Vertices[1], start, end) <= shape.Radius + Epsilon;
                case NavigationShapeType.Edge:
                    for (int index = 1; index < shape.Vertices.Length; index++)
                        if (SegmentsIntersect(shape.Vertices[index - 1], shape.Vertices[index], start, end)) return true;
                    return false;
                default:
                    if (PointInPolygon(start, shape.Vertices) || PointInPolygon(end, shape.Vertices)) return true;
                    for (int index = 0; index < shape.Vertices.Length; index++)
                        if (SegmentsIntersect(shape.Vertices[index], shape.Vertices[(index + 1) % shape.Vertices.Length], start, end)) return true;
                    return false;
            }
        }

        private static bool CircleIntersectsRect(Vector2 center, float radius, Rect rect)
        {
            float dx = Mathf.Max(rect.xMin - center.x, 0f, center.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - center.y, 0f, center.y - rect.yMax);
            return dx * dx + dy * dy < radius * radius - Epsilon;
        }

        private static bool SegmentIntersectsExpandedRect(Vector2 a, Vector2 b, float radius, Rect rect)
        {
            Rect expanded = rect;
            expanded.xMin -= radius; expanded.xMax += radius; expanded.yMin -= radius; expanded.yMax += radius;
            return SegmentIntersectsRect(a, b, expanded)
                || DistanceSegments(a, b, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin)) <= radius + Epsilon
                || DistanceSegments(a, b, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax)) <= radius + Epsilon
                || DistanceSegments(a, b, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)) <= radius + Epsilon
                || DistanceSegments(a, b, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin)) <= radius + Epsilon;
        }

        private static bool PolygonIntersectsRect(Vector2[] vertices, Rect rect)
        {
            for (int index = 0; index < vertices.Length; index++)
                if (IsStrictlyInside(vertices[index], rect)) return true;
            if (PointInPolygon(rect.center, vertices)) return true;
            for (int index = 0; index < vertices.Length; index++)
                if (SegmentIntersectsRectInterior(vertices[index], vertices[(index + 1) % vertices.Length], rect)) return true;
            return false;
        }

        private static bool SegmentIntersectsRectInterior(Vector2 a, Vector2 b, Rect rect)
        {
            if (Mathf.Min(a.x, b.x) >= rect.xMax - Epsilon || Mathf.Max(a.x, b.x) <= rect.xMin + Epsilon
                || Mathf.Min(a.y, b.y) >= rect.yMax - Epsilon || Mathf.Max(a.y, b.y) <= rect.yMin + Epsilon)
                return false;
            return SegmentIntersectsRect(a, b, rect);
        }

        private static bool IsStrictlyInside(Vector2 point, Rect rect)
            => point.x > rect.xMin + Epsilon && point.x < rect.xMax - Epsilon && point.y > rect.yMin + Epsilon && point.y < rect.yMax - Epsilon;

        private static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect rect)
        {
            float enter = 0f; float exit = 1f; Vector2 delta = b - a;
            return Clip(-delta.x, a.x - rect.xMin, ref enter, ref exit)
                && Clip(delta.x, rect.xMax - a.x, ref enter, ref exit)
                && Clip(-delta.y, a.y - rect.yMin, ref enter, ref exit)
                && Clip(delta.y, rect.yMax - a.y, ref enter, ref exit)
                && exit > enter + Epsilon;
        }

        private static bool Clip(float numerator, float denominator, ref float enter, ref float exit)
        {
            if (Mathf.Abs(numerator) <= Epsilon) return denominator >= 0f;
            float value = denominator / numerator;
            if (numerator > 0f) exit = Mathf.Min(exit, value); else enter = Mathf.Max(enter, value);
            return enter <= exit;
        }

        private static bool PointInPolygon(Vector2 point, Vector2[] vertices)
        {
            bool inside = false;
            for (int index = 0, previous = vertices.Length - 1; index < vertices.Length; previous = index++)
            {
                Vector2 a = vertices[index]; Vector2 b = vertices[previous];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float first = Cross(b - a, c - a); float second = Cross(b - a, d - a);
            float third = Cross(d - c, a - c); float fourth = Cross(d - c, b - c);
            bool proper = (first > Epsilon && second < -Epsilon || first < -Epsilon && second > Epsilon)
                && (third > Epsilon && fourth < -Epsilon || third < -Epsilon && fourth > Epsilon);
            return proper
                || Mathf.Abs(first) <= Epsilon && IsPointOnSegment(c, a, b)
                || Mathf.Abs(second) <= Epsilon && IsPointOnSegment(d, a, b)
                || Mathf.Abs(third) <= Epsilon && IsPointOnSegment(a, c, d)
                || Mathf.Abs(fourth) <= Epsilon && IsPointOnSegment(b, c, d);
        }

        private static bool IsPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
            => point.x >= Mathf.Min(start.x, end.x) - Epsilon
                && point.x <= Mathf.Max(start.x, end.x) + Epsilon
                && point.y >= Mathf.Min(start.y, end.y) - Epsilon
                && point.y <= Mathf.Max(start.y, end.y) + Epsilon;

        private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start; float length = delta.sqrMagnitude;
            float parameter = length <= Epsilon ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, delta) / length);
            return Vector2.Distance(point, start + delta * parameter);
        }

        private static float DistanceSegments(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (SegmentsIntersect(a, b, c, d)) return 0f;
            return Mathf.Min(Mathf.Min(DistancePointToSegment(a, c, d), DistancePointToSegment(b, c, d)),
                Mathf.Min(DistancePointToSegment(c, a, b), DistancePointToSegment(d, a, b)));
        }

        private static float PolygonWinding(Vector2[] vertices)
        {
            float result = 0f;
            for (int index = 0; index < vertices.Length; index++) result += Cross(vertices[index], vertices[(index + 1) % vertices.Length]);
            return result;
        }

        private static void AddUnique(List<NavigationSupport> results, NavigationSupport candidate)
        {
            for (int index = 0; index < results.Count; index++)
                if (results[index].Surface == candidate.Surface && Vector2.Distance(results[index].Position, candidate.Position) <= Epsilon) return;
            results.Add(candidate);
        }

        private static void BuildSupportCandidates(Shape shape, Vector2 origin, float cellSize, RectInt cellBounds, List<NavigationSupport> results)
        {
            if (!shape.HasSupport) return;

            List<NavigationSupport> surfaceCandidates = new();
            List<float> ordinarySamples = new();
            int firstCell = Mathf.CeilToInt((shape.Min.x - origin.x) / cellSize - 0.5f);
            int lastCell = Mathf.FloorToInt((shape.Max.x - origin.x) / cellSize - 0.5f);
            firstCell = Mathf.Max(firstCell, cellBounds.xMin);
            lastCell = Mathf.Min(lastCell, cellBounds.xMax - 1);
            for (int cell = firstCell; cell <= lastCell; cell++)
            {
                float x = origin.x + (cell + 0.5f) * cellSize;
                ordinarySamples.Add(x);
                TryAddSupportAtX(shape, x, surfaceCandidates);
            }

            TryAddSupportAtX(shape, shape.Min.x, surfaceCandidates);
            TryAddSupportAtX(shape, shape.Max.x, surfaceCandidates);
            for (int vertex = 0; vertex < shape.Vertices.Length; vertex++)
                TryAddSupportAtX(shape, shape.Vertices[vertex].x, surfaceCandidates);

            int edgeCount = shape.ShapeType == NavigationShapeType.Polygon
                ? shape.Vertices.Length : shape.Vertices.Length - 1;
            for (int edge = 0; edge < edgeCount; edge++)
            {
                Vector2 a = shape.Vertices[edge];
                Vector2 b = shape.Vertices[(edge + 1) % shape.Vertices.Length];
                if (Mathf.Abs(a.x - b.x) <= Epsilon) continue;
                float minX = Mathf.Min(a.x, b.x);
                float maxX = Mathf.Max(a.x, b.x);
                bool hasOrdinarySample = false;
                for (int sample = 0; sample < ordinarySamples.Count; sample++)
                    if (ordinarySamples[sample] >= minX - Epsilon && ordinarySamples[sample] <= maxX + Epsilon)
                    {
                        hasOrdinarySample = true;
                        break;
                    }
                if (!hasOrdinarySample) TryAddSupportAtX(shape, (minX + maxX) * 0.5f, surfaceCandidates);
            }

            if (shape.ShapeType == NavigationShapeType.Circle)
                TryAddSupportAtX(shape, shape.Vertices[0].x, surfaceCandidates);
            else if (shape.ShapeType == NavigationShapeType.Capsule)
                TryAddSupportAtX(shape, (shape.Vertices[0].x + shape.Vertices[1].x) * 0.5f, surfaceCandidates);

            for (int index = 0; index < surfaceCandidates.Count; index++) results.Add(surfaceCandidates[index]);
        }

        private static void TryAddSupportAtX(Shape shape, float x, List<NavigationSupport> results)
        {
            if (x < shape.Min.x - Epsilon || x > shape.Max.x + Epsilon
                || !TryGetSurfaceAtX(shape, x, out float y, out Vector2 normal)
                || !IsAllowedSupport(shape, normal)) return;
            AddUnique(results, new NavigationSupport(new NavigationSurfaceId(shape.SourceId, shape.FeatureId),
                shape.Kind, new Vector2(x, y), normal));
        }

        private static Vector2Int WorldToCell(Vector2 position, Vector2 origin, float cellSize, RectInt cellBounds)
        {
            Vector2 local = (position - origin) / cellSize;
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(local.x), cellBounds.xMin, cellBounds.xMax - 1),
                Mathf.Clamp(Mathf.FloorToInt(local.y), cellBounds.yMin, cellBounds.yMax - 1));
        }

        private static void AddUnique(List<NavigationSurfaceCrossing> results, NavigationSurfaceCrossing candidate)
        {
            for (int index = 0; index < results.Count; index++)
                if (results[index].Surface == candidate.Surface && Vector2.Distance(results[index].Position, candidate.Position) <= Epsilon) return;
            results.Add(candidate);
        }

        private static int CompareSupport(NavigationSupport left, NavigationSupport right)
        {
            int surface = CompareSurface(left.Surface, right.Surface);
            if (surface != 0) return surface;
            int x = left.Position.x.CompareTo(right.Position.x);
            return x != 0 ? x : left.Position.y.CompareTo(right.Position.y);
        }

        private static int CompareSurface(NavigationSurfaceId left, NavigationSurfaceId right)
            => left.SourceId.CompareTo(right.SourceId) != 0 ? left.SourceId.CompareTo(right.SourceId) : left.FeatureId.CompareTo(right.FeatureId);

        private static int CompareShape(Shape left, Shape right)
            => CompareSurface(new NavigationSurfaceId(left.SourceId, left.FeatureId), new NavigationSurfaceId(right.SourceId, right.FeatureId));

        private static void GetCellRange(Vector2 min, Vector2 max, Vector2 origin, float cellSize, RectInt bounds,
            out int minX, out int maxX, out int minY, out int maxY)
        {
            minX = Mathf.Clamp(Mathf.FloorToInt((min.x - origin.x - Epsilon) / cellSize), bounds.xMin, bounds.xMax - 1);
            maxX = Mathf.Clamp(Mathf.FloorToInt((max.x - origin.x + Epsilon) / cellSize), bounds.xMin, bounds.xMax - 1);
            minY = Mathf.Clamp(Mathf.FloorToInt((min.y - origin.y - Epsilon) / cellSize), bounds.yMin, bounds.yMax - 1);
            maxY = Mathf.Clamp(Mathf.FloorToInt((max.y - origin.y + Epsilon) / cellSize), bounds.yMin, bounds.yMax - 1);
        }

        private static float Cross(Vector2 left, Vector2 right) => left.x * right.y - left.y * right.x;

        private sealed class Shape
        {
            public readonly int SourceId;
            public readonly int FeatureId;
            public readonly NavigationShapeType ShapeType;
            public readonly Vector2[] Vertices;
            public readonly float Radius;
            public readonly NavigationSurfaceKind Kind;
            public readonly bool HasSupport;
            public readonly Vector2 OneWayDirection;
            public readonly float OneWayCosHalfArc;
            public readonly float DirectedNormalSign;
            public readonly Vector2 Min;
            public readonly Vector2 Max;

            public Shape(NavigationShapeData data)
            {
                SourceId = data.SourceId;
                FeatureId = data.FeatureId;
                ShapeType = data.ShapeType;
                Vertices = Copy(data.Vertices);
                Radius = data.Radius;
                Kind = data.Kind;
                HasSupport = data.HasSupport;
                OneWayDirection = data.OneWayDirection;
                OneWayCosHalfArc = data.OneWayCosHalfArc;
                DirectedNormalSign = data.DirectedNormalSign;
                Vector2 min = Vertices[0];
                Vector2 max = Vertices[0];
                for (int index = 1; index < Vertices.Length; index++) { min = Vector2.Min(min, Vertices[index]); max = Vector2.Max(max, Vertices[index]); }
                Min = min - Vector2.one * Radius;
                Max = max + Vector2.one * Radius;
            }

            private static Vector2[] Copy(IReadOnlyList<Vector2> source)
            {
                Vector2[] result = new Vector2[source.Count];
                for (int index = 0; index < result.Length; index++) result[index] = source[index];
                return result;
            }
        }
    }
}
