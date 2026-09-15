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

        private bool SameGoal(NavigationGoalRegion previous, NavigationGoalRegion latest) => previous != null && ReferenceEquals(previous.Snapshot, latest.Snapshot) && previous.IsReusableFor(latest, Mathf.Max(NavigationRuntime.CellSize, latest.ArrivalErrorBound));

        // Ordinary position changes do not invalidate in-flight work. A capability may opt in
        // to a narrower positional rule through ShouldInvalidateTraceTarget.
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
                return active == null || !IsIrreversible(active);
            }
        }

        /// <summary>
        /// Returns true only when two compatible targets are unambiguously on opposite sides
        /// of the current body. Targets inside the neutral band do not invalidate work.
        /// </summary>
        protected static bool HasSignificantTargetSideChange(NavigationGoalRegion previous, NavigationGoalRegion current, Bounds body)
        {
            if (previous == null || current == null || !CompatibleGoal(previous, current)) return false;

            float neutralMargin = Mathf.Max(
                Mathf.Max(previous.ArrivalErrorBound, current.ArrivalErrorBound),
                NavigationWorldQueries.GeometryEpsilon);
            int previousSide = GetTargetSide(previous.TargetBounds, body, neutralMargin);
            int currentSide = GetTargetSide(current.TargetBounds, body, neutralMargin);
            return previousSide != 0 && currentSide != 0 && previousSide != currentSide;
        }

        private static int GetTargetSide(Bounds target, Bounds body, float neutralMargin)
        {
            if (target.max.x < body.min.x - neutralMargin) return -1;
            if (target.min.x > body.max.x + neutralMargin) return 1;
            return 0;
        }

        /// <summary>
        /// Detects a change in the current planning intent before any candidate is selected. Historical
        /// route metadata remains unchanged when an irreversible action is retained.
        /// </summary>
        private bool RefreshPlanningIntent(NavigationGoalRegion goal, Bounds body)
        {
            NavigationGoalRegion previous = intentGoal;
            if (previous == null)
            {
                intentGoal = goal;
                return false;
            }

            bool invalidated = !CompatibleGoal(previous, goal)
                || (type == Behaviour.Trace && ShouldInvalidateTraceTarget(previous, goal, body));
            if (!invalidated) return false;

            intentGoal = goal;
            // Invalidate the previous sample before any new target can be considered complete.
            // This resets observation progress without cancelling an irreversible physical action.
            ResetActionProgress();
            NavigationRouteSegment active = ActiveSegment;
            CancelPlanningRequests();
            ResetSmartFallbackBackoff();

            if (active is GroundRouteSegment)
            {
                executor?.Cancel();
                route = null;
                routeIndex = 0;
                if (RigidBody)
                    RigidBody.linearVelocity = new Vector2(0f, RigidBody.linearVelocity.y);
            }
            else if (active is FlyRouteSegment)
            {
                executor?.Cancel();
                route = null;
                routeIndex = 0;
                if (RigidBody) RigidBody.linearVelocity = Vector2.zero;
            }
            else if (active != null)
            {
                // Retain only the irreversible action. Its executor continues, while the
                // normal maintenance pass submits a continuation from active.End.
                NavigationGoalRegion previousGoal = route != null ? route.GoalRegion : goal;
                route = NavigationRoute.Partial(active.Start, previousGoal, active.End,
                    new[] { active });
                routeIndex = 0;
            }
            else
            {
                route = null;
                routeIndex = 0;
            }
            return true;
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
                CancelPlanningRequests();
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
                if (followsActive)
                {
                    // The terminal no-route receipt has been consumed. Keep the physical
                    // predecessor running, but do not reprocess this completed receipt every tick.
                    CancelPlanningRequests();
                    return true;
                }
                if (active != null)
                {
                    RejectPrimaryCandidate(goal);
                    return true;
                }

                CancelPlanningRequests();
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
                GetRouteSegments(route, routeIndex), route.ReachesGoal);
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
        private ActionPreparation TryAdoptRoute(NavigationRoute candidate, CandidateSource source, NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (!CanReplaceActiveAction)
                return source == CandidateSource.PrimaryRequest
                    && CanWaitForPrimaryContinuation(candidate, goal)
                    ? ActionPreparation.Waiting
                    : ActionPreparation.Unavailable;
            if (!TryConnectRoute(candidate, body, out NavigationRoute connected))
            {
                return source == CandidateSource.PrimaryRequest
                    && CanWaitForPrimaryContinuation(candidate, goal)
                    ? ActionPreparation.Waiting
                    : ActionPreparation.Unavailable;
            }
            if (connected == null || connected.Count == 0)
            {
                return ActionPreparation.Unavailable;
            }
            if (!RouteAllowed(goal, anchor, connected))
            {
                return ActionPreparation.Unavailable;
            }

            ActionPreparation preparation = PrepareExecutor(connected.Segments[0], body, executor, out MovementExecutor prepared);
            if (preparation != ActionPreparation.Ready) return preparation;
            if (prepared == null || !prepared.IsExecuting)
                throw new InvalidOperationException("Ready acquisition must supply an executing executor.");

            if (!ReferenceEquals(executor, prepared)) executor?.Dispose();
            executor = prepared;
            route = connected;
            routeIndex = 0;
            OnRouteAdopted(source);
            return ActionPreparation.Ready;
        }

        /// <summary>
        /// Keeps a valid primary continuation pending while its committed predecessor is still
        /// physically irreversible. Waiting is intentionally limited to that request boundary;
        /// local candidates and unrelated predecessors must be rejected instead.
        /// </summary>
        private bool CanWaitForPrimaryContinuation(NavigationRoute candidate, NavigationGoalRegion goal)
        {
            NavigationRouteSegment active = ActiveSegment;
            return request != null
                && active != null
                && ReferenceEquals(request.CommittedSegment, active)
                && CompatibleGoal(request.GoalRegion, goal)
                && candidate != null
                && CompatibleGoal(candidate.GoalRegion, goal)
                && RouteAllowed(request.GoalRegion, request.Start, candidate);
        }

        private void OnRouteAdopted(CandidateSource source)
        {
            switch (source)
            {
                case CandidateSource.PrimaryRequest:
                    if (path == PathMode.Smart) ResetSmartFallbackBackoff();
                    CancelPlanningRequests();
                    break;
                case CandidateSource.LocalFallback:
                    // Keep the primary Smart request alive. It may complete later and replace
                    // this reversible fallback at the same fixed-step adoption boundary.
                    CancelFallbackRequest();
                    fallbackBackoffLevel = Math.Min(fallbackBackoffLevel + 1, 2);
                    break;
            }
        }

        /// <summary>Maintains request ownership only; route adoption is exclusive to <see cref="TryAcquireAction"/>.</summary>
        private void MaintainPlanning(NavigationGoalRegion goal, Vector2 anchor, Bounds body, bool advanceResponseBudget)
        {
            if (IsComplete) return;
            bool submittedPrimary = EnsurePrimaryRequest(goal, anchor, body);
            if (submittedPrimary || !advanceResponseBudget || path != PathMode.Smart
                || ActiveSegment != null || request == null || request.Operation.IsCompleted || fallbackRequest != null)
                return;

            if (request.AdvanceFallbackBudget(CurrentSmartWaitThreshold))
                SubmitFallbackRequest(goal, anchor);
        }

        private int CurrentSmartWaitThreshold => fallbackBackoffLevel == 0 ? 4 : fallbackBackoffLevel == 1 ? 8 : 16;

        private bool EnsurePrimaryRequest(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (request != null) return false;
            NavigationRouteSegment action = ActiveSegment;
            bool changed = route != null && !SamePlanningTarget(route.GoalRegion, goal);
            if (action == null && route != null && routeIndex < route.Count) return false;
            if (action != null && !changed && routeIndex + 1 < route.Count) return false;

            if (action != null && !changed && route != null && routeIndex < route.Count)
            {
                Vector2 endpointCenter = route.ResolvedGoal + (Vector2)body.center - anchor;
                if (route.ReachesGoal && goal.IsComplete(endpointCenter, body.size)) return false;
            }

            NavigationRouteSegment predecessor = null;
            if (action != null && (!changed || IsIrreversible(action)))
            {
                predecessor = action;
            }
            else if (action == null && !changed && route != null && !route.ReachesGoal
                && route.Count > 0 && routeIndex == route.Count)
            {
                // Any fully consumed partial action (including local Ground/Fly) continues
                // from its endpoint. Complete routes do not need a continuation request.
                predecessor = route.Segments[route.Count - 1];
            }

            Vector2 start = predecessor != null ? predecessor.End : anchor;
            NavigationPlanningPurpose purpose = predecessor != null
                ? NavigationPlanningPurpose.EndpointContinuation
                : NavigationPlanningPurpose.InitialRoute;
            if (!TryCreatePlanningRequest(start, goal, PlanningExtent, purpose, predecessor, out NavigationPlanningRequest created))
                return false;
            request = created;
            // A successfully submitted primary request is the next accepted planning intent.
            intentGoal = goal;
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

        private bool TryCreatePlanningRequest(Vector2 start, NavigationGoalRegion goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, NavigationRouteSegment predecessor, out NavigationPlanningRequest created)
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
        }

        private void RejectPrimaryCandidate(NavigationGoalRegion goal)
        {
            CancelPlanningRequests();
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

        private void RefreshProgressBaseline(NavigationGoalRegion goal, Vector2 anchor, bool force)
        {
            bool changed = force || !SameGoal(progressGoal, goal);
            if (changed || !retryAnchor.HasValue || !IsWithinContinuationTolerance(retryAnchor.Value, anchor))
            {
                progressGoal = goal;
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
            // Detach both fields before cancellation can invoke callbacks.
            NavigationPlanningRequest primary = request;
            NavigationPlanningRequest local = fallbackRequest;
            request = null;
            fallbackRequest = null;

            try { local?.Release(true); }
            finally { primary?.Release(true); }
        }

        private void CancelFallbackRequest()
        {
            NavigationPlanningRequest previous = fallbackRequest;
            fallbackRequest = null;
            previous?.Release(true);
        }
    }
}
