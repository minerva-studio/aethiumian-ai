using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Immutable planner snapshot for one world, start anchor, and goal.
    /// Coordinators may replace uncommitted suffixes; this object never owns execution.
    /// The captured <see cref="World"/> is the world the goal was planned against, so route
    /// consumers can still prove that a route belongs to the world they are executing in.
    /// </summary>
    public sealed class NavigationRoute
    {
        /// <summary>
        /// Gets the immutable world this route was planned against.
        /// </summary>
        public INavigationWorld World { get; }

        /// <summary>
        /// Gets the world-space origin used by the planner.
        /// </summary>
        public Vector2 Start { get; }

        /// <summary>
        /// Gets the immutable goal this route was planned against.
        /// </summary>
        public NavigationGoalRequest Goal { get; }

        /// <summary>
        /// Gets the world-space endpoint selected by the planner.
        /// </summary>
        public Vector2 Endpoint { get; }

        /// <summary>
        /// Gets the read-only route segments in execution order.
        /// </summary>
        public IReadOnlyList<NavigationRouteSegment> Segments { get; }

        /// <summary>
        /// Gets the number of route segments.
        /// </summary>
        public int Count => Segments.Count;

        /// <summary>
        /// Gets whether this route reaches the goal captured when the route was created.
        /// It does not mean the movement's current goal is already satisfied.
        /// </summary>
        public bool ReachesGoal { get; }

        private NavigationRoute(Vector2 start, NavigationGoalRequest goal, INavigationWorld world, Vector2 resolvedGoal, NavigationRouteSegment[] segments, bool reachesGoal)
        {
            Start = start;
            Goal = goal;
            World = world;
            Endpoint = resolvedGoal;
            ReachesGoal = reachesGoal;
            Segments = segments;
        }


        /// <summary>
        /// Gets the route segments starting at the specified index, in execution order.
        /// </summary>
        /// <param name="first"></param>
        /// <returns></returns>
        public RouteSegmentEnumerator GetRouteSegments(int first)
        {
            if (first < 0 || first > Count)
                throw new ArgumentOutOfRangeException(nameof(first), first,
                    "A route segment suffix must start inside the route, or at its end for an empty suffix.");
            return new(this, first);
        }

        /// <summary>
        /// Replaces this route's segments while preserving its origin, goal, world, endpoint, and completeness.
        /// </summary>
        public NavigationRoute WithSegments(IEnumerable<NavigationRouteSegment> replacementSegments) => CreateInternal(Start, Goal, World, Endpoint, replacementSegments, ReachesGoal);

        /// <summary>
        /// Creates a route against an immutable goal with an explicit goal-arrival fact.
        /// </summary>
        public static NavigationRoute Create(Vector2 start, NavigationGoalRequest goal, INavigationWorld world, Vector2 resolvedGoal, IEnumerable<NavigationRouteSegment> segments, bool reachesGoal)
            => CreateInternal(start, goal, world, resolvedGoal, segments, reachesGoal);

        /// <summary>
        /// Creates a route that represents a complete search or direct result.
        /// </summary>
        public static NavigationRoute Complete(Vector2 start, NavigationGoalRequest goal, INavigationWorld world, Vector2 resolvedGoal, IEnumerable<NavigationRouteSegment> segments)
            => CreateInternal(start, goal, world, resolvedGoal, segments, true);

        /// <summary>
        /// Creates a produced route whose next action does not yet reach the planning goal.
        /// </summary>
        public static NavigationRoute Partial(Vector2 start, NavigationGoalRequest goal, INavigationWorld world, Vector2 resolvedGoal, IEnumerable<NavigationRouteSegment> segments)
            => CreateInternal(start, goal, world, resolvedGoal, segments, false);

        private static NavigationRoute CreateInternal(Vector2 start, NavigationGoalRequest goal, INavigationWorld world, Vector2 resolvedGoal, IEnumerable<NavigationRouteSegment> segments, bool reachesGoal)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

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

                return new NavigationRoute(start, goal, world, resolvedGoal, copiedSegments, reachesGoal);
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

            return new NavigationRoute(start, goal, world, resolvedGoal, copiedSegments, reachesGoal);
        }


        public RouteSegmentEnumerator GetEnumerator() => new(this);


        public struct RouteSegmentEnumerator : IEnumerator<NavigationRouteSegment>, IEnumerator, IEnumerable<NavigationRouteSegment>, IEnumerable
        {
            private readonly NavigationRoute route;
            private int beginningIndex;
            private int index;

            internal RouteSegmentEnumerator(NavigationRoute route) : this(route, 0) { }
            internal RouteSegmentEnumerator(NavigationRoute route, int beginningIndex)
            {
                this.route = route;
                this.beginningIndex = beginningIndex;
                this.index = beginningIndex - 1;
            }

            public NavigationRouteSegment Current => route.Segments[index];

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (index < route.Count) index++;
                return index < route.Count;
            }

            public void Reset()
            {
                index = beginningIndex - 1;
            }

            public readonly void Dispose() { }

            public readonly RouteSegmentEnumerator GetEnumerator() => this;

            readonly IEnumerator<NavigationRouteSegment> IEnumerable<NavigationRouteSegment>.GetEnumerator() => this;

            readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
