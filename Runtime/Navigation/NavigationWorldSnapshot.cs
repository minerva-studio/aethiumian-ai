using System;
using System.Collections.Generic;
using UnityEngine;
using static Aethiumian.AI.Navigation.NavigationArithmetic;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Managed immutable navigation snapshot backed by captured geometry and spatial buckets.</summary>
    public sealed class NavigationWorldSnapshot : NavigationWorld
    {
        private const float Epsilon = NavigationConstant.Epsilon;
        private readonly AABB worldBounds;
        private readonly Shape[] shapes;
        private readonly Dictionary<Vector2Int, int[]> buckets;
        private readonly NavigationSupportCandidate[] supportCandidates;
        private readonly Dictionary<Vector2Int, int[]> supportCandidateBuckets;
        private readonly Dictionary<Vector2Int, int> regions;

        private NavigationWorldSnapshot(AABB worldBounds,
            Shape[] shapes,
            Dictionary<Vector2Int, int[]> buckets,
            NavigationSupportCandidate[] supportCandidates,
            Dictionary<Vector2Int, int[]> supportCandidateBuckets,
            Dictionary<Vector2Int, int> regions,
            int supportCacheEntryLimit,
            int supportCacheCandidateLimit)
            : base(supportCacheEntryLimit, supportCacheCandidateLimit)
        {
            this.worldBounds = worldBounds;
            this.shapes = shapes;
            this.buckets = buckets;
            this.supportCandidates = supportCandidates;
            this.supportCandidateBuckets = supportCandidateBuckets;
            this.regions = regions;
        }

        public override AABB WorldBounds => worldBounds;

        /// <summary>Copies detached geometry and builds immutable spatial and region indexes.</summary>
        public static NavigationWorldSnapshot Create(AABB worldBounds, IReadOnlyList<NavigationShapeData> shapeData, IReadOnlyList<NavigationRegionData> regionData)
            => Create(worldBounds, shapeData, regionData, NavigationConstant.SupportAnchorSpacing, SupportCandidateCache.DefaultEntryLimit, SupportCandidateCache.DefaultCandidateLimit);

        /// <summary>Builds a snapshot with package-internal discretization and cache limits for focused cache validation.</summary>
        internal static NavigationWorldSnapshot Create(AABB worldBounds, IReadOnlyList<NavigationShapeData> shapeData, IReadOnlyList<NavigationRegionData> regionData, float supportAnchorSpacing, int supportCacheEntryLimit, int supportCacheCandidateLimit)
        {
            if (!NavigationNumeric.IsFinite(worldBounds.Min) || !NavigationNumeric.IsFinite(worldBounds.Max)
                || worldBounds.SizeX <= 0f || worldBounds.SizeY <= 0f) throw new ArgumentException("Navigation bounds must be positive and finite.", nameof(worldBounds));
            if (!NavigationNumeric.IsFinite(supportAnchorSpacing) || supportAnchorSpacing <= 0f) throw new ArgumentOutOfRangeException(nameof(supportAnchorSpacing));
            if (shapeData == null) throw new ArgumentNullException(nameof(shapeData));
            if (regionData == null) throw new ArgumentNullException(nameof(regionData));
            AABBInt indexBounds = GetIndexBounds(worldBounds);

            Shape[] shapes = new Shape[shapeData.Count];
            Dictionary<Vector2Int, List<int>> mutableBuckets = new();
            for (int index = 0; index < shapeData.Count; index++)
            {
                Shape shape = new(shapeData[index]);
                shapes[index] = shape;
                AABBInt shapeRange = ClampIndexRange(GetIndexRange(shape.Bounds, worldBounds), indexBounds);
                for (int y = shapeRange.MinY; y < shapeRange.MaxY; y++)
                    for (int x = shapeRange.MinX; x < shapeRange.MaxX; x++)
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
                pair.Value.Sort((left, right) => shapes[left].SurfaceId.CompareTo(shapes[right].SurfaceId));
                buckets.Add(pair.Key, pair.Value.ToArray());
            }

            Dictionary<Vector2Int, int> regions = new();
            for (int index = 0; index < regionData.Count; index++)
            {
                NavigationRegionData region = regionData[index];
                AABBInt regionRange = GetIndexRangeExclusive(region.WorldBounds, worldBounds);
                for (int y = regionRange.MinY; y < regionRange.MaxY; y++)
                    for (int x = regionRange.MinX; x < regionRange.MaxX; x++)
                    {
                        Vector2Int key = new(x, y);
                        if (regions.ContainsKey(key)) throw new ArgumentException("Navigation regions must not overlap.", nameof(regionData));
                        regions.Add(key, region.RegionId);
                    }
            }

            List<NavigationSupport> supports = new();
            for (int shapeIndex = 0; shapeIndex < shapes.Length; shapeIndex++)
                BuildSupportCandidates(shapes[shapeIndex], worldBounds, supportAnchorSpacing, supports);

            supports.Sort();
            NavigationSupportCandidate[] supportCandidates = new NavigationSupportCandidate[supports.Count];
            Dictionary<Vector2Int, List<int>> mutableCandidateBuckets = new();
            for (int i = 0; i < supports.Count; i++)
            {
                NavigationSupportCandidate candidate = new(i, supports[i]);
                supportCandidates[i] = candidate;
                Vector2Int worldIndex = WorldToIndex(candidate.Support.Position, worldBounds, indexBounds);
                if (!mutableCandidateBuckets.TryGetValue(worldIndex, out List<int> entries))
                    mutableCandidateBuckets.Add(worldIndex, entries = new List<int>());
                entries.Add(i);
            }

            Dictionary<Vector2Int, int[]> supportCandidateBuckets = new();
            foreach (KeyValuePair<Vector2Int, List<int>> pair in mutableCandidateBuckets)
                supportCandidateBuckets.Add(pair.Key, pair.Value.ToArray());

            return new NavigationWorldSnapshot(worldBounds, shapes, buckets, supportCandidates, supportCandidateBuckets, regions, supportCacheEntryLimit, supportCacheCandidateLimit);
        }

        public override bool IsBodyClear(AABB body, float surfaceContactTolerance)
        {
            if (!worldBounds.Contains(body.Min) || !worldBounds.Contains(body.Max)) return false;
            AABB tested = new(new Vector2(body.MinX, body.MinY + surfaceContactTolerance), body.Max);
            if (tested.SizeY <= Epsilon) return true;
            foreach (int index in QueryShapeIndexes(tested))
                if (shapes[index].Kind == NavigationSurfaceKind.Solid && shapes[index].IsShapeIntersection(tested)) return false;
            return true;
        }

        public override bool IsBodyPathClear(AABB startBody, Vector2 displacement, float surfaceContactTolerance)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / Mathf.Max(Epsilon, NavigationConstant.BodySweepSampleSpacing)));
            for (int index = 0; index <= samples; index++)
            {
                AABB body = startBody.Translate(displacement * (index / (float)samples));
                if (!IsBodyClear(body, surfaceContactTolerance)) return false;
            }
            return true;
        }

        public override bool IsLineOfSightClear(Vector2 start, Vector2 end)
        {
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            if (!worldBounds.Contains(start) || !worldBounds.Contains(end)) return false;
            Vector2 min = Vector2.Min(start, end);
            Vector2 max = Vector2.Max(start, end);
            AABB query = new(min, max);
            foreach (int index in QueryShapeIndexes(query))
                if (shapes[index].Kind == NavigationSurfaceKind.Solid && shapes[index].IsShapeSegmentIntersection(start, end)) return false;
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
            AABB query = AABB.FromMinAndSize(
                new Vector2(feet.x - bodySize.x * 0.5f, feet.y - snapDistance - Epsilon),
                new Vector2(bodySize.x, bodySize.y + snapDistance + Epsilon));

            if (TryFindCenterSupport(query, feet, bodySize, snapDistance, out support)) return true;
            return TryFindFootEdgeSupport(query, feet, bodySize, snapDistance, out support);
        }

        /// <inheritdoc />
        public override bool TryGetSupportBelow(Vector2 position, out NavigationSupport support)
        {
            Validate.Finite(position, nameof(position));
            support = default;
            if (position.x < worldBounds.MinX || position.x > worldBounds.MaxX
                || position.y < worldBounds.MinY - Epsilon) return false;

            // Use a narrow spatial query, not the discretely sampled standing candidates.
            // A sloped or curved surface must be evaluated at the caller's exact X.
            AABB query = new(new Vector2(position.x - Epsilon, worldBounds.MinY - Epsilon),
                new Vector2(position.x + Epsilon, Mathf.Min(position.y + Epsilon, worldBounds.MaxY)));
            bool found = false;
            foreach (int index in QueryShapeIndexes(query))
            {
                Shape shape = shapes[index];
                if (!shape.HasSupport
                    || !shape.TryGetSurfaceAtX(position.x, out float y, out Vector2 normal, Mathf.Min(position.y, worldBounds.MaxY), supportedOnly: true)
                    || y < worldBounds.MinY - Epsilon) continue;

                NavigationSupport candidate = new(shape.SurfaceId, shape.Kind, new Vector2(position.x, y), normal);
                if (!found || y > support.Position.y + Epsilon || (Mathf.Abs(y - support.Position.y) <= Epsilon && candidate.CompareTo(support) < 0))
                {
                    support = candidate;
                    found = true;
                }
            }
            return found;
        }

        private bool TryFindCenterSupport(AABB query, Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
        {
            NavigationSupport best = default;
            float bestDistance = float.PositiveInfinity;
            bool found = false;
            foreach (int index in QueryShapeIndexes(query))
            {
                Shape shape = shapes[index];
                ConsiderSupportAtX(shape, feet.x, feet, bodySize, snapDistance, ref found, ref best, ref bestDistance);
            }
            support = best;
            return found;
        }

        private bool TryFindFootEdgeSupport(AABB query, Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
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
            ConsiderSupportAtX(shape, probeX, feet, bodySize, snapDistance, ref found, ref best, ref bestDistance);
        }

        private void ConsiderSupportAtX(Shape shape, float probeX, Vector2 feet, Vector2 bodySize, float snapDistance, ref bool found, ref NavigationSupport best, ref float bestDistance)
        {
            if (!shape.HasSupport
                || !shape.TryGetSurfaceAtX(probeX, out float y, out Vector2 normal, feet.y + snapDistance)
                || !shape.IsAllowedSupport(normal)
                || y > feet.y + snapDistance || y < feet.y - snapDistance) return;

            AABB body = AABB.FromMinAndSize(new Vector2(feet.x - bodySize.x * 0.5f, y), bodySize);
            if (!IsBodyClear(body, snapDistance)) return;

            NavigationSupport candidate = new(shape.SurfaceId, shape.Kind, new Vector2(feet.x, y), normal);
            float distance = Mathf.Abs(feet.y - y);
            if (!found || distance < bestDistance - Epsilon || (distance <= bestDistance + Epsilon && candidate.CompareTo(best) < 0))
            {
                best = candidate;
                bestDistance = distance;
                found = true;
            }
        }

        protected override void CollectSupportCandidatesCore(AABB anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            foreach (int candidateId in QuerySupportCandidateIds(anchorBounds))
            {
                NavigationSupportCandidate candidate = supportCandidates[candidateId];
                NavigationSupport support = candidate.Support;
                if (!anchorBounds.Contains(support.Position)) continue;
                AABB body = AABB.FromMinAndSize(
                    new Vector2(support.Position.x - bodySize.x * 0.5f, support.Position.y), bodySize);
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
            int steps = Mathf.Max(8, Mathf.CeilToInt(delta.magnitude / Mathf.Max(Epsilon, NavigationConstant.OneWayCrossingSampleSpacing)));
            float halfWidth = bodyWidth * 0.5f;
            AABB sweptBounds = new(
                new Vector2(Mathf.Min(previousFeet.x, currentFeet.x) - halfWidth - Epsilon,
                    Mathf.Min(previousFeet.y, currentFeet.y) - Epsilon),
                new Vector2(Mathf.Max(previousFeet.x, currentFeet.x) + halfWidth + Epsilon,
                    Mathf.Max(previousFeet.y, currentFeet.y) + Epsilon));
            foreach (int shapeIndex in QueryShapeIndexes(sweptBounds))
            {
                Shape shape = shapes[shapeIndex];
                if (shape.Kind != NavigationSurfaceKind.OneWay
                    || shape.Bounds.MaxX < sweptBounds.MinX - Epsilon || shape.Bounds.MinX > sweptBounds.MaxX + Epsilon
                    || shape.Bounds.MaxY < sweptBounds.MinY - Epsilon || shape.Bounds.MinY > sweptBounds.MaxY + Epsilon)
                    continue;
                for (int offsetIndex = -1; offsetIndex <= 1; offsetIndex++)
                {
                    float offset = offsetIndex * halfWidth;
                    bool previousValid = shape.TryGetSurfaceAtX(previousFeet.x + offset, out _, out _);
                    float previousDifference = previousValid && shape.TryGetSurfaceAtX(previousFeet.x + offset, out float firstY, out _)
                        ? previousFeet.y - firstY : 0f;
                    for (int stepIndex = 1; stepIndex <= steps; stepIndex++)
                    {
                        float fraction = stepIndex / (float)steps;
                        Vector2 feet = Vector2.Lerp(previousFeet, currentFeet, fraction);
                        bool currentValid = shape.TryGetSurfaceAtX(feet.x + offset, out float currentY, out Vector2 currentNormal);
                        float currentDifference = currentValid ? feet.y - currentY : 0f;
                        bool crossedFromAbove = previousDifference >= -Epsilon && currentDifference < -Epsilon;
                        bool crossedFromBelow = previousDifference < -Epsilon && currentDifference >= -Epsilon;
                        if (previousValid && currentValid && shape.IsAllowedSupport(currentNormal)
                            && (crossedFromAbove || crossedFromBelow))
                        {
                            float denominator = previousDifference - currentDifference;
                            float local = denominator <= Epsilon ? 1f : Mathf.Clamp01(previousDifference / denominator);
                            float eventFraction = ((stepIndex - 1) + local) / steps;
                            float crossingX = Mathf.Lerp(previousFeet.x, currentFeet.x, eventFraction) + offset;
                            shape.TryGetSurfaceAtX(crossingX, out float crossingY, out Vector2 crossingNormal, float.PositiveInfinity);
                            AddUnique(results, new NavigationSurfaceCrossing(shape.SurfaceId, new Vector2(crossingX, crossingY), crossingNormal, eventFraction));
                        }
                        previousValid = currentValid;
                        previousDifference = currentDifference;
                    }
                }
            }
            results.Sort((left, right) => left.Fraction.CompareTo(right.Fraction) != 0
                ? left.Fraction.CompareTo(right.Fraction)
                : left.Surface.CompareTo(right.Surface));
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
            Vector2 local = (position - worldBounds.Min) / NavigationConstant.SpatialIndexBucketSize;
            Vector2Int worldIndex = new(Mathf.FloorToInt(local.x), Mathf.FloorToInt(local.y));
            return regions.TryGetValue(worldIndex, out region);
        }

        private IEnumerable<int> QueryShapeIndexes(AABB bounds)
        {
            HashSet<int> seen = new();
            AABBInt range = ClampIndexRange(GetIndexRange(bounds, worldBounds), GetIndexBounds(worldBounds));
            for (int y = range.MinY; y < range.MaxY; y++)
                for (int x = range.MinX; x < range.MaxX; x++)
                    if (buckets.TryGetValue(new Vector2Int(x, y), out int[] entries))
                        for (int index = 0; index < entries.Length; index++)
                            if (seen.Add(entries[index])) yield return entries[index];
        }

        private IEnumerable<int> QuerySupportCandidateIds(AABB bounds)
        {
            AABBInt range = ClampIndexRange(GetIndexRange(bounds, worldBounds), GetIndexBounds(worldBounds));
            for (int y = range.MinY; y < range.MaxY; y++)
                for (int x = range.MinX; x < range.MaxX; x++)
                    if (supportCandidateBuckets.TryGetValue(new Vector2Int(x, y), out int[] entries))
                        for (int index = 0; index < entries.Length; index++)
                            yield return entries[index];
        }

        private static void AddUnique(List<NavigationSupport> results, NavigationSupport candidate)
        {
            for (int index = 0; index < results.Count; index++)
                if (results[index].Surface == candidate.Surface && Vector2.Distance(results[index].Position, candidate.Position) <= Epsilon) return;
            results.Add(candidate);
        }

        private static void BuildSupportCandidates(Shape shape, AABB worldBounds, float supportAnchorSpacing, List<NavigationSupport> results)
        {
            if (!shape.HasSupport) return;

            List<NavigationSupport> surfaceCandidates = new();
            List<float> ordinarySamples = new();
            int firstAnchor = Mathf.CeilToInt((shape.Bounds.MinX - worldBounds.MinX) / supportAnchorSpacing - 0.5f);
            int lastAnchor = Mathf.FloorToInt((shape.Bounds.MaxX - worldBounds.MinX) / supportAnchorSpacing - 0.5f);
            firstAnchor = Mathf.Max(firstAnchor, 0);
            lastAnchor = Mathf.Min(lastAnchor, Mathf.Max(0, Mathf.CeilToInt(worldBounds.SizeX / supportAnchorSpacing) - 1));
            for (int anchor = firstAnchor; anchor <= lastAnchor; anchor++)
            {
                float x = worldBounds.MinX + (anchor + 0.5f) * supportAnchorSpacing;
                ordinarySamples.Add(x);
                TryAddSupportAtX(shape, x, surfaceCandidates);
            }

            TryAddSupportAtX(shape, shape.Bounds.MinX, surfaceCandidates);
            TryAddSupportAtX(shape, shape.Bounds.MaxX, surfaceCandidates);
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
            if (x < shape.Bounds.MinX - Epsilon || x > shape.Bounds.MaxX + Epsilon
                || !shape.TryGetSurfaceAtX(x, out float y, out Vector2 normal)
                || !shape.IsAllowedSupport(normal)) return;
            AddUnique(results, new NavigationSupport(shape.SurfaceId, shape.Kind, new Vector2(x, y), normal));
        }

        private static Vector2Int WorldToIndex(Vector2 position, AABB worldBounds, AABBInt indexBounds)
        {
            Vector2 local = (position - worldBounds.Min) / NavigationConstant.SpatialIndexBucketSize;
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(local.x), indexBounds.MinX, indexBounds.MaxX - 1),
                Mathf.Clamp(Mathf.FloorToInt(local.y), indexBounds.MinY, indexBounds.MaxY - 1));
        }

        private static void AddUnique(List<NavigationSurfaceCrossing> results, NavigationSurfaceCrossing candidate)
        {
            for (int index = 0; index < results.Count; index++)
                if (results[index].Surface == candidate.Surface && Vector2.Distance(results[index].Position, candidate.Position) <= Epsilon) return;
            results.Add(candidate);
        }

        private static AABBInt GetIndexBounds(AABB worldBounds)
        {
            float bucket = NavigationConstant.SpatialIndexBucketSize;
            return new AABBInt(Vector2Int.zero, new Vector2Int(
                Mathf.Max(1, Mathf.CeilToInt(worldBounds.SizeX / bucket)),
                Mathf.Max(1, Mathf.CeilToInt(worldBounds.SizeY / bucket))));
        }

        /// <summary>Returns the half-open bucket range covering the box, including its upper edges.</summary>
        private static AABBInt GetIndexRange(AABB bounds, AABB worldBounds)
        {
            float bucket = NavigationConstant.SpatialIndexBucketSize;
            return new AABBInt(
                new Vector2Int(
                    Mathf.FloorToInt((bounds.MinX - worldBounds.MinX - Epsilon) / bucket),
                    Mathf.FloorToInt((bounds.MinY - worldBounds.MinY - Epsilon) / bucket)),
                new Vector2Int(
                    Mathf.FloorToInt((bounds.MaxX - worldBounds.MinX + Epsilon) / bucket) + 1,
                    Mathf.FloorToInt((bounds.MaxY - worldBounds.MinY + Epsilon) / bucket) + 1));
        }

        /// <summary>Returns the half-open bucket range covering the box's interior only.</summary>
        private static AABBInt GetIndexRangeExclusive(AABB bounds, AABB worldBounds)
        {
            float bucket = NavigationConstant.SpatialIndexBucketSize;
            return new AABBInt(
                new Vector2Int(
                    Mathf.FloorToInt((bounds.MinX - worldBounds.MinX) / bucket),
                    Mathf.FloorToInt((bounds.MinY - worldBounds.MinY) / bucket)),
                new Vector2Int(
                    Mathf.CeilToInt((bounds.MaxX - worldBounds.MinX) / bucket),
                    Mathf.CeilToInt((bounds.MaxY - worldBounds.MinY) / bucket)));
        }

        /// <summary>Intersects a bucket range with the index bounds; a disjoint range collapses to empty.</summary>
        private static AABBInt ClampIndexRange(AABBInt range, AABBInt indexBounds)
        {
            int minX = Mathf.Min(Mathf.Max(range.MinX, indexBounds.MinX), indexBounds.MaxX);
            int minY = Mathf.Min(Mathf.Max(range.MinY, indexBounds.MinY), indexBounds.MaxY);
            int maxX = Mathf.Max(Mathf.Min(range.MaxX, indexBounds.MaxX), minX);
            int maxY = Mathf.Max(Mathf.Min(range.MaxY, indexBounds.MaxY), minY);
            return new AABBInt(new Vector2Int(minX, minY), new Vector2Int(maxX, maxY));
        }

    }
}
