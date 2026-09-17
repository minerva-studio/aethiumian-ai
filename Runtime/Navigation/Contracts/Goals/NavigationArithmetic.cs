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
            if (SegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd)) return 0f;
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

        /// <summary>
        /// Returns the cross product of two vectors, used for orientation tests.
        /// </summary>
        public static float Cross(Vector2 left, Vector2 right) => left.x * right.y - left.y * right.x;

        /// <summary>
        /// Returns the summed winding of a polygon; the sign reports its orientation.
        /// </summary>
        public static float PolygonWinding(Vector2[] vertices)
        {
            float result = 0f;
            for (int index = 0; index < vertices.Length; index++) result += Cross(vertices[index], vertices[(index + 1) % vertices.Length]);
            return result;
        }

        /// <summary>
        /// Returns whether a point lies inside a polygon using the crossing-number rule.
        /// </summary>
        public static bool PointInPolygon(Vector2 point, Vector2[] vertices)
        {
            bool inside = false;
            for (int index = 0, previous = vertices.Length - 1; index < vertices.Length; previous = index++)
            {
                Vector2 a = vertices[index];
                Vector2 b = vertices[previous];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Returns whether a point lies on a segment within the navigation epsilon.
        /// </summary>
        public static bool IsPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
            => point.x >= Mathf.Min(start.x, end.x) - NavigationConstant.Epsilon
                && point.x <= Mathf.Max(start.x, end.x) + NavigationConstant.Epsilon
                && point.y >= Mathf.Min(start.y, end.y) - NavigationConstant.Epsilon
                && point.y <= Mathf.Max(start.y, end.y) + NavigationConstant.Epsilon;

        /// <summary>
        /// Returns whether two segments touch or cross, including collinear contact.
        /// </summary>
        public static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float first = Cross(b - a, c - a);
            float second = Cross(b - a, d - a);
            float third = Cross(d - c, a - c);
            float fourth = Cross(d - c, b - c);
            bool proper = (first > NavigationConstant.Epsilon && second < -NavigationConstant.Epsilon
                    || first < -NavigationConstant.Epsilon && second > NavigationConstant.Epsilon)
                && (third > NavigationConstant.Epsilon && fourth < -NavigationConstant.Epsilon
                    || third < -NavigationConstant.Epsilon && fourth > NavigationConstant.Epsilon);
            return proper
                || Mathf.Abs(first) <= NavigationConstant.Epsilon && IsPointOnSegment(c, a, b)
                || Mathf.Abs(second) <= NavigationConstant.Epsilon && IsPointOnSegment(d, a, b)
                || Mathf.Abs(third) <= NavigationConstant.Epsilon && IsPointOnSegment(a, c, d)
                || Mathf.Abs(fourth) <= NavigationConstant.Epsilon && IsPointOnSegment(b, c, d);
        }

        /// <summary>
        /// Returns whether a point lies strictly inside a rectangle, ignoring its boundary.
        /// </summary>
        public static bool IsStrictlyInside(Vector2 point, Rect rect)
            => point.x > rect.xMin + NavigationConstant.Epsilon && point.x < rect.xMax - NavigationConstant.Epsilon
                && point.y > rect.yMin + NavigationConstant.Epsilon && point.y < rect.yMax - NavigationConstant.Epsilon;

        /// <summary>
        /// Clips the parametric interval [0, 1] against one rectangle slab. The numerator is the negated
        /// direction component and the denominator the offset from the slab's lower bound.
        /// </summary>
        public static bool ClipRectAxis(float numerator, float denominator, ref float enter, ref float exit)
        {
            if (Mathf.Abs(numerator) <= NavigationConstant.Epsilon) return denominator >= 0f;
            float value = denominator / numerator;
            if (numerator > 0f) exit = Mathf.Min(exit, value); else enter = Mathf.Max(enter, value);
            return enter <= exit;
        }

        /// <summary>
        /// Returns whether a segment crosses the interior of a rectangle. Boundary contact alone does not
        /// count, which is what separates this predicate from <see cref="SegmentIntersectsClosedRect"/>.
        /// </summary>
        public static bool SegmentIntersectsRectInterior(Vector2 a, Vector2 b, Rect rect)
        {
            float enter = 0f;
            float exit = 1f;
            Vector2 delta = b - a;
            return ClipRectAxis(-delta.x, a.x - rect.xMin, ref enter, ref exit)
                && ClipRectAxis(delta.x, rect.xMax - a.x, ref enter, ref exit)
                && ClipRectAxis(-delta.y, a.y - rect.yMin, ref enter, ref exit)
                && ClipRectAxis(delta.y, rect.yMax - a.y, ref enter, ref exit)
                && exit > enter + NavigationConstant.Epsilon;
        }

        /// <summary>
        /// Returns whether a circle overlaps a rectangle's interior.
        /// </summary>
        public static bool CircleIntersectsRect(Vector2 center, float radius, Rect rect)
        {
            float dx = Mathf.Max(rect.xMin - center.x, 0f, center.x - rect.xMax);
            float dy = Mathf.Max(rect.yMin - center.y, 0f, center.y - rect.yMax);
            return dx * dx + dy * dy < radius * radius - NavigationConstant.Epsilon;
        }

        /// <summary>
        /// Returns whether a capsule around a segment overlaps a rectangle.
        /// </summary>
        public static bool SegmentIntersectsExpandedRect(Vector2 a, Vector2 b, float radius, Rect rect)
        {
            Rect expanded = rect;
            expanded.xMin -= radius; expanded.xMax += radius; expanded.yMin -= radius; expanded.yMax += radius;
            return SegmentIntersectsRectInterior(a, b, expanded)
                || DistanceBetweenSegments(a, b, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin)) <= radius + NavigationConstant.Epsilon
                || DistanceBetweenSegments(a, b, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax)) <= radius + NavigationConstant.Epsilon
                || DistanceBetweenSegments(a, b, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)) <= radius + NavigationConstant.Epsilon
                || DistanceBetweenSegments(a, b, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin)) <= radius + NavigationConstant.Epsilon;
        }

        /// <summary>
        /// Returns whether a polygon overlaps a rectangle's interior.
        /// </summary>
        public static bool PolygonIntersectsRect(Vector2[] vertices, Rect rect)
        {
            for (int index = 0; index < vertices.Length; index++)
                if (IsStrictlyInside(vertices[index], rect)) return true;
            if (PointInPolygon(rect.center, vertices)) return true;
            for (int index = 0; index < vertices.Length; index++)
                if (SegmentIntersectsRectInterior(vertices[index], vertices[(index + 1) % vertices.Length], rect)) return true;
            return false;
        }








        public static bool TryGetCircleSurface(Vector2 center, float radius, float x, out float y, out Vector2 normal)
        {
            y = float.NegativeInfinity; normal = Vector2.up;
            float dx = x - center.x;
            if (Mathf.Abs(dx) > radius + NavigationConstant.Epsilon || radius <= NavigationConstant.Epsilon) return false;
            float dy = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dx * dx));
            y = center.y + dy;
            normal = new Vector2(dx, dy).normalized;
            return true;
        }

        public static bool TryGetSegmentSurface(Vector2 a, Vector2 b, float x, out float y, out Vector2 normal, float polygonSign = 0f, bool orientUp = true)
        {
            y = 0f; normal = Vector2.up;
            float deltaX = b.x - a.x;
            if (Mathf.Abs(deltaX) <= NavigationConstant.Epsilon) return false;
            float parameter = (x - a.x) / deltaX;
            if (parameter < -NavigationConstant.Epsilon || parameter > 1f + NavigationConstant.Epsilon) return false;
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
    }
}
