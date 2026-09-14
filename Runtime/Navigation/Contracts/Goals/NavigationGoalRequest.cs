using System;
using UnityEngine;
using static Aethiumian.AI.Navigation.NavigationArithmetic;
using static Aethiumian.AI.Navigation.Validate;

namespace Aethiumian.AI.Navigation
{

    /// <summary>
    /// Immutable, Unity-scene-independent data captured at a movement caller boundary.
    /// </summary>
    public readonly struct NavigationGoalRequest
    {
        public Bounds TargetBounds { get; }
        public DistanceMetric DistanceMetric { get; }
        public bool RequiresLineOfSight { get; }
        public float ArrivalTolerance { get; }
        public float RetreatDistance { get; }
        public NavigationGoalGeometry Geometry { get; }

        public NavigationGoalRequest(Bounds targetBounds, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance, float retreatDistance)
        {
            ValidateMetric(distanceMetric);
            Bounds(targetBounds, nameof(targetBounds));
            NonNegativeFinite(arrivalTolerance, nameof(arrivalTolerance));
            NonNegativeFinite(retreatDistance, nameof(retreatDistance));
            TargetBounds = targetBounds;
            Geometry = geometry;
            DistanceMetric = distanceMetric;
            RequiresLineOfSight = requiresLineOfSight;
            ArrivalTolerance = arrivalTolerance;
            RetreatDistance = retreatDistance;
        }




        /// <summary>
        /// Returns the lower-center acceptance bounds for the supplied body width.
        /// </summary>
        public Bounds GetLowerCenterAcceptanceBounds(float bodyWidth, float cellSize = 1f)
        {
            NonNegativeFinite(bodyWidth, nameof(bodyWidth));
            return new Bounds(new Vector3(TargetBounds.center.x, TargetBounds.min.y, 0f), new Vector3(TargetBounds.size.x + bodyWidth + ArrivalTolerance * 2f, cellSize * 2f, 0f));
        }

        /// <summary>
        /// Returns whether a lower-center anchor is inside this Ground Walk goal.
        /// </summary>
        public bool ContainsLowerCenterGoal(Vector2 lowerCenter, float bodyWidth) => DistanceToLowerCenterGoal(lowerCenter, bodyWidth) <= NavigationWorldQueries.GeometryEpsilon;

        /// <summary>
        /// Returns the distance from a lower-center anchor to this Ground Walk goal.
        /// </summary>
        public float DistanceToLowerCenterGoal(Vector2 lowerCenter, float bodyWidth)
        {
            Bounds acceptanceBounds = GetLowerCenterAcceptanceBounds(bodyWidth);
            Finite(lowerCenter, nameof(lowerCenter));
            return DistanceToPoint(lowerCenter, acceptanceBounds);
        }

        /// <summary>
        /// Returns the minimum distance from a lower-center segment to this Ground Walk goal.
        /// </summary>
        public float DistanceToLowerCenterGoalSegment(Vector2 start, Vector2 end, float bodyWidth)
        {
            Bounds acceptanceBounds = GetLowerCenterAcceptanceBounds(bodyWidth);
            Finite(start, nameof(start));
            Finite(end, nameof(end));
            return DistanceToSegmentBounds(start, end, acceptanceBounds);
        }

        /// <summary>
        /// Gets the best-effort distance from a mover center to the raw target center.
        /// </summary>
        public float GuidanceDistance(Vector2 center, Vector2 bodySize)
        {
            Finite(center, nameof(center));
            NonNegativeVector(bodySize, nameof(bodySize));
            if (Geometry == NavigationGoalGeometry.Retreat) return -DistanceToCenteredBody(center, bodySize);
            Vector2 delta = center - new Vector2(TargetBounds.center.x, TargetBounds.center.y);
            return Geometry == NavigationGoalGeometry.GroundRange ? delta.magnitude : MetricLength(Mathf.Abs(delta.x), Mathf.Abs(delta.y), DistanceMetric);
        }

        /// <summary>
        /// Returns the distance from a center-anchored body AABB to this region.
        /// </summary>
        public float DistanceToCenteredBody(Vector2 center, Vector2 bodySize)
        {
            Finite(center, nameof(center));
            NonNegativeVector(bodySize, nameof(bodySize));
            return DistanceToBody(new Bounds(center, bodySize), TargetBounds, DistanceMetric);
        }





        public static NavigationGoalRequest GroundRange(Bounds targetBounds, float arrivalTolerance, bool requiresLineOfSight = false)
            => new(targetBounds, NavigationGoalGeometry.GroundRange, DistanceMetric.Euclidean, requiresLineOfSight, arrivalTolerance, 0f);

        public static NavigationGoalRequest Proximity(Bounds targetBounds, DistanceMetric distanceMetric, float arrivalTolerance, bool requiresLineOfSight = false)
            => new(targetBounds, NavigationGoalGeometry.Proximity, distanceMetric, requiresLineOfSight, arrivalTolerance, 0f);

        /// <summary>Creates an open-ended goal that completes after the mover is far enough from the target.</summary>
        public static NavigationGoalRequest Retreat(Bounds targetBounds, DistanceMetric distanceMetric, float retreatDistance)
            => new(targetBounds, NavigationGoalGeometry.Retreat, distanceMetric, false, 0f, retreatDistance);

        public static NavigationGoalRequest Point(Vector2 destination, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance)
            => new(new Bounds(destination, Vector3.zero), geometry, distanceMetric, requiresLineOfSight, arrivalTolerance, 0f);

        /// <summary>Creates an equivalent request with only its captured target bounds replaced.</summary>
        public NavigationGoalRequest WithTargetBounds(Bounds targetBounds)
            => new(targetBounds, Geometry, DistanceMetric, RequiresLineOfSight, ArrivalTolerance, RetreatDistance);


        private static void ValidateMetric(DistanceMetric value)
        {
            if (!Enum.IsDefined(typeof(DistanceMetric), value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown distance metric.");
        }
    }

    public static class NavigationArithmetic
    {
        public static float DistanceToBody(Bounds body, Bounds target, DistanceMetric metric)
        {
            Bounds(body, nameof(body));
            Bounds(target, nameof(target));
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

        public static float DistanceToPoint(Vector2 point, Bounds bounds)
        {
            float x = Mathf.Max(bounds.min.x - point.x, 0f, point.x - bounds.max.x);
            float y = Mathf.Max(bounds.min.y - point.y, 0f, point.y - bounds.max.y);
            return Mathf.Sqrt(x * x + y * y);
        }

        public static float DistanceToSegmentBounds(Vector2 start, Vector2 end, Bounds bounds)
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

        /// <summary>Calculates the distance from a segment to a rectangle using a specific distance metric.</summary>
        public static float DistanceToSegmentBoundsMetric(Vector2 start, Vector2 end, float minX, float maxX, float minY, float maxY, DistanceMetric metric)
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

        /// <summary>Calculates the distance from a point to a rectangle using a specific distance metric.</summary>
        public static float DistanceToRectMetric(Vector2 point, float minX, float maxX, float minY, float maxY, DistanceMetric metric)
        {
            float x = Mathf.Max(minX - point.x, 0f, point.x - maxX);
            float y = Mathf.Max(minY - point.y, 0f, point.y - maxY);
            return MetricLength(x, y, metric);
        }

        /// <summary>Calculates the distance from an axis-aligned segment to another axis-aligned segment using a specific distance metric.</summary>
        public static float DistanceToAxisSegmentMetric(Vector2 start, Vector2 end, Vector2 edgeStart, Vector2 edgeEnd, DistanceMetric metric)
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



        /// <summary>Calculates the distance from a point on a segment to a point on an axis-aligned segment using a specific distance metric.</summary>
        public static float DistanceToAxisSegmentAt(Vector2 start, Vector2 end, Vector2 edgeStart, Vector2 edgeEnd, DistanceMetric metric, float t)
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

        /// <summary>Calculates the length of a vector using a specific distance metric.</summary>
        public static float MetricLength(float x, float y, DistanceMetric metric)
        {
            switch (metric)
            {
                case DistanceMetric.Euclidean: return Mathf.Sqrt(x * x + y * y);
                case DistanceMetric.Manhattan: return x + y;
                case DistanceMetric.Chebyshev: return Mathf.Max(x, y);
                default: throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric.");
            }
        }

        /// <summary>
        /// Calculates the distance from a point to an open rectangle using Euclidean distance.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="minX"></param>
        /// <param name="maxX"></param>
        /// <param name="minY"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public static float DistanceToOpenRect(Vector2 point, float minX, float maxX, float minY, float maxY)
        {
            float dx = Mathf.Max(minX - point.x, 0f, point.x - maxX);
            float dy = Mathf.Max(minY - point.y, 0f, point.y - maxY);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static bool SegmentIntersectsOpenRect(Vector2 start, Vector2 end, float minX, float maxX, float minY, float maxY)
        {
            float lower = 0f;
            float upper = 1f;
            if (!ClipOpenAxis(start.x, end.x - start.x, minX, maxX, ref lower, ref upper)
                || !ClipOpenAxis(start.y, end.y - start.y, minY, maxY, ref lower, ref upper)) return false;
            return lower < upper;
        }

        /// <summary>
        /// Clips a line segment against an open axis.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="lower"></param>
        /// <param name="upper"></param>
        /// <returns></returns>
        public static bool ClipOpenAxis(float origin, float direction, float min, float max, ref float lower, ref float upper)
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

        /// <summary>
        /// Calculates the distance between two line segments.
        /// </summary>
        /// <param name="firstStart"></param>
        /// <param name="firstEnd"></param>
        /// <param name="secondStart"></param>
        /// <param name="secondEnd"></param>
        /// <returns></returns>
        public static float DistanceBetweenSegments(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
        {
            if (SegmentIntersectsClosedRect(firstStart, firstEnd, secondStart, secondEnd)) return 0f;
            float firstStartDistance = DistanceToSegment(firstStart, secondStart, secondEnd);
            float firstEndDistance = DistanceToSegment(firstEnd, secondStart, secondEnd);
            float secondStartDistance = DistanceToSegment(secondStart, firstStart, firstEnd);
            float secondEndDistance = DistanceToSegment(secondEnd, firstStart, firstEnd);
            return Mathf.Min(Mathf.Min(firstStartDistance, firstEndDistance),
                Mathf.Min(secondStartDistance, secondEndDistance));
        }


        /// <summary>
        /// Checks if a line segment intersects a closed rectangle.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static bool SegmentIntersectsClosedRect(Vector2 start, Vector2 end, Vector2 min, Vector2 max)
        {
            float lower = 0f;
            float upper = 1f;
            return ClipClosedAxis(start.x, end.x - start.x, min.x, max.x, ref lower, ref upper)
                && ClipClosedAxis(start.y, end.y - start.y, min.y, max.y, ref lower, ref upper);
        }


        /// <summary>
        /// Clips a line segment against a closed axis.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="lower"></param>
        /// <param name="upper"></param>
        /// <returns></returns>
        public static bool ClipClosedAxis(float origin, float direction, float min, float max, ref float lower, ref float upper)
        {
            if (Mathf.Abs(direction) <= 0.0000001f) return origin >= min && origin <= max;
            float first = (min - origin) / direction;
            float second = (max - origin) / direction;
            if (first > second) (first, second) = (second, first);
            lower = Mathf.Max(lower, first);
            upper = Mathf.Min(upper, second);
            return lower <= upper;
        }

        /// <summary>
        /// Calculates the distance from a point to a line segment.
        /// </summary>
        /// <param name="point"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
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
