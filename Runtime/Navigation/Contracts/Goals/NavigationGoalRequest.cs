using System;
using UnityEngine;
using static Aethiumian.AI.Navigation.NavigationArithmetic;
using static Aethiumian.AI.Navigation.Validate;

namespace Aethiumian.AI.Navigation
{

    /// <summary>
    /// Immutable, Unity-scene-independent data captured at a movement caller boundary.
    /// This is the single goal definition: it owns target geometry, arrival/retreat parameters,
    /// the distance metric, the line-of-sight requirement and every pure-geometry completion
    /// calculation. Environment participation (line of sight, obstacles) belongs to
    /// <see cref="NavigationGoalWorldExtensions"/>.
    /// </summary>
    public readonly struct NavigationGoalRequest : IEquatable<NavigationGoalRequest>
    {
        /// <summary>
        /// Fixed world-space foot-height difference still accepted by a Ground Range goal.
        /// This is goal geometry, not a world parameter: it is independent of the navigation cell
        /// size and of route-reuse thresholds, which answer a different question.
        /// </summary>
        internal const float GroundFootHeightTolerance = 1f;

        public AABB TargetBounds { get; }
        public NavigationGoalGeometry Geometry { get; }
        public DistanceMetric DistanceMetric { get; }
        public bool RequiresLineOfSight { get; }
        public float ArrivalTolerance { get; }
        public float RetreatDistance { get; }



        /// <summary>
        /// Gets the geometry-specific threshold applied after constructing the acceptance region.
        /// Ground Range already folds <see cref="ArrivalTolerance"/> into its acceptance extent, so
        /// applying it again here would complete the target twice as early; only the geometry epsilon
        /// remains. Proximity and Retreat measure a real distance and use the authored tolerance.
        /// </summary>
        public float CompletionTolerance => IsGroundWalk ? NavigationWorldQueries.GeometryEpsilon : ArrivalTolerance;

        /// <summary>Gets whether this goal uses Ground Walk lower-center geometry.</summary>
        public bool IsGroundWalk => Geometry == NavigationGoalGeometry.GroundRange;

        /// <summary>Gets whether this goal completes by increasing distance from its target.</summary>
        public bool IsRetreat => Geometry == NavigationGoalGeometry.Retreat;

        /// <summary>
        /// Gets the center used as a deterministic planning heuristic. Ground Range reasons about
        /// the continuous lower edge of its target, without integer rounding.
        /// </summary>
        public Vector2 Anchor => IsGroundWalk ? TargetBounds.LowerCenter : TargetBounds.Center;

        public NavigationGoalRequest(AABB targetBounds, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance, float retreatDistance)
        {
            ValidateMetric(distanceMetric);
            Aabb(targetBounds, nameof(targetBounds));
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
        /// Returns the lower-center acceptance box for the supplied body width. The horizontal extent
        /// folds this goal's <see cref="ArrivalTolerance"/> into the gap between body and target, and
        /// the vertical extent is the fixed <see cref="GroundFootHeightTolerance"/> foot-height
        /// difference. Neither depends on the navigation cell size.
        /// </summary>
        public AABB GetLowerCenterAcceptanceBounds(float bodyWidth)
        {
            NonNegativeFinite(bodyWidth, nameof(bodyWidth));
            float centerX = TargetBounds.Center.x;
            float feetY = TargetBounds.MinY;
            float halfWidth = (TargetBounds.Size.x + bodyWidth) * 0.5f + ArrivalTolerance;
            return new AABB(centerX - halfWidth, feetY - GroundFootHeightTolerance,
                centerX + halfWidth, feetY + GroundFootHeightTolerance);
        }




        /// <summary>Returns the pure-geometry completion distance without evaluating the optional LOS constraint.</summary>
        public float GeometryCompletionDistance(Vector2 center, Vector2 bodySize)
        {
            switch (Geometry)
            {
                case NavigationGoalGeometry.Retreat:
                    return Mathf.Max(0f, RetreatDistance - DistanceToCenteredBody(center, bodySize));
                case NavigationGoalGeometry.GroundRange:
                    return DistanceToLowerCenterGoal(center - Vector2.up * (bodySize.y * 0.5f), bodySize.x);
                default:
                    return DistanceToCenteredBody(center, bodySize);
            }
        }

        /// <summary>
        /// Returns the pure-geometry completion distance over one swept center segment. Ground Range
        /// and centered geometries measure the swept lower-center body box; Retreat reports the best
        /// of its two endpoints, matching its increase-only completion rule.
        /// </summary>
        public float GeometrySweptCompletionDistance(Vector2 startCenter, Vector2 endCenter, Vector2 bodySize)
        {
            NonNegativeVector(bodySize, nameof(bodySize));
            if (IsRetreat)
                return Mathf.Min(GeometryCompletionDistance(startCenter, bodySize), GeometryCompletionDistance(endCenter, bodySize));

            Vector2 offset = Vector2.up * (bodySize.y * 0.5f);
            return DistanceToLowerCenterBodySegment(startCenter - offset, endCenter - offset, bodySize);
        }

        /// <summary>Clips a finite center sweep to the convex interval that satisfies goal geometry.</summary>
        public bool TryGetGeometryCompletionInterval(Vector2 startCenter, Vector2 endCenter, Vector2 bodySize, out float entry, out float exit)
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
                bool complete = GeometryCompletionDistance(Vector2.Lerp(startCenter, endCenter, middle), bodySize) <= CompletionTolerance;
                if (complete == entering) upper = middle;
                else lower = middle;
            }
            return entering ? upper : lower;
        }




