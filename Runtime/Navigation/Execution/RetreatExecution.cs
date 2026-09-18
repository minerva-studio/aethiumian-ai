using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns one Movement execution's Retreat identity and approach budget.</summary>
    public sealed class RetreatExecution
    {
        private readonly GameObject target;
        private readonly float? maximumApproachDistance;
        private float approachDistanceUsed;

        /// <summary>A null target opts out of identity tracking; a destroyed target is invalid.</summary>
        public RetreatExecution(GameObject target, float maximumApproachDistance)
        {
            if (!ReferenceEquals(target, null) && !target)
                throw new ArgumentException("Retreat target has been destroyed.", nameof(target));
            Validate.NonNegativeFinite(maximumApproachDistance, nameof(maximumApproachDistance));

            this.target = target;
            this.maximumApproachDistance = maximumApproachDistance > 0f ? maximumApproachDistance : null;
        }

        /// <summary>Gets the unused approach budget, or zero when the execution is unlimited.</summary>
        public float RemainingApproachDistance => maximumApproachDistance is float limit ? Mathf.Max(0f, limit - approachDistanceUsed) : 0f;

        /// <summary>Gets whether this execution has a finite cumulative approach budget.</summary>
        public bool HasApproachLimit => maximumApproachDistance.HasValue;

        /// <summary>Checks whether the sampled target is still the target captured for this execution.</summary>
        public bool IsCurrentTarget(GameObject currentTarget) => ReferenceEquals(target, null) || target && currentTarget && target == currentTarget;

        /// <summary>Records physical approach distance and reports whether the cumulative budget remains available.</summary>
        public bool RecordApproachDistance(float additionalApproachDistance)
        {
            Validate.NonNegativeFinite(additionalApproachDistance, nameof(additionalApproachDistance));

            approachDistanceUsed += additionalApproachDistance;
            return !maximumApproachDistance.HasValue || approachDistanceUsed <= maximumApproachDistance.Value + NavigationWorldQueries.GeometryEpsilon;
        }

        /// <summary>Checks an explicit route suffix against the remaining approach budget.</summary>
        public bool IsWithinRemainingApproachDistance(float additionalApproachDistance)
        {
            Validate.NonNegativeFinite(additionalApproachDistance, nameof(additionalApproachDistance));
            return !maximumApproachDistance.HasValue || additionalApproachDistance <= RemainingApproachDistance + NavigationWorldQueries.GeometryEpsilon;
        }

        /// <summary>Checks a caller-selected route suffix against the remaining approach budget.</summary>
        internal bool AllowsRoute(NavigationGoalRequest goal, Vector2 routeStart, IReadOnlyList<NavigationRouteSegment> suffix)
        {
            if (!goal.IsRetreat || !HasApproachLimit) return true;

            return IsWithinRemainingApproachDistance(RetreatNavigationGeometry.RouteApproachDistance(routeStart, goal.TargetBounds.Center, suffix));
        }

    }
}
