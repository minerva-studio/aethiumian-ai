using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Owns one Movement execution's Retreat identity and approach budget.
    /// </summary>
    public sealed class RetreatExecution
    {
        private readonly GameObject target;
        private readonly float? maximumApproachDistance;
        private float approachDistanceUsed;

        /// <summary>
        /// Captures the target identity and cumulative approach limit. A zero maximum means unlimited;
        /// a destroyed target is invalid.
        /// </summary>
        public RetreatExecution(GameObject target, float maximumApproachDistance)
        {
            if (!ReferenceEquals(target, null) && !target)
                throw new ArgumentException("Retreat target has been destroyed.", nameof(target));
            Validate.NonNegativeFinite(maximumApproachDistance, nameof(maximumApproachDistance));

            this.target = target;
            this.maximumApproachDistance = maximumApproachDistance > 0f ? maximumApproachDistance : null;
        }

        /// <summary>
        /// Gets the unused approach budget, or null when the execution is unlimited.
        /// </summary>
        public float? RemainingApproachDistance => maximumApproachDistance is float limit ? Mathf.Max(0f, limit - approachDistanceUsed) : null;

        /// <summary>
        /// Checks whether the sampled target is still the target captured for this execution.
        /// </summary>
        public bool IsCurrentTarget(GameObject currentTarget)
        {
            return ReferenceEquals(target, null) || target && currentTarget && target == currentTarget;
        }

        /// <summary>
        /// Records physical approach distance and reports whether the cumulative budget remains available.
        /// </summary>
        public bool RecordApproachDistance(float additionalApproachDistance)
        {
            Validate.NonNegativeFinite(additionalApproachDistance, nameof(additionalApproachDistance));

            approachDistanceUsed += additionalApproachDistance;
            return !maximumApproachDistance.HasValue || approachDistanceUsed <= maximumApproachDistance.Value + NavigationWorldQueries.GeometryEpsilon;
        }

        /// <summary>
        /// Checks an explicit route suffix against the remaining approach budget.
        /// </summary>
        public bool IsWithinRemainingApproachDistance(float additionalApproachDistance)
        {
            Validate.NonNegativeFinite(additionalApproachDistance, nameof(additionalApproachDistance));
            return RemainingApproachDistance is not float remainingApproachDistance
                || additionalApproachDistance <= remainingApproachDistance + NavigationWorldQueries.GeometryEpsilon;
        }

        /// <summary>
        /// Checks a caller-selected route suffix against the remaining approach budget.
        /// </summary>
        internal bool AllowsRoute(NavigationGoalRequest goal, Vector2 routeStart, IReadOnlyList<NavigationRouteSegment> suffix)
        {
            if (!goal.IsRetreat || !maximumApproachDistance.HasValue) return true;

            return IsWithinRemainingApproachDistance(RetreatNavigationGeometry.RouteApproachDistance(routeStart, goal.TargetBounds.Center, suffix));
        }

    }
}
