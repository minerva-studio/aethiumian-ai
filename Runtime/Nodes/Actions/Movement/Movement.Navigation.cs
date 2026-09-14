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

        private void ReceiveRoute(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (request == null) return;
            if (!CompatibleGoal(request.GoalRegion, goal)) { CancelRequest(); return; }
            if (!request.Operation.IsCompleted) return;
            // A completed request remains its own candidate/terminal receipt. Release its
            // cancellation resource now, but retain the immutable result until hand-off.
            request.Release(false);
            if (request.Operation.IsCancelled) { CancelRequest(); return; }
            if (request.Operation.Exception != null) throw request.Operation.Exception;

            NavigationRouteSegment active = ActiveSegment;
            bool followsActive = active != null && ReferenceEquals(request.CommittedSegment, active);
            if (active != null && request.CommittedSegment != null && !followsActive)
            { RejectRoute(goal); return; }
            NavigationPlanResult result = request.Operation.PlanResult;
            NavigationRoute candidate = result.Route;
            if (candidate == null || candidate.Count == 0)
            {
                // A negative receipt describes its exact target, not a nearby or newer one.
                if (!request.GoalRegion.GoalKey.Equals(goal.GoalKey)) { RejectRoute(goal); return; }
                if (followsActive) return;
                if (active != null) { RejectRoute(goal); return; }
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

            if (IsIrreversible(active))
            {
                if (!followsActive || !RouteAllowed(goal, request.Start, candidate))
                    RejectRoute(goal);
                return;
            }
            if (!TryConnectRoute(candidate, body, out NavigationRoute connected)
                || connected == null || connected.Count == 0)
            {
                // Only a still-useful continuation of this exact action may wait for contact.
                if (followsActive && RouteAllowed(goal, request.Start, candidate)) return;
                RejectRoute(goal);
                return;
            }
            if (!RouteAllowed(goal, anchor, connected)) { RejectRoute(goal); return; }
            if (active != null)
            {
                ActionPreparation preparation = PrepareRoute(connected, body);
                if (preparation == ActionPreparation.Waiting) return;
                if (preparation == ActionPreparation.Unavailable) { RejectRoute(goal); return; }
            }
            else
            {
                route = connected;
                routeIndex = 0;
            }
            previousCenter = null;
            CancelRequest();
        }

        private void RejectRoute(NavigationGoalRegion goal)
        {
            CancelRequest();
            if (ActiveSegment == null && !AllowRetry()) EndMovement(false, goal);
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
            ActionPreparation preparation = PrepareRoute(connected, body);
            if (preparation == ActionPreparation.Waiting) return false;
            if (preparation == ActionPreparation.Unavailable)
            {
                route = null;
                if (IsGoalSatisfied(goal, body, false)) EndMovement(true, goal);
                else if (!AllowRetry()) EndMovement(false, goal);
                return false;
            }
            return true;
        }

        // Only Ready may mutate the executor. Publish its route after successful preparation
        // so a deferred or rejected replacement never describes the old execution.
        private ActionPreparation PrepareRoute(NavigationRoute connected, Bounds body)
        {
            ActionPreparation result = PrepareExecutor(connected.Segments[0], body, executor, out MovementExecutor prepared);
            if (result != ActionPreparation.Ready) return result;
            if (prepared == null || !prepared.IsExecuting)
                throw new InvalidOperationException("Ready acquisition must supply an executing executor.");
            if (!ReferenceEquals(executor, prepared)) executor?.Dispose();
            executor = prepared;
            route = connected;
            routeIndex = 0;
            return ActionPreparation.Ready;
        }

        private void RequestNextRoute(NavigationGoalRegion goal, Vector2 anchor, Bounds body)
        {
            if (request != null || IsComplete) return;
            NavigationRouteSegment action = ActiveSegment;
            bool changed = route != null && !SamePlanningTarget(route.GoalRegion, goal);
            if (!changed && route != null && routeIndex < route.Count)
            {
                if (action == null || routeIndex + 1 < route.Count) return;
                Vector2 endpointCenter = route.ResolvedGoal + (Vector2)body.center - anchor;
                if (route.SearchComplete && goal.IsComplete(endpointCenter, body.size)) return;
            }

            NavigationRouteSegment predecessor = null;
            if (action != null && (!changed || IsIrreversible(action)))
            {
                predecessor = action;
            }
            else if (action == null
                && !changed
                && route != null
                && !route.SearchComplete
                && route.Count > 0
                && routeIndex == route.Count
                && route.Segments[route.Count - 1] is GroundRouteSegment completedGround)
            {
                // Completion may consume a short Ground segment without physical movement.
                // Continue from its endpoint; adoption still reconnects from the real body.
                predecessor = completedGround;
            }
            Vector2 start = predecessor != null ? predecessor.End : anchor;
            var purpose = predecessor != null
                ? NavigationPlanningPurpose.EndpointContinuation
                : NavigationPlanningPurpose.InitialRoute;
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
