using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Immutable planner snapshot for one world, start position, and goal. Every position this route
    /// exposes - <see cref="Start"/>, <see cref="Endpoint"/>, and every segment position - is
    /// expressed in its single <see cref="CoordinateFrame"/>.
    /// Coordinators may replace uncommitted suffixes; this object never owns execution.
    /// The captured <see cref="World"/> is the world the goal was planned against, so route
    /// consumers can still prove that a route belongs to the world they are executing in.
    /// </summary>
    public sealed class NavigationRoute
    {
        // An empty route has no segment that could declare its frame or its positions, so it stores
        // one zero-length position together with the frame its caller declared. Both fields stay
        // unused for a non-empty route, whose positions and frame derive from its segments.
        private readonly Vector2 emptyPosition;
        private readonly NavigationRouteCoordinateFrame emptyFrame;

        /// <summary>
        /// Gets the immutable world this route was planned against.
        /// </summary>
        public INavigationWorld World { get; }

        /// <summary>
        /// Gets the immutable goal this route was planned against.
        /// </summary>
        public NavigationGoalRequest Goal { get; }

        /// <summary>
        /// Gets the read-only route segments in execution order.
        /// </summary>
        public IReadOnlyList<NavigationRouteSegment> Segments { get; }

        /// <summary>
        /// Gets the number of route segments.
        /// </summary>
        public int Count => Segments.Count;

        /// <summary>
        /// Gets the world-space origin used by the planner, in <see cref="CoordinateFrame"/>: the
        /// first segment's start, or the stored zero-length position of an empty route.
        /// </summary>
        public Vector2 Start => Count == 0 ? emptyPosition : Segments[0].Start;

        /// <summary>
        /// Gets the world-space endpoint selected by the planner, in <see cref="CoordinateFrame"/>:
        /// the final segment's end, or the stored zero-length position of an empty route.
        /// </summary>
        public Vector2 Endpoint => Count == 0 ? emptyPosition : Segments[Count - 1].End;

        /// <summary>
        /// Gets the coordinate frame shared by <see cref="Start"/>, <see cref="Endpoint"/>, and every
        /// segment position. It is derived from the segments; an empty route keeps the frame its
        /// caller declared.
        /// </summary>
        public NavigationRouteCoordinateFrame CoordinateFrame => Count == 0 ? emptyFrame : Segments[0].CoordinateFrame;

        /// <summary>
        /// Gets whether this route reaches the goal captured when the route was created.
        /// It does not mean the movement's current goal is already satisfied.
        /// </summary>
        public bool ReachesGoal { get; }

        /// <summary>Creates a non-empty route whose coordinate frame is declared by its segments.</summary>
        private NavigationRoute(NavigationGoalRequest goal, INavigationWorld world, NavigationRouteSegment[] segments, bool reachesGoal)
        {
            Goal = goal;
            World = world;
            Segments = segments;
            ReachesGoal = reachesGoal;
        }

        /// <summary>Creates the zero-length route of one declared position.</summary>
        private NavigationRoute(Vector2 position, NavigationRouteCoordinateFrame frame, NavigationGoalRequest goal, INavigationWorld world, bool reachesGoal)
            : this(goal, world, Array.Empty<NavigationRouteSegment>(), reachesGoal)
        {
            emptyPosition = position;
            emptyFrame = frame;
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
        /// Resolves <see cref="Endpoint"/> into the body AABB it represents, using the supplied body as
        /// the size template. A <see cref="NavigationRouteCoordinateFrame.GroundAnchor"/> endpoint
        /// becomes the lower-center anchor of a body of that size; a
        /// <see cref="NavigationRouteCoordinateFrame.BodyCenter"/> endpoint is already a body center.
        /// This is the route's body conversion, so consumers never re-derive an anchor themselves.
        /// </summary>
        public AABB ResolveEndpointBody(AABB bodyTemplate) => ResolveBodyAt(Endpoint, bodyTemplate);

        /// <summary>
        /// Resolves one route position expressed in <see cref="CoordinateFrame"/> into the body AABB it
        /// represents, using the supplied body as the size template. Every route position - an endpoint,
        /// a predecessor segment end, or a continuation origin - goes through this single conversion.
        /// </summary>
        public AABB ResolveBodyAt(Vector2 routePosition, AABB bodyTemplate)
        {
            Validate.Aabb(bodyTemplate, nameof(bodyTemplate));
            Validate.Finite(routePosition, nameof(routePosition));
            return CoordinateFrame == NavigationRouteCoordinateFrame.GroundAnchor
                ? AABB.FromLowerCenter(routePosition, bodyTemplate.Size)
                : AABB.FromCenterAndSize(routePosition, bodyTemplate.Size);
        }

        /// <summary>
        /// Replaces this route's segments while preserving its goal, world, completeness, and the
        /// positions its replacement spans. An empty replacement is valid only for a zero-length
        /// route, and keeps that route's declared coordinate frame.
        /// </summary>
        public NavigationRoute WithSegments(IEnumerable<NavigationRouteSegment> replacementSegments)
        {
            NavigationRouteSegment[] copiedSegments = CopySegments(replacementSegments);
            if (copiedSegments.Length == 0)
            {
                if (!Start.Equals(Endpoint))
                    throw new ArgumentException("An empty route is valid only when Start equals Endpoint.", nameof(replacementSegments));

                return new NavigationRoute(Start, CoordinateFrame, Goal, World, ReachesGoal);
            }

            // A replacement is not a new route: it must still span the positions it replaces.
            if (!copiedSegments[0].Start.Equals(Start) || !copiedSegments[^1].End.Equals(Endpoint))
                throw new ArgumentException("A route replacement must span the positions it replaces.", nameof(replacementSegments));

            return CreateInternal(Goal, World, copiedSegments, ReachesGoal);
        }

        /// <summary>
        /// Creates a route against an immutable goal with an explicit goal-arrival fact. The route
        /// derives <see cref="Start"/> and <see cref="Endpoint"/> from its own segment chain, which must
        /// be continuous and declare one coordinate frame; a zero-length route uses <see cref="Empty"/>
        /// instead, because an empty segment collection cannot declare a frame.
        /// </summary>
        public static NavigationRoute Create(NavigationGoalRequest goal, INavigationWorld world, IEnumerable<NavigationRouteSegment> segments, bool reachesGoal)
            => CreateInternal(goal, world, CopySegments(segments), reachesGoal);

        /// <summary>
        /// Creates a route that represents a complete search or direct result.
        /// </summary>
        public static NavigationRoute Complete(NavigationGoalRequest goal, INavigationWorld world, IEnumerable<NavigationRouteSegment> segments)
            => CreateInternal(goal, world, CopySegments(segments), true);

        /// <summary>
        /// Creates a produced route whose next action does not yet reach the planning goal.
        /// </summary>
        public static NavigationRoute Partial(NavigationGoalRequest goal, INavigationWorld world, IEnumerable<NavigationRouteSegment> segments)
            => CreateInternal(goal, world, CopySegments(segments), false);

        /// <summary>
        /// Creates the zero-length route of a plan that is already at its position. An empty route has
        /// no segment to declare its coordinate frame, so the caller states it explicitly.
        /// </summary>
        public static NavigationRoute Empty(Vector2 position, NavigationGoalRequest goal, INavigationWorld world, NavigationRouteCoordinateFrame frame, bool reachesGoal)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            if (!NavigationNumeric.IsFinite(position))
                throw new ArgumentException("Navigation plan coordinates must be finite world coordinates.", nameof(position));

            if (!Enum.IsDefined(typeof(NavigationRouteCoordinateFrame), frame))
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "Unknown route coordinate frame.");

            return new NavigationRoute(position, frame, goal, world, reachesGoal);
        }

        private static NavigationRoute CreateInternal(NavigationGoalRequest goal, INavigationWorld world, NavigationRouteSegment[] segments, bool reachesGoal)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            if (segments.Length == 0)
                throw new ArgumentException("An empty route must declare its coordinate frame explicitly; use NavigationRoute.Empty.", nameof(segments));

            for (int i = 1; i < segments.Length; i++)
            {
                if (!segments[i - 1].End.Equals(segments[i].Start))
                {
                    throw new ArgumentException("Route segments must form a continuous world-space chain.", nameof(segments));
                }
            }

            NavigationRouteCoordinateFrame frame = segments[0].CoordinateFrame;
            for (int i = 1; i < segments.Length; i++)
            {
                if (segments[i].CoordinateFrame != frame)
                {
                    throw new ArgumentException("A navigation route cannot mix coordinate frames.", nameof(segments));
                }
            }

            return new NavigationRoute(goal, world, segments, reachesGoal);
        }

        private static NavigationRouteSegment[] CopySegments(IEnumerable<NavigationRouteSegment> segments)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));

            NavigationRouteSegment[] copiedSegments = new List<NavigationRouteSegment>(segments).ToArray();
            for (int i = 0; i < copiedSegments.Length; i++)
            {
                if (copiedSegments[i] == null)
                {
                    throw new ArgumentException("Navigation routes cannot contain null segments.", nameof(segments));
                }
            }

            return copiedSegments;
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
