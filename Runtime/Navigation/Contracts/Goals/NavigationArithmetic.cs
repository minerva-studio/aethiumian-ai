using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    public static class NavigationArithmetic
    {
        public static float DistanceToPoint(Vector2 point, AABB bounds)
        {
            float x = Mathf.Max(bounds.Min.x - point.x, 0f, point.x - bounds.Max.x);
            float y = Mathf.Max(bounds.Min.y - point.y, 0f, point.y - bounds.Max.y);
            return Mathf.Sqrt(x * x + y * y);
        }

        public static float DistanceToSegmentBounds(Vector2 start, Vector2 end, AABB bounds)
        {
            if (SegmentIntersectsClosedRect(start, end, bounds.Min, bounds.Max)) return 0f;
            float distance = Mathf.Min(DistanceToPoint(start, bounds), DistanceToPoint(end, bounds));
            Vector2 bottomLeft = bounds.Min;
            Vector2 bottomRight = new(bounds.Max.x, bounds.Min.y);
            Vector2 topRight = bounds.Max;
            Vector2 topLeft = new(bounds.Min.x, bounds.Max.y);
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, bottomLeft, bottomRight));
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, bottomRight, topRight));
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, topRight, topLeft));
            distance = Mathf.Min(distance, DistanceBetweenSegments(start, end, topLeft, bottomLeft));
            return distance;
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
            if (!ClipOpenAxis(start.x, end.x - start.x, minX, maxX, ref lower, ref upper) || !ClipOpenAxis(start.y, end.y - start.y, minY, maxY, ref lower, ref upper)) return false;
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
            if (Mathf.Abs(direction) <= NavigationConstant.DegenerateAxis)
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
            return Mathf.Min(Mathf.Min(firstStartDistance, firstEndDistance), Mathf.Min(secondStartDistance, secondEndDistance));
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
            if (Mathf.Abs(direction) <= NavigationConstant.DegenerateAxis) return origin >= min && origin <= max;
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
            float projection = lengthSquared <= NavigationConstant.DegenerateAxis
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(point - start, direction) / lengthSquared);
            return Vector2.Distance(point, start + direction * projection);
        }

        /// <summary>
        /// Determines whether two points are approximately equal within a given epsilon.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <param name="epsilon"></param>
        /// <returns></returns>
        public static bool Approximately(Vector2 first, Vector2 second, float epsilon)
        {
            return Mathf.Abs(first.x - second.x) <= epsilon && Mathf.Abs(first.y - second.y) <= epsilon;
        }
    }
}
