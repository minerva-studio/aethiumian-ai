using Aethiumian.AI.Navigation;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Navigation.Diagnostics;
#endif
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public abstract partial class Movement
    {
        private enum CandidateSource { ExistingRoute, PrimaryRequest, LocalFallback }

        private static bool IsIrreversible(NavigationRouteSegment segment)
            => segment is JumpRouteSegment or FallRouteSegment or DropThroughRouteSegment;

        private bool SameGoal(NavigationGoalRegion previous, NavigationGoalRegion latest)
            => previous != null && ReferenceEquals(previous.Snapshot, latest.Snapshot)
                && previous.IsReusableFor(latest, Mathf.Max(NavigationRuntime.CellSize, latest.ArrivalErrorBound));

        // Position changes do not invalidate in-flight work. Semantic changes do.
        private static bool CompatibleGoal(NavigationGoalRegion previous, NavigationGoalRegion latest)
        {
            if (previous == null || !ReferenceEquals(previous.Snapshot, latest.Snapshot)) return false;
            NavigationGoalKey first = previous.GoalKey;
            NavigationGoalKey second = latest.GoalKey;
            return first.Geometry == second.Geometry && first.DistanceMetric == second.DistanceMetric
                && first.RequiresLineOfSight == second.RequiresLineOfSight
                && first.ArrivalTolerance.Equals(second.ArrivalTolerance)
                && first.RetreatDistance.Equals(second.RetreatDistance)
                && first.SnapshotCellSize.Equals(second.SnapshotCellSize)
                && first.TargetBounds.size.Equals(second.TargetBounds.size);
        }

        private static bool SamePlanningTarget(NavigationGoalRegion previous, NavigationGoalRegion latest)
            => CompatibleGoal(previous, latest)
                && ((Vector2)(previous.Center - latest.Center)).sqrMagnitude
                    <= NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon;

        private bool CanReplaceActiveAction
        {
            get
            {
                NavigationRouteSegment active = ActiveSegment;
                return active == null || (replacementPolicy == ActionReplacementPolicy.Normal && !IsIrreversible(active));
            }
        }

        private void ValidatePlanningContext(NavigationGoalRegion goal)
        {
            if (request != null && !CompatibleGoal(request.GoalRegion, goal))
            {
                CancelPlanningRequests();
                replacementPolicy = ActionReplacementPolicy.Normal;
                ResetSmartFallbackBackoff();
                return;
            }
            if (fallbackRequest != null && !CompatibleGoal(fallbackRequest.GoalRegion, goal))
                CancelFallbackRequest();
        }

        /// <summary>Arbitrates completed candidates in one fixed-step owner entry point.</summary>
        private void TryAcquireAction(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (TryHandlePrimaryResult(goal, anchor, body) || IsComplete) return;
            if (TryHandleExistingRoute(goal, anchor, body) || IsComplete) return;
            if (ActiveSegment == null) TryHandleFallbackResult(goal, anchor, body);
        }

        private bool TryHandlePrimaryResult(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            NavigationPlanningRequest primary = request;
            if (primary == null || !primary.Operation.IsCompleted) return false;
            primary.Release(false);
            if (primary.Operation.IsCancelled)
            {
                CancelPrimaryRequest();
                return false;
            }
            if (primary.Operation.Exception != null) throw primary.Operation.Exception;

            NavigationRouteSegment active = ActiveSegment;
            bool followsActive = active != null && ReferenceEquals(primary.CommittedSegment, active);
            if (active != null && primary.CommittedSegment != null && !followsActive)
            {
                RejectPrimaryCandidate(goal);
                return true;
            }

            NavigationPlanResult result = primary.Operation.PlanResult;
            NavigationRoute candidate = result.Route;
            if (candidate == null || candidate.Count == 0)
            {
                if (!primary.GoalRegion.GoalKey.Equals(goal.GoalKey))
                {
                    CancelPlanningRequests();
                    return false;
                }
                if (followsActive) return true;
                if (active != null)
                {
                    RejectPrimaryCandidate(goal);
                    return true;
                }

                CancelPrimaryRequest();
                if (IsGoalSatisfied(goal, body, false)) { EndMovement(true, goal); return true; }
                if (route != null && routeIndex < route.Count) return false;
                if (result.Termination == NavigationPlanTermination.BudgetReached)
                {
                    if (!AllowRetry()) EndMovement(false, goal);
                }
                else EndMovement(false, goal);
                return true;
            }

            ActionPreparation preparation = TryAdoptRoute(candidate, CandidateSource.PrimaryRequest, goal, anchor, body);
            if (preparation == ActionPreparation.Unavailable) RejectPrimaryCandidate(goal);
            // Ready and Waiting both consume this selection boundary. Waiting retains the
            // completed Smart candidate instead of letting a lower-priority local candidate win.
            return true;
        }

        private bool TryHandleExistingRoute(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (ActiveSegment != null || route == null || routeIndex >= route.Count) return false;
            NavigationRoute remaining = routeIndex == 0 ? route : NavigationRoute.Create(
                route.Segments[routeIndex].Start, route.GoalRegion, route.ResolvedGoal,
                GetRouteSegments(route, routeIndex), route.SearchComplete);
            ActionPreparation preparation = TryAdoptRoute(remaining, CandidateSource.ExistingRoute, goal, anchor, body);
            if (preparation != ActionPreparation.Unavailable) return true;

            route = null;
            routeIndex = 0;
            if (!AllowRetry()) EndMovement(false, goal);
            return true;
        }

        private bool TryHandleFallbackResult(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            NavigationPlanningRequest local = fallbackRequest;
            if (local == null || !local.Operation.IsCompleted) return false;
            local.Release(false);
            if (local.Operation.IsCancelled) { CancelFallbackRequest(); return true; }
            if (local.Operation.Exception != null) throw local.Operation.Exception;

            NavigationRoute candidate = local.Operation.PlanResult.Route;
            if (candidate == null || candidate.Count == 0)
            {
                // A local miss is never a global Smart terminal result.
                CancelFallbackRequest();
                return true;
            }

            ActionPreparation preparation = TryAdoptRoute(candidate, CandidateSource.LocalFallback, goal, anchor, body);
            if (preparation == ActionPreparation.Unavailable) CancelFallbackRequest();
            // Waiting retains this local candidate. A later Smart receipt still wins at the
            // next selection boundary because primary processing happens first.
            return true;
        }

        /// <summary>Reconnects, validates, prepares, and only then commits a route candidate.</summary>
        private ActionPreparation TryAdoptRoute(NavigationRoute candidate, CandidateSource source,
            NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (!CanReplaceActiveAction) return ActionPreparation.Waiting;
            if (!TryConnectRoute(candidate, body, out NavigationRoute connected)
                || connected == null || connected.Count == 0
                || !RouteAllowed(goal, anchor, connected)) return ActionPreparation.Unavailable;

            ActionPreparation preparation = PrepareExecutor(connected.Segments[0], body, executor, out MovementExecutor prepared);
            if (preparation != ActionPreparation.Ready) return preparation;
            if (prepared == null || !prepared.IsExecuting)
                throw new InvalidOperationException("Ready acquisition must supply an executing executor.");

            if (!ReferenceEquals(executor, prepared)) executor?.Dispose();
            executor = prepared;
            route = connected;
            routeIndex = 0;
            replacementPolicy = source == CandidateSource.LocalFallback
                ? ActionReplacementPolicy.AfterCompletion
                : ActionReplacementPolicy.Normal;
            OnRouteAdopted(source);
            return ActionPreparation.Ready;
        }

        private void OnRouteAdopted(CandidateSource source)
        {
            switch (source)
            {
                case CandidateSource.PrimaryRequest:
                    if (path == PathMode.Smart) ResetSmartFallbackBackoff();
                    CancelFallbackRequest();
                    CancelPrimaryRequest();
                    break;
                case CandidateSource.LocalFallback:
                    CancelFallbackRequest();
                    CancelPrimaryRequest();
                    fallbackBackoffLevel = Math.Min(fallbackBackoffLevel + 1, 2);
                    break;
            }
        }

        /// <summary>Maintains request ownership only; route adoption is exclusive to <see cref="TryAcquireAction"/>.</summary>
        private void MaintainPlanning(NavigationGoalRegion goal, Vector2 anchor, Bounds body,
            bool advanceResponseBudget, NavigationRouteSegment completedFallbackAction = null)
        {
            if (IsComplete) return;
            bool submittedPrimary = EnsurePrimaryRequest(goal, anchor, body, completedFallbackAction);
            if (submittedPrimary || !advanceResponseBudget || path != PathMode.Smart
                || ActiveSegment != null || request == null || request.Operation.IsCompleted || fallbackRequest != null)
                return;

            if (request.AdvanceFallbackBudget(CurrentSmartWaitThreshold))
                SubmitFallbackRequest(goal, anchor);
        }

        private int CurrentSmartWaitThreshold
            => fallbackBackoffLevel == 0 ? 4 : fallbackBackoffLevel == 1 ? 8 : 16;

        private bool EnsurePrimaryRequest(NavigationGoalRegion goal, Vector2 anchor, Bounds body,
            NavigationRouteSegment completedFallbackAction)
        {
            if (request != null) return false;
            NavigationRouteSegment action = ActiveSegment;
            bool changed = route != null && !SamePlanningTarget(route.GoalRegion, goal);
            if (action == null && route != null && routeIndex < route.Count) return false;
            if (action != null && replacementPolicy != ActionReplacementPolicy.AfterCompletion
                && !changed && routeIndex + 1 < route.Count) return false;

            if (action != null && replacementPolicy != ActionReplacementPolicy.AfterCompletion
                && !changed && route != null && routeIndex < route.Count)
            {
                Vector2 endpointCenter = route.ResolvedGoal + (Vector2)body.center - anchor;
                if (route.SearchComplete && goal.IsComplete(endpointCenter, body.size)) return false;
            }

            NavigationRouteSegment predecessor = completedFallbackAction;
            if (predecessor == null && action != null
                && (replacementPolicy == ActionReplacementPolicy.AfterCompletion || !changed || IsIrreversible(action)))
            {
                predecessor = action;
            }
            else if (predecessor == null && action == null && !changed && route != null && !route.SearchComplete
                && route.Count > 0 && routeIndex == route.Count
                && route.Segments[route.Count - 1] is GroundRouteSegment completedGround)
            {
                predecessor = completedGround;
            }

            Vector2 start = predecessor != null ? predecessor.End : anchor;
            NavigationPlanningPurpose purpose = predecessor != null
                ? NavigationPlanningPurpose.EndpointContinuation
                : NavigationPlanningPurpose.InitialRoute;
            if (!TryCreatePlanningRequest(start, goal, PlanningExtent, purpose, predecessor, out NavigationPlanningRequest created))
                return false;
            request = created;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (purpose == NavigationPlanningPurpose.InitialRoute) MovementReplanDiagnostics.RecordInitialPlan();
#endif
            return true;
        }

        private void SubmitFallbackRequest(NavigationGoalRegion goal, Vector2 anchor)
        {
            if (fallbackRequest != null || request == null) return;
            if (TryCreatePlanningRequest(anchor, goal, NavigationPlanningExtent.NextAction,
                NavigationPlanningPurpose.InitialRoute, null, out NavigationPlanningRequest created))
                fallbackRequest = created;
        }

        private bool TryCreatePlanningRequest(Vector2 start, NavigationGoalRegion goal,
            NavigationPlanningExtent extent, NavigationPlanningPurpose purpose,
            NavigationRouteSegment predecessor, out NavigationPlanningRequest created)
        {
            created = null;
            CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(ExecutionCancellation);
            try
            {
                if (!TryRequestRoute(start, goal, extent, purpose, cancellation.Token, out NavigationPlanningOperation operation))
                {
                    cancellation.Dispose();
                    return false;
                }
                if (operation == null) throw new InvalidOperationException("Route request returned no operation.");
                created = new NavigationPlanningRequest(operation, start, goal, purpose, predecessor, cancellation);
                return true;
            }
            catch
            {
                cancellation.Cancel();
                cancellation.Dispose();
                throw;
            }
        }

        private void CompleteCurrentAction()
        {
            routeIndex++;
            replacementPolicy = ActionReplacementPolicy.Normal;
        }

        private void RejectPrimaryCandidate(NavigationGoalRegion goal)
        {
            CancelFallbackRequest();
            CancelPrimaryRequest();
            if (ActiveSegment == null && !AllowRetry()) EndMovement(false, goal);
        }

        private bool RouteAllowed(NavigationGoalRegion goal, Vector2 anchor, NavigationRoute candidate)
            => retreat == null || retreat.AllowsRoute(goal, anchor, GetRouteEndpoints(candidate));

        protected static IReadOnlyList<Vector2> GetRouteEndpoints(NavigationRoute candidate)
        {
            var result = new Vector2[candidate.Count];
            for (int i = 0; i < result.Length; i++) result[i] = candidate.Segments[i].End;
            return result;
        }

        protected static IEnumerable<NavigationRouteSegment> GetRouteSegments(NavigationRoute candidate, int first)
        {
            for (int i = first; i < candidate.Count; i++) yield return candidate.Segments[i];
        }

        protected static bool IsWithinContinuationTolerance(Vector2 first, Vector2 second)
            => Vector2.Distance(first, second) <= NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon;

        private void RefreshRetryBaseline(NavigationGoalRegion goal, Vector2 anchor)
        {
            bool changed = !SameGoal(retryGoal, goal);
            if (changed || !retryAnchor.HasValue || !IsWithinContinuationTolerance(retryAnchor.Value, anchor))
            {
                retryGoal = goal;
                retryAnchor = anchor;
                retries = 0;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            MovementReplanDiagnostics.RecordGoalCheck(changed);
            if (previousCenter.HasValue && Vector2.Distance(previousCenter.Value, NavigationCenterAnchor) > NavigationWorldQueries.GeometryEpsilon)
                MovementReplanDiagnostics.RecordAnchorMove();
#endif
        }

        private bool AllowRetry() => ++retries <= MaximumNoProgressAttempts;

        private void ResetSmartFallbackBackoff() => fallbackBackoffLevel = 0;

        private void CancelPlanningRequests()
        {
            CancelFallbackRequest();
            CancelPrimaryRequest();
        }

        private void CancelPrimaryRequest()
        {
            NavigationPlanningRequest previous = request;
            request = null;
            previous?.Release(true);
        }

        private void CancelFallbackRequest()
        {
            NavigationPlanningRequest previous = fallbackRequest;
            fallbackRequest = null;
            previous?.Release(true);
        }
    }
}
