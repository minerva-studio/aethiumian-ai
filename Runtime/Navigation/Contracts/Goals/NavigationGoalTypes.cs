using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>Product-level movement goal selection, serialized by coordinators.</summary>
    public enum MovementGoal
    {
        Default = 0,
        Confront,
        Proximity,
        FiringPosition,
    }

    /// <summary>Metric used when measuring the axial gap between two AABBs.</summary>
    public enum DistanceMetric
    {
        Euclidean = 0,
        Manhattan,
        Chebyshev,
    }

    /// <summary>Planner geometry independent of product-level movement goal names.</summary>
    public enum NavigationGoalGeometry
    {
        GroundRange,
        Proximity,
        Retreat,
    }


    public static class DistanceMetricExtensions
    {
        public static float MetricLength(this DistanceMetric metric, float x, float y)
        {
            return metric switch
            {
                DistanceMetric.Euclidean => Mathf.Sqrt(x * x + y * y),
                DistanceMetric.Manhattan => Mathf.Abs(x) + Mathf.Abs(y),
                DistanceMetric.Chebyshev => Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)),
                _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric."),
            };
        }

        public static float MetricLength(this DistanceMetric metric, Vector2 delta)
        {
            return metric switch
            {
                DistanceMetric.Euclidean => delta.magnitude,
                DistanceMetric.Manhattan => Mathf.Abs(delta.x) + Mathf.Abs(delta.y),
                DistanceMetric.Chebyshev => Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)),
                _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric."),
            };
        }

        public static float MetricLength(this DistanceMetric metric, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            return metric switch
            {
                DistanceMetric.Euclidean => delta.magnitude,
                DistanceMetric.Manhattan => Mathf.Abs(delta.x) + Mathf.Abs(delta.y),
                DistanceMetric.Chebyshev => Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)),
                _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric."),
            };
        }

        public static float MetricLength(this DistanceMetric metric, Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            return metric switch
            {
                DistanceMetric.Euclidean => delta.magnitude,
                DistanceMetric.Manhattan => Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.z),
                DistanceMetric.Chebyshev => Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z)),
                _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric."),
            };
        }



        public static float DistanceToBody(this DistanceMetric metric, AABB body, AABB target)
        {
            // AABB exposes settable corners and its constructor cannot reject NaN, so goal geometry
            // must still validate both boxes at this boundary.
            Validate.Aabb(body, nameof(body));
            Validate.Aabb(target, nameof(target));
            if (!Enum.IsDefined(typeof(DistanceMetric), metric))
                throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown distance metric.");

            float x = Mathf.Max(target.Min.x - body.Max.x, body.Min.x - target.Max.x, 0f);
            float y = Mathf.Max(target.Min.y - body.Max.y, body.Min.y - target.Max.y, 0f);
            return metric.MetricLength(x, y);
        }

        /// <summary>
        /// Calculates the distance from a point on a segment to a point on an axis-aligned segment using a specific distance metric.
        /// </summary>
        public static float DistanceToAxisSegmentAt(this DistanceMetric metric, Vector2 edgeStart, Vector2 edgeEnd, Vector2 point)
        {
            float edgeParameter = Mathf.Abs(edgeEnd.x - edgeStart.x) >= Mathf.Abs(edgeEnd.y - edgeStart.y)
                ? Mathf.Clamp(point.x, Mathf.Min(edgeStart.x, edgeEnd.x), Mathf.Max(edgeStart.x, edgeEnd.x)) - edgeStart.x
                : Mathf.Clamp(point.y, Mathf.Min(edgeStart.y, edgeEnd.y), Mathf.Max(edgeStart.y, edgeEnd.y)) - edgeStart.y;
            float edgeLength = Mathf.Abs(edgeEnd.x - edgeStart.x) >= Mathf.Abs(edgeEnd.y - edgeStart.y)
                ? edgeEnd.x - edgeStart.x
                : edgeEnd.y - edgeStart.y;
            float edgeT = Mathf.Abs(edgeLength) <= NavigationConstant.DegenerateAxis ? 0f : edgeParameter / edgeLength;
            Vector2 closest = Vector2.Lerp(edgeStart, edgeEnd, edgeT);
            Vector2 delta = point - closest;
            return metric.MetricLength(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        }

        /// <summary>
        /// Calculates the distance from a point to a rectangle using a specific distance metric.
        /// </summary>
        public static float DistanceToRectMetric(this DistanceMetric metric, Vector2 point, float minX, float maxX, float minY, float maxY)
        {
            float x = Mathf.Max(minX - point.x, 0f, point.x - maxX);
            float y = Mathf.Max(minY - point.y, 0f, point.y - maxY);
            return metric.MetricLength(x, y);
        }

        /// <summary>
        /// Calculates the distance from an axis-aligned segment to another axis-aligned segment using a specific distance metric.
        /// </summary>
        public static float DistanceToAxisSegmentMetric(this DistanceMetric metric, Vector2 start, Vector2 end, Vector2 edgeStart, Vector2 edgeEnd)
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

                float firstDistance = metric.DistanceToAxisSegmentAt(edgeStart, edgeEnd, Vector2.Lerp(start, end, first));
                float secondDistance = metric.DistanceToAxisSegmentAt(edgeStart, edgeEnd, Vector2.Lerp(start, end, second));
                if (firstDistance <= secondDistance) upper = second;
                else lower = first;
            }

            return metric.DistanceToAxisSegmentAt(edgeStart, edgeEnd, Vector2.Lerp(start, end, (lower + upper) * 0.5f));
        }

        /// <summary>
        /// Calculates the distance from a segment to a rectangle using a specific distance metric.
        /// </summary>
        public static float DistanceToSegmentBoundsMetric(this DistanceMetric metric, Vector2 start, Vector2 end, float minX, float maxX, float minY, float maxY)
        {
            if (metric == DistanceMetric.Euclidean)
                return NavigationArithmetic.DistanceToSegmentBounds(start, end, new AABB(minX, minY, maxX, maxY));

            // Fixed-step ternary search intentionally bounds approximation error for the selected metric.
            if (NavigationArithmetic.SegmentIntersectsClosedRect(start, end, new Vector2(minX, minY), new Vector2(maxX, maxY))) return 0f;
            float distance = metric.DistanceToRectMetric(start, minX, maxX, minY, maxY);
            distance = Mathf.Min(distance, metric.DistanceToRectMetric(end, minX, maxX, minY, maxY));
            distance = Mathf.Min(distance, metric.DistanceToAxisSegmentMetric(start, end, new Vector2(minX, minY), new Vector2(maxX, minY)));
            distance = Mathf.Min(distance, metric.DistanceToAxisSegmentMetric(start, end, new Vector2(maxX, minY), new Vector2(maxX, maxY)));
            distance = Mathf.Min(distance, metric.DistanceToAxisSegmentMetric(start, end, new Vector2(maxX, maxY), new Vector2(minX, maxY)));
            distance = Mathf.Min(distance, metric.DistanceToAxisSegmentMetric(start, end, new Vector2(minX, maxY), new Vector2(minX, minY)));
            return distance;
        }
    }
}
