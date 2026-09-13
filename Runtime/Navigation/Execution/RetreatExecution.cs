using System;
using UnityEngine;
namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns one Movement execution's Retreat identity, progress sample and approach budget.</summary>
    public sealed class RetreatExecution
    {
        private readonly GameObject target;
        private readonly float maximumApproachDistance;
        private readonly bool hasApproachLimit;
        private float approachDistanceUsed;
        private Vector2 previousBodyCenter;
        private float bestCompletionDistance = float.PositiveInfinity;
        private bool hasPreviousSample;

        /// <summary>A null target opts out of identity tracking; a destroyed target is invalid.</summary>
        public RetreatExecution(GameObject target, float maximumApproachDistance)
        {
            if (!ReferenceEquals(target, null) && !target)
                throw new ArgumentException("Retreat target has been destroyed.", nameof(target));
            Validate.NonNegativeFinite(maximumApproachDistance, nameof(maximumApproachDistance));

            this.target = target;
            this.maximumApproachDistance = maximumApproachDistance;
            hasApproachLimit = maximumApproachDistance > 0f;
        }

        public float RemainingApproachDistance => hasApproachLimit ? Mathf.Max(0f, maximumApproachDistance - approachDistanceUsed) : 0f;
        public bool HasApproachLimit => hasApproachLimit;

        public bool IsCurrentTarget(GameObject currentTarget)
            => ReferenceEquals(target, null) || target && currentTarget && target == currentTarget;

        /// <summary>Charges one physical displacement against the captured target sample and reports progress.</summary>
        public bool TryObserve(Vector2 bodyStart, Vector2 bodyEnd, Vector2 targetCenter,
            float startCompletionDistance, float endCompletionDistance, out bool madeNewBestProgress)
        {
            Validate.Finite(bodyStart, nameof(bodyStart));
            Validate.Finite(bodyEnd, nameof(bodyEnd));
            Validate.Finite(targetCenter, nameof(targetCenter));
            Validate.NonNegativeFinite(startCompletionDistance, nameof(startCompletionDistance));
            Validate.NonNegativeFinite(endCompletionDistance, nameof(endCompletionDistance));

            madeNewBestProgress = false;
            if (!hasPreviousSample)
            {
                approachDistanceUsed += RetreatNavigationGeometry.SegmentApproachDistance(
                    bodyStart, bodyEnd, targetCenter);
                previousBodyCenter = bodyEnd;
                bestCompletionDistance = startCompletionDistance;
                hasPreviousSample = true;
                if (hasApproachLimit
                    && approachDistanceUsed > maximumApproachDistance + NavigationWorldQueries.GeometryEpsilon)
                    return false;

                if (endCompletionDistance < bestCompletionDistance - NavigationWorldQueries.GeometryEpsilon)
                {
                    bestCompletionDistance = endCompletionDistance;
                    madeNewBestProgress = true;
                }

                return true;
            }

            // Use the target center captured at the beginning of this physical step so
            // target motion never consumes the mover's approach budget.
            approachDistanceUsed += RetreatNavigationGeometry.SegmentApproachDistance(
                previousBodyCenter, bodyEnd, targetCenter);
            previousBodyCenter = bodyEnd;
            if (hasApproachLimit
                && approachDistanceUsed > maximumApproachDistance + NavigationWorldQueries.GeometryEpsilon)
                return false;

            if (endCompletionDistance < bestCompletionDistance - NavigationWorldQueries.GeometryEpsilon)
            {
                bestCompletionDistance = endCompletionDistance;
                madeNewBestProgress = true;
            }

            return true;
        }

        /// <summary>Checks an explicit route suffix against the remaining approach budget.</summary>
        public bool IsWithinRemainingApproachDistance(float additionalApproachDistance)
        {
            Validate.NonNegativeFinite(additionalApproachDistance, nameof(additionalApproachDistance));
            return !hasApproachLimit
                || additionalApproachDistance <= RemainingApproachDistance
                    + NavigationWorldQueries.GeometryEpsilon;
        }

        /// <summary>Invalidates the previous physical sample at a pause or execution boundary.</summary>
        public void InvalidateSample()
        {
            hasPreviousSample = false;
            bestCompletionDistance = float.PositiveInfinity;
        }

    }
}
