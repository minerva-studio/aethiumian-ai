using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{

    /// <summary>
    /// Immutable planner snapshot for one world, start anchor, and goal region.
    /// Coordinators may replace uncommitted suffixes; this object never owns execution.
    /// </summary>
    public sealed class NavigationRoute
    {
        private readonly ReadOnlyCollection<NavigationRouteSegment> segments;

        /// <summary>Gets the world-space origin used by the planner.</summary>
        public Vector2 Start { get; }

        /// <summary>Gets the center of the requested goal region.</summary>
        public Vector2 RequestedGoal => GoalRegion.Center;

        /// <summary>Gets the immutable goal geometry used by the planner.</summary>
        public NavigationGoalRegion GoalRegion { get; }

        /// <summary>Gets the best-effort world-space endpoint selected by the planner.</summary>
        public Vector2 ResolvedGoal { get; }

        /// <summary>Gets the read-only route segments in execution order.</summary>
        public IReadOnlyList<NavigationRouteSegment> Segments => segments;

        /// <summary>Gets the number of route segments.</summary>
        public int Count => segments.Count;

        /// <summary>Gets whether this route is a terminal ordinary-navigation result.</summary>
        public bool SearchComplete { get; }

        private NavigationRoute(Vector2 start, NavigationGoalRegion goalRegion, Vector2 resolvedGoal,
            NavigationRouteSegment[] segments, bool searchComplete)
        {
            Start = start;
            GoalRegion = goalRegion;
            ResolvedGoal = resolvedGoal;
            SearchComplete = searchComplete;
            this.segments = new ReadOnlyCollection<NavigationRouteSegment>(segments);
        }

        /// <summary>Creates a route against an immutable goal region.</summary>
        public static NavigationRoute Create(Vector2 start, NavigationGoalRegion goalRegion, Vector2 resolvedGoal,
            IEnumerable<NavigationRouteSegment> segments, bool searchComplete = true)
        {
            if (goalRegion == null) throw new ArgumentNullException(nameof(goalRegion));
            return CreateInternal(start, goalRegion, resolvedGoal, segments, searchComplete);
        }

        /// <summary>Creates a route that represents a complete search or direct result.</summary>
        public static NavigationRoute Complete(Vector2 start, NavigationGoalRegion goalRegion, Vector2 resolvedGoal,
            IEnumerable<NavigationRouteSegment> segments)
        {
            if (goalRegion == null) throw new ArgumentNullException(nameof(goalRegion));
            return CreateInternal(start, goalRegion, resolvedGoal, segments, true);
        }

        /// <summary>Creates a route that represents an executable prefix or best-effort result.</summary>
        public static NavigationRoute Partial(Vector2 start, NavigationGoalRegion goalRegion, Vector2 resolvedGoal,
            IEnumerable<NavigationRouteSegment> segments)
        {
            if (goalRegion == null) throw new ArgumentNullException(nameof(goalRegion));
            return CreateInternal(start, goalRegion, resolvedGoal, segments, false);
        }

        /// <summary>Replaces this route's segments while preserving its origin, goal, endpoint, and completeness.</summary>
        public NavigationRoute WithSegments(IEnumerable<NavigationRouteSegment> replacementSegments)
            => CreateInternal(Start, GoalRegion, ResolvedGoal, replacementSegments, SearchComplete);

        private static NavigationRoute CreateInternal(Vector2 start, NavigationGoalRegion goalRegion, Vector2 resolvedGoal,
            IEnumerable<NavigationRouteSegment> segments, bool searchComplete)
        {
            if (!NavigationNumeric.IsFinite(start) || !NavigationNumeric.IsFinite(resolvedGoal))
                throw new ArgumentException("Navigation plan coordinates must be finite world coordinates.");

            if (segments == null) throw new ArgumentNullException(nameof(segments));

            NavigationRouteSegment[] copiedSegments = new List<NavigationRouteSegment>(segments).ToArray();
            for (int i = 0; i < copiedSegments.Length; i++)
            {
                if (copiedSegments[i] == null)
                {
                    throw new ArgumentException("Navigation routes cannot contain null segments.", nameof(segments));
                }
            }

            if (copiedSegments.Length == 0)
            {
                if (!start.Equals(resolvedGoal))
                {
                    throw new ArgumentException("An empty route is valid only when Start equals ResolvedGoal.", nameof(segments));
                }

                return new NavigationRoute(start, goalRegion, resolvedGoal, copiedSegments, searchComplete);
            }

            if (!copiedSegments[0].Start.Equals(start))
            {
                throw new ArgumentException("The first route segment must start at Start.", nameof(segments));
            }

            for (int i = 1; i < copiedSegments.Length; i++)
            {
                if (!copiedSegments[i - 1].End.Equals(copiedSegments[i].Start))
                {
                    throw new ArgumentException("Route segments must form a continuous world-space chain.", nameof(segments));
                }
            }

            if (!copiedSegments[^1].End.Equals(resolvedGoal))
            {
                throw new ArgumentException("The final route segment must end at ResolvedGoal.", nameof(segments));
            }

            return new NavigationRoute(start, goalRegion, resolvedGoal, copiedSegments, searchComplete);
        }

    }

}
