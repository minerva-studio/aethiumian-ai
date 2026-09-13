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
        /// <summary>
        /// Captured planning context owned by one navigation session. Read on the Unity main
        /// thread; retained context remains inspectable after completion or cancellation.
        /// </summary>
        public sealed class NavigationPlanningRequest
        {
            private CancellationTokenSource cancellation;
            /// <summary>The asynchronous result owned by this request.</summary>
            public NavigationPlanningOperation Operation { get; }
            /// <summary>The execution anchor captured when planning began.</summary>
            public Vector2 Start { get; }
            /// <summary>The immutable target and world captured for this request.</summary>
            public NavigationGoalRegion GoalRegion { get; }
            /// <summary>Whether this request starts movement or supplies its continuation.</summary>
            public NavigationPlanningPurpose Purpose { get; }
            /// <summary>The action this continuation follows, or null for an initial route.</summary>
            public NavigationRouteSegment CommittedSegment { get; }

            internal NavigationPlanningRequest(NavigationPlanningOperation operation, Vector2 start,
                NavigationGoalRegion goal, NavigationPlanningPurpose purpose,
                NavigationRouteSegment committedSegment, CancellationTokenSource cancellation)
            {
                Operation = operation;
                Start = start;
                GoalRegion = goal;
                Purpose = purpose;
                CommittedSegment = committedSegment;
                this.cancellation = cancellation;
            }

            internal void Release(bool cancel)
            {
                CancellationTokenSource resource = cancellation;
                cancellation = null;
                if (resource == null) return;
                try
                {
                    if (cancel) resource.Cancel();
                }
                finally
                {
                    resource.Dispose();
                }
            }
        }

        // One execution owns one session. It polls operations; background work never calls the node.
        // Physical executor state and shared sweep/watchdog baselines remain with the existing owners.
        /// <summary>Owns one Movement execution's navigation. Read state directly; drive operations only on the
        /// Unity main thread after Awake and before cleanup. Retained sessions must not be reused on re-entry.</summary>
        public sealed class RollingNavigationSession
        {
            private readonly Movement owner;
            private NavigationPlanningRequest planningRequest;
            private NavigationRoute navigationRoute;
            private NavigationRoute pendingReplacementRoute;
            private int navigationRouteIndex;
            private NavigationRouteSegment committedRouteSegment;
            private int committedRouteNextIndex;
            private DeferredNavigationTerminal deferredNavigationTerminal;
            private NavigationGoalRegion latestGoalRegion;
            private bool hasSubmittedNavigationPlan;
            private Vector2 lastContinuationAnchor;
            private bool hasContinuationAnchor;
            private int noProgressContinuationCount;
            private const int MaximumNoProgressContinuations = 3;

            /// <summary>Captures a planning terminal that may settle only after its bound action reaches its endpoint.</summary>
            private sealed class DeferredNavigationTerminal
            {
                internal readonly NavigationGoalRegion GoalRegion;
                internal readonly NavigationRouteSegment CommittedSegment;

                internal DeferredNavigationTerminal(
                    NavigationGoalRegion goalRegion, NavigationRouteSegment committedSegment)
                {
                    GoalRegion = goalRegion;
                    CommittedSegment = committedSegment;
                }
            }

            internal RollingNavigationSession(Movement owner) => this.owner = owner;

            public int CurrentNavigationRouteIndex => navigationRouteIndex;
            /// <summary>Whether the retained route no longer makes progress toward the latest goal.</summary>
            public bool IsCurrentNavigationPlanStale => !IsNavigationRouteUseful(
                navigationRoute, latestGoalRegion, owner.NavigationRequestAnchor);
            public NavigationPlanningOperation CurrentPlanningOperation => planningRequest?.Operation;
            /// <summary>Captured context for the in-flight request; inspect on the Unity main thread.</summary>
            public NavigationPlanningRequest CurrentPlanningRequest => planningRequest;
            public NavigationRoute CurrentNavigationRoute => navigationRoute;
            public NavigationRoute PendingNavigationRoute => pendingReplacementRoute;
            public NavigationRouteSegment CurrentCommittedRouteSegment => committedRouteSegment;
            /// <summary>The latest execution goal. Routes retain their own captured GoalRegion.</summary>
            public NavigationGoalRegion CurrentNavigationGoal => latestGoalRegion;

            internal void ResetExecution()
            {
                deferredNavigationTerminal = null;
                hasSubmittedNavigationPlan = false;
                hasContinuationAnchor = false;
                noProgressContinuationCount = 0;
            }

            // Movement cancels first and resets its shared sweep baseline before clearing routes.
            internal void ClearRoutes()
            {
                navigationRoute = null;
                pendingReplacementRoute = null;
                navigationRouteIndex = 0;
                committedRouteSegment = null;
                committedRouteNextIndex = 0;
                latestGoalRegion = null;
                deferredNavigationTerminal = null;
                hasContinuationAnchor = false;
                noProgressContinuationCount = 0;
            }

            internal void ResetSubmissionHistory()
            {
                hasSubmittedNavigationPlan = false;
            }

            /// <summary>Materializes endpoints only for a consuming behaviour. An explicit index selects a shortcut;
            /// otherwise the active route includes its committed segment and remaining cursor.</summary>
            public IReadOnlyList<Vector2> GetRouteSuffixEndpoints(NavigationRoute route, int? firstSegmentIndex = null)
            {
                if (route == null) return Array.Empty<Vector2>();
                if (!firstSegmentIndex.HasValue && ReferenceEquals(route, navigationRoute) && committedRouteSegment != null)
                {
                    List<Vector2> committedSuffix = new() { committedRouteSegment.End };
                    AppendRouteEndpoints(route, committedRouteNextIndex, committedSuffix);
                    return committedSuffix;
                }

                int startIndex = firstSegmentIndex ?? (ReferenceEquals(route, navigationRoute) ? navigationRouteIndex : 0);
                if (startIndex < 0 || startIndex >= route.Count)
                    return Array.Empty<Vector2>();
                List<Vector2> endpoints = new(route.Count - startIndex);
                AppendRouteEndpoints(route, startIndex, endpoints);
                return endpoints;
            }

            private static void AppendRouteEndpoints(NavigationRoute route, int firstSegmentIndex, List<Vector2> endpoints)
            {
                for (int index = Mathf.Max(0, firstSegmentIndex); index < route.Count; index++)
                    endpoints.Add(route.Segments[index].End);
            }

            private bool IsNavigationRouteUseful(NavigationRoute route, NavigationGoalRegion goalRegion, Vector2 anchor)
            {
                if (route == null || goalRegion == null || route.Count == 0
                    || !ReferenceEquals(route.GoalRegion.Snapshot, goalRegion.Snapshot)) return false;

                float currentDistance = owner.NavigationDistanceToGoal(goalRegion, anchor);
                float endpointDistance = owner.NavigationDistanceToGoal(goalRegion, route.ResolvedGoal);
                return endpointDistance + NavigationWorldQueries.GeometryEpsilon < currentDistance
                    && (owner.RetreatExecution == null
                        || owner.RetreatExecution.AllowsRoute(goalRegion, anchor, route));
            }

            /// <summary>
            /// Prepares a route from the actual body anchor against the latest goal and world.
            /// Call on the Unity main thread during this execution. This neither publishes a route
            /// nor starts planning or movement; a false result leaves session state unchanged.
            /// </summary>
            public bool TryPrepareRoute(NavigationRoute route, out NavigationRoute executableRoute)
            {
                executableRoute = null;
                if (route == null || route.Count == 0 || latestGoalRegion == null
                    || !ReferenceEquals(route.GoalRegion.Snapshot, latestGoalRegion.Snapshot)) return false;

                NavigationRoute candidate = route;
                if (candidate.Segments[0] is not FlyRouteSegment
                    && !IsWithinContinuationTolerance(candidate.Start, owner.NavigationRequestAnchor)
                    && !owner.TryReconnectNavigationRoute(candidate, owner.NavigationRequestAnchor, out candidate))
                    return false;
                if (candidate == null || candidate.Count == 0
                    || !owner.TryValidateNavigationRoute(candidate)
                    || owner.RetreatExecution != null && !owner.RetreatExecution.AllowsRoute(
                        latestGoalRegion, owner.NavigationRequestAnchor, candidate))
                    return false;

                if (candidate.Segments[0] is FlyRouteSegment fly
                    && candidate.Start != owner.NavigationRequestAnchor)
                {
                    List<NavigationRouteSegment> segments = new(candidate.Segments);
                    segments[0] = new FlyRouteSegment(owner.NavigationRequestAnchor, fly.End);
                    candidate = NavigationRoute.Create(owner.NavigationRequestAnchor, candidate.GoalRegion,
                        candidate.ResolvedGoal, segments, candidate.SearchComplete);
                }
                executableRoute = candidate;
                return true;
            }

            /// <summary>Clears route state so the next plan starts from the current physical anchor.</summary>
            public void RestartFromPhysicalState()
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                MovementReplanDiagnostics.RecordNavigationRestart();
#endif
                ResetNavigationPlanningRequest(true);
                navigationRoute = null;
                pendingReplacementRoute = null;
                navigationRouteIndex = 0;
                committedRouteSegment = null;
                committedRouteNextIndex = 0;
                deferredNavigationTerminal = null;
                hasContinuationAnchor = false;
                noProgressContinuationCount = 0;
                owner.hasPreviousNavigationCenter = false;
            }

            /// <summary>
            /// Advances the shared rolling-route lifecycle while the derived policy owns only one physical action.
            /// </summary>
            public void Tick()
            {
                Vector2 previousCenter = owner.previousNavigationCenter;
                bool hadPreviousCenter = owner.hasPreviousNavigationCenter;
                try
                {
                    TickRollingNavigationCore(previousCenter, hadPreviousCenter);
                }
                finally
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (hadPreviousCenter
                        && (owner.NavigationCenterAnchor - previousCenter).sqrMagnitude
                            > NavigationWorldQueries.GeometryEpsilon)
                        MovementReplanDiagnostics.RecordAnchorMove();
#endif
                    if (owner.IsComplete)
                    {
                        owner.hasPreviousNavigationCenter = false;
                    }
                    else
                    {
                        owner.previousNavigationCenter = owner.NavigationCenterAnchor;
                        owner.hasPreviousNavigationCenter = true;
                    }
                }
            }

            // Select a connected route before driving physics. Consumed route points only
            // advance the cursor; they never spend a fixed tick or submit additional work.
            private void TickRollingNavigationCore(Vector2 previousCenter, bool hadPreviousCenter)
            {
                MapNavigationRuntime navigation = owner.RequireNavigationRuntime(owner.GetType().Name);
                NavigationGoalRegion goal = owner.GetNavigationGoalRegion();
                if (goal == null) return;
                if (RefreshNavigationGoal(goal, owner.NavigationRequestAnchor,
                    Mathf.Max(navigation.CellSize, goal.ArrivalErrorBound)))
                {
                    hadPreviousCenter = false;
                }

                bool irreversible = committedRouteSegment != null
                    && owner.IsCommittedTraversalIrreversible(committedRouteSegment);
                bool sweptGoal = hadPreviousCenter
                    && goal.SweptIsComplete(previousCenter, owner.NavigationCenterAnchor, owner.NavigationBodySize);
                if (!irreversible && owner.IsNavigationGoalReached(goal, committedRouteSegment, sweptGoal))
                {
                    owner.CompleteNavigationPolicy();
                    return;
                }
                if (!irreversible && committedRouteSegment != null
                    && owner.IsCommittedTraversalReversed(committedRouteSegment, goal))
                {
                    ResetNavigationPlanningRequest(true);
                    owner.CancelCommittedTraversal();
                    committedRouteSegment = null;
                    deferredNavigationTerminal = null;
                    DiscardRoute(true);
                }

                if (planningRequest != null && !CanUseRequest(planningRequest, goal))
                    ResetNavigationPlanningRequest(true);

                ReceivePlanningResult();
                if (owner.IsComplete) return;

                bool canReplace = committedRouteSegment == null
                    || !owner.IsCommittedTraversalIrreversible(committedRouteSegment);
                if (canReplace && pendingReplacementRoute != null)
                {
                    NavigationRoute candidate = pendingReplacementRoute;
                    pendingReplacementRoute = null;
                    if (TryPrepareRoute(candidate, out NavigationRoute prepared))
                    {
                        if (committedRouteSegment != null) owner.CancelCommittedTraversal();
                        committedRouteSegment = null;
                        AdoptNavigationRoute(prepared);
                    }
                }

                // Reuse a safe direct connection when a moving target changes its destination.
                if (canReplace && !owner.CompleteAfterOneNavigationSegment
                    && !goal.IsRetreat && (navigationRoute == null
                        || !IsExactNavigationGoal(navigationRoute.GoalRegion, goal))
                    && owner.TryCreateDirectNavigationRoute(goal, out NavigationRoute direct))
                {
                    if (committedRouteSegment != null) owner.CancelCommittedTraversal();
                    committedRouteSegment = null;
                    AdoptNavigationRoute(direct);
                    ResetNavigationPlanningRequest(true);
                }

                if (committedRouteSegment != null)
                {
                    if (!AdvanceCommittedTraversal())
                    {
                        if (!owner.IsComplete) RequestMissingRoute();
                        return;
                    }
                    if (owner.IsComplete) return;
                }

                TryContinueRoute();
                if (!owner.IsComplete) RequestMissingRoute();
            }

            private bool CanUseRequest(NavigationPlanningRequest request, NavigationGoalRegion goal)
            {
                if (!ReferenceEquals(request.GoalRegion.Snapshot, goal.Snapshot)
                    || request.GoalRegion.Request.Geometry != goal.Request.Geometry) return false;
                if (request.CommittedSegment != null && committedRouteSegment != null
                    && !ReferenceEquals(request.CommittedSegment, committedRouteSegment)) return false;
                Vector2 previousDirection = request.GoalRegion.Center - owner.NavigationRequestAnchor;
                Vector2 direction = goal.Center - owner.NavigationRequestAnchor;
                return Vector2.Dot(previousDirection, direction) >= 0f;
            }

            private void ReceivePlanningResult()
            {
                NavigationPlanningRequest request = planningRequest;
                if (request == null || !request.Operation.IsCompleted) return;

                NavigationPlanningOutcome outcome = request.Operation.Outcome;
                bool sameGoal = IsExactNavigationGoal(request.GoalRegion, latestGoalRegion);
                // Retain a negative endpoint answer as the receipt for this running action.
                // It neither stops ground movement nor resubmits identical work every tick.
                bool emptyAnswer = outcome == NavigationPlanningOutcome.NoPath
                    || outcome == NavigationPlanningOutcome.RouteFound && request.Operation.Result.Count == 0;
                if (emptyAnswer && sameGoal && committedRouteSegment != null
                    && !owner.IsCommittedTraversalIrreversible(committedRouteSegment)) return;
                ResetNavigationPlanningRequest(false);
                if (outcome == NavigationPlanningOutcome.Cancelled) return;
                if (outcome == NavigationPlanningOutcome.Faulted)
                {
                    if (sameGoal) throw request.Operation.Exception;
                    return;
                }

                NavigationRoute route = request.Operation.Result;
                if (outcome == NavigationPlanningOutcome.NoPath
                    || outcome == NavigationPlanningOutcome.RouteFound && route.Count == 0)
                {
                    if (!sameGoal) return;
                    if (committedRouteSegment != null && owner.IsCommittedTraversalIrreversible(committedRouteSegment))
                    {
                        deferredNavigationTerminal = new DeferredNavigationTerminal(
                            request.GoalRegion, committedRouteSegment);
                    }
                    else if (committedRouteSegment != null) return;
                    else if (owner.IsNavigationGoalReached(latestGoalRegion, null, false))
                        owner.CompleteNavigationPolicy();
                    else
                        owner.CompleteNavigationFailurePolicy();
                    return;
                }
                if (outcome != NavigationPlanningOutcome.RouteFound) return;

                // A ground endpoint continuation can remain useful after the target moves. Keep
                // that suffix and rebind only its goal metadata before the generic stale-result
                // arbitration. Progress is measured from the committed endpoint, not the actor's
                // earlier position: the existing action already supplies that part of the progress.
                if (!sameGoal
                    && request.Purpose == NavigationPlanningPurpose.EndpointContinuation
                    && committedRouteSegment is GroundRouteSegment
                    && ReferenceEquals(request.CommittedSegment, committedRouteSegment)
                    && TryCreateCurrentGoalContinuation(route, request.Start, latestGoalRegion, out NavigationRoute continuation))
                {
                    pendingReplacementRoute = continuation;
                    return;
                }

                if (committedRouteSegment != null && owner.IsCommittedTraversalIrreversible(committedRouteSegment)
                    && ReferenceEquals(request.CommittedSegment, committedRouteSegment))
                {
                    DeferNavigationRoute(route, request.Start);
                    return;
                }

                // An unrelated obsolete answer must not displace a useful running action.
                if (committedRouteSegment != null && !sameGoal
                    && IsNavigationRouteUseful(navigationRoute, latestGoalRegion, owner.NavigationRequestAnchor))
                    return;
                if (committedRouteSegment != null && owner.IsCommittedTraversalIrreversible(committedRouteSegment))
                {
                    DeferNavigationRoute(route, request.Start);
                    return;
                }
                pendingReplacementRoute = route;
            }

            // True means the old action completed normally, so the caller may hand off once.
            private bool AdvanceCommittedTraversal()
            {
                NavigationRouteSegment segment = committedRouteSegment;
                ExecutionResult result = owner.TickCommittedNavigationTraversal(segment);
                if (result.Status == ExecutionStatus.Running) return false;

                committedRouteSegment = null;
                navigationRouteIndex = committedRouteNextIndex;
                if (planningRequest != null && planningRequest.Operation.IsCompleted
                    && ReferenceEquals(planningRequest.CommittedSegment, segment))
                    ResetNavigationPlanningRequest(false);
                if (result.Status == ExecutionStatus.Failed)
                {
                    if (result.FailureReason == ExecutionFailureReason.UnexpectedSupport)
                    {
                        ResetEndpointContinuationPlanning();
                        deferredNavigationTerminal = null;
                        owner.HandleUnexpectedNavigationLanding(latestGoalRegion);
                    }
                    else if (result.FailureReason == ExecutionFailureReason.Obstructed && owner.isSmart)
                    {
                        // Execution is already terminal. Retry through the normal planner
                        // cadence instead of pretending that the obstructed segment completed.
                        ResetNavigationPlanningRequest(true);
                        DiscardRoute(true);
                    }
                    else owner.CompleteNavigationFailurePolicy();
                    return false;
                }
                if (owner.IsNavigationGoalReached(latestGoalRegion, segment, false))
                {
                    owner.CompleteNavigationPolicy();
                    return false;
                }

                DeferredNavigationTerminal terminal = deferredNavigationTerminal;
                deferredNavigationTerminal = null;
                if (terminal != null && ReferenceEquals(terminal.CommittedSegment, segment)
                    && IsExactNavigationGoal(terminal.GoalRegion, latestGoalRegion))
                {
                    owner.CompleteNavigationFailurePolicy();
                    return false;
                }
                if (owner.CompleteAfterOneNavigationSegment)
                {
                    owner.CompleteNavigationPolicy();
                    return false;
                }
                return true;
            }

            private void TryContinueRoute()
            {
                if (pendingReplacementRoute != null)
                {
                    NavigationRoute candidate = pendingReplacementRoute;
                    pendingReplacementRoute = null;
                    if (TryPrepareRoute(candidate, out NavigationRoute prepared))
                        AdoptNavigationRoute(prepared);
                }
                if (navigationRoute == null) return;

                // The cursor only moves forward through this finite route. Completed points
                // consume no physics tick and cannot initiate another planning loop.
                while (navigationRouteIndex < navigationRoute.Count)
                {
                    NavigationRouteSegment next = navigationRoute.Segments[navigationRouteIndex];
                    if (!owner.IsNavigationSegmentConsumed(next)) break;
                    navigationRouteIndex++;
                }
                if (navigationRouteIndex >= navigationRoute.Count)
                {
                    if (owner.IsNavigationGoalReached(latestGoalRegion, null, false))
                        owner.CompleteNavigationPolicy();
                    else ClearNavigationRouteForContinuation();
                    return;
                }
                NavigationRoute remaining = navigationRouteIndex == 0 ? navigationRoute
                    : NavigationRoute.Create(navigationRoute.Segments[navigationRouteIndex].Start,
                        navigationRoute.GoalRegion, navigationRoute.ResolvedGoal,
                        GetRouteSegments(navigationRoute, navigationRouteIndex), navigationRoute.SearchComplete);
                // A newly accepted route is already prepared at this anchor. A consumed tail
                // needs preparation once, because the body may have stopped before its start.
                if (!IsWithinContinuationTolerance(remaining.Start, owner.NavigationRequestAnchor))
                {
                    if (!TryPrepareRoute(remaining, out remaining))
                    {
                        ClearNavigationRouteForContinuation();
                        return;
                    }
                }
                AdoptNavigationRoute(remaining);
                if (!owner.TrySelectNavigationSegment(navigationRoute, 0,
                    out NavigationRouteSegment segment, out int nextIndex))
                {
                    ClearNavigationRouteForContinuation();
                    return;
                }
                NavigationSegmentCommitResult result = owner.TryCommitNavigationSegment(segment);
                if (result == NavigationSegmentCommitResult.Deferred) return;
                if (result == NavigationSegmentCommitResult.Rejected)
                {
                    if (owner.CompleteAfterOneNavigationSegment) owner.CompleteNavigationFailurePolicy();
                    else ClearNavigationRouteForContinuation();
                    return;
                }
                committedRouteSegment = segment;
                committedRouteNextIndex = nextIndex;
                AdvanceCommittedTraversal();
            }

            private void RequestMissingRoute()
            {
                if (planningRequest != null || pendingReplacementRoute != null
                    || deferredNavigationTerminal != null) return;
                if (committedRouteSegment != null)
                {
                    TryBeginCommittedEndpointContinuationPlanning(latestGoalRegion);
                    return;
                }
                if (navigationRoute == null)
                    TryBeginPlanning(owner.NavigationRequestAnchor, latestGoalRegion);
            }

            private static IEnumerable<NavigationRouteSegment> GetRouteSegments(
                NavigationRoute route, int firstSegmentIndex)
            {
                for (int index = firstSegmentIndex; index < route.Count; index++)
                    yield return route.Segments[index];
            }

            /// <summary>Publishes the latest goal while retaining any useful route or in-flight request.</summary>
            private bool RefreshNavigationGoal(NavigationGoalRegion goalRegion, Vector2 bodyAnchor, float movementThreshold)
            {
                NavigationGoalRegion reference = GetNavigationGoalReference();
                bool changed = reference == null
                    || !ReferenceEquals(reference.Snapshot, goalRegion.Snapshot)
                    || !reference.IsReusableFor(goalRegion, movementThreshold);
                if (reference != null)
                {
                    Vector2 referenceDirection = reference.Center - bodyAnchor;
                    Vector2 currentDirection = goalRegion.Center - bodyAnchor;
                    bool meaningfulDirectionChange = Mathf.Max(referenceDirection.magnitude, currentDirection.magnitude)
                        > movementThreshold;
                    changed |= meaningfulDirectionChange
                        && referenceDirection.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon
                        && currentDirection.sqrMagnitude > NavigationWorldQueries.GeometryEpsilon
                        && Vector2.Dot(referenceDirection, currentDirection) < 0f;
                }

                latestGoalRegion = goalRegion;
                // Terminal conclusions expire on any target change, independently of
                // the movement threshold used to classify replanning diagnostics.
                if (deferredNavigationTerminal != null
                    && !IsExactNavigationGoal(deferredNavigationTerminal.GoalRegion, goalRegion))
                    deferredNavigationTerminal = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                MovementReplanDiagnostics.RecordGoalCheck(changed);
#endif
                if (changed)
                {
                    deferredNavigationTerminal = null;
                    hasContinuationAnchor = false;
                    noProgressContinuationCount = 0;
                }

                return changed;
            }

            /// <summary>Gets the route or request goal used for threshold-based target-motion checks.</summary>
            private NavigationGoalRegion GetNavigationGoalReference()
            {
                if (planningRequest != null) return planningRequest.GoalRegion;
                return pendingReplacementRoute?.GoalRegion
                    ?? navigationRoute?.GoalRegion
                    ?? latestGoalRegion;
            }

            /// <summary>Prefetches a missing tail while the last committed action is still running.</summary>
            private void TryBeginCommittedEndpointContinuationPlanning(NavigationGoalRegion goalRegion)
            {
                if (owner.CompleteAfterOneNavigationSegment
                    || committedRouteSegment == null
                    || pendingReplacementRoute != null
                    || deferredNavigationTerminal != null
                    || planningRequest != null
                    || navigationRoute == null
                    || committedRouteNextIndex < navigationRoute.Count)
                    return;

                Vector2 endpointCenter = navigationRoute.ResolvedGoal
                    + owner.NavigationCenterAnchor - owner.NavigationRequestAnchor;
                if (navigationRoute.SearchComplete
                    && goalRegion.IsComplete(endpointCenter, owner.NavigationBodySize))
                    return;

                TryBeginPlanning(committedRouteSegment.End, goalRegion, committedRouteSegment);
            }

            /// <summary>
            /// Submits missing work immediately. An existing request or prepared result prevents duplicate work.
            /// The legacy bypassReplanCooldown argument is retained for source compatibility; no time gate exists.
            /// </summary>
            public bool TryBeginPlanning(
                Vector2 start,
                NavigationGoalRegion goalRegion,
                NavigationRouteSegment committedSegment = null,
                bool bypassReplanCooldown = false)
            {
                if (planningRequest != null || pendingReplacementRoute != null) return false;
                if (hasContinuationAnchor
                    && !IsWithinContinuationTolerance(owner.NavigationRequestAnchor, lastContinuationAnchor))
                {
                    hasContinuationAnchor = false;
                    noProgressContinuationCount = 0;
                }
                if (committedSegment == null && noProgressContinuationCount >= MaximumNoProgressContinuations)
                {
                    pendingReplacementRoute = null;
                    owner.CompleteNavigationFailurePolicy();
                    return false;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!hasSubmittedNavigationPlan)
                {
                    MovementReplanDiagnostics.RecordInitialPlan();
                    hasSubmittedNavigationPlan = true;
                }

#endif
                MapNavigationRuntime navigation = owner.RequireNavigationRuntime(owner.GetType().Name);
                CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(owner.CancellationToken);
                NavigationPlanningPurpose purpose = committedSegment == null
                    ? NavigationPlanningPurpose.InitialRoute : NavigationPlanningPurpose.EndpointContinuation;
                try
                {
                    NavigationPlanningOperation operation = owner.CreateNavigationPlanningOperation(
                        navigation, start, goalRegion.Request, cancellation.Token, purpose)
                        ?? throw new InvalidOperationException("Navigation planning returned no operation.");
                    planningRequest = new NavigationPlanningRequest(
                        operation, start, goalRegion, purpose, committedSegment, cancellation);
                }
                catch
                {
                    cancellation.Cancel();
                    cancellation.Dispose();
                    throw;
                }
                return true;
            }

            /// <summary>Atomically adopts a completed route and its captured request identity.</summary>
            private void AdoptNavigationRoute(NavigationRoute route)
            {
                navigationRoute = route;
                navigationRouteIndex = 0;
                pendingReplacementRoute = null;
                deferredNavigationTerminal = null;
            }

            /// <summary>
            /// Associates a connected, useful continuation with the latest goal metadata.
            /// A suffix must improve on its own start before its old goal identity can be replaced.
            /// </summary>
            private bool TryCreateCurrentGoalContinuation(
                NavigationRoute route,
                Vector2 requestStart,
                NavigationGoalRegion goalRegion,
                out NavigationRoute continuation)
            {
                continuation = TryCreateCommittedContinuationRoute(route, requestStart);
                if (!IsNavigationRouteUseful(continuation, goalRegion, requestStart))
                {
                    continuation = null;
                    return false;
                }
                if (IsExactNavigationGoal(continuation.GoalRegion, goalRegion)) return true;

                continuation = NavigationRoute.Create(
                    continuation.Start,
                    goalRegion,
                    continuation.ResolvedGoal,
                    continuation.Segments,
                    continuation.SearchComplete);
                return true;
            }

            /// <summary>
            /// Defers a replacement until the current committed action finishes. A result that
            /// cannot be reduced to a proven suffix remains quarantined and is revalidated at
            /// the physical endpoint before it can affect execution.
            /// </summary>
            private void DeferNavigationRoute(NavigationRoute route, Vector2 requestStart)
            {
                NavigationRoute continuation = TryCreateCommittedContinuationRoute(route, requestStart);
                pendingReplacementRoute = continuation
                    ?? (route != null && IsWithinContinuationTolerance(route.Start, requestStart)
                        ? route
                        : null);
            }

            /// <summary>
            /// Removes the already committed irreversible segment from a late planning result.
            /// A late result that cannot identify that segment is discarded instead of replaying
            /// the irreversible action from the old request origin.
            /// </summary>
            private NavigationRoute TryCreateCommittedContinuationRoute(NavigationRoute route, Vector2 requestStart)
            {
                if (route == null || committedRouteSegment == null) return null;
                if (!IsWithinContinuationTolerance(route.Start, requestStart)) return null;

                // A request can legitimately be created after the committed segment's
                // predicted endpoint has become the new anchor. In that case the result is
                // already a continuation and must not be searched for the old segment again.
                if (IsWithinContinuationTolerance(requestStart, committedRouteSegment.End))
                    return route;

                // In this lifecycle a pending operation was submitted before its first
                // result was committed, so the committed segment is the result's first
                // segment. This keeps the hand-off O(1) and avoids accepting a coincidental
                // same-shaped segment later in an unrelated route.
                if (route.Count == 0 || !IsSameCommittedSegment(route.Segments[0], committedRouteSegment))
                    return null;

                int suffixIndex = 1;
                if (suffixIndex >= route.Count)
                {
                    return NavigationRoute.Create(
                        committedRouteSegment.End,
                        route.GoalRegion,
                        committedRouteSegment.End,
                        Array.Empty<NavigationRouteSegment>(),
                        route.SearchComplete);
                }

                NavigationRouteSegment[] suffix = new NavigationRouteSegment[route.Count - suffixIndex];
                for (int suffixOffset = 0; suffixOffset < suffix.Length; suffixOffset++)
                    suffix[suffixOffset] = route.Segments[suffixIndex + suffixOffset];

                return NavigationRoute.Create(
                    suffix[0].Start,
                    route.GoalRegion,
                    route.ResolvedGoal,
                    suffix,
                    route.SearchComplete);
            }

            private static bool IsSameCommittedSegment(
                NavigationRouteSegment candidate,
                NavigationRouteSegment committed)
            {
                if (candidate == null || committed == null
                    || candidate.GetType() != committed.GetType()) return false;

                return IsWithinContinuationTolerance(candidate.Start, committed.Start)
                    && IsWithinContinuationTolerance(candidate.End, committed.End);
            }

            private static bool IsWithinContinuationTolerance(Vector2 first, Vector2 second)
            {
                float tolerance = NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon;
                return (first - second).sqrMagnitude <= tolerance * tolerance;
            }

            /// <summary>Matches two captured goals by immutable key and world-snapshot identity.</summary>
            private static bool IsExactNavigationGoal(NavigationGoalRegion first, NavigationGoalRegion second)
                => first != null && second != null
                    && ReferenceEquals(first.Snapshot, second.Snapshot)
                    && first.GoalKey.Equals(second.GoalKey);

            /// <summary>Discards uncommitted route data. The legacy retry argument no longer controls timing.</summary>
            public void DiscardRoute(bool allowImmediateRetry)
            {
                navigationRoute = null;
                navigationRouteIndex = 0;
                pendingReplacementRoute = null;
            }

            /// <summary>Clears the consumed route so continuation begins from the next real body anchor.</summary>
            private void ClearNavigationRouteForContinuation()
            {
                Vector2 anchor = owner.NavigationRequestAnchor;
                if (!hasContinuationAnchor
                    || !IsWithinContinuationTolerance(anchor, lastContinuationAnchor))
                {
                    lastContinuationAnchor = anchor;
                    hasContinuationAnchor = true;
                    noProgressContinuationCount = 0;
                }
                else
                {
                    noProgressContinuationCount++;
                }
                navigationRoute = null;
                navigationRouteIndex = 0;
                pendingReplacementRoute = null;
            }

            /// <summary>Releases only the request and its cancellation resources.</summary>
            internal void ResetNavigationPlanningRequest(bool cancel)
            {
                NavigationPlanningRequest request = planningRequest;
                planningRequest = null;
                request?.Release(cancel);
            }

            private void ResetEndpointContinuationPlanning()
            {
                if (planningRequest?.CommittedSegment != null)
                    ResetNavigationPlanningRequest(true);
                pendingReplacementRoute = null;
            }

        }
    }
}
