using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Pure geometry helpers for the center-distance budget of a Retreat traversal.</summary>
    public static class RetreatNavigationGeometry
    {
        /// <summary>Returns the greatest decrease in target-center distance along one straight segment.</summary>
        public static float SegmentApproachDistance(Vector2 start, Vector2 end, Vector2 targetCenter)
        {
            Validate.Finite(start, nameof(start));
            Validate.Finite(end, nameof(end));
            Validate.Finite(targetCenter, nameof(targetCenter));

            Vector2 segment = end - start;
            float segmentLengthSquared = segment.sqrMagnitude;
            float parameter = segmentLengthSquared <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon
                ? 0f
                : Mathf.Clamp01(Vector2.Dot(targetCenter - start, segment) / segmentLengthSquared);
            Vector2 closest = start + segment * parameter;
            return Mathf.Max(0f, Vector2.Distance(start, targetCenter) - Vector2.Distance(closest, targetCenter));
        }

        /// <summary>Returns cumulative center-distance decreases over a sequence of route endpoints.</summary>
        public static float RouteApproachDistance(Vector2 start, Vector2 targetCenter, IReadOnlyList<NavigationRouteSegment> segments)
        {
            Validate.Finite(start, nameof(start));
            Validate.Finite(targetCenter, nameof(targetCenter));
            if (segments == null) throw new ArgumentNullException(nameof(segments));

            return RouteApproachDistanceCore(start, targetCenter, segments);
        }

        public static float RouteApproachDistance(Vector2 start, Vector2 targetCenter, NavigationRoute route)
        {
            Validate.Finite(start, nameof(start));
            Validate.Finite(targetCenter, nameof(targetCenter));
            return RouteApproachDistanceCore(start, targetCenter, route);
        }

        private static float RouteApproachDistanceCore<T>(Vector2 start, Vector2 targetCenter, T segments)
            where T : IReadOnlyList<NavigationRouteSegment>
        {
            float total = 0f;
            Vector2 previous = start;
            for (int index = 0; index < segments.Count; index++)
            {
                NavigationRouteSegment segment = segments[index] ?? throw new ArgumentException("Retreat routes cannot contain null segments.", nameof(segments));
                total += SegmentApproachDistance(previous, segment.End, targetCenter);
                previous = segment.End;
            }

            return total;
        }

    }

}
