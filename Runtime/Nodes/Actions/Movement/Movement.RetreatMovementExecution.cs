using Aethiumian.AI.Navigation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Navigation.Diagnostics;
#endif
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public abstract partial class Movement
    {
        /// <summary>
        /// Owns the process state of one Retreat execution. Goal selection remains with Movement;
        /// explicit samples drive identity tracking and physical tick accounting. This object
        /// enforces the approach budget, and settles the last sample before completion.
        /// </summary>
        public sealed class RetreatMovementExecution
        {
            private readonly float maximumIdleDuration;
            private readonly RetreatExecution execution;
            // This clock measures progress away from a threat, not toward a Fly steering
            // point. RetreatExecution owns the corresponding best-progress sample history.
            private float idleDuration;
            private NavigationGoalRegion tickGoalRegion;
            private Vector2 tickStartCenter;
            private bool tickActive;

            /// <summary>Captures one run's target, approach budget and optional timeout (zero disables it).</summary>
            internal RetreatMovementExecution(GameObject target, float approachBudget, float timeout)
            {
                Validate.NonNegativeFinite(timeout, nameof(timeout));
                maximumIdleDuration = timeout;
                execution = new RetreatExecution(target, approachBudget);
            }

            public float RemainingApproachDistance => execution.RemainingApproachDistance;
            public bool HasApproachLimit => execution.HasApproachLimit;
            public NavigationGoalRegion CurrentGoalRegion => tickGoalRegion;

            /// <summary>Captures the caller's single target and position sample for this permitted tick.</summary>
            internal bool BeginTick(GameObject target, NavigationGoalRegion goal, Vector2 center)
            {
                if (!execution.IsCurrentTarget(target)) return false;

                tickGoalRegion = goal;
                if (tickGoalRegion == null || !tickGoalRegion.IsRetreat) return false;

                tickStartCenter = center;
                tickActive = true;
                return true;
            }

            /// <summary>Settles an active sample at most once, including completion during the same tick.</summary>
            internal bool FinalizeTick(Vector2 center, Vector2 bodySize, float deltaTime)
            {
                if (!tickActive) return true;
                tickActive = false;
                if (tickGoalRegion == null) return false;

                float startCompletionDistance = tickGoalRegion.CompletionDistance(
                    tickStartCenter, bodySize);
                float completionDistance = tickGoalRegion.CompletionDistance(
                    center, bodySize);
                if (!execution.TryObserve(
                    tickStartCenter,
                    center,
                    tickGoalRegion.Center,
                    startCompletionDistance,
                    completionDistance,
                    out bool madeNewBestProgress))
                    return false;

                if (maximumIdleDuration == 0f || madeNewBestProgress)
                {
                    idleDuration = 0f;
                    return true;
                }
                idleDuration += deltaTime;
                if (idleDuration < maximumIdleDuration) return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                MovementReplanDiagnostics.RecordMovementStall();
#endif
                return false;
            }

            internal void DiscardPendingTick()
            {
                tickActive = false;
                tickGoalRegion = null;
            }

            internal bool HasReachedGoal(Vector2 center, Vector2 bodySize)
                => tickGoalRegion != null
                    && (tickGoalRegion.IsComplete(center, bodySize)
                        || tickGoalRegion.SweptIsComplete(
                            tickStartCenter, center, bodySize));

            internal void InvalidateSample()
            {
                idleDuration = 0f;
                execution.InvalidateSample();
                DiscardPendingTick();
            }

            /// <summary>Checks caller-selected route endpoints without owning route consumption or planning.</summary>
            public bool AllowsRoute(
                NavigationGoalRegion goal,
                Vector2 anchor,
                IReadOnlyList<Vector2> suffix)
            {
                NavigationGoalRegion constraintGoal = tickGoalRegion ?? goal;
                if (constraintGoal == null || !constraintGoal.IsRetreat
                    || !execution.HasApproachLimit)
                    return true;

                return execution.IsWithinRemainingApproachDistance(
                    RetreatNavigationGeometry.RouteApproachDistance(
                        anchor,
                        constraintGoal.Center,
                        suffix));
            }

            public bool AllowsSegment(NavigationGoalRegion goal, Vector2 anchor, Vector2 endpoint)
            {
                NavigationGoalRegion constraintGoal = tickGoalRegion ?? goal;
                if (constraintGoal == null || !constraintGoal.IsRetreat
                    || !execution.HasApproachLimit)
                    return true;

                return execution.IsWithinRemainingApproachDistance(
                    RetreatNavigationGeometry.SegmentApproachDistance(
                        anchor, endpoint, constraintGoal.Center));
            }
        }

    }
}
