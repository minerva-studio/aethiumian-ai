using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Immutable route value over stable segment storage. Default means no route; a valid empty
    /// route records its position and coordinate frame. Nonempty slices retain the entire backing
    /// array until all copies release it. Execution state belongs to the movement owner.
    /// </summary>
    public readonly struct NavigationRoute : IReadOnlyList<NavigationRouteSegment>
    {
        private readonly NavigationRouteSegment[] segments;
        private readonly int offset;
        private readonly Vector2 emptyPosition;
        private readonly NavigationRouteCoordinateFrame emptyFrame;

        /// <summary>Distinguishes a produced route, including an empty route, from default.</summary>
        public bool HasValue => segments != null;
        /// <summary>Gets the stable goal captured by planning.</summary>
        public NavigationGoalRequest Goal { get; }
        /// <summary>Gets the number of segments in this range; default has zero segments.</summary>
        public int Count { get; }
        /// <summary>Gets whether planning reached its captured goal.</summary>
        public bool ReachesGoal { get; }
        /// <summary>Gets a segment relative to this route's range.</summary>
        public NavigationRouteSegment this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                return segments[offset + index];
            }
        }
        /// <summary>Gets the first segment origin or the valid empty route position.</summary>
        public Vector2 Start
        {
            get { RequireValue(); return Count == 0 ? emptyPosition : segments[offset].Start; }
        }
        /// <summary>Gets the final endpoint or the valid empty route position.</summary>
        public Vector2 Endpoint
        {
            get { RequireValue(); return Count == 0 ? emptyPosition : segments[offset + Count - 1].End; }
        }
        /// <summary>Gets the coordinate frame; unavailable for default.</summary>
        public NavigationRouteCoordinateFrame CoordinateFrame
        {
            get { RequireValue(); return Count == 0 ? emptyFrame : segments[offset].CoordinateFrame; }
        }

        private NavigationRoute(NavigationGoalRequest goal, bool reachesGoal, NavigationRouteSegment[] segments, int offset, int count, Vector2 emptyPosition = default, NavigationRouteCoordinateFrame emptyFrame = default)
        {
            Goal = goal;
            ReachesGoal = reachesGoal;
            this.segments = segments;
            this.offset = offset;
            Count = count;
            this.emptyPosition = emptyPosition;
            this.emptyFrame = emptyFrame;
        }

        private void RequireValue()
        {
            if (!HasValue) throw new InvalidOperationException("No navigation route is present.");
        }

        /// <summary>
        /// Shares a suffix without copying segments. The end index produces a valid empty route
        /// at this route's endpoint; default cannot be sliced.
        /// </summary>
        public NavigationRoute Slice(int first)
        {
            RequireValue();
            if ((uint)first > (uint)Count) throw new ArgumentOutOfRangeException(nameof(first));
            if (first == Count)
                return new NavigationRoute(Goal, ReachesGoal, Array.Empty<NavigationRouteSegment>(),
                    0, 0, Endpoint, CoordinateFrame);
            return new NavigationRoute(Goal, ReachesGoal, segments, offset + first, Count - first);
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
            return new(segments, offset + first, Count - first);
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
        /// Replaces this route's segments while preserving its goal and completeness, and the
        /// positions its replacement spans. An empty replacement is valid only for a zero-length
        /// route, and keeps that route's declared coordinate frame.
        /// </summary>
        public NavigationRoute WithSegments(IEnumerable<NavigationRouteSegment> replacementSegments)
        {
            RequireValue();
            NavigationRouteSegment[] copiedSegments = CopySegments(replacementSegments);
            if (copiedSegments.Length == 0)
            {
                if (!Start.Equals(Endpoint))
                    throw new ArgumentException("An empty route is valid only when Start equals Endpoint.", nameof(replacementSegments));

                return new NavigationRoute(Goal, ReachesGoal, Array.Empty<NavigationRouteSegment>(), 0, 0, Start, CoordinateFrame);
            }

            // A replacement is not a new route: it must still span the positions it replaces.
            if (!copiedSegments[0].Start.Equals(Start) || !copiedSegments[^1].End.Equals(Endpoint))
                throw new ArgumentException("A route replacement must span the positions it replaces.", nameof(replacementSegments));

            return CreateInternal(Goal, copiedSegments, ReachesGoal);
        }

        /// <summary>
        /// Creates a route against an immutable goal with an explicit goal-arrival fact. The route
        /// derives <see cref="Start"/> and <see cref="Endpoint"/> from its own segment chain, which must
        /// be continuous and declare one coordinate frame; a zero-length route uses <see cref="Empty"/>
        /// instead, because an empty segment collection cannot declare a frame.
        /// </summary>
        public static NavigationRoute Create(NavigationGoalRequest goal, IEnumerable<NavigationRouteSegment> segments, bool reachesGoal)
            => CreateInternal(goal, CopySegments(segments), reachesGoal);

        /// <summary>
        /// Creates a route that represents a complete search or direct result.
        /// </summary>
        public static NavigationRoute Complete(NavigationGoalRequest goal, IEnumerable<NavigationRouteSegment> segments)
            => CreateInternal(goal, CopySegments(segments), true);

        /// <summary>
        /// Creates a produced route whose next action does not yet reach the planning goal.
        /// </summary>
        public static NavigationRoute Partial(NavigationGoalRequest goal, IEnumerable<NavigationRouteSegment> segments)
            => CreateInternal(goal, CopySegments(segments), false);

        /// <summary>
        /// Creates the zero-length route of a plan that is already at its position. An empty route has
        /// no segment to declare its coordinate frame, so the caller states it explicitly.
        /// </summary>
        public static NavigationRoute Empty(Vector2 position, NavigationGoalRequest goal, NavigationRouteCoordinateFrame frame, bool reachesGoal)
        {
            if (!NavigationNumeric.IsFinite(position))
                throw new ArgumentException("Navigation plan coordinates must be finite world coordinates.", nameof(position));

            if (!Enum.IsDefined(typeof(NavigationRouteCoordinateFrame), frame))
                throw new ArgumentOutOfRangeException(nameof(frame), frame, "Unknown route coordinate frame.");

            return new NavigationRoute(goal, reachesGoal, Array.Empty<NavigationRouteSegment>(), 0, 0, position, frame);
        }

        private static NavigationRoute CreateInternal(NavigationGoalRequest goal, NavigationRouteSegment[] segments, bool reachesGoal)
        {
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

            return new NavigationRoute(goal, reachesGoal, segments, 0, segments.Length);
        }

        /// <summary>Creates a single-segment route without an intermediate caller-owned array.</summary>
        internal static NavigationRoute CreateSingleSegment(NavigationGoalRequest goal, NavigationRouteSegment segment, bool reachesGoal)
        {
            if (segment == null)
                throw new ArgumentException("Navigation routes cannot contain null segments.", nameof(segment));
            return CreateInternal(goal, new[] { segment }, reachesGoal);
        }

        private static NavigationRouteSegment[] CopySegments(IEnumerable<NavigationRouteSegment> segments)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));

            NavigationRouteSegment[] copiedSegments;
            if (segments is ICollection<NavigationRouteSegment> collection)
            {
                copiedSegments = new NavigationRouteSegment[collection.Count];
                collection.CopyTo(copiedSegments, 0);
            }
            else
            {
                copiedSegments = new List<NavigationRouteSegment>(segments).ToArray();
            }
            for (int i = 0; i < copiedSegments.Length; i++)
            {
                if (copiedSegments[i] == null)
                {
                    throw new ArgumentException("Navigation routes cannot contain null segments.", nameof(segments));
                }
            }

            return copiedSegments;
        }


        /// <summary>Enumerates this range without allocating when consumed by concrete type.</summary>
        public RouteSegmentEnumerator GetEnumerator() => new(segments, offset, Count);
        IEnumerator<NavigationRouteSegment> IEnumerable<NavigationRouteSegment>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>A segment-range cursor; it does not copy the route's goal data.</summary>
        public struct RouteSegmentEnumerator : IEnumerator<NavigationRouteSegment>, IEnumerable<NavigationRouteSegment>
        {
            private readonly NavigationRouteSegment[] segments;
            private readonly int beginningIndex;
            private readonly int endIndex;
            private int index;

            internal RouteSegmentEnumerator(NavigationRouteSegment[] segments, int offset, int count)
            {
                this.segments = segments;
                beginningIndex = offset;
                endIndex = offset + count;
                index = offset - 1;
            }

            public readonly NavigationRouteSegment Current => segments[index];
            readonly object IEnumerator.Current => Current;
            public bool MoveNext()
            {
                if (index < endIndex) index++;
                return index < endIndex;
            }
            public void Reset() => index = beginningIndex - 1;
            public readonly void Dispose() { }
            public readonly RouteSegmentEnumerator GetEnumerator() => this;
            readonly IEnumerator<NavigationRouteSegment> IEnumerable<NavigationRouteSegment>.GetEnumerator() => this;
            readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
