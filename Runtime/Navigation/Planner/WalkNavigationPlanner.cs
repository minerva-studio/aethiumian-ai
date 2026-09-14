using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Plans bounded walking traversal with ordinary ground, jump, fall, and drop-through successors.</summary>
    public sealed class WalkNavigationPlanner : NavigationPlanner<WalkNavigationParameters>
    {
        private readonly GroundJumpSolver jumpSolver;

        /// <summary>Creates a planner that shares one immutable world's jump solver.</summary>
        public WalkNavigationPlanner(INavigationWorld world, int maxExpandedNodes, GroundJumpSolver jumpSolver) : base(world, maxExpandedNodes)
        {
            this.jumpSolver = jumpSolver;
        }

        /// <summary>
        /// Reconnects an uncommitted Ground route to an observed grounded anchor.
        /// The first Ground segment may be trimmed when the body has already entered it,
        /// or replaced with a short validated connection when the body is still within
        /// the preceding executor completion range. Every replacement rechecks support
        /// continuity and body clearance; non-ground segments are never rewritten.
        /// The supplied route must already be validated against this immutable world by a
        /// planner; this method validates only the newly created connection.
        /// </summary>
        public static bool TryReconnectGroundRoute(INavigationWorld world, NavigationRoute route,
            Vector2 observedStart, Vector2 bodySize, float supportSnapDistance,
            float groundContactTolerance, out NavigationRoute reconnectedRoute)
        {
            return TryReconnectGroundRoute(world, route, observedStart, bodySize,
                supportSnapDistance, groundContactTolerance,
                Mathf.Max(0.2f, supportSnapDistance + NavigationWorldQueries.GeometryEpsilon),
                out reconnectedRoute);
        }

        /// <summary>
        /// Reconnects a Ground route using the executor's explicit horizontal completion range.
        /// This overload is the preferred entry for runtime consumers that own the executor
        /// speed and fixed-step values.
        /// </summary>
        public static bool TryReconnectGroundRoute(INavigationWorld world, NavigationRoute route,
            Vector2 observedStart, Vector2 bodySize, float supportSnapDistance,
            float groundContactTolerance, float executionHorizontalCompletionTolerance,
            out NavigationRoute reconnectedRoute)
        {
            reconnectedRoute = null;
            if (world == null || route == null || route.Count == 0 || !NavigationNumeric.IsFinite(observedStart)
                || !NavigationNumeric.IsFinite(bodySize) || bodySize.x <= 0f || bodySize.y <= 0f
                || !NavigationNumeric.IsFinite(supportSnapDistance) || supportSnapDistance < 0f
                || !NavigationNumeric.IsFinite(groundContactTolerance) || groundContactTolerance < 0f
                || !NavigationNumeric.IsFinite(executionHorizontalCompletionTolerance)
                || executionHorizontalCompletionTolerance < 0f
                || route.Segments[0] is not GroundRouteSegment)
                return false;
            if (!world.TryResolveGroundSupport(observedStart, bodySize, supportSnapDistance,
                out Vector2 snappedStart, out _))
                return false;

            int segmentIndex = 0;
            while (segmentIndex < route.Count && route.Segments[segmentIndex] is GroundRouteSegment ground)
            {
                // Consume a validated straight run as one physical action. Never merge across
                // a turn, level change, or an irreversible traversal boundary.
                Vector2 end = ground.End;
                float direction = Mathf.Sign(end.x - ground.Start.x);
                while (Mathf.Abs(end.y - ground.Start.y) <= NavigationWorldQueries.GeometryEpsilon
                    && segmentIndex + 1 < route.Count
                    && route.Segments[segmentIndex + 1] is GroundRouteSegment followingGround
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

                float lateralDistance = Mathf.Abs(delta.x * (snappedStart.y - ground.Start.y)
                    - delta.y * (snappedStart.x - ground.Start.x)) / length;
                if (lateralDistance > supportSnapDistance
                    || Mathf.Abs(snappedStart.y - ground.Start.y) > groundContactTolerance)
                    return false;

                float projectedDistance = Vector2.Dot(snappedStart - ground.Start, delta) / length;
                if (projectedDistance <= length + NavigationWorldQueries.GeometryEpsilon)
                {
                    // The immutable world already proved the original segment. Only the
                    // bridge before its start is new geometry; do not rescan the entire tail.
                    Vector2 connectionEnd = ReferenceEquals(world, route.GoalRegion.Snapshot)
                        ? (projectedDistance < 0f ? ground.Start : snappedStart) : end;
                    if (!TryValidateGroundConnection(world, snappedStart, connectionEnd, bodySize,
                        supportSnapDistance, groundContactTolerance)) return false;
                    return BuildReconnectedGroundRoute(route, segmentIndex, snappedStart, end,
                        out reconnectedRoute);
                }

                if (projectedDistance - length > executionHorizontalCompletionTolerance)
                    return false;
                segmentIndex++;
            }

            if (segmentIndex >= route.Count) return false;
            NavigationRouteSegment next = route.Segments[segmentIndex];
            if (next is GroundRouteSegment nextGround)
            {
                if (!TryValidateGroundConnection(world, snappedStart, nextGround.End, bodySize,
                    supportSnapDistance, groundContactTolerance)) return false;
                return BuildReconnectedGroundRoute(route, segmentIndex, snappedStart, nextGround.End,
                    out reconnectedRoute);
            }

            if (!IsWithinDistance(snappedStart, next.Start, executionHorizontalCompletionTolerance))
                return false;
            return BuildRouteSuffix(route, segmentIndex, out reconnectedRoute);
        }

        private static bool BuildReconnectedGroundRoute(NavigationRoute route, int segmentIndex,
            Vector2 start, Vector2 end, out NavigationRoute reconnectedRoute)
        {
            List<NavigationRouteSegment> segments = new() { new GroundRouteSegment(start, end) };
            for (int index = segmentIndex + 1; index < route.Count; index++)
                segments.Add(route.Segments[index]);
            reconnectedRoute = route.SearchComplete
                ? NavigationRoute.Complete(start, route.GoalRegion, route.ResolvedGoal, segments)
                : NavigationRoute.Partial(start, route.GoalRegion, route.ResolvedGoal, segments);
            return true;
        }

        private static bool BuildRouteSuffix(NavigationRoute route, int segmentIndex,
            out NavigationRoute reconnectedRoute)
        {
            List<NavigationRouteSegment> segments = new(route.Count - segmentIndex);
            for (int index = segmentIndex; index < route.Count; index++)
                segments.Add(route.Segments[index]);
            reconnectedRoute = route.SearchComplete
                ? NavigationRoute.Complete(segments[0].Start, route.GoalRegion, route.ResolvedGoal, segments)
                : NavigationRoute.Partial(segments[0].Start, route.GoalRegion, route.ResolvedGoal, segments);
            return true;
        }

        private static bool IsWithinDistance(Vector2 first, Vector2 second, float distance)
            => (first - second).sqrMagnitude <= distance * distance
                + NavigationWorldQueries.GeometryEpsilon * NavigationWorldQueries.GeometryEpsilon;

        /// <summary>Builds a Ground Walk region from a physical lower-center goal and plans against it.</summary>
        public bool TryPlan(Vector2 start, Vector2 physicalGoal,
            float arrivalErrorBound, WalkNavigationParameters parameters, out NavigationRoute route)
        {
            NavigationGoalRequest request = NavigationGoalRequest.GroundRange(
                new Bounds(physicalGoal, Vector3.zero), arrivalErrorBound);
            NavigationGoalRegion goalRegion = NavigationGoalRegion.Bind(request, World);
            return TryPlan(start, goalRegion, parameters, out route);
        }

        /// <summary>Runs the shared action-graph search for a Walk request.</summary>
        public override NavigationPlanResult Plan(Vector2 start, NavigationGoalRegion goalRegion,
            WalkNavigationParameters parameters, CancellationToken cancellationToken = default,
            NavigationPlanningDiagnostics diagnostics = null, bool allowExecutablePrefix = false)
        {
            ValidatePlanInputs(start, goalRegion, cancellationToken);
            parameters = PrepareParameters(parameters);
            ValidateParameters(parameters);

            NavigationPlanResult result;
            if (!World.TryResolveGroundSupport(start, parameters.BodySize, parameters.SupportSnapDistance,
                out Vector2 resolvedStart, out NavigationSupport startSupport))
                result = NavigationPlanResult.NoResult;
            else
            {
                Vector2 startCenter = resolvedStart + Vector2.up * (parameters.BodySize.y * 0.5f);
                if (goalRegion.IsComplete(startCenter, parameters.BodySize))
                {
                    result = NavigationPlanResult.ResultProduced(NavigationRoute.Complete(
                        resolvedStart, goalRegion, resolvedStart, Array.Empty<NavigationRouteSegment>()));
                }
                else if (TryCreateDirectGroundRoute(World, resolvedStart, goalRegion, parameters,
                    out NavigationRoute directRoute))
                {
                    result = NavigationPlanResult.ResultProduced(directRoute);
                }
                else
                {
                    NavigationSearchRequest request = new(World, resolvedStart, startSupport, goalRegion,
                        parameters.BodySize, NavigationActions.GroundMove | NavigationActions.Jump
                            | NavigationActions.Fall | NavigationActions.DropThrough,
                        MaxExpandedNodes, allowExecutablePrefix, NavigationNodeIdentity.Ground(-1),
                        node => EnumerateSharedTransitions(node, parameters, goalRegion, diagnostics),
                        position => goalRegion.IsGroundWalk
                            ? goalRegion.DistanceToLowerCenterGoal(position, parameters.BodySize.x) : 0f);
                    result = RunSearch(request, diagnostics, cancellationToken);
                }
            }

            return result.Route == null ? result : result.WithRoute(
                PrepareRouteForExecution(result.Route, parameters, cancellationToken));
        }

        /// <summary>Runs one Simple Walk action without entering Smart search.</summary>
        public NavigationPlanResult PlanSingleStep(Vector2 start, NavigationGoalRegion goalRegion,
            WalkNavigationParameters parameters, CancellationToken cancellationToken = default,
            NavigationPlanningDiagnostics diagnostics = null)
        {
            ValidatePlanInputs(start, goalRegion, cancellationToken);
            parameters = PrepareParameters(parameters);
            ValidateParameters(parameters);
            foreach (NavigationRoute route in PlanSingleStepIncremental(start, goalRegion, parameters, diagnostics))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (route != null)
                    return NavigationPlanResult.ResultProduced(
                        PrepareRouteForExecution(route, parameters, cancellationToken));
            }

            return NavigationPlanResult.NoResult;
        }

        /// <summary>
        /// Builds the Smart Walk fast path only when the current ground can directly enter
        /// the goal. Unlike Simple Walk, this does not enumerate jump, fall, or local actions.
        /// </summary>
        private static bool TryCreateDirectGroundRoute(INavigationWorld world, Vector2 start, NavigationGoalRegion goalRegion, WalkNavigationParameters parameters, out NavigationRoute route)
        {
            route = null;
            if (!goalRegion.IsGroundWalk) return false;
            Vector2 end = new(goalRegion.Center.x, start.y);
            if (!world.TryResolveGroundSupport(end, parameters.BodySize, parameters.SupportSnapDistance,
                out Vector2 snappedEnd, out _)) return false;
            end = snappedEnd;
            Vector2 endCenter = end + Vector2.up * (parameters.BodySize.y * 0.5f);
            if (!goalRegion.IsComplete(endCenter, parameters.BodySize)
                || !TryValidateGroundConnection(world, start, end, parameters.BodySize,
                    parameters.SupportSnapDistance, parameters.GroundContactTolerance))
                return false;

            route = NavigationRoute.Complete(start, goalRegion, end,
                new NavigationRouteSegment[] { new GroundRouteSegment(start, end) });
            return true;
        }

        private IEnumerable<NavigationTransitionWork> EnumerateSharedTransitions(NavigationSearchNode node, WalkNavigationParameters parameters, NavigationGoalRegion goalRegion, NavigationPlanningDiagnostics diagnostics)
        {
            foreach (Successor successor in EnumerateLocalSuccessors(node.Position, node.Identity.CandidateId, node.Support, parameters, goalRegion, diagnostics))
            {
                yield return NavigationTransitionWork.WorkUnit;
                if (successor.Step == null) continue;
                yield return NavigationTransitionWork.Edge(successor.ToTransition());
            }

            GroundJumpParameters jumpParameters = new(parameters.BodySize, parameters.Gravity, parameters.GravityScale,
                parameters.LinearDamping, parameters.JumpHeight, parameters.JumpLength, parameters.SimulationTimeStep,
                parameters.SupportSnapDistance, parameters.GroundContactTolerance);
            foreach (GroundJumpSuccessor jump in GroundJumpSuccessorEnumerator.Enumerate(jumpSolver, node.Position,
                node.Support, goalRegion, jumpParameters, diagnostics, true, node.Identity.CandidateId))
            {
                yield return NavigationTransitionWork.WorkUnit;
                if (jump == null) continue;
                Vector2 landingCenter = jump.Trajectory.LandingPosition + Vector2.up * (parameters.BodySize.y * 0.5f);
                yield return NavigationTransitionWork.Edge(NavigationTransition.JumpLanding(
                    NavigationNodeIdentity.Ground(jump.LandingCandidateId), jump.Trajectory.LandingPosition,
                    jump.LandingSupport, jump.CreateSegment(),
                    Vector2.Distance(node.Position, jump.Trajectory.LandingPosition)
                        + jump.Trajectory.FlightDuration + 0.5f,
                    goalRegion.IsComplete(landingCenter, parameters.BodySize),
                    goalRegion.GuidanceDistance(landingCenter, parameters.BodySize)));
            }
        }

        /// <summary>Expands the current supported position once and returns its best valid action.</summary>
        private IEnumerable<NavigationRoute> PlanSingleStepIncremental(Vector2 start,
            NavigationGoalRegion goalRegion, WalkNavigationParameters parameters, NavigationPlanningDiagnostics diagnostics)
        {
            if (!World.TryResolveGroundSupport(start, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 resolvedStart, out NavigationSupport startSupport))
                yield break;

            Vector2 startCenter = resolvedStart + Vector2.up * (parameters.BodySize.y * 0.5f);
            float startDistance = goalRegion.CompletionDistance(startCenter, parameters.BodySize);
            float startGuidanceDistance = goalRegion.GuidanceDistance(startCenter, parameters.BodySize);
            if (goalRegion.IsComplete(startCenter, parameters.BodySize))
            {
                yield return NavigationRoute.Complete(resolvedStart, goalRegion, resolvedStart,
                    Array.Empty<NavigationRouteSegment>());
                yield break;
            }

            diagnostics?.RecordPathExpansion();
            Successor? best = null;
            foreach (Successor? item in EnumerateSingleStepCandidates(resolvedStart, startSupport,
                startDistance, startGuidanceDistance, goalRegion, parameters, diagnostics))
            {
                yield return null;
                if (!item.HasValue) continue;
                Successor candidate = item.Value;
                if (!candidate.CompletesGoal
                    && !HasStrictSingleStepProgress(candidate, startDistance, startGuidanceDistance))
                    continue;
                if (!best.HasValue || IsBetterSingleStep(candidate, best.Value)) best = candidate;
            }

            if (best.HasValue) yield return BuildSingleStepPlan(resolvedStart, goalRegion, best.Value);
        }

        /// <summary>Enumerates every valid Simple Walk action while retaining incremental work boundaries.</summary>
        private IEnumerable<Successor?> EnumerateSingleStepCandidates(Vector2 start, NavigationSupport startSupport,
            float startCompletionDistance, float startGuidanceDistance, NavigationGoalRegion goalRegion,
            WalkNavigationParameters parameters, NavigationPlanningDiagnostics diagnostics)
        {
            foreach (Successor local in EnumerateLocalSuccessors(start, -1, startSupport, parameters, goalRegion, diagnostics))
            {
                if (local.Step == null)
                {
                    yield return null;
                    continue;
                }

                if (local.Step is GroundRouteSegment)
                {
                    // Preserve initial eligibility before extending this one Ground action.
                    if (!local.CompletesGoal
                        && !HasStrictSingleStepProgress(local, startCompletionDistance, startGuidanceDistance))
                        continue;
                    foreach (Successor? ground in ExtendSimpleGroundMove(World, start, local, goalRegion, parameters))
                        yield return ground;
                    continue;
                }

                yield return local;
            }

            GroundJumpParameters jumpParameters = CreateJumpParameters(parameters);
            foreach (GroundJumpSuccessor jump in GroundJumpSuccessorEnumerator.Enumerate(jumpSolver, start,
                startSupport, goalRegion, jumpParameters, diagnostics, true, -1))
            {
                yield return null;
                if (jump == null) continue;
                yield return CreateSuccessor(new NavigationSupportCandidate(jump.LandingCandidateId, jump.LandingSupport),
                    jump.CreateSegment(), Vector2.Distance(start, jump.Trajectory.LandingPosition)
                        + jump.Trajectory.FlightDuration + 0.5f, goalRegion, parameters.BodySize);
            }
        }

        /// <summary>Extends a validated Ground action and yields its fully scored final successor.</summary>
        private static IEnumerable<Successor?> ExtendSimpleGroundMove(INavigationWorld world, Vector2 start,
            Successor current, NavigationGoalRegion goalRegion, WalkNavigationParameters parameters)
        {
            current = CreateSuccessor(current.Position, new GroundRouteSegment(start, current.Position),
                Mathf.Abs(current.Position.x - start.x), goalRegion, parameters.BodySize);
            float direction = Mathf.Sign(current.Position.x - start.x);
            float spacing = Mathf.Min(0.2f, world.CellSize * 0.25f);
            while (!current.CompletesGoal && direction * (goalRegion.Center.x - current.Position.x) > Tolerance)
            {
                Vector2 next = new(Mathf.MoveTowards(current.Position.x, goalRegion.Center.x, spacing), current.Position.y);
                NavigationRouteSegment validated = null;
                foreach (NavigationRouteSegment step in EnumerateGroundMove(world, current.Position, next, parameters))
                {
                    yield return null;
                    if (step != null) validated = step;
                }

                if (validated == null || Mathf.Abs(validated.End.y - start.y) > parameters.GroundContactTolerance)
                    break;
                Successor candidate = CreateSuccessor(validated.End,
                    new GroundRouteSegment(start, validated.End), Mathf.Abs(validated.End.x - start.x),
                    goalRegion, parameters.BodySize);
                if (!candidate.CompletesGoal
                    && !HasStrictSingleStepProgress(candidate, current.CompletionDistance, current.GuidanceDistance))
                    break;
                current = candidate;
            }

            yield return current;
        }

        /// <summary>Enumerates nearby real support anchors without collapsing surfaces to cells.</summary>
        private IEnumerable<Successor> EnumerateLocalSuccessors(Vector2 start, int currentCandidateId, NavigationSupport currentSupport, WalkNavigationParameters parameters, NavigationGoalRegion goalRegion, NavigationPlanningDiagnostics diagnostics)
        {
            Vector2 snappedStart = start;
            Rect anchors = new(snappedStart.x - World.CellSize * 1.5f, World.Origin.y + World.CellBounds.yMin * World.CellSize,
                World.CellSize * 3f, snappedStart.y - World.Origin.y - World.CellBounds.yMin * World.CellSize
                    + parameters.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon);
            List<NavigationSupportCandidate> candidates = new();
            World.CollectSupportCandidates(anchors, parameters.BodySize, candidates);
            for (int index = 0; index < candidates.Count; index++)
            {
                NavigationSupportCandidate candidate = candidates[index];
                if (candidate.Id == currentCandidateId) continue;
                NavigationRouteSegment step = null;
                foreach (NavigationRouteSegment groundStep in EnumerateGroundMove(World, snappedStart, candidate.Support.Position, parameters))
                {
                    yield return default;
                    if (groundStep != null) { step = groundStep; break; }
                }
                if (step != null && parameters.Speed > Tolerance)
                {
                    yield return CreateSuccessor(candidate, step, Vector2.Distance(snappedStart, candidate.Support.Position),
                        goalRegion, parameters.BodySize);
                    continue;
                }
                if (candidate.Support.Position.y >= snappedStart.y - Tolerance) continue;
                foreach (NavigationRouteSegment fallStep in EnumerateFall(World, snappedStart, candidate.Support.Position, parameters))
                {
                    yield return default;
                    if (fallStep != null)
                    {
                        yield return CreateSuccessor(candidate, fallStep,
                            Vector2.Distance(snappedStart, candidate.Support.Position) + 1f, goalRegion, parameters.BodySize);
                        break;
                    }
                }
                if (currentSupport.Kind != NavigationSurfaceKind.OneWay) continue;
                foreach (NavigationRouteSegment dropStep in EnumerateDropThrough(World, snappedStart, candidate.Support.Position, parameters))
                {
                    yield return default;
                    if (dropStep != null)
                    {
                        yield return CreateSuccessor(candidate, dropStep,
                            Vector2.Distance(snappedStart, candidate.Support.Position) + 0.25f, goalRegion, parameters.BodySize);
                        break;
                    }
                }
            }
        }

        /// <summary>Enumerates horizontal ground validation samples.</summary>
        private static IEnumerable<NavigationRouteSegment> EnumerateGroundMove(INavigationWorld world, Vector2 start, Vector2 end,
            WalkNavigationParameters parameters)
        {
            if (!world.TryResolveGroundSupport(start, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 snappedStart, out _)
                || !world.TryResolveGroundSupport(end, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 snappedEnd, out _)
                || Mathf.Abs(snappedStart.y - snappedEnd.y) > parameters.GroundContactTolerance)
            {
                yield return null;
                yield break;
            }

            float maximumSampleSpacing = Mathf.Min(world.CellSize * 0.25f,
                parameters.BodySize.x * 0.25f, parameters.BodySize.y * 0.25f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(
                Mathf.Abs(snappedEnd.x - snappedStart.x) / maximumSampleSpacing));
            Vector2 previous = snappedStart;
            for (int i = 0; i <= samples; i++)
            {
                Vector2 position = Vector2.Lerp(snappedStart, snappedEnd, i / (float)samples);
                bool clear = i == 0 || world.IsLowerCenterSegmentClear(previous, position, parameters.BodySize,
                    maximumSampleSpacing, parameters.GroundContactTolerance);
                clear &= world.CanStandAt(position, parameters.BodySize, parameters.SupportSnapDistance, out _);
                previous = position;
                yield return null;
                if (!clear)
                {
                    yield return null;
                    yield break;
                }
            }

            yield return new GroundRouteSegment(snappedStart, snappedEnd);
        }

        /// <summary>Validates continuous support and body clearance without imposing a route-length limit.</summary>
        public static bool TryValidateGroundConnection(INavigationWorld world, Vector2 start, Vector2 end,
            Vector2 bodySize, float supportSnapDistance, float groundContactTolerance)
        {
            if (!world.TryResolveGroundSupport(start, bodySize, supportSnapDistance, out Vector2 snappedStart, out _)
                || !world.TryResolveGroundSupport(end, bodySize, supportSnapDistance, out Vector2 snappedEnd, out _)
                || Mathf.Abs(snappedStart.y - snappedEnd.y) > groundContactTolerance)
                return false;

            float horizontalDistance = Mathf.Abs(snappedEnd.x - snappedStart.x);
            float spacing = Mathf.Min(0.2f, world.CellSize * 0.25f,
                bodySize.x * 0.25f, bodySize.y * 0.25f);
            int samples = Mathf.Max(1, Mathf.CeilToInt(horizontalDistance / spacing));

            Vector2 previous = snappedStart;
            for (int index = 0; index <= samples; index++)
            {
                Vector2 current = Vector2.Lerp(snappedStart, snappedEnd, index / (float)samples);
                bool clear = index == 0 || world.IsLowerCenterSegmentClear(previous, current,
                    bodySize, spacing, groundContactTolerance);
                clear &= world.CanStandAt(current, bodySize, supportSnapDistance, out _);
                if (!clear) return false;
                previous = current;
            }

            return true;
        }

        /// <summary>Enumerates downward one-way validation samples.</summary>
        private static IEnumerable<NavigationRouteSegment> EnumerateDropThrough(INavigationWorld world, Vector2 start, Vector2 end,
            WalkNavigationParameters parameters)
        {
            if (!world.TryResolveGroundSupport(start, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 snappedStart, out NavigationSupport support)
                || !world.TryResolveGroundSupport(end, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 snappedEnd, out _)
                || support.Kind != NavigationSurfaceKind.OneWay || snappedEnd.y >= snappedStart.y - Tolerance
                || Mathf.Abs(snappedEnd.x - snappedStart.x) > Tolerance)
            {
                yield return null;
                yield break;
            }

            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(snappedStart, snappedEnd) / 0.15f));
            float maximumSampleSpacing = Mathf.Min(world.CellSize * 0.25f,
                parameters.BodySize.x * 0.25f, parameters.BodySize.y * 0.25f);
            Vector2 previous = snappedStart;
            for (int i = 1; i <= samples; i++)
            {
                Vector2 current = Vector2.Lerp(snappedStart, snappedEnd, i / (float)samples);
                bool clear = world.IsLowerCenterSegmentClear(previous, current, parameters.BodySize,
                    maximumSampleSpacing, parameters.GroundContactTolerance)
                    && !world.CrossesOneWayDown(previous, current, parameters.BodySize.x, snappedStart.y);
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
        private static IEnumerable<NavigationRouteSegment> EnumerateFall(INavigationWorld world, Vector2 start, Vector2 end,
            WalkNavigationParameters parameters)
        {
            if (!world.TryResolveGroundSupport(start, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 snappedStart, out _)
                || !world.TryResolveGroundSupport(end, parameters.BodySize, parameters.SupportSnapDistance, out Vector2 snappedEnd, out _))
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
            float maximumSampleSpacing = Mathf.Min(world.CellSize * 0.25f,
                parameters.BodySize.x * 0.25f, parameters.BodySize.y * 0.25f);
            int horizontalSamples = Mathf.Max(1, Mathf.CeilToInt(horizontalDistance / 0.15f));
            Vector2 previous = snappedStart;
            for (int i = 1; i <= horizontalSamples; i++)
            {
                Vector2 current = Vector2.Lerp(snappedStart, ledgeExit, i / (float)horizontalSamples);
                bool clear = world.IsLowerCenterSegmentClear(previous, current, parameters.BodySize,
                    maximumSampleSpacing, parameters.GroundContactTolerance);
                previous = current;
                yield return null;
                if (!clear)
                {
                    yield return null;
                    yield break;
                }
            }

            int descentSamples = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(ledgeExit, snappedEnd) / 0.15f));
            previous = ledgeExit;
            for (int i = 1; i <= descentSamples; i++)
            {
                Vector2 current = Vector2.Lerp(ledgeExit, snappedEnd, i / (float)descentSamples);
                bool clear = world.IsLowerCenterSegmentClear(previous, current, parameters.BodySize,
                    maximumSampleSpacing, parameters.GroundContactTolerance)
                    && !world.CrossesOneWayDown(previous, current, parameters.BodySize.x);
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

        /// <summary>Scans real support surfaces below a column without using an integer-height cache.</summary>
        private static IEnumerable<Vector2?> EnumerateFirstLanding(INavigationWorld world, int x, int fromY,
            Vector2 bodySize, float supportSnapDistance, NavigationPlanningDiagnostics diagnostics)
        {
            float minimumY = world.Origin.y + world.CellBounds.yMin * world.CellSize;
            float maximumY = world.Origin.y + (fromY + 1) * world.CellSize;
            Rect bounds = new(world.Origin.x + x * world.CellSize - bodySize.x * 0.5f,
                minimumY, bodySize.x + world.CellSize, Mathf.Max(0f, maximumY - minimumY));
            List<NavigationSupportCandidate> candidates = new();
            world.CollectSupportCandidates(bounds, bodySize, candidates);
            for (int index = 0; index < candidates.Count; index++)
            {
                Vector2 candidate = candidates[index].Support.Position;
                bool standable = candidate.y < maximumY - Tolerance && world.CanStandAt(candidate, bodySize, supportSnapDistance, out _);
                diagnostics?.RecordFallScan();
                if (standable) { yield return candidate; yield break; }
            }
            yield return null;
        }

        private static Successor CreateSuccessor(NavigationSupportCandidate candidate, NavigationRouteSegment step, float cost, NavigationGoalRegion goalRegion, Vector2 bodySize, float? bestEffortDistance = null)
            => CreateSuccessor(candidate.Id, candidate.Support, step, cost, goalRegion, bodySize, bestEffortDistance);

        private static Successor CreateSuccessor(Vector2 position, NavigationRouteSegment step, float cost, NavigationGoalRegion goalRegion, Vector2 bodySize, float? bestEffortDistance = null)
            => CreateSuccessor(-1, default, step, cost, goalRegion, bodySize, bestEffortDistance);

        private static Successor CreateSuccessor(int candidateId, NavigationSupport support, NavigationRouteSegment step, float cost, NavigationGoalRegion goalRegion, Vector2 bodySize, float? bestEffortDistance = null)
        {
            Vector2 position = step.End;
            Vector2 startCenter = step.Start + Vector2.up * (bodySize.y * 0.5f);
            Vector2 endCenter = position + Vector2.up * (bodySize.y * 0.5f);
            float endpointDistance = goalRegion.CompletionDistance(endCenter, bodySize);
            bool completesGoal = goalRegion.IsComplete(endCenter, bodySize)
                || step is GroundRouteSegment && goalRegion.SweptIsComplete(startCenter, endCenter, bodySize);
            return new Successor(candidateId, support, position, step, cost, endpointDistance,
                goalRegion.GuidanceDistance(endCenter, bodySize), bestEffortDistance ?? endpointDistance, completesGoal);
        }

        /// <summary>Accepts a Simple Walk candidate only when completion or plateau guidance strictly improves.</summary>
        private static bool HasStrictSingleStepProgress(Successor candidate, float startCompletionDistance, float startGuidanceDistance)
            => IsStrictlyLess(candidate.CompletionDistance, startCompletionDistance) || (IsEquivalent(candidate.CompletionDistance, startCompletionDistance) && IsStrictlyLess(candidate.GuidanceDistance, startGuidanceDistance));

        /// <summary>Builds a plan containing exactly one validated local traversal step.</summary>
        private static NavigationRoute BuildSingleStepPlan(Vector2 start, NavigationGoalRegion goalRegion, Successor successor)
            => NavigationRoute.Complete(start, goalRegion, successor.Position, new[] { successor.Step });

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
            Validate.PositiveVector(profile.BodySize, nameof(profile));
            Validate.NonNegativeFinite(profile.Speed, nameof(profile));
            Validate.NonNegativeFinite(profile.GravityScale, nameof(profile));
            Validate.NonNegativeFinite(profile.LinearDamping, nameof(profile));
            Validate.NonNegativeFinite(profile.JumpHeight, nameof(profile));
            Validate.NonNegativeFinite(profile.JumpLength, nameof(profile));
            Validate.PositiveFinite(profile.SimulationTimeStep, nameof(profile));
            Validate.Finite(profile.Gravity, nameof(profile));
        }

        private static GroundJumpParameters CreateJumpParameters(WalkNavigationParameters parameters)
            => new(parameters.BodySize, parameters.Gravity, parameters.GravityScale, parameters.LinearDamping,
                parameters.JumpHeight, parameters.JumpLength, parameters.SimulationTimeStep,
                parameters.SupportSnapDistance, parameters.GroundContactTolerance);

        private static WalkNavigationParameters CaptureSupportSnapDistance(WalkNavigationParameters parameters)
            => parameters.SupportSnapDistance > 0f
                ? parameters
                : parameters.WithSupportSnapDistance(NavigationWorldQueries.SupportSnapDistance);

        private static WalkNavigationParameters CaptureGroundContactTolerance(WalkNavigationParameters parameters)
            => parameters.GroundContactTolerance > 0f
                ? parameters
                : parameters.WithGroundContactTolerance(NavigationWorldQueries.GeometryEpsilon);

        private NavigationRoute PrepareRouteForExecution(NavigationRoute route,
            WalkNavigationParameters parameters, CancellationToken cancellationToken)
            => GroundJumpSuccessorEnumerator.PrepareRouteForExecution(jumpSolver, route,
                CreateJumpParameters(parameters), cancellationToken);

        private static WalkNavigationParameters PrepareParameters(WalkNavigationParameters parameters)
            => CaptureGroundContactTolerance(CaptureSupportSnapDistance(parameters));

        private readonly struct Successor
        {
            public readonly int CandidateId;
            public readonly NavigationSupport Support;
            public readonly Vector2 Position;
            public readonly NavigationRouteSegment Step;
            public readonly float Cost;
            public readonly float CompletionDistance;
            public readonly float GuidanceDistance;
            public readonly float BestEffortDistance;
            public readonly bool CompletesGoal;

            public Successor(int candidateId, NavigationSupport support, Vector2 position, NavigationRouteSegment step, float cost, float completionDistance,
                float guidanceDistance, float bestEffortDistance, bool completesGoal)
            {
                CandidateId = candidateId;
                Support = support;
                Position = position;
                Step = step;
                Cost = cost;
                CompletionDistance = completionDistance;
                GuidanceDistance = guidanceDistance;
                BestEffortDistance = bestEffortDistance;
                CompletesGoal = completesGoal;
            }

            public NavigationTransition ToTransition()
                => NavigationTransition.GroundSuccessor(CandidateId, Position, Support, Step, Cost,
                    CompletesGoal, BestEffortDistance);
        }
    }
}
