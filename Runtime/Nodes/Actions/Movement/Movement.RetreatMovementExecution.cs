using Aethiumian.AI.Navigation;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public abstract partial class Movement
    {
        /// <summary>
        /// Owns the process state of one Retreat execution. Goal selection remains with the
        /// provider; this object delegates identity tracking, samples physical ticks,
        /// enforces the approach budget, and settles the last sample before completion.
        /// </summary>
        public sealed class RetreatMovementExecution
        {
            private readonly Movement owner;
            private readonly RetreatExecution execution;
            // This clock measures progress away from a threat, not toward a Fly steering
            // point. RetreatExecution owns the corresponding best-progress sample history.
            private float idleDuration;
            private NavigationGoalRegion tickGoalRegion;
            private Vector2 tickStartCenter;
            private bool tickActive;

            internal RetreatMovementExecution(Movement owner)
            {
                this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
                execution = new RetreatExecution(owner.tracing?.GameObjectValue, owner.MaxApproachDistance);
            }

            private float MaximumIdleDuration
                => owner.MonitorRetreatStall ? owner.MaximumIdleDuration : 0f;

            public float RemainingApproachDistance => execution.RemainingApproachDistance;
            public bool HasApproachLimit => execution.HasApproachLimit;
            public NavigationGoalRegion CurrentGoalRegion => tickGoalRegion;

            internal bool BeginTick()
            {
                if (!execution.IsCurrentTarget(owner.tracing?.GameObjectValue)) return false;

                tickGoalRegion = owner.GetNavigationGoalRegion();
                if (tickGoalRegion == null || !tickGoalRegion.IsRetreat) return false;

                tickStartCenter = owner.NavigationCenterAnchor;
                tickActive = true;
                return true;
            }

            internal bool FinalizeTick()
            {
                if (!tickActive) return true;
                tickActive = false;
                if (tickGoalRegion == null) return false;

                float startCompletionDistance = tickGoalRegion.CompletionDistance(
                    tickStartCenter, owner.NavigationBodySize);
                float completionDistance = tickGoalRegion.CompletionDistance(
                    owner.NavigationCenterAnchor, owner.NavigationBodySize);
                if (!execution.TryObserve(
                    tickStartCenter,
                    owner.NavigationCenterAnchor,
                    tickGoalRegion.Center,
                    startCompletionDistance,
                    completionDistance,
                    out bool madeNewBestProgress))
                    return false;

                float maximumIdleDuration = MaximumIdleDuration;
                if (maximumIdleDuration == 0f || madeNewBestProgress)
                {
                    idleDuration = 0f;
                    return true;
                }
                idleDuration += Time.fixedDeltaTime;
                if (idleDuration < maximumIdleDuration) return true;
                RecordStallFailure(ExecutionResult.Failure(ExecutionFailureReason.Stalled));
                return false;
            }

            internal void DiscardPendingTick()
            {
                tickActive = false;
                tickGoalRegion = null;
            }

            internal bool HasReachedGoal()
                => tickGoalRegion != null
                    && (tickGoalRegion.IsComplete(owner.NavigationCenterAnchor, owner.NavigationBodySize)
                        || tickGoalRegion.SweptIsComplete(
                            tickStartCenter, owner.NavigationCenterAnchor, owner.NavigationBodySize));

            internal void InvalidateSample()
            {
                idleDuration = 0f;
                execution.InvalidateSample();
                DiscardPendingTick();
            }

            public bool AllowsRoute(
                NavigationGoalRegion goal,
                Vector2 anchor,
                NavigationRoute route,
                int? firstSegmentIndex = null)
            {
                NavigationGoalRegion constraintGoal = tickGoalRegion ?? goal;
                if (constraintGoal == null || !constraintGoal.IsRetreat
                    || !execution.HasApproachLimit)
                    return true;

                return execution.IsWithinRemainingApproachDistance(
                    RetreatNavigationGeometry.RouteApproachDistance(
                        anchor,
                        constraintGoal.Center,
                        owner.Navigation.GetRouteSuffixEndpoints(route, firstSegmentIndex)));
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
