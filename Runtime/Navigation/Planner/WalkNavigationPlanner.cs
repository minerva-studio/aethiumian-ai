using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Plans bounded walking traversal with ordinary ground, jump, fall, and drop-through successors.</summary>
    public sealed class WalkNavigationPlanner : NavigationPlanner<WalkNavigationParameters>
    {
        /// <inheritdoc />
        public override NavigationActions SupportedActions => NavigationActions.GroundMove | NavigationActions.Jump | NavigationActions.Fall | NavigationActions.DropThrough;

        private readonly GroundJumpSolver jumpSolver;

        /// <summary>Creates a planner that shares one immutable world's jump solver.</summary>
        public WalkNavigationPlanner(INavigationWorld world, int maxExpandedNodes, GroundJumpSolver jumpSolver) : base(world, maxExpandedNodes)
        {
            this.jumpSolver = jumpSolver;
        }

        /// <summary>
        /// Reconnects an uncommitted Ground route to an observed grounded body.
        /// The first Ground segment may be trimmed when the body has already entered it,
        /// or replaced with a short validated connection when the body is still within
        /// the preceding executor completion range. Every replacement rechecks support
        /// continuity and body clearance; non-ground segments are never rewritten.
        /// The supplied route must already be validated against this immutable world by a
        /// planner; this method validates only the newly created connection.
        /// </summary>
        public static bool TryReconnectGroundRoute(INavigationWorld world, NavigationRoute route, AABB observedBody, out NavigationRoute reconnectedRoute)
        {
            float executionHorizontalCompletionTolerance = Mathf.Max(NavigationConstant.ArrivalFloor, NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon);
            return TryReconnectGroundRoute(world, route, observedBody, executionHorizontalCompletionTolerance, out reconnectedRoute);
        }

        /// <summary>
        /// Reconnects a Ground route using the executor's explicit horizontal completion range.
        /// This overload is the preferred entry for runtime consumers that own the executor
        /// speed and fixed-step values.
        /// </summary>
        public static bool TryReconnectGroundRoute(INavigationWorld world, NavigationRoute route, AABB observedBody, float executionHorizontalCompletionTolerance, out NavigationRoute reconnectedRoute)
        {
            reconnectedRoute = default;
            float supportSnapDistance = NavigationWorldQueries.SupportSnapDistance;
            float groundContactTolerance = GroundTraversalEndpointPolicy.VerticalSupportTolerance;
            if (world == null || !route.HasValue || route.Count == 0
                || !NavigationNumeric.IsFinite(observedBody.Min) || !NavigationNumeric.IsFinite(observedBody.Max)
                || observedBody.SizeX <= 0f || observedBody.SizeY <= 0f
                || !NavigationNumeric.IsFinite(supportSnapDistance) || supportSnapDistance < 0f
                || !NavigationNumeric.IsFinite(groundContactTolerance) || groundContactTolerance < 0f
                || !NavigationNumeric.IsFinite(executionHorizontalCompletionTolerance)
                || executionHorizontalCompletionTolerance < 0f
                || route[0] is not GroundRouteSegment)
                return false;
            Vector2 bodySize = observedBody.Size;
            if (!world.TryResolveGroundSupport(observedBody, supportSnapDistance, out Vector2 snappedStart, out _))
                return false;

            int segmentIndex = 0;
            while (segmentIndex < route.Count && route[segmentIndex] is GroundRouteSegment ground)
            {
                // Consume a validated straight run as one physical action. Never merge across
                // a turn, level change, or an irreversible traversal boundary.
                Vector2 end = ground.End;
                float direction = Mathf.Sign(end.x - ground.Start.x);
                while (Mathf.Abs(end.y - ground.Start.y) <= NavigationWorldQueries.GeometryEpsilon
                    && segmentIndex + 1 < route.Count
                    && route[segmentIndex + 1] is GroundRouteSegment followingGround
                    && direction * (followingGround.End.x - followingGround.Start.x) > 0f
                    && Mathf.Abs(followingGround.End.y - ground.Start.y) <= NavigationWorldQueries.GeometryEpsilon)
                {
                    end = followingGround.End;
                    segmentIndex++;
                }
                Vector2 delta = end - ground.Start;
                float length = delta.magnitude;
                if (length <= NavigationWorldQueries.GeometryEpsilon)
                {
                    if (!IsWithinDistance(snappedStart, end, executionHorizontalCompletionTolerance))
                        return false;
                    segmentIndex++;
                    continue;
                }

                float lateralDistance = Mathf.Abs(delta.x * (snappedStart.y - ground.Start.y) - delta.y * (snappedStart.x - ground.Start.x)) / length;
                if (lateralDistance > supportSnapDistance || Mathf.Abs(snappedStart.y - ground.Start.y) > groundContactTolerance)
                    return false;

                float projectedDistance = Vector2.Dot(snappedStart - ground.Start, delta) / length;
                if (projectedDistance <= length + NavigationWorldQueries.GeometryEpsilon)
                {
                    // The immutable world already proved the original segment. Only the
                    // bridge before its start is new geometry; do not rescan the entire tail.
                    Vector2 connectionEnd = projectedDistance < 0f ? ground.Start : snappedStart;
                    if (!TryValidateGroundConnection(world, AABB.FromLowerCenter(snappedStart, bodySize),
                        AABB.FromLowerCenter(connectionEnd, bodySize)))
                        return false;
                    return BuildReconnectedGroundRoute(route, segmentIndex, snappedStart, end,
                        out reconnectedRoute);
                }

                if (projectedDistance - length > executionHorizontalCompletionTolerance)
                    return false;
                segmentIndex++;
            }

            if (segmentIndex >= route.Count) return false;
            NavigationRouteSegment next = route[segmentIndex];
            if (next is GroundRouteSegment nextGround)
            {
                if (!TryValidateGroundConnection(world, AABB.FromLowerCenter(snappedStart, bodySize), AABB.FromLowerCenter(nextGround.End, bodySize)))
                    return false;
                return BuildReconnectedGroundRoute(route, segmentIndex, snappedStart, nextGround.End, out reconnectedRoute);
            }

            if (!IsWithinDistance(snappedStart, next.Start, executionHorizontalCompletionTolerance))
                return false;
            return BuildRouteSuffix(route, segmentIndex, out reconnectedRoute);
        }

        private static bool BuildReconnectedGroundRoute(NavigationRoute route, int segmentIndex, Vector2 start, Vector2 end, out NavigationRoute reconnectedRoute)
        {
            List<NavigationRouteSegment> segments = new() { new GroundRouteSegment(start, end) };
            for (int index = segmentIndex + 1; index < route.Count; index++)
                segments.Add(route[index]);
            reconnectedRoute = route.ReachesGoal
                ? NavigationRoute.Complete(route.Goal, segments)
                : NavigationRoute.Partial(route.Goal, segments);
            return true;
        }

        private static bool BuildRouteSuffix(NavigationRoute route, int segmentIndex, out NavigationRoute reconnectedRoute)
        {
            reconnectedRoute = route.Slice(segmentIndex);
            return true;
        }

        private static bool IsWithinDistance(Vector2 first, Vector2 second, float distance)
            => (first - second).sqrMagnitude <= distance * distance + NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon;

        /// <summary>Builds a Ground Walk goal from a physical lower-center target and plans against it.</summary>
        public bool TryPlan(AABB body, Vector2 physicalGoal, float arrivalErrorBound, WalkNavigationParameters parameters, out NavigationRoute route)
        {
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(physicalGoal), arrivalErrorBound);
            return TryPlan(body, goal, parameters, out route);
        }

        /// <summary>Runs the shared action-graph search for a Walk request.</summary>
        public override NavigationPlanResult Plan(AABB body, NavigationGoalRequest goal, WalkNavigationParameters parameters, CancellationToken cancellationToken = default, NavigationPlanningDiagnostics diagnostics = null)
        {
            ValidatePlanInputs(body, cancellationToken);
            ValidateParameters(parameters);
            Vector2 bodySize = body.Size;

            NavigationPlanResult result;
            if (!World.TryResolveGroundSupport(AABB.FromLowerCenter(body.LowerCenter, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 resolvedStart, out NavigationSupport startSupport))
            {
                return NavigationPlanResult.NoResult;
            }
            else
            {
                AABB resolvedStartBody = AABB.FromLowerCenter(resolvedStart, bodySize);
                if (World.IsGoalComplete(goal, resolvedStartBody))
                {
                    NavigationRoute route = NavigationRoute.Empty(resolvedStart, goal, NavigationRouteCoordinateFrame.GroundAnchor, true);
                    result = NavigationPlanResult.ResultProduced(route);
                }
                else if (TryCreateDirectGroundRoute(World, resolvedStart, goal, bodySize, out NavigationRoute directRoute))
                {
                    result = NavigationPlanResult.ResultProduced(directRoute);
                }
                else
                {
                    SearchRequest request = new(this, resolvedStart, startSupport, goal, parameters, bodySize, diagnostics);
                    result = RunSearch(request, diagnostics, cancellationToken);
                }
            }

            if (!result.Route.HasValue)
            {
                return result;
            }
            else
            {
                return result.WithRoute(jumpSolver.PrepareRouteForExecution(result.Route, parameters.GetJumpParameters(bodySize), cancellationToken));
            }
        }

        /// <summary>Runs one Simple Walk action without entering Smart search.</summary>
        public override NavigationPlanResult PlanSingleStep(AABB body, NavigationGoalRequest goal, WalkNavigationParameters parameters, CancellationToken cancellationToken = default)
        {
            ValidatePlanInputs(body, cancellationToken);
            ValidateParameters(parameters);
            Vector2 bodySize = body.Size;
            bool hasRoute = TryPlanSingleStep(body, goal, parameters, bodySize, cancellationToken, out NavigationRoute route);
            cancellationToken.ThrowIfCancellationRequested();
            if (!hasRoute)
                return NavigationPlanResult.NoResult;
            NavigationRoute preparedRoute = jumpSolver.PrepareRouteForExecution(route, parameters.GetJumpParameters(bodySize), cancellationToken);
            return NavigationPlanResult.ResultProduced(preparedRoute);
        }

        /// <summary>
        /// Builds the Smart Walk fast path only when the current ground can directly enter
        /// the goal. Unlike Simple Walk, this does not enumerate jump, fall, or local actions.
        /// </summary>
        private bool TryCreateDirectGroundRoute(INavigationWorld world, Vector2 start, NavigationGoalRequest goal, Vector2 bodySize, out NavigationRoute route)
        {
            route = default;
            if (!goal.IsGroundWalk) return false;
            Vector2 end = new(goal.TargetBounds.CenterX, start.y);
            if (!world.TryResolveGroundSupport(AABB.FromLowerCenter(end, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedEnd, out _)) return false;
            end = snappedEnd;
            if (!world.IsGoalComplete(goal, AABB.FromLowerCenter(end, bodySize)) || !TryValidateGroundConnection(world, AABB.FromLowerCenter(start, bodySize), AABB.FromLowerCenter(end, bodySize)))
                return false;

            route = NavigationRoute.CreateSingleSegment(goal, new GroundRouteSegment(start, end), true);
            return true;
        }

        private sealed class SearchRequest : NavigationSearchRequest
        {
            private readonly WalkNavigationPlanner planner;
            private readonly WalkNavigationParameters parameters;
            private readonly Vector2 bodySize;
            private readonly NavigationPlanningDiagnostics diagnostics;

            public SearchRequest(
                WalkNavigationPlanner planner,
                Vector2 start,
                NavigationSupport startSupport,
                NavigationGoalRequest goal,
                WalkNavigationParameters parameters,
                Vector2 bodySize,
                NavigationPlanningDiagnostics diagnostics)
                : base(start, startSupport, goal, planner.MaxExpandedNodes, NavigationNodeIdentity.Ground(-1))
            {
                this.planner = planner;
                this.parameters = parameters;
                this.bodySize = bodySize;
                this.diagnostics = diagnostics;
            }

            public override IEnumerable<NavigationTransition?> EnumerateTransitions(NavigationSearchNode node)
                => planner.EnumerateSharedTransitions(node, parameters, bodySize, Goal, diagnostics);

            public override float EvaluateHeuristic(Vector2 position)
                => Goal.IsGroundWalk ? Goal.DistanceToLowerCenterGoal(position, bodySize.x) : 0f;
        }

        private IEnumerable<NavigationTransition?> EnumerateSharedTransitions(NavigationSearchNode node, WalkNavigationParameters parameters, Vector2 bodySize, NavigationGoalRequest goal, NavigationPlanningDiagnostics diagnostics)
        {
            // Allocation risk in this shared iterator is still unmeasured. Preserve WorkUnit/Edge order because Smart search uses it for budget and cancellation boundaries.
            foreach (Successor successor in EnumerateLocalSuccessors(node.Position, node.Identity.CandidateId, node.Support, parameters, bodySize, goal))
            {
                yield return null;
                if (successor.Step == null) continue;
                yield return successor.ToTransition();
            }

            GroundJumpParameters jumpParameters = parameters.GetJumpParameters(bodySize);
            foreach (GroundJumpSuccessor? work in GroundJumpSuccessorEnumerator.Enumerate(jumpSolver, node.Position, node.Support, goal, jumpParameters, diagnostics, true, node.Identity.CandidateId))
            {
                yield return null;
                if (!work.HasValue) continue;
                GroundJumpSuccessor jump = work.Value;
                yield return NavigationTransition.JumpLanding(
                    NavigationNodeIdentity.Ground(jump.LandingCandidateId), jump.Trajectory.LandingPosition,
                    jump.LandingSupport, jump.CreateSegment(),
                    Vector2.Distance(node.Position, jump.Trajectory.LandingPosition)
                        + jump.Trajectory.FlightDuration + 0.5f,
                    World.IsGoalComplete(goal, AABB.FromLowerCenter(jump.Trajectory.LandingPosition, bodySize)));
            }
        }

        /// <summary>Expands the current supported position once and returns its best valid action.</summary>
        private bool TryPlanSingleStep(AABB body, NavigationGoalRequest goal, WalkNavigationParameters parameters, Vector2 bodySize, CancellationToken cancellationToken, out NavigationRoute route)
        {
            route = default;
            if (!World.TryResolveGroundSupport(AABB.FromLowerCenter(body.LowerCenter, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 resolvedStart, out NavigationSupport startSupport))
                return false;

            AABB startBody = AABB.FromLowerCenter(resolvedStart, bodySize);
            float startDistance = World.GetGoalCompletionDistance(goal, startBody);
            float startGuidanceDistance = goal.GuidanceDistance(startBody);
            if (World.IsGoalComplete(goal, startBody))
            {
                route = NavigationRoute.Empty(resolvedStart, goal, NavigationRouteCoordinateFrame.GroundAnchor, true);
                return true;
            }

            Successor? best = null;
            foreach (Successor local in EnumerateLocalSuccessors(resolvedStart, -1, startSupport, parameters, bodySize, goal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (local.Step == null) continue;

                Successor candidate = local;
                if (local.Step is GroundRouteSegment)
                {
                    // Preserve initial eligibility before extending this one Ground action.
                    if (!local.CompletesGoal && !HasStrictSingleStepProgress(local, startDistance, startGuidanceDistance))
                        continue;
                    candidate = ExtendSimpleGroundMove(World, resolvedStart, local, goal, bodySize, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                ConsiderSingleStepCandidate(ref best, candidate, startDistance, startGuidanceDistance);
            }

            GroundJumpParameters jumpParameters = parameters.GetJumpParameters(bodySize);
            foreach (GroundJumpSuccessor? work in GroundJumpSuccessorEnumerator.Enumerate(jumpSolver, resolvedStart, startSupport, goal, jumpParameters, null, true, -1))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!work.HasValue) continue;
                GroundJumpSuccessor jump = work.Value;
                Successor candidate = CreateSuccessor(new NavigationSupportCandidate(jump.LandingCandidateId, jump.LandingSupport),
                    jump.CreateSegment(), Vector2.Distance(resolvedStart, jump.Trajectory.LandingPosition)
                        + jump.Trajectory.FlightDuration + 0.5f, goal, bodySize);
                ConsiderSingleStepCandidate(ref best, candidate, startDistance, startGuidanceDistance);
            }

            if (!best.HasValue) return false;
            route = BuildSingleStepPlan(goal, best.Value);
            return true;
        }

        private void ConsiderSingleStepCandidate(ref Successor? best, Successor candidate, float startCompletionDistance, float startGuidanceDistance)
        {
            if (!candidate.CompletesGoal && !HasStrictSingleStepProgress(candidate, startCompletionDistance, startGuidanceDistance))
                return;
            if (!best.HasValue || IsBetterSingleStep(candidate, best.Value)) best = candidate;
        }

        /// <summary>Extends a validated Ground action and returns its fully scored final successor.</summary>
        private Successor ExtendSimpleGroundMove(INavigationWorld world, Vector2 start, Successor current,
            NavigationGoalRequest goal, Vector2 bodySize, CancellationToken cancellationToken)
        {
            Vector2 endpoint = current.Position;
            var score = EvaluateEndpoint(start, endpoint, true, goal, bodySize);
            float cost = Mathf.Abs(endpoint.x - start.x);
            float direction = Mathf.Sign(endpoint.x - start.x);
            float spacing = NavigationConstant.MaximumTraversalSampleSpacing;
            while (!score.CompletesGoal && direction * (goal.TargetBounds.CenterX - endpoint.x) > Tolerance)
            {
                Vector2 next = new(Mathf.MoveTowards(endpoint.x, goal.TargetBounds.CenterX, spacing), endpoint.y);
                (Vector2 Start, Vector2 End)? validated = null;
                foreach (var step in EnumerateGroundMove(world, endpoint, next, bodySize))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (step.HasValue) validated = step;
                }

                if (!validated.HasValue || Mathf.Abs(validated.Value.End.y - start.y) > GroundTraversalEndpointPolicy.VerticalSupportTolerance)
                    break;
                Vector2 candidateEnd = validated.Value.End;
                var candidateScore = EvaluateEndpoint(start, candidateEnd, true, goal, bodySize);
                if (!candidateScore.CompletesGoal
                    && !HasStrictSingleStepProgress(candidateScore.CompletionDistance, candidateScore.GuidanceDistance,
                        score.CompletionDistance, score.GuidanceDistance))
                    break;
                endpoint = candidateEnd;
                score = candidateScore;
                cost = Mathf.Abs(endpoint.x - start.x);
            }

            return new Successor(-1, default, endpoint, new GroundRouteSegment(start, endpoint), cost,
                score.CompletionDistance, score.GuidanceDistance, score.CompletesGoal);
        }

        /// <summary>Enumerates nearby real support anchors without collapsing surfaces to cells.</summary>
        private IEnumerable<Successor> EnumerateLocalSuccessors(Vector2 start, int currentCandidateId, NavigationSupport currentSupport, WalkNavigationParameters parameters, Vector2 bodySize, NavigationGoalRequest goal)
        {
            // Allocation risk in this shared successor iterator is still unmeasured. Keep its WorkUnit yields and candidate order aligned with Smart budget and cancellation handling.
            Vector2 snappedStart = start;
            AABB anchors = AABB.FromMinAndSize(
                new Vector2(snappedStart.x - NavigationConstant.GroundHopReach, World.WorldBounds.MinY),
                new Vector2(NavigationConstant.GroundHopReach * 2f,
                    snappedStart.y - World.WorldBounds.MinY + NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon));
            IReadOnlyList<NavigationSupportCandidate> candidates = World.GetSupportCandidates(anchors, bodySize);
            for (int index = 0; index < candidates.Count; index++)
            {
                NavigationSupportCandidate candidate = candidates[index];
                if (candidate.Id == currentCandidateId) continue;
                NavigationRouteSegment step = null;
                foreach (var groundStep in EnumerateGroundMove(World, snappedStart, candidate.Support.Position, bodySize))
                {
                    yield return default;
                    if (groundStep.HasValue)
                    {
                        step = new GroundRouteSegment(groundStep.Value.Start, groundStep.Value.End);
                        break;
                    }
                }
                if (step != null && parameters.Speed > Tolerance)
                {
                    yield return CreateSuccessor(candidate, step, Vector2.Distance(snappedStart, candidate.Support.Position),
                        goal, bodySize);
                    continue;
                }
                if (candidate.Support.Position.y >= snappedStart.y - Tolerance) continue;
                foreach (NavigationRouteSegment fallStep in EnumerateFall(World, snappedStart, candidate.Support.Position, bodySize))
                {
                    yield return default;
                    if (fallStep != null)
                    {
                        yield return CreateSuccessor(candidate, fallStep,
                            Vector2.Distance(snappedStart, candidate.Support.Position) + 1f, goal, bodySize);
                        break;
                    }
                }
                if (currentSupport.Kind != NavigationSurfaceKind.OneWay) continue;
                foreach (NavigationRouteSegment dropStep in EnumerateDropThrough(World, snappedStart, candidate.Support.Position, bodySize))
                {
                    yield return default;
                    if (dropStep != null)
                    {
                        yield return CreateSuccessor(candidate, dropStep,
                            Vector2.Distance(snappedStart, candidate.Support.Position) + 0.25f, goal, bodySize);
                        break;
                    }
                }
            }
        }

        /// <summary>Enumerates horizontal ground validation samples.</summary>
        private static IEnumerable<(Vector2 Start, Vector2 End)?> EnumerateGroundMove(INavigationWorld world, Vector2 start, Vector2 end, Vector2 bodySize)
        {
            if (!world.TryResolveGroundSupport(AABB.FromLowerCenter(start, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedStart, out _)
                || !world.TryResolveGroundSupport(AABB.FromLowerCenter(end, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedEnd, out _)
                || Mathf.Abs(snappedStart.y - snappedEnd.y) > GroundTraversalEndpointPolicy.VerticalSupportTolerance)
            {
                yield return null;
                yield break;
            }

            float sampleSpacing = Mathf.Min(NavigationConstant.MaximumSupportSampleSpacing, bodySize.x * 0.25f, bodySize.y * 0.25f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(snappedEnd.x - snappedStart.x) / sampleSpacing));
            Vector2 previous = snappedStart;
            for (int i = 0; i <= samples; i++)
            {
                Vector2 position = Vector2.Lerp(snappedStart, snappedEnd, i / (float)samples);
                bool clear = i == 0 || world.IsBodyPathClear(AABB.FromLowerCenter(previous, bodySize), position - previous, GroundTraversalEndpointPolicy.VerticalSupportTolerance);
                clear &= world.CanStandAt(AABB.FromLowerCenter(position, bodySize), NavigationWorldQueries.SupportSnapDistance, out _);
                previous = position;
                yield return null;
                if (!clear)
                {
                    yield return null;
                    yield break;
                }
            }

            yield return (snappedStart, snappedEnd);
        }

        /// <summary>Validates continuous support and body clearance without imposing a route-length limit.</summary>
        public static bool TryValidateGroundConnection(INavigationWorld world, AABB startBody, AABB endBody)
        {
            Vector2 bodySize = startBody.Size;
            if (!world.TryResolveGroundSupport(startBody, NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedStart, out _)
                || !world.TryResolveGroundSupport(endBody, NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedEnd, out _)
                || Mathf.Abs(snappedStart.y - snappedEnd.y) > GroundTraversalEndpointPolicy.VerticalSupportTolerance)
                return false;

            float horizontalDistance = Mathf.Abs(snappedEnd.x - snappedStart.x);
            float spacing = Mathf.Min(NavigationConstant.MaximumTraversalSampleSpacing, bodySize.x * 0.25f, bodySize.y * 0.25f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(horizontalDistance / spacing));

            Vector2 previous = snappedStart;
            for (int index = 0; index <= samples; index++)
            {
                Vector2 current = Vector2.Lerp(snappedStart, snappedEnd, index / (float)samples);
                bool clear = index == 0 || world.IsBodyPathClear(AABB.FromLowerCenter(previous, bodySize),
                    current - previous, GroundTraversalEndpointPolicy.VerticalSupportTolerance);
                clear &= world.CanStandAt(AABB.FromLowerCenter(current, bodySize), NavigationWorldQueries.SupportSnapDistance, out _);
                if (!clear) return false;
                previous = current;
            }

            return true;
        }

        /// <summary>Enumerates downward one-way validation samples.</summary>
        private static IEnumerable<NavigationRouteSegment> EnumerateDropThrough(INavigationWorld world, Vector2 start, Vector2 end, Vector2 bodySize)
        {
            if (!world.TryResolveGroundSupport(AABB.FromLowerCenter(start, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedStart, out NavigationSupport support)
                || !world.TryResolveGroundSupport(AABB.FromLowerCenter(end, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedEnd, out _)
                || support.Kind != NavigationSurfaceKind.OneWay || snappedEnd.y >= snappedStart.y - Tolerance
                || Mathf.Abs(snappedEnd.x - snappedStart.x) > Tolerance)
            {
                yield return null;
                yield break;
            }

            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(snappedStart, snappedEnd) / NavigationConstant.MaximumVerticalValidationSpacing));
            Vector2 previous = snappedStart;
            for (int i = 1; i <= samples; i++)
            {
                Vector2 current = Vector2.Lerp(snappedStart, snappedEnd, i / (float)samples);
                bool clear = world.IsBodyPathClear(AABB.FromLowerCenter(previous, bodySize), current - previous, GroundTraversalEndpointPolicy.VerticalSupportTolerance)
                        && !world.CrossesOneWayDown(AABB.FromLowerCenter(previous, bodySize), current - previous, snappedStart.y);
                previous = current;
                yield return null;
                if (!clear)
                {
                    yield return null;
                    yield break;
                }
            }

            yield return new DropThroughRouteSegment(snappedStart, snappedEnd);
        }

        /// <summary>Enumerates horizontal exit and downward fall samples.</summary>
        private static IEnumerable<NavigationRouteSegment> EnumerateFall(INavigationWorld world, Vector2 start, Vector2 end, Vector2 bodySize)
        {
            if (!world.TryResolveGroundSupport(AABB.FromLowerCenter(start, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedStart, out _)
                || !world.TryResolveGroundSupport(AABB.FromLowerCenter(end, bodySize), NavigationWorldQueries.SupportSnapDistance, out Vector2 snappedEnd, out _))
            {
                yield return null;
                yield break;
            }

            float horizontalDistance = Mathf.Abs(snappedEnd.x - snappedStart.x);
            if (snappedEnd.y >= snappedStart.y - Tolerance || horizontalDistance < 0.5f || horizontalDistance > 1.5f)
            {
                yield return null;
                yield break;
            }

            Vector2 ledgeExit = new(snappedEnd.x, snappedStart.y);
            int horizontalSamples = Mathf.Max(1, Mathf.CeilToInt(horizontalDistance / NavigationConstant.MaximumVerticalValidationSpacing));
            Vector2 previous = snappedStart;
            for (int i = 1; i <= horizontalSamples; i++)
            {
                Vector2 current = Vector2.Lerp(snappedStart, ledgeExit, i / (float)horizontalSamples);
                bool clear = world.IsBodyPathClear(AABB.FromLowerCenter(previous, bodySize),
                    current - previous, GroundTraversalEndpointPolicy.VerticalSupportTolerance);
                previous = current;
                yield return null;
                if (!clear)
                {
                    yield return null;
                    yield break;
                }
            }

            int descentSamples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(ledgeExit, snappedEnd) / NavigationConstant.MaximumVerticalValidationSpacing));
            previous = ledgeExit;
            for (int i = 1; i <= descentSamples; i++)
            {
                Vector2 current = Vector2.Lerp(ledgeExit, snappedEnd, i / (float)descentSamples);
                bool clear = world.IsBodyPathClear(AABB.FromLowerCenter(previous, bodySize),
                    current - previous, GroundTraversalEndpointPolicy.VerticalSupportTolerance)
                    && !world.CrossesOneWayDown(AABB.FromLowerCenter(previous, bodySize), current - previous);
                previous = current;
                yield return null;
                if (!clear)
                {
                    yield return null;
                    yield break;
                }
            }

            yield return new FallRouteSegment(snappedStart, ledgeExit, snappedEnd);
        }

        private Successor CreateSuccessor(NavigationSupportCandidate candidate, NavigationRouteSegment step, float cost, NavigationGoalRequest goal, Vector2 bodySize)
            => CreateSuccessor(candidate.Id, candidate.Support, step, cost, goal, bodySize);

        private Successor CreateSuccessor(int candidateId, NavigationSupport support, NavigationRouteSegment step, float cost, NavigationGoalRequest goal, Vector2 bodySize)
        {
            var score = EvaluateEndpoint(step.Start, step.End, step is GroundRouteSegment, goal, bodySize);
            return new Successor(candidateId, support, step.End, step, cost, score.CompletionDistance,
                score.GuidanceDistance, score.CompletesGoal);
        }

        private (float CompletionDistance, float GuidanceDistance, bool CompletesGoal) EvaluateEndpoint(
            Vector2 start, Vector2 end, bool ground, NavigationGoalRequest goal, Vector2 bodySize)
        {
            AABB startBody = AABB.FromLowerCenter(start, bodySize);
            AABB endBody = AABB.FromLowerCenter(end, bodySize);
            float endpointDistance = World.GetGoalCompletionDistance(goal, endBody);
            bool completesGoal = World.IsGoalComplete(goal, endBody)
                || ground && World.IsGoalCompleteAlong(goal, startBody, endBody);
            return (endpointDistance, goal.GuidanceDistance(endBody), completesGoal);
        }

        /// <summary>Accepts a Simple Walk candidate only when completion or plateau guidance strictly improves.</summary>
        private static bool HasStrictSingleStepProgress(Successor candidate, float startCompletionDistance, float startGuidanceDistance)
            => HasStrictSingleStepProgress(candidate.CompletionDistance, candidate.GuidanceDistance,
                startCompletionDistance, startGuidanceDistance);

        private static bool HasStrictSingleStepProgress(float completionDistance, float guidanceDistance,
            float startCompletionDistance, float startGuidanceDistance)
            => IsStrictlyLess(completionDistance, startCompletionDistance)
                || (IsEquivalent(completionDistance, startCompletionDistance) && IsStrictlyLess(guidanceDistance, startGuidanceDistance));

        /// <summary>Builds a plan containing exactly one validated local traversal step.</summary>
        private NavigationRoute BuildSingleStepPlan(NavigationGoalRequest goal, Successor successor)
            => NavigationRoute.CreateSingleSegment(goal, successor.Step, true);

        /// <summary>Compares Simple Walk actions under the local completion and progress contract.</summary>
        private static bool IsBetterSingleStep(Successor candidate, Successor best)
        {
            if (candidate.CompletesGoal != best.CompletesGoal) return candidate.CompletesGoal;
            if (!candidate.CompletesGoal)
            {
                // Partial actions currently prioritize progress over cost.
                // Revisit cost plus remaining distance if this favors expensive jumps
                // for marginal progress; completion and guidance are not interchangeable.
                if (IsStrictlyLess(candidate.CompletionDistance, best.CompletionDistance)) return true;
                if (!IsEquivalent(candidate.CompletionDistance, best.CompletionDistance)) return false;
                if (IsStrictlyLess(candidate.GuidanceDistance, best.GuidanceDistance)) return true;
                if (!IsEquivalent(candidate.GuidanceDistance, best.GuidanceDistance)) return false;
            }

            if (IsStrictlyLess(candidate.Cost, best.Cost)) return true;
            if (!IsEquivalent(candidate.Cost, best.Cost)) return false;
            if (IsStrictlyLess(candidate.Position.x, best.Position.x)) return true;
            if (!IsEquivalent(candidate.Position.x, best.Position.x)) return false;
            return IsStrictlyLess(candidate.Position.y, best.Position.y);
        }

        private static void ValidateParameters(WalkNavigationParameters profile)
        {
            Validate.NonNegativeFinite(profile.Speed, nameof(profile));
            Validate.NonNegativeFinite(profile.GravityScale, nameof(profile));
            Validate.NonNegativeFinite(profile.LinearDamping, nameof(profile));
            Validate.NonNegativeFinite(profile.JumpHeight, nameof(profile));
            Validate.NonNegativeFinite(profile.JumpLength, nameof(profile));
            Validate.PositiveFinite(profile.SimulationTimeStep, nameof(profile));
            Validate.Finite(profile.Gravity, nameof(profile));
        }

        private readonly struct Successor
        {
            public readonly int CandidateId;
            public readonly NavigationSupport Support;
            public readonly Vector2 Position;
            public readonly NavigationRouteSegment Step;
            public readonly float Cost;
            public readonly float CompletionDistance;
            public readonly float GuidanceDistance;
            public readonly bool CompletesGoal;

            public Successor(int candidateId, NavigationSupport support, Vector2 position, NavigationRouteSegment step, float cost, float completionDistance,
                float guidanceDistance, bool completesGoal)
            {
                CandidateId = candidateId;
                Support = support;
                Position = position;
                Step = step;
                Cost = cost;
                CompletionDistance = completionDistance;
                GuidanceDistance = guidanceDistance;
                CompletesGoal = completesGoal;
            }

            public NavigationTransition ToTransition()
                => NavigationTransition.GroundSuccessor(CandidateId, Position, Support, Step, Cost,
                    CompletesGoal);
        }
    }
}
