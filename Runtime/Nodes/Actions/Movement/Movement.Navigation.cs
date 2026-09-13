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
        private static bool IsIrreversible(NavigationRouteSegment segment)
            => segment is JumpRouteSegment or FallRouteSegment or DropThroughRouteSegment;

        private bool SameGoal(NavigationGoalRegion previous, NavigationGoalRegion latest)
            => previous != null && ReferenceEquals(previous.Snapshot, latest.Snapshot)
                && previous.IsReusableFor(latest, Mathf.Max(NavigationRuntime.CellSize, latest.ArrivalErrorBound));

        private void ReceiveRoute(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (request == null) return;
            if (!SameGoal(request.GoalRegion, goal)) { CancelRequest(); return; }
            if (!request.Operation.IsCompleted) return;
            // A completed request remains its own candidate/terminal receipt. Release its
            // cancellation resource now, but retain the immutable result until hand-off.
            request.Release(false);
            if (request.Operation.IsCancelled) { CancelRequest(); return; }
            if (request.Operation.Exception != null) throw request.Operation.Exception;
            if (IsIrreversible(ActiveSegment)) return;

            NavigationPlanResult result = request.Operation.PlanResult;
            NavigationRoute candidate = result.Route;
            if (candidate == null || candidate.Count == 0)
            {
                if (ActiveSegment != null) return;
                CancelRequest();
                if (IsGoalSatisfied(goal, body, false)) { EndMovement(true, goal); return; }
                if (route != null && routeIndex < route.Count) return;
                if (result.Termination == NavigationPlanTermination.BudgetReached)
                {
                    if (!AllowRetry()) EndMovement(false, goal);
                }
                else EndMovement(false, goal);
                return;
            }
            if (!TryConnectRoute(candidate, body, out NavigationRoute connected)
                || connected == null || connected.Count == 0 || !RouteAllowed(goal, anchor, connected))
            {
                // A continuation from an upcoming endpoint may not connect yet. Keep the
                // current physical action and inspect the same result once it has finished.
                if (ActiveSegment != null && request.CommittedSegment != null) return;
                CancelRequest();
                if (ActiveSegment == null && !AllowRetry()) EndMovement(false, goal);
                return;
            }
            executor?.Cancel();
            route = connected;
            routeIndex = 0;
            previousCenter = null;
            CancelRequest();
        }

        private bool PrepareNextAction(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (route == null || routeIndex >= route.Count) return false;
            NavigationRoute remaining = routeIndex == 0 ? route : NavigationRoute.Create(
                route.Segments[routeIndex].Start, route.GoalRegion, route.ResolvedGoal,
                GetRouteSegments(route, routeIndex), route.SearchComplete);
            if (!TryConnectRoute(remaining, body, out NavigationRoute connected)
                || connected == null || connected.Count == 0 || !RouteAllowed(goal, anchor, connected))
            {
                route = null;
                routeIndex = 0;
                if (!AllowRetry()) EndMovement(false, goal);
                return false;
            }
            route = connected;
            routeIndex = 0;
            ActionPreparation preparation = PrepareExecutor(route.Segments[0], goal, body, executor,
                out MovementExecutor prepared);
            if (preparation == ActionPreparation.Waiting) return false;
            if (preparation == ActionPreparation.Unavailable)
            {
                route = null;
                if (IsGoalSatisfied(goal, body, false)) EndMovement(true, goal);
                else if (!AllowRetry()) EndMovement(false, goal);
                return false;
            }
            if (prepared == null || !prepared.IsExecuting)
                throw new InvalidOperationException("Ready acquisition must supply an executing executor.");
            if (!ReferenceEquals(executor, prepared))
            {
                executor?.Dispose();
                executor = prepared;
            }
            return true;
        }

        private void RequestNextRoute(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (request != null || IsComplete) return;
            NavigationRouteSegment action = ActiveSegment;
            bool changed = route != null && !SameGoal(route.GoalRegion, goal);
            if (!changed && route != null && routeIndex < route.Count)
            {
                if (action == null || routeIndex + 1 < route.Count) return;
                Vector2 endpointCenter = route.ResolvedGoal + (Vector2)body.center - anchor;
                if (route.SearchComplete && goal.IsComplete(endpointCenter, body.size)) return;
            }
            Vector2 start = action != null && (!changed || IsIrreversible(action)) ? action.End : anchor;
            NavigationRouteSegment predecessor = action != null && start == action.End ? action : null;
            var purpose = predecessor == null ? NavigationPlanningPurpose.InitialRoute : NavigationPlanningPurpose.EndpointContinuation;
            CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(ExecutionCancellation);
            try
            {
                if (!TryRequestRoute(start, goal, purpose, cancellation.Token, out NavigationPlanningOperation operation))
                { cancellation.Dispose(); return; }
                if (operation == null) throw new InvalidOperationException("Route request returned no operation.");
                request = new NavigationPlanningRequest(operation, start, goal, purpose, predecessor, cancellation);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (purpose == NavigationPlanningPurpose.InitialRoute) MovementReplanDiagnostics.RecordInitialPlan();
#endif
            }
            catch
            {
                cancellation.Cancel();
                cancellation.Dispose();
                throw;
            }
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
        private bool AllowRetry()
        {
            return ++retries <= MaximumNoProgressAttempts;
        }
        private void CancelRequest()
        {
            NavigationPlanningRequest previous = request;
            request = null;
            previous?.Release(true);
        }
    }
}
