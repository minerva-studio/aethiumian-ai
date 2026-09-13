using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Immutable target geometry and behavior tolerance supplied to navigation planners.</summary>
    public sealed class NavigationGoalRegion
    {
        private readonly float cellSize;
        private readonly NavigationGoalRequest request;
        private readonly INavigationWorld snapshot;

        /// <summary>Gets the merged target world-space AABB.</summary>
        public Bounds TargetBounds => request.TargetBounds;

        /// <summary>Gets the immutable planner data captured for this region.</summary>
        public NavigationGoalRequest Request => request;

        /// <summary>Gets the exact identity used for cache and target-motion comparisons.</summary>
        public NavigationGoalKey GoalKey => new NavigationGoalKey(request.TargetBounds, request.Geometry, request.DistanceMetric, request.RequiresLineOfSight, request.ArrivalTolerance, request.RetreatDistance, cellSize);

        /// <summary>Gets the explicitly bound immutable world snapshot, when this region is bound.</summary>
        public INavigationWorld Snapshot => snapshot;

        /// <summary>Gets the geometry used by this region without exposing product goal names.</summary>
        public NavigationGoalGeometry Geometry => request.Geometry;

        /// <summary>Gets whether this region completes by increasing distance from its target.</summary>
        public bool IsRetreat => request.Geometry == NavigationGoalGeometry.Retreat;

        /// <summary>Gets whether this region requires line of sight at completion.</summary>
        public bool RequiresLineOfSight => request.RequiresLineOfSight;

        /// <summary>Gets the behavior-level distance accepted at runtime.</summary>
        public float ArrivalErrorBound => request.ArrivalTolerance;

        /// <summary>Gets the independent distance required to complete a Retreat goal.</summary>
        public float RetreatDistance => request.RetreatDistance;

        /// <summary>Gets the center used as a deterministic planning heuristic.</summary>
        public Vector2 Center => request.Geometry == NavigationGoalGeometry.GroundRange
            ? new Vector2(request.TargetBounds.center.x, request.TargetBounds.min.y)
            : request.TargetBounds.center;

        /// <summary>Gets whether this region uses Smart Walk lower-center semantics.</summary>
        public bool IsGroundWalk => request.Geometry == NavigationGoalGeometry.GroundRange;

        /// <summary>Gets the snapshot cell size captured by a Ground Walk region.</summary>
        public float CellSize => cellSize;

        private NavigationGoalRegion(NavigationGoalRequest request, INavigationWorld snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (!NavigationNumeric.IsFinite(snapshot.CellSize) || snapshot.CellSize <= 0f)
                throw new ArgumentOutOfRangeException(nameof(snapshot), "Snapshot cell size must be finite and positive.");
            this.request = request;
            this.snapshot = snapshot;
            cellSize = snapshot.CellSize;
        }

        /// <summary>Binds immutable planner data to one explicit navigation snapshot.</summary>
        public static NavigationGoalRegion Bind(NavigationGoalRequest request, INavigationWorld snapshot)
            => new NavigationGoalRegion(request, snapshot);

        /// <summary>Returns the lower-center acceptance bounds for the supplied body width.</summary>
        public Bounds GetLowerCenterAcceptanceBounds(float bodyWidth)
        {
            ValidateGroundWalkBodyWidth(bodyWidth);
            return new Bounds(new Vector3(request.TargetBounds.center.x, request.TargetBounds.min.y, 0f), new Vector3(request.TargetBounds.size.x + bodyWidth + ArrivalErrorBound * 2f, cellSize * 2f, 0f));
        }

        /// <summary>Returns the distance from a lower-center anchor to this Ground Walk goal.</summary>
        internal float DistanceToLowerCenterGoal(Vector2 lowerCenter, float bodyWidth)
        {
            Bounds acceptanceBounds = GetLowerCenterAcceptanceBounds(bodyWidth);
            Validate.Finite(lowerCenter, nameof(lowerCenter));
            return DistanceToPoint(lowerCenter, acceptanceBounds);
        }

        /// <summary>Returns the minimum distance from a lower-center segment to this Ground Walk goal.</summary>
        internal float DistanceToLowerCenterGoalSegment(Vector2 start, Vector2 end, float bodyWidth)
        {
            Bounds acceptanceBounds = GetLowerCenterAcceptanceBounds(bodyWidth);
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            return DistanceToSegmentBounds(start, end, acceptanceBounds);
        }

        /// <summary>Returns whether a lower-center anchor is inside this Ground Walk goal.</summary>
        internal bool ContainsLowerCenterGoal(Vector2 lowerCenter, float bodyWidth)
            => DistanceToLowerCenterGoal(lowerCenter, bodyWidth) <= NavigationWorldQueries.GeometryEpsilon;

        /// <summary>Gets the metric-aware completion distance for a center-anchored body.</summary>
        public float CompletionDistance(Vector2 center, Vector2 bodySize)
        {
            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            if (RequiresLineOfSight && !HasRequiredLineOfSight(center)) return float.PositiveInfinity;
            return GeometryCompletionDistance(center, bodySize);
        }

        /// <summary>Returns whether a candidate center satisfies the map-solid LOS constraint, when requested.</summary>
        internal bool HasRequiredLineOfSight(Vector2 center)
        {
            Validate.Finite(center, nameof(center));
            return !RequiresLineOfSight || Snapshot.IsLineOfSightClear(center, request.TargetBounds.center);
        }

        /// <summary>Returns whether a center-anchored body has completed this region.</summary>
        public bool IsComplete(Vector2 center, Vector2 bodySize)
            => CompletionDistance(center, bodySize) <= CompletionTolerance;

        /// <summary>Gets the best-effort distance from a mover center to the raw target center.</summary>
        public float GuidanceDistance(Vector2 center, Vector2 bodySize)
        {
            Validate.Finite(center, nameof(center));
            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            if (IsRetreat) return -DistanceToCenteredBody(center, bodySize);
            Vector2 delta = center - new Vector2(request.TargetBounds.center.x, request.TargetBounds.center.y);
            return IsGroundWalk
                ? delta.magnitude
                : MetricLength(Mathf.Abs(delta.x), Mathf.Abs(delta.y), request.DistanceMetric);
        }

        /// <summary>Gets the minimum selected-metric completion distance over one swept center segment.</summary>
        public float SweptCompletionDistance(Vector2 startCenter, Vector2 endCenter, Vector2 bodySize)
        {
            if (IsRetreat)
            {
                return Mathf.Min(CompletionDistance(startCenter, bodySize), CompletionDistance(endCenter, bodySize));
            }

            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            Vector2 offset = Vector2.up * (bodySize.y * 0.5f);
            return DistanceToLowerCenterBodySegment(startCenter - offset, endCenter - offset, bodySize);
        }

        /// <summary>Returns whether one swept center segment enters this goal's finite completion contract.</summary>
        public bool SweptIsComplete(Vector2 startCenter, Vector2 endCenter, Vector2 bodySize)
        {
            if (!RequiresLineOfSight)
                return SweptCompletionDistance(startCenter, endCenter, bodySize) <= CompletionTolerance;

            Validate.Finite(startCenter, nameof(startCenter));
            Validate.Finite(endCenter, nameof(endCenter));
            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            if (!TryGetGeometryCompletionInterval(startCenter, endCenter, bodySize, out float entry, out float exit))
                return false;

            float spacing = Mathf.Max(NavigationWorldQueries.GeometryEpsilon, CellSize * 0.5f);
            double deltaX = (double)endCenter.x - startCenter.x;
            double deltaY = (double)endCenter.y - startCenter.y;
            double intervalLength = Math.Sqrt(deltaX * deltaX + deltaY * deltaY) * (exit - entry);
            double requiredSamples = Math.Ceiling(intervalLength / spacing);
            if (requiredSamples > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(endCenter), "Swept completion interval is too long to sample at the required NavWorld spacing.");
            int samples = Math.Max(1, (int)requiredSamples);
            for (int index = 0; index <= samples; index++)
            {
                float parameter = Mathf.Lerp(entry, exit, index / (float)samples);
                Vector2 sample = Vector2.Lerp(startCenter, endCenter, parameter);
                if (HasRequiredLineOfSight(sample)) return true;
            }
            return false;
        }

        /// <summary>Returns the completion distance without evaluating the optional LOS constraint.</summary>
        private float GeometryCompletionDistance(Vector2 center, Vector2 bodySize)
            => IsRetreat
                ? Mathf.Max(0f, request.RetreatDistance - DistanceToCenteredBody(center, bodySize))
                : IsGroundWalk
                ? DistanceToLowerCenterGoal(center - Vector2.up * (bodySize.y * 0.5f), bodySize.x)
                : DistanceToCenteredBody(center, bodySize);

        /// <summary>Clips a finite center sweep to the convex interval that satisfies goal geometry.</summary>
        private bool TryGetGeometryCompletionInterval(Vector2 startCenter, Vector2 endCenter, Vector2 bodySize,
            out float entry, out float exit)
        {
            const int searchIterations = 48;
            float startDistance = GeometryCompletionDistance(startCenter, bodySize);
            if (startCenter == endCenter)
            {
                entry = 0f;
                exit = 0f;
                return startDistance <= CompletionTolerance;
            }

            float left = 0f;
            float right = 1f;
            for (int iteration = 0; iteration < searchIterations; iteration++)
            {
                float first = (left * 2f + right) / 3f;
                float second = (left + right * 2f) / 3f;
                float firstDistance = GeometryCompletionDistance(Vector2.Lerp(startCenter, endCenter, first), bodySize);
                float secondDistance = GeometryCompletionDistance(Vector2.Lerp(startCenter, endCenter, second), bodySize);
                if (firstDistance < secondDistance) right = second;
                else if (secondDistance < firstDistance) left = first;
                else
                {
                    left = first;
                    right = second;
                }
            }

            float minimum = (left + right) * 0.5f;
            if (GeometryCompletionDistance(Vector2.Lerp(startCenter, endCenter, minimum), bodySize)
                > CompletionTolerance)
            {
                entry = default;
                exit = default;
                return false;
            }

            entry = startDistance <= CompletionTolerance
                ? 0f
                : FindCompletionBoundary(startCenter, endCenter, bodySize, 0f, minimum, true);
            exit = GeometryCompletionDistance(endCenter, bodySize) <= CompletionTolerance
                ? 1f
                : FindCompletionBoundary(startCenter, endCenter, bodySize, minimum, 1f, false);
            return true;
        }

        /// <summary>Finds one boundary of the convex geometry-completion interval.</summary>
        private float FindCompletionBoundary(Vector2 startCenter, Vector2 endCenter, Vector2 bodySize, float lower, float upper, bool entering)
        {
            const int searchIterations = 48;
            for (int iteration = 0; iteration < searchIterations; iteration++)
            {
                float middle = (lower + upper) * 0.5f;
                bool complete = GeometryCompletionDistance(Vector2.Lerp(startCenter, endCenter, middle), bodySize)
                    <= CompletionTolerance;
                if (complete == entering) upper = middle;
                else lower = middle;
            }
            return entering ? upper : lower;
        }

        /// <summary>Gets the geometry-specific threshold applied after constructing the acceptance region.</summary>
        private float CompletionTolerance => IsGroundWalk ? NavigationWorldQueries.GeometryEpsilon : ArrivalErrorBound;

        /// <summary>Compares two regions using only their immutable target semantics.</summary>
        public bool IsReusableFor(NavigationGoalRegion latest, float horizontalThreshold)
        {
            if (latest == null) return false;
            if (request.Geometry != latest.request.Geometry) return false;
            NavigationGoalKey currentKey = GoalKey;
            NavigationGoalKey latestKey = latest.GoalKey;
            if (currentKey.Geometry != latestKey.Geometry
                || currentKey.DistanceMetric != latestKey.DistanceMetric
                || currentKey.RequiresLineOfSight != latestKey.RequiresLineOfSight
                || !currentKey.ArrivalTolerance.Equals(latestKey.ArrivalTolerance)
                || !currentKey.RetreatDistance.Equals(latestKey.RetreatDistance)
                || !currentKey.SnapshotCellSize.Equals(latestKey.SnapshotCellSize))
                return false;
            if (!NavigationNumeric.IsFinite(horizontalThreshold) || horizontalThreshold < 0f)
                throw new ArgumentOutOfRangeException(nameof(horizontalThreshold));
            // Extents are exact; only the center may move within the reuse threshold.
            if (!currentKey.TargetBounds.size.Equals(latestKey.TargetBounds.size)) return false;
            if (request.Geometry != NavigationGoalGeometry.GroundRange)
                return Vector2.Distance(Center, latest.Center) <= horizontalThreshold;

            bool horizontalChanged = Mathf.Max(
                Mathf.Abs(request.TargetBounds.min.x - latest.request.TargetBounds.min.x),
                Mathf.Abs(request.TargetBounds.max.x - latest.request.TargetBounds.max.x)) > horizontalThreshold;
            bool levelChanged = Mathf.Abs(request.TargetBounds.min.y - latest.request.TargetBounds.min.y) > cellSize;
            return !horizontalChanged && !levelChanged;
        }

        /// <summary>Returns the distance from a lower-center body AABB to this region.</summary>
        public float DistanceToLowerCenterBody(Vector2 lowerCenter, Vector2 bodySize)
        {
            Validate.Finite(lowerCenter, nameof(lowerCenter));
            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            return IsGroundWalk
                ? DistanceToLowerCenterGoal(lowerCenter, bodySize.x)
                : DistanceToBody(new Bounds(
                    lowerCenter + Vector2.up * (bodySize.y * 0.5f), bodySize), request.TargetBounds, request.DistanceMetric);
        }

        /// <summary>Returns the minimum distance from a lower-center body swept along a segment to this region.</summary>
        internal float DistanceToLowerCenterBodySegment(Vector2 start, Vector2 end, Vector2 bodySize)
        {
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            if (IsGroundWalk) return DistanceToLowerCenterGoalSegment(start, end, bodySize.x);
            float minX = request.TargetBounds.min.x - bodySize.x * 0.5f;
            float maxX = request.TargetBounds.max.x + bodySize.x * 0.5f;
            float minY = request.TargetBounds.min.y - bodySize.y;
            float maxY = request.TargetBounds.max.y;
            return DistanceToSegmentBoundsMetric(start, end, minX, maxX, minY, maxY, request.DistanceMetric);
        }

        /// <summary>Returns the distance from a center-anchored body AABB to this region.</summary>
        public float DistanceToCenteredBody(Vector2 center, Vector2 bodySize)
        {
            Validate.Finite(center, nameof(center));
            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            return DistanceToBody(new Bounds(center, bodySize), request.TargetBounds, request.DistanceMetric);
        }

        /// <summary>Returns the axial gap between two finite, non-negative 2D AABBs.</summary>
        public static float AabbAxialGapDistance(Bounds first, Bounds second, DistanceMetric metric) => DistanceToBody(first, second, metric);

        /// <summary>Returns whether a lower-center body AABB has entered this region tolerance.</summary>
        public bool ContainsLowerCenterBody(Vector2 lowerCenter, Vector2 bodySize)
        {
            Validate.NonNegativeVector(bodySize, nameof(bodySize));
            return IsComplete(lowerCenter + Vector2.up * (bodySize.y * 0.5f), bodySize);
        }

        /// <summary>Returns whether a center-anchored body AABB has entered this region tolerance.</summary>
        public bool ContainsCenteredBody(Vector2 center, Vector2 bodySize) => IsComplete(center, bodySize);

        private static float DistanceToBody(Bounds body, Bounds target, DistanceMetric metric)
        {
            Validate.Bounds(body, nameof(body));
            Validate.Bounds(target, nameof(target));
            if (!Enum.IsDefined(typeof(DistanceMetric), metric))
                throw new ArgumentOutOfRangeException(nameof(metric), metric,
                    "Unknown distance metric.");

            float x = Mathf.Max(target.min.x - body.max.x, body.min.x - target.max.x, 0f);
            float y = Mathf.Max(target.min.y - body.max.y, body.min.y - target.max.y, 0f);
            switch (metric)
            {
                case DistanceMetric.Euclidean: return Mathf.Sqrt(x * x + y * y);
                case DistanceMetric.Manhattan: return x + y;
                case DistanceMetric.Chebyshev: return Mathf.Max(x, y);
                default:
                    throw new ArgumentOutOfRangeException(nameof(metric), metric,
                    "Unknown distance metric.");
            }
        }

        private static float DistanceToPoint(Vector2 point, Bounds bounds)
        {
            float x = Mathf.Max(bounds.min.x - point.x, 0f, point.x - bounds.max.x);
            float y = Mathf.Max(bounds.min.y - point.y, 0f, point.y - bounds.max.y);
            return Mathf.Sqrt(x * x + y * y);
        }

        private static float DistanceToSegmentBounds(Vector2 start, Vector2 end, Bounds bounds)
        {
            if (SegmentIntersectsClosedRect(start, end, bounds.min, bounds.max)) return 0f;
            float distance = Mathf.Min(DistanceToPoint(start, bounds), DistanceToPoint(end, bounds));
            Vector2 bottomLeft = new(bounds.min.x, bounds.min.y);
            Vector2 bottomRight = new(bounds.max.x, bounds.min.y);
            Vector2 topRight = new(bounds.max.x, bounds.max.y);
            Vector2 topLeft = new(bounds.min.x, bounds.max.y);
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, bottomLeft, bottomRight));
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, bottomRight, topRight));
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, topRight, topLeft));
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, topLeft, bottomLeft));
            return distance;
        }

        private static void ValidateGroundWalkBodyWidth(float bodyWidth)
        {
            Validate.NonNegativeFinite(bodyWidth, nameof(bodyWidth));
        }

        private static float DistanceToSegmentBoundsMetric(Vector2 start, Vector2 end,
            float minX, float maxX, float minY, float maxY, DistanceMetric metric)
        {
            if (metric == DistanceMetric.Euclidean)
            {
                Bounds bounds = new(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f),
                    new Vector3(maxX - minX, maxY - minY, 0f));
                return DistanceToSegmentBounds(start, end, bounds);
            }

            // Fixed-step ternary search intentionally bounds approximation error for the selected metric.
            if (SegmentIntersectsClosedRect(start, end, new Vector2(minX, minY), new Vector2(maxX, maxY))) return 0f;
            float distance = DistanceToRectMetric(start, minX, maxX, minY, maxY, metric);
            distance = Mathf.Min(distance, DistanceToRectMetric(end, minX, maxX, minY, maxY, metric));
            distance = Mathf.Min(distance, DistanceToAxisSegmentMetric(start, end,
                new Vector2(minX, minY), new Vector2(maxX, minY), metric));
            distance = Mathf.Min(distance, DistanceToAxisSegmentMetric(start, end,
                new Vector2(maxX, minY), new Vector2(maxX, maxY), metric));
            distance = Mathf.Min(distance, DistanceToAxisSegmentMetric(start, end,
                new Vector2(maxX, maxY), new Vector2(minX, maxY), metric));
            distance = Mathf.Min(distance, DistanceToAxisSegmentMetric(start, end,
                new Vector2(minX, maxY), new Vector2(minX, minY), metric));
            return distance;
        }

        private static float DistanceToRectMetric(Vector2 point, float minX, float maxX, float minY, float maxY,
            DistanceMetric metric)
        {
            float x = Mathf.Max(minX - point.x, 0f, point.x - maxX);
            float y = Mathf.Max(minY - point.y, 0f, point.y - maxY);
            return MetricLength(x, y, metric);
        }

        private static float DistanceToAxisSegmentMetric(Vector2 start, Vector2 end, Vector2 edgeStart,
            Vector2 edgeEnd, DistanceMetric metric)
        {
            // After 48 ternary iterations, the normalized parameter interval is at most
            // (2/3)^48 of its original interval; world-space uncertainty scales with
            // the finite segment length. Float rounding dominates in practice. This
            // assumes finite inputs and a convex metric-distance function along the
            // axis-aligned edge.
            float lower = 0f;
            float upper = 1f;
            for (int i = 0; i < 48; i++)
            {
                float first = (2f * lower + upper) / 3f;
                float second = (lower + 2f * upper) / 3f;
                if (DistanceToAxisSegmentAt(start, end, edgeStart, edgeEnd, metric, first)
                    <= DistanceToAxisSegmentAt(start, end, edgeStart, edgeEnd, metric, second)) upper = second;
                else lower = first;
            }
            return DistanceToAxisSegmentAt(start, end, edgeStart, edgeEnd, metric, (lower + upper) * 0.5f);
        }

        private static float DistanceToAxisSegmentAt(Vector2 start, Vector2 end, Vector2 edgeStart,
            Vector2 edgeEnd, DistanceMetric metric, float t)
        {
            Vector2 point = Vector2.Lerp(start, end, t);
            float edgeParameter = Mathf.Abs(edgeEnd.x - edgeStart.x) >= Mathf.Abs(edgeEnd.y - edgeStart.y)
                ? Mathf.Clamp(point.x, Mathf.Min(edgeStart.x, edgeEnd.x), Mathf.Max(edgeStart.x, edgeEnd.x))
                    - edgeStart.x
                : Mathf.Clamp(point.y, Mathf.Min(edgeStart.y, edgeEnd.y), Mathf.Max(edgeStart.y, edgeEnd.y))
                    - edgeStart.y;
            float edgeLength = Mathf.Abs(edgeEnd.x - edgeStart.x) >= Mathf.Abs(edgeEnd.y - edgeStart.y)
                ? edgeEnd.x - edgeStart.x : edgeEnd.y - edgeStart.y;
            float edgeT = Mathf.Abs(edgeLength) <= 0.0000001f ? 0f : edgeParameter / edgeLength;
            Vector2 closest = Vector2.Lerp(edgeStart, edgeEnd, edgeT);
            Vector2 delta = point - closest;
            return MetricLength(Mathf.Abs(delta.x), Mathf.Abs(delta.y), metric);
        }

        private static float MetricLength(float x, float y, DistanceMetric metric)
        {
            switch (metric)
            {
                case DistanceMetric.Euclidean: return Mathf.Sqrt(x * x + y * y);
                case DistanceMetric.Manhattan: return x + y;
                case DistanceMetric.Chebyshev: return Mathf.Max(x, y);
                default: throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric.");
            }
        }

        private static float DistanceToOpenRect(Vector2 point, float minX, float maxX, float minY, float maxY)
        {
            float dx = Mathf.Max(minX - point.x, 0f, point.x - maxX);
            float dy = Mathf.Max(minY - point.y, 0f, point.y - maxY);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static bool SegmentIntersectsOpenRect(Vector2 start, Vector2 end,
            float minX, float maxX, float minY, float maxY)
        {
            float lower = 0f;
            float upper = 1f;
            if (!ClipOpenAxis(start.x, end.x - start.x, minX, maxX, ref lower, ref upper)
                || !ClipOpenAxis(start.y, end.y - start.y, minY, maxY, ref lower, ref upper)) return false;
            return lower < upper;
        }

        private static bool ClipOpenAxis(float origin, float direction, float min, float max,
            ref float lower, ref float upper)
        {
            if (Mathf.Abs(direction) <= 0.0000001f)
                return origin > min && origin < max;

            float first = (min - origin) / direction;
            float second = (max - origin) / direction;
            if (first > second) (first, second) = (second, first);
            lower = Mathf.Max(lower, first);
            upper = Mathf.Min(upper, second);
            return lower < upper;
        }

        private static float DistanceBetweenSegments(Vector2 firstStart, Vector2 firstEnd,
            Vector2 secondStart, Vector2 secondEnd)
        {
            if (SegmentIntersectsClosedRect(firstStart, firstEnd, secondStart, secondEnd)) return 0f;
            float firstStartDistance = DistanceToSegment(firstStart, secondStart, secondEnd);
            float firstEndDistance = DistanceToSegment(firstEnd, secondStart, secondEnd);
            float secondStartDistance = DistanceToSegment(secondStart, firstStart, firstEnd);
            float secondEndDistance = DistanceToSegment(secondEnd, firstStart, firstEnd);
            return Mathf.Min(Mathf.Min(firstStartDistance, firstEndDistance),
                Mathf.Min(secondStartDistance, secondEndDistance));
        }

        private static bool SegmentIntersectsClosedRect(Vector2 start, Vector2 end, Vector2 min, Vector2 max)
        {
            float lower = 0f;
            float upper = 1f;
            return ClipClosedAxis(start.x, end.x - start.x, min.x, max.x, ref lower, ref upper)
                && ClipClosedAxis(start.y, end.y - start.y, min.y, max.y, ref lower, ref upper);
        }

        private static bool ClipClosedAxis(float origin, float direction, float min, float max,
            ref float lower, ref float upper)
        {
            if (Mathf.Abs(direction) <= 0.0000001f) return origin >= min && origin <= max;
            float first = (min - origin) / direction;
            float second = (max - origin) / direction;
            if (first > second) (first, second) = (second, first);
            lower = Mathf.Max(lower, first);
            upper = Mathf.Min(upper, second);
            return lower <= upper;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;
            float lengthSquared = direction.sqrMagnitude;
            float projection = lengthSquared <= 0.0000001f
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(point - start, direction) / lengthSquared);
            return Vector2.Distance(point, start + direction * projection);
        }

    }

}
