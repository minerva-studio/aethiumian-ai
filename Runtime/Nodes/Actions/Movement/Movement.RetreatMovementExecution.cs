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
        /// explicit body samples drive identity tracking and physical tick accounting. This object
        /// enforces the approach budget, and settles the last sample before completion.
        /// </summary>
        public sealed class RetreatMovementExecution
        {
            private readonly float maximumIdleDuration;
            private readonly RetreatExecution execution;
            // This clock measures progress away from a threat, not toward a Fly steering
            // point. RetreatExecution owns the corresponding best-progress sample history.
            private float idleDuration;
            private INavigationWorld tickWorld;
            private NavigationGoalRequest? tickGoal;
            private AABB tickStartBody;
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
            public NavigationGoalRequest? CurrentGoal => tickGoal;

            /// <summary>Captures the caller's world, single target and body sample for this permitted tick.</summary>
            internal bool BeginTick(INavigationWorld world, GameObject target, NavigationGoalRequest goal, AABB body)
            {
                if (!execution.IsCurrentTarget(target)) return false;

                tickGoal = goal;
                if (!tickGoal.Value.IsRetreat) return false;

                tickWorld = world;
                tickStartBody = body;
                tickActive = true;
                return true;
            }

            /// <summary>Settles an active sample at most once, including completion during the same tick.</summary>
            internal bool FinalizeTick(AABB body, float deltaTime)
            {
                if (!tickActive) return true;
                tickActive = false;
                if (!tickGoal.HasValue) return false;

                float startCompletionDistance = tickWorld.GetGoalCompletionDistance(tickGoal.Value, tickStartBody);
                float completionDistance = tickWorld.GetGoalCompletionDistance(tickGoal.Value, body);
                if (!execution.TryObserve(
                    tickStartBody.Center,
                    body.Center,
                    tickGoal.Value.TargetBounds.Center,
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
                tickGoal = null;
                tickWorld = null;
            }

            internal bool HasReachedGoal(AABB body)
                => tickGoal.HasValue && tickWorld != null
                    && (tickWorld.IsGoalComplete(tickGoal.Value, body)
                        || tickWorld.IsGoalCompleteAlong(tickGoal.Value, tickStartBody, body));

            internal void InvalidateSample()
            {
                idleDuration = 0f;
                execution.InvalidateSample();
                DiscardPendingTick();
            }

            /// <summary>Checks a caller-selected route suffix without owning route consumption or planning.</summary>
            public bool AllowsRoute(NavigationGoalRequest goal, Vector2 routeStart, IReadOnlyList<NavigationRouteSegment> suffix)
            {
                if (!TryResolveRetreatConstraint(goal, out NavigationGoalRequest constraint)) return true;

                return execution.IsWithinRemainingApproachDistance(
                    RetreatNavigationGeometry.RouteApproachDistance(routeStart, constraint.TargetBounds.Center, suffix));
            }

            public bool AllowsSegment(NavigationGoalRequest goal, Vector2 bodyPosition, Vector2 endpoint)
            {
                if (!TryResolveRetreatConstraint(goal, out NavigationGoalRequest constraint)) return true;

                return execution.IsWithinRemainingApproachDistance(
                    RetreatNavigationGeometry.SegmentApproachDistance(bodyPosition, endpoint, constraint.TargetBounds.Center));
            }

            /// <summary>Resolves the retreat constraint to this tick's sample when one exists.</summary>
            private bool TryResolveRetreatConstraint(NavigationGoalRequest goal, out NavigationGoalRequest constraint)
            {
                constraint = tickGoal ?? goal;
                return constraint.IsRetreat && execution.HasApproachLimit;
            }
        }

    }
}