        /// <summary>
        /// Returns the distance from a lower-center anchor to this Ground Walk goal.
        /// </summary>
        public float DistanceToLowerCenterGoal(Vector2 lowerCenter, float bodyWidth)
        {
            AABB acceptanceBounds = GetLowerCenterAcceptanceBounds(bodyWidth);
            Finite(lowerCenter, nameof(lowerCenter));
            return DistanceToPoint(lowerCenter, acceptanceBounds);
        }

        /// <summary>
        /// Returns the minimum distance from a lower-center segment to this Ground Walk goal.
        /// </summary>
        public float DistanceToLowerCenterGoalSegment(Vector2 start, Vector2 end, float bodyWidth)
        {
            AABB acceptanceBounds = GetLowerCenterAcceptanceBounds(bodyWidth);
            Finite(start, nameof(start));
            Finite(end, nameof(end));
            return DistanceToSegmentBounds(start, end, acceptanceBounds);
        }

        /// <summary>Returns the distance from a lower-center body AABB to this target.</summary>
        public float DistanceToLowerCenterBody(Vector2 lowerCenter, Vector2 bodySize)
        {
            Finite(lowerCenter, nameof(lowerCenter));
            NonNegativeVector(bodySize, nameof(bodySize));
            return IsGroundWalk
                ? DistanceToLowerCenterGoal(lowerCenter, bodySize.x)
                : DistanceMetric.DistanceToBody(AABB.FromCenterAndSize(lowerCenter + Vector2.up * (bodySize.y * 0.5f), bodySize), TargetBounds);
        }

