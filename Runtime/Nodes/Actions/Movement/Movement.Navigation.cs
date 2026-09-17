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
        private enum CandidateSource
        {
            ExistingRoute,
            PrimaryRequest,
            LocalFallback
        }

        private bool CanReplaceActiveAction
        {
            get
            {
                NavigationRouteSegment active = ActiveSegment;
                return active == null || active.IsReversible;
            }
        }

        /// <summary>
        /// Updates planning work when the accepted target is no longer reusable. A moving target
        /// does not stop a safe action; it only makes its pending planning result eligible for refresh.
        /// </summary>
        private bool RefreshPlanningIntent(NavigationGoalRequest goal)
        {
            NavigationGoalRequest? previous = intentGoal;
            if (!previous.HasValue)
            {
                intentGoal = goal;
                return false;
            }

            if (!previous.Value.HasCompatibleSemantics(goal))
            {
                intentGoal = goal;
                ResetActionProgress();
                simpleWaitTicks = 0;
                NavigationRouteSegment active = ActiveSegment;
                CancelPlanningRequests();
                ResetSmartFallbackBackoff();

                if (active is GroundRouteSegment)
                {
                    executor?.Cancel(); route = null; routeIndex = 0;
                    if (RigidBody) RigidBody.linearVelocity = new Vector2(0f, RigidBody.linearVelocity.y);
                }
                else if (active is FlyRouteSegment)
                {
                    executor?.Cancel(); route = null; routeIndex = 0;
                    if (RigidBody) RigidBody.linearVelocity = Vector2.zero;
                }
                else if (active != null)
                {
                    // A historical action keeps the goal and world it was planned against; an
                    // irreversible predecessor must never be re-labelled with the newest target.
                    NavigationGoalRequest historicalGoal = route != null ? route.Goal : goal;
                    INavigationWorld historicalWorld = route != null ? route.World : NavigationWorld;
                    route = NavigationRoute.Partial(active.Start, historicalGoal, historicalWorld, active.End, new[] { active });
                    routeIndex = 0;
                }
                else { route = null; routeIndex = 0; }
                return true;
            }

            // Positional changes are handled against the pending request after receipt selection.
            // They never cancel or reset the currently executing action here.
            return false;
        }

        /// <summary>
        /// Replaces only a still-pending positional request after its refresh interval. Completed
        /// receipts have already passed through the candidate arbiter at this point.
        /// </summary>
        private void RefreshPendingPlanningRequest(NavigationGoalRequest goal)
        {
            NavigationPlanningRequest pending = request;
            if (pending == null || pending.Operation.IsCompleted)
                return;

            bool stale = !pending.Goal.IsReusableFor(goal) && !RouteCoversGoal(route, routeIndex, NavigationBodyAabb, goal);
            if (!pending.AdvanceStaleness(stale)) return;

            // Refresh only the primary Smart request; an in-flight Simple request belongs to
            // the independent action-supply lifecycle and must not be cancelled here.
            CancelPrimaryRequest();
        }

        /// <summary>
        /// Returns whether a route's remaining suffix already satisfies the current goal. The route is
        /// evaluated in the world that planned it, which is also the world-ownership check that used to
        /// be carried by the goal's bound snapshot.
        /// </summary>
        private static bool RouteCoversGoal(NavigationRoute candidate, int first, AABB body, NavigationGoalRequest goal)
        {
            if (candidate == null || first >= candidate.Count) return false;
            INavigationWorld world = candidate.World;
            Vector2 half = Vector2.up * (body.Size.y * 0.5f);
            for (int i = first; i < candidate.Count; i++)
            {
                NavigationRouteSegment segment = candidate.Segments[i];
                if (segment is GroundRouteSegment)
                {
                    Vector2 start = segment.Start + half;
                    Vector2 end = segment.End + half;
                    if (world.IsGoalCompleteAlong(goal, start, end, body.Size)) return true;
                }
                else if (segment is FlyRouteSegment)
                {
                    // Aerial movement is accepted at a waypoint; the goal predicate performs
                    // the geometry and line-of-sight checks for the body center.
                    if (world.IsGoalComplete(goal, segment.End, body.Size)) return true;
                }
                else if (world.IsGoalComplete(goal, segment.End, body.Size)) return true;
            }
            return false;
        }

        /// <summary>Arbitrates completed candidates in one fixed-step owner entry point.</summary>
        private bool TryAcquireAction(NavigationGoalRequest goal, Vector2 anchor, AABB body)
        {
            TryHandlePrimaryResult(goal, anchor, body);
            if (IsComplete) return false;

            TryHandleExistingRoute(goal, anchor, body);
            if (IsComplete) return false;

            // A completed Smart receipt that was stale, cancelled, or not physically
            // adoptable must not prevent an independent Simple receipt from supplying a
            // reversible action in the same fixed step.  Current Smart results still win
            // naturally because adoption updates the route before this demand check.
            if (CanReplaceActiveAction && NeedsSimpleAcquisition(goal, body) && TryHandleFallbackResult(goal, anchor, body))
                return true;

            return false;
        }

        private bool TryHandlePrimaryResult(NavigationGoalRequest goal, Vector2 anchor, AABB body)
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
                // GoalKey is an exact cache identity. Runtime handoff uses the semantic
                // compatibility contract so harmless collider sampling noise cannot discard a
                // completed result before it reaches the executor.
                if (!primary.Goal.HasCompatibleSemantics(goal))
                {
                    CancelPrimaryRequest();
                    return false;
                }
                if (followsActive)
                {
                    // The terminal no-route receipt has been consumed. Keep the physical
                    // predecessor running, but do not reprocess this completed receipt every tick.
                    CancelPrimaryRequest();
                    return true;
                }
                if (active != null)
                {
                    RejectPrimaryCandidate(goal);
                    return true;
                }

                CancelPrimaryRequest();
                // A terminal result is only authoritative for the goal that was captured
                // when the request was submitted.  If the target moved outside that goal's
                // reusable region while the search was pending, let the normal maintenance
                // path submit a fresh request instead of ending the node for a stale miss.
                if (!primary.Goal.IsReusableFor(goal))
                    return false;

                simpleWaitTicks = 0;
                if (fallbackRequest != null) return true;
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

        private bool TryHandleExistingRoute(NavigationGoalRequest goal, Vector2 anchor, AABB body)
        {
            if (ActiveSegment != null || route == null || routeIndex >= route.Count) return false;
            NavigationRoute remaining = routeIndex == 0 ? route : NavigationRoute.Create(route.Segments[routeIndex].Start, route.Goal, route.World, route.ResolvedGoal, route.GetRouteSegments(routeIndex), route.ReachesGoal);
            ActionPreparation preparation = TryAdoptRoute(remaining, CandidateSource.ExistingRoute, goal, anchor, body);
            if (preparation != ActionPreparation.Unavailable) return true;

            route = null;
            routeIndex = 0;
            CancelPrimaryRequest();
            if (fallbackRequest == null && !AllowRetry()) EndMovement(false, goal);
            return true;
        }

        private bool TryHandleFallbackResult(NavigationGoalRequest goal, Vector2 anchor, AABB body)
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
        private ActionPreparation TryAdoptRoute(NavigationRoute candidate, CandidateSource source, NavigationGoalRequest goal, Vector2 anchor, AABB body)
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

            bool servesCurrentIntent = connected.Goal.HasCompatibleSemantics(goal)
                && (connected.Goal.IsReusableFor(goal) || RouteCoversGoal(connected, 0, body, goal));

            ActionPreparation preparation = PrepareExecutor(connected.Segments[0], body, executor, out MovementExecutor prepared);
            if (preparation != ActionPreparation.Ready) return preparation;
            if (prepared == null || !prepared.IsExecuting)
                throw new InvalidOperationException("Ready acquisition must supply an executing executor.");

            if (!ReferenceEquals(executor, prepared)) executor?.Dispose();
            executor = prepared;
            route = connected;
            routeIndex = 0;
            OnRouteAdopted(source, servesCurrentIntent);
            return ActionPreparation.Ready;
        }

        /// <summary>
        /// Keeps a valid primary continuation pending while its committed predecessor is still
        /// physically irreversible. Waiting is intentionally limited to that request boundary;
        /// local candidates and unrelated predecessors must be rejected instead.
        /// </summary>
        private bool CanWaitForPrimaryContinuation(NavigationRoute candidate, NavigationGoalRequest goal)
        {
            NavigationRouteSegment active = ActiveSegment;
            return request != null
                && active != null
                && ReferenceEquals(request.CommittedSegment, active)
                && request.Goal.HasCompatibleSemantics(goal)
                && candidate != null
                && candidate.Goal.HasCompatibleSemantics(goal)
                && RouteAllowed(request.Goal, request.Start, candidate);
        }

        private void OnRouteAdopted(CandidateSource source, bool servesCurrentIntent)
        {
            switch (source)
            {
                case CandidateSource.PrimaryRequest:
                    if (servesCurrentIntent)
                    {
                        if (path == PathMode.Smart) ResetSmartFallbackBackoff();
                        // A Smart route for the current intent starts a fresh response
                        // interval and makes a pending local candidate unnecessary.
                        simpleWaitTicks = 0;
                        CancelPrimaryRequest();
                        CancelFallbackRequest();
                    }
                    else
                    {
                        // An executable continuation for an older intent must not rewrite
                        // the independent Simple response budget or discard its candidate.
                        CancelPrimaryRequest();
                    }
                    break;
                case CandidateSource.LocalFallback:
                    // Keep the primary Smart request alive. It may complete later and replace
                    // this reversible fallback at the same fixed-step adoption boundary.
                    CancelFallbackRequest();
                    fallbackBackoffLevel = Math.Min(fallbackBackoffLevel + 1, 2);
                    // Each committed Simple action starts the next independent cooldown.
                    simpleWaitTicks = 0;
                    break;
            }
        }

        /// <summary>Maintains request ownership only; route adoption is exclusive to <see cref="TryAcquireAction"/>.</summary>
        private void MaintainPlanning(NavigationGoalRequest goal, Vector2 anchor, AABB body, bool skipCountingThisTick)
        {
            if (IsComplete) return;
            EnsurePrimaryRequest(goal, anchor, body);
            MaintainSimpleAcquisition(goal, anchor, NeedsSimpleAcquisition(goal, body),
                skipCountingThisTick);
        }

        private bool NeedsSimpleAcquisition(NavigationGoalRequest goal, AABB body)
        {
            NavigationRouteSegment active = ActiveSegment;
            if (active == null) return true;
            if (!active.IsReversible) return false;
            // A reversible partial action is already supplying movement for this planning
            // target.  Do not stack another Simple request while that action is executing;
            // once it completes, the active-segment check above becomes false and the next
            // independent cooldown can request another action.
            if (route != null && route.Goal.IsSamePlanningTarget(goal)) return false;
            return !RouteCoversGoal(route, routeIndex, body, goal);
        }

        private void MaintainSimpleAcquisition(NavigationGoalRequest goal, Vector2 anchor, bool needsAction, bool skipCountingThisTick)
        {
            if (path != PathMode.Smart || !needsAction)
            {
                simpleWaitTicks = 0;
                return;
            }

            if (fallbackRequest != null || skipCountingThisTick)
                return;

            simpleWaitTicks = Math.Min(simpleWaitTicks + 1, CurrentSmartWaitThreshold);
            if (simpleWaitTicks < CurrentSmartWaitThreshold)
                return;

            simpleWaitTicks = 0;
            SubmitFallbackRequest(goal, anchor);
        }

        private int CurrentSmartWaitThreshold => fallbackBackoffLevel == 0 ? 4 : fallbackBackoffLevel == 1 ? 8 : 16;

        private bool EnsurePrimaryRequest(NavigationGoalRequest goal, Vector2 anchor, AABB body)
        {
            if (request != null) return false;
            NavigationRouteSegment action = ActiveSegment;
            bool changed = route != null && !route.Goal.IsSamePlanningTarget(goal);
            if (action == null && route != null && routeIndex < route.Count) return false;
            if (action != null && !changed && routeIndex + 1 < route.Count) return false;

            if (action != null && !changed && route != null && routeIndex < route.Count)
            {
                Vector2 endpointCenter = route.ResolvedGoal + body.Center - anchor;
                if (route.ReachesGoal && route.World.IsGoalComplete(goal, endpointCenter, body.Size)) return false;
            }

            NavigationRouteSegment predecessor = null;
            if (action != null && (!changed || !action.IsReversible))
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
            NavigationPlanningPurpose purpose = predecessor != null ? NavigationPlanningPurpose.EndpointContinuation : NavigationPlanningPurpose.InitialRoute;
            if (!TryCreatePlanningRequest(start, goal, PlanningExtent, purpose, predecessor, out NavigationPlanningRequest created))
                return false;
            request = created;
            // A successfully submitted primary request is the next accepted planning intent,
            // including an endpoint continuation for an irreversible predecessor.
            intentGoal = goal;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (purpose == NavigationPlanningPurpose.InitialRoute) MovementReplanDiagnostics.RecordInitialPlan();
#endif
            return true;
        }

        private void SubmitFallbackRequest(NavigationGoalRequest goal, Vector2 anchor)
        {
            if (fallbackRequest != null) return;
            if (TryCreatePlanningRequest(anchor, goal, NavigationPlanningExtent.NextAction,
                NavigationPlanningPurpose.InitialRoute, null, out NavigationPlanningRequest created))
                fallbackRequest = created;
        }

        private bool TryCreatePlanningRequest(Vector2 start, NavigationGoalRequest goal, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose, NavigationRouteSegment predecessor, out NavigationPlanningRequest created)
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

        private void RejectPrimaryCandidate(NavigationGoalRequest goal)
        {
            CancelPrimaryRequest();
            if (ActiveSegment == null && fallbackRequest == null && !AllowRetry()) EndMovement(false, goal);
        }

        private bool RouteAllowed(NavigationGoalRequest goal, Vector2 anchor, NavigationRoute candidate)
            => retreat == null || retreat.AllowsRoute(goal, anchor, candidate.Segments);

        protected static bool IsWithinContinuationTolerance(Vector2 first, Vector2 second)
            => Vector2.Distance(first, second) <= NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon;

        private void RefreshProgressBaseline(NavigationGoalRequest goal, Vector2 anchor, bool force)
        {
            bool changed = force || !progressGoal.HasValue || !progressGoal.Value.IsReusableFor(goal);
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

        private void CancelPrimaryRequest()
        {
            NavigationPlanningRequest primary = request;
            request = null;
            primary?.Release(true);
        }

        private void CancelFallbackRequest()
        {
            NavigationPlanningRequest previous = fallbackRequest;
            fallbackRequest = null;
            previous?.Release(true);
        }
    }
}