        /// <summary>Returns the minimum distance from a lower-center body swept along a segment to this target.</summary>
        public float DistanceToLowerCenterBodySegment(Vector2 start, Vector2 end, Vector2 bodySize)
        {
            Finite(start, nameof(start));
            Finite(end, nameof(end));
            NonNegativeVector(bodySize, nameof(bodySize));
            if (IsGroundWalk)
                return DistanceToLowerCenterGoalSegment(start, end, bodySize.x);

            float minX = TargetBounds.MinX - bodySize.x * 0.5f;
            float maxX = TargetBounds.MaxX + bodySize.x * 0.5f;
            float minY = TargetBounds.MinY - bodySize.y;
            float maxY = TargetBounds.MaxY;
            return DistanceMetric.DistanceToSegmentBoundsMetric(start, end, minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Returns the distance from a center-anchored body AABB to this target.
        /// </summary>
        public float DistanceToCenteredBody(Vector2 center, Vector2 bodySize)
        {
            Finite(center, nameof(center));
            NonNegativeVector(bodySize, nameof(bodySize));
            return DistanceMetric.DistanceToBody(AABB.FromCenterAndSize(center, bodySize), TargetBounds);
        }

        /// <summary>
        /// Gets the best-effort distance from a mover center to the raw target center.
        /// </summary>
        public float GuidanceDistance(Vector2 center, Vector2 bodySize)
        {
            Finite(center, nameof(center));
            NonNegativeVector(bodySize, nameof(bodySize));
            if (IsRetreat) return -DistanceToCenteredBody(center, bodySize);
            Vector2 delta = center - TargetBounds.Center;
            return IsGroundWalk ? delta.magnitude : DistanceMetric.MetricLength(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        }







        /// <summary>
        /// Returns whether this request is compatible with the previous one for route reuse. 
        /// The caller's position thresholds are applied to the target center, and the geometry and distance metric must match exactly.
        /// 
        /// This is the route-reuse relation and is deliberately looser than <see cref="Equals(NavigationGoalRequest)"/>, which stays exact for cache identity.
        /// </summary>
        /// <param name="latest"></param>
        /// <returns></returns>
        public bool IsSamePlanningTarget(NavigationGoalRequest latest)
        {
            if (!HasCompatibleSemantics(latest)) return false;
            return (Anchor - latest.Anchor).sqrMagnitude <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon;
        }

        /// <summary>
        /// Returns whether two requests describe the same goal semantics. Target positions may
        /// differ; only sampling noise in the target extent is tolerated. This is the route-reuse
        /// relation and is deliberately looser than <see cref="Equals(NavigationGoalRequest)"/>,
        /// which stays exact for cache identity.
        /// </summary>
        public bool HasCompatibleSemantics(NavigationGoalRequest other)
        {
            return Geometry == other.Geometry
                && DistanceMetric == other.DistanceMetric
                && RequiresLineOfSight == other.RequiresLineOfSight
                && ArrivalTolerance.Equals(other.ArrivalTolerance)
                && RetreatDistance.Equals(other.RetreatDistance)
                && Approximately(TargetBounds.Size, other.TargetBounds.Size, NavigationWorldQueries.GeometryEpsilon);
        }



        /// <summary>
        /// Route-reuse comparison. The goal semantics must match, and the target may only have drifted
        /// within the re-planning tolerance: horizontally up to the larger of
        /// <see cref="NavigationConstant.TargetMotionTolerance"/> and this goal's arrival tolerance,
        /// vertically up to the target-motion tolerance alone. The vertical bound answers "how far may
        /// the target level move before this route stops being worth reusing"; it is deliberately
        /// unrelated to Ground Range's fixed foot-height acceptance rule.
        /// </summary>
        public bool IsReusableFor(NavigationGoalRequest latest)
        {
            if (!HasCompatibleSemantics(latest)) return false;

            float horizontalThreshold = Mathf.Max(NavigationConstant.TargetMotionTolerance, latest.ArrivalTolerance);
            float verticalThreshold = NavigationConstant.TargetMotionTolerance;

            // Target extents have already passed the sampling-noise check above; only the
            // target position is compared against the re-planning tolerance here.
            if (!IsGroundWalk)
                return Vector2.Distance(Anchor, latest.Anchor) <= horizontalThreshold;

            bool horizontalChanged = Mathf.Max(Mathf.Abs(TargetBounds.MinX - latest.TargetBounds.MinX), Mathf.Abs(TargetBounds.MaxX - latest.TargetBounds.MaxX)) > horizontalThreshold;
            bool levelChanged = Mathf.Abs(TargetBounds.MinY - latest.TargetBounds.MinY) > verticalThreshold;
            return !horizontalChanged && !levelChanged;
        }




        public static NavigationGoalRequest GroundRange(AABB targetBounds, float arrivalTolerance, bool requiresLineOfSight = false)
            => new(targetBounds, NavigationGoalGeometry.GroundRange, DistanceMetric.Euclidean, requiresLineOfSight, arrivalTolerance, 0f);

        public static NavigationGoalRequest Proximity(AABB targetBounds, DistanceMetric distanceMetric, float arrivalTolerance, bool requiresLineOfSight = false)
            => new(targetBounds, NavigationGoalGeometry.Proximity, distanceMetric, requiresLineOfSight, arrivalTolerance, 0f);

        /// <summary>Creates an open-ended goal that completes after the mover is far enough from the target.</summary>
        public static NavigationGoalRequest Retreat(AABB targetBounds, DistanceMetric distanceMetric, float retreatDistance)
            => new(targetBounds, NavigationGoalGeometry.Retreat, distanceMetric, false, 0f, retreatDistance);

        public static NavigationGoalRequest Point(Vector2 destination, NavigationGoalGeometry geometry, DistanceMetric distanceMetric, bool requiresLineOfSight, float arrivalTolerance)
            => new(AABB.Point(destination), geometry, distanceMetric, requiresLineOfSight, arrivalTolerance, 0f);

        /// <summary>Creates an equivalent request with only its captured target bounds replaced.</summary>
        public NavigationGoalRequest WithTargetBounds(AABB targetBounds)
            => new(targetBounds, Geometry, DistanceMetric, RequiresLineOfSight, ArrivalTolerance, RetreatDistance);


        private static void ValidateMetric(DistanceMetric value)
        {
            if (!Enum.IsDefined(typeof(DistanceMetric), value))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown distance metric.");
        }




        /// <summary>
        /// Compares every immutable goal field exactly. This is the request identity used by
        /// dictionary and failure-cache keys; route reuse uses <see cref="HasCompatibleSemantics"/>.
        /// </summary>
        public bool Equals(NavigationGoalRequest other)
        {
            return TargetBounds.Equals(other.TargetBounds)
                && Geometry == other.Geometry
                && DistanceMetric == other.DistanceMetric
                && RequiresLineOfSight == other.RequiresLineOfSight
                && ArrivalTolerance.Equals(other.ArrivalTolerance)
                && RetreatDistance.Equals(other.RetreatDistance);
        }

        public override bool Equals(object obj) => obj is NavigationGoalRequest other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TargetBounds, Geometry, DistanceMetric, RequiresLineOfSight, ArrivalTolerance, RetreatDistance);

        public static bool operator ==(NavigationGoalRequest left, NavigationGoalRequest right) => left.Equals(right);
        public static bool operator !=(NavigationGoalRequest left, NavigationGoalRequest right) => !left.Equals(right);
    }
}
