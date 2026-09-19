using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies caller-facing pure navigation contracts without assets or scene objects.</summary>
    public sealed partial class NavigationCoreContractTests
    {
        /// <summary>Verifies a zero-value work budget fails instead of causing a non-progressing poll loop.</summary>
        [Test]
        public void NavigationSearchRejectsDefaultWorkBudget()
        {
            TestNavigationWorld world = new(new AABBInt(-2, -2, 2, 2), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(Vector2.right), DistanceMetric.Euclidean, 0f);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, NavigationNodeIdentity.Ground(-1),
                _ => Array.Empty<NavigationTransitionWork>());
            using NavigationSearch search = new NavigationSearch(request);

            Assert.That(() => search.Advance(default, default), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies a positive work budget advances an artificial cross-cell action graph.</summary>
        [Test]
        public void NavigationSearchAdvancesArtificialTransition()
        {
            TestNavigationWorld world = new(new AABBInt(-2, -2, 2, 2), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(Vector2.right), DistanceMetric.Euclidean, 0f);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, NavigationNodeIdentity.Ground(-1), node =>
                    ArtificialTransitions(node));
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate update = search.Advance(new NavigationWorkBudget(4, 1000d), default);

            Assert.That(update.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(update.Route, Is.Not.Null);
            Assert.That(update.Route.Segments.Count, Is.EqualTo(1));
        }

        /// <summary>Verifies the node expansion limit does not truncate the active node's successor stream.</summary>
        [Test]
        public void NavigationSearchCompletesActiveNodeAtExpansionLimit()
        {
            TestNavigationWorld world = new(new AABBInt(-2, -2, 2, 2), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(Vector2.right), DistanceMetric.Euclidean, 0f);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 1, NavigationNodeIdentity.Ground(-1),
                ArtificialTransitions);
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate firstSlice = search.Advance(new NavigationWorkBudget(1, 1000d), default);
            NavigationSearchUpdate update = search.Advance(new NavigationWorkBudget(1, 1000d), default);

            Assert.That(firstSlice.Status, Is.EqualTo(NavigationSearchStatus.Pending));
            Assert.That(update.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(update.ExpandedNodes, Is.EqualTo(1));
        }

        /// <summary>Verifies endpoint continuation keeps searching across work slices.</summary>
        [Test]
        public void NavigationSearchContinuesAcrossWorkSlices()
        {
            TestNavigationWorld world = new(new AABBInt(-2, -2, 6, 2), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(3f, 0f)), DistanceMetric.Euclidean, 0f);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, NavigationNodeIdentity.Ground(-1),
                ArtificialTailTransitions);
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate first = search.Advance(new NavigationWorkBudget(1, 1000d), default);
            NavigationSearchUpdate second = search.Advance(new NavigationWorkBudget(1, 1000d), default);
            NavigationSearchUpdate third = search.Advance(new NavigationWorkBudget(1, 1000d), default);

            Assert.That(first.Status, Is.EqualTo(NavigationSearchStatus.Pending));
            Assert.That(second.Status, Is.EqualTo(NavigationSearchStatus.Pending));
            Assert.That(third.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(third.Route.Segments.Count, Is.EqualTo(3));
        }

        /// <summary>Verifies a total search budget is terminal and never publishes a frontier route.</summary>
        [Test]
        public void NavigationSearchTotalBudgetIsTerminalWithoutRoute()
        {
            TestNavigationWorld world = new(new AABBInt(-2, -2, 2, 2), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(Vector2.right), DistanceMetric.Euclidean, 0f);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, NavigationNodeIdentity.Ground(-1),
                ArtificialTransitions, maxTotalWorkUnits: 1);
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate update = search.Advance(new NavigationWorkBudget(8, 1000d), default);

            Assert.That(update.Status, Is.EqualTo(NavigationSearchStatus.BudgetReached));
            Assert.That(update.Route, Is.Null);
            Assert.That(search.Advance(new NavigationWorkBudget(8, 1000d), default).Status,
                Is.EqualTo(NavigationSearchStatus.Pending));
        }

        /// <summary>Verifies ground reconnection preserves a validated multi-segment tail.</summary>
        [Test]
        public void GroundRouteReconnectsForwardDriftAndPreservesTail()
        {
            NavigationWorldSnapshot world = CreateGroundWorld(
                new NavigationShapeData(20, 0, NavigationShapeType.Edge,
                    new[] { new Vector2(0f, 1f), new Vector2(7f, 1f) }, 0f,
                    NavigationSurfaceKind.Solid, true));
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(6f, 1f)), DistanceMetric.Euclidean, 0f);
            NavigationRoute route = NavigationRoute.Create(goal,
                new NavigationRouteSegment[]
                {
                    new GroundRouteSegment(new Vector2(1f, 1f), new Vector2(4f, 1f)),
                    new FallRouteSegment(new Vector2(4f, 1f), new Vector2(5f, 1f), new Vector2(5f, -1f)),
                }, true);

            Assert.That(WalkNavigationPlanner.TryReconnectGroundRoute(
                world, route, AABB.FromLowerCenter(new Vector2(1.2f, 1f), new Vector2(0.8f, 0.8f)),
                out NavigationRoute reconnected), Is.True);
            Assert.That(reconnected.Start, Is.EqualTo(new Vector2(1.2f, 1f)));
            Assert.That(reconnected.Segments[0].End, Is.EqualTo(new Vector2(4f, 1f)));
            Assert.That(reconnected.Segments[1], Is.SameAs(route.Segments[1]));
        }

        /// <summary>Verifies a small pre-segment drift uses the executor completion range without skipping support validation.</summary>
        [Test]
        public void GroundRouteReconnectsWithinExecutorCompletionRangeBeforeNextSegment()
        {
            NavigationWorldSnapshot world = CreateGroundWorld(
                new NavigationShapeData(23, 0, NavigationShapeType.Edge,
                    new[] { new Vector2(0f, 1f), new Vector2(7f, 1f) }, 0f,
                    NavigationSurfaceKind.Solid, true));
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(5f, 1f)), DistanceMetric.Euclidean, 0f);
            NavigationRoute route = NavigationRoute.Create(goal,
                new[] { new GroundRouteSegment(new Vector2(3f, 1f), new Vector2(5f, 1f)) }, true);

            Assert.That(WalkNavigationPlanner.TryReconnectGroundRoute(
                world, route, AABB.FromLowerCenter(new Vector2(2.81f, 1f), new Vector2(0.8f, 0.8f)),
                0.2f, out NavigationRoute reconnected), Is.True);
            Assert.That(reconnected.Start.x, Is.EqualTo(2.81f).Within(0.0001f));
            Assert.That(reconnected.Segments[0].Start.x, Is.EqualTo(2.81f).Within(0.0001f));
            Assert.That(reconnected.Segments[0].End, Is.EqualTo(new Vector2(5f, 1f)));
        }

        /// <summary>Verifies ground reconnection rejects a support gap instead of skipping an obstacle.</summary>
        [Test]
        public void GroundRouteReconnectRejectsSupportGap()
        {
            NavigationWorldSnapshot world = CreateGroundWorld(
                new NavigationShapeData(21, 0, NavigationShapeType.Edge,
                    new[] { new Vector2(0f, 1f), new Vector2(2f, 1f) }, 0f,
                    NavigationSurfaceKind.Solid, true),
                new NavigationShapeData(22, 0, NavigationShapeType.Edge,
                    new[] { new Vector2(4f, 1f), new Vector2(7f, 1f) }, 0f,
                    NavigationSurfaceKind.Solid, true));
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(6f, 1f)), DistanceMetric.Euclidean, 0f);
            NavigationRoute route = NavigationRoute.Create(goal,
                new[] { new GroundRouteSegment(new Vector2(4f, 1f), new Vector2(6f, 1f)) }, true);

            Assert.That(WalkNavigationPlanner.TryReconnectGroundRoute(
                world, route, AABB.FromLowerCenter(new Vector2(1.2f, 1f), new Vector2(0.8f, 0.8f)),
                out _), Is.False);
        }

        /// <summary>Verifies shifted query windows return the same stable candidate identities and coordinates.</summary>
        [Test]
        public void NavigationSupportCandidatesRemainStableAcrossShiftedWindows()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                AABB.FromMinAndSize(0, 0, 32, 4),
                new[]
                {
                    new NavigationShapeData(1, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(8f, 1f), new Vector2(24f, 1f) }, 0f,
                        NavigationSurfaceKind.Solid, true),
                }, Array.Empty<NavigationRegionData>());
            List<NavigationSupportCandidate> first = new();
            List<NavigationSupportCandidate> second = new();
            world.CollectSupportCandidates(AABB.FromMinAndSize(11.3004f, 0f, 4f, 3f), new Vector2(0.8f, 0.8f), first);
            world.CollectSupportCandidates(AABB.FromMinAndSize(11.343741f, 0f, 4f, 3f), new Vector2(0.8f, 0.8f), second);

            Assert.That(first.Count, Is.EqualTo(4));
            Assert.That(second.Count, Is.EqualTo(4));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].Id, Is.EqualTo(first[index].Id));
                Assert.That(second[index].Support.Position, Is.EqualTo(first[index].Support.Position));
            }
        }

        /// <summary>Verifies repeated finite-window expansion cannot create candidates outside the snapshot directory.</summary>
        [Test]
        public void NavigationSupportCandidateExpansionIsFiniteAndClosed()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                AABB.FromMinAndSize(0, 0, 8, 4),
                new[]
                {
                    new NavigationShapeData(2, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(0f, 1f), new Vector2(7f, 1f) }, 0f,
                        NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, 1f),
                }, Array.Empty<NavigationRegionData>());
            List<NavigationSupportCandidate> directory = new();
            world.CollectSupportCandidates(AABB.FromMinAndSize(0f, 0f, 8f, 3f), new Vector2(0.8f, 0.8f), directory);
            HashSet<int> expectedIds = new();
            for (int index = 0; index < directory.Count; index++) expectedIds.Add(directory[index].Id);

            for (int iteration = 0; iteration < 16; iteration++)
            {
                List<NavigationSupportCandidate> expansion = new();
                world.CollectSupportCandidates(AABB.FromMinAndSize(0f, 0f, 8f, 3f), new Vector2(0.8f, 0.8f), expansion);
                Assert.That(expansion.Count, Is.EqualTo(directory.Count));
                for (int index = 0; index < expansion.Count; index++)
                {
                    Assert.That(expectedIds.Contains(expansion[index].Id), Is.True);
                    Assert.That(expansion[index].Support.Position, Is.EqualTo(directory[index].Support.Position));
                }
            }
        }

        /// <summary>Verifies short platforms and same-cell surfaces remain distinct without height quantization.</summary>
        [Test]
        public void NavigationSupportCandidatesPreserveShortAndOverlappingSurfaces()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                AABB.FromMinAndSize(0, 0, 5, 5),
                new[]
                {
                    new NavigationShapeData(3, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(0.05f, 1.38f), new Vector2(0.35f, 1.38f) }, 0f,
                        NavigationSurfaceKind.Solid, true),
                    new NavigationShapeData(4, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(2f, 2f), new Vector2(3f, 2f) }, 0f,
                        NavigationSurfaceKind.Solid, true),
                    new NavigationShapeData(5, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(2f, 3f), new Vector2(3f, 3f) }, 0f,
                        NavigationSurfaceKind.Solid, true),
                }, Array.Empty<NavigationRegionData>());
            List<NavigationSupportCandidate> candidates = new();
            world.CollectSupportCandidates(AABB.FromMinAndSize(0f, 0f, 5f, 4f), new Vector2(0.05f, 0.2f), candidates);

            Assert.That(candidates.Exists(candidate => candidate.Support.Surface == new NavigationSurfaceId(3, 0)
                && candidate.Support.Position.y == 1.38f), Is.True);
            Assert.That(candidates.Exists(candidate => candidate.Support.Surface == new NavigationSurfaceId(4, 0)), Is.True);
            Assert.That(candidates.Exists(candidate => candidate.Support.Surface == new NavigationSurfaceId(5, 0)), Is.True);
            Assert.That(candidates.Exists(candidate => candidate.Support.Position.y == 1.38f), Is.True);
        }

        [Test]
        public void RetreatIdentity_NullTargetAllowsBudgetObservation()
        {
            RetreatExecution execution = new(null, 0f);
            Assert.That(execution.IsCurrentTarget(null), Is.True);
            Assert.That(execution.RecordApproachDistance(1f), Is.True);
            Assert.That(execution.HasApproachLimit, Is.False);
        }

        [Test]
        public void RetreatIdentity_DestroyedOrReplacedTargetIsRejected()
        {
            GameObject target = new("Retreat identity");
            GameObject replacement = new("Replacement identity");
            try
            {
                RetreatExecution execution = new(target, 1f);
                Assert.That(execution.IsCurrentTarget(target), Is.True);
                Assert.That(execution.IsCurrentTarget(replacement), Is.False);
                UnityEngine.Object.DestroyImmediate(target);
                Assert.That(execution.IsCurrentTarget(target), Is.False);
                Assert.That(execution.IsCurrentTarget(null), Is.False);
            }
            finally
            {
                if (target) UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(replacement);
            }
        }

        /// <summary>Verifies Movement can record cumulative approach distance without a second idle state.</summary>
        [Test]
        public void RetreatExecution_AccumulatesApproachDistanceAndRejectsOverflow()
        {
            RetreatExecution execution = new(null, 2f);

            Assert.That(execution.RemainingApproachDistance, Is.EqualTo(2f));
            Assert.That(execution.RecordApproachDistance(1f), Is.True);
            Assert.That(execution.RemainingApproachDistance, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(execution.RecordApproachDistance(1f), Is.True);
            Assert.That(execution.RemainingApproachDistance, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(execution.RecordApproachDistance(0.01f), Is.False);
            Assert.That(execution.RemainingApproachDistance, Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>Verifies route budget checks use the caller's current goal, not route metadata.</summary>
        [Test]
        public void RetreatExecution_UsesCurrentGoalForRouteBudget()
        {
            NavigationGoalRequest staleGoal = NavigationGoalRequest.Retreat(
                AABB.Point(new Vector2(-10f, 0f)), DistanceMetric.Euclidean, 10f);
            NavigationGoalRequest currentGoal = NavigationGoalRequest.Retreat(
                AABB.Point(new Vector2(10f, 0f)), DistanceMetric.Euclidean, 10f);
            NavigationRoute staleRoute = NavigationRoute.Create(staleGoal,
                new[] { new FlyRouteSegment(Vector2.zero, Vector2.right) }, false);
            RetreatExecution execution = new(null, 0.5f);

            Assert.That(execution.AllowsRoute(currentGoal, staleRoute.Start, staleRoute.Segments), Is.False,
                "Route admission must use the current tick goal even when route metadata is stale.");
            Assert.That(execution.AllowsRoute(staleGoal, staleRoute.Start, staleRoute.Segments), Is.True,
                "Route admission must not substitute the current goal with route metadata.");
        }

        /// <summary>Verifies jump route segments expose geometry without an executable trajectory.</summary>
        [Test]
        public void JumpRouteSegmentCarriesGeometryOnly()
        {
            JumpTrajectorySolution solution = CreateSolution(Vector2.zero, new Vector2(2, 1), 2, 5);
            JumpRouteSegment segment = new(solution.StartPosition, solution.LandingPosition);

            Assert.That(segment.Start, Is.EqualTo(solution.StartPosition));
            Assert.That(segment.End, Is.EqualTo(solution.LandingPosition));
            Assert.That(segment.Start, Is.EqualTo(solution.StartPosition));
            Assert.That(segment.End, Is.EqualTo(solution.LandingPosition));
        }

        /// <summary>Verifies fall route segments retain the ledge-exit waypoint and reject contradictory geometry.</summary>
        [Test]
        public void FallRouteSegmentCarriesValidatedLedgeExit()
        {
            FallRouteSegment step = new(Vector2.zero, Vector2.right, new Vector2(1, -2));

            Assert.That(step.LedgeExit, Is.EqualTo(Vector2.right));
            Assert.That(() => new FallRouteSegment(Vector2.zero, Vector2.one, new Vector2(1, -2)),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new FallRouteSegment(Vector2.zero, Vector2.right, Vector2.up),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies routes copy their input and expose mutation-resistant segments.</summary>
        [Test]
        public void NavigationRouteCopiesAndProtectsSegments()
        {
            List<NavigationRouteSegment> source = new() { new GroundRouteSegment(Vector2.zero, Vector2.right) };
            NavigationRoute route = NavigationRoute.Create(PointGoal(new Vector2(2, 1), 0f), source, true);
            source.Add(new FlyRouteSegment(Vector2.right, new Vector2(2, 1)));

            Assert.That(route.Count, Is.EqualTo(1));
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments, Is.Not.SameAs(source));
        }

        /// <summary>Verifies requested and resolved goals remain distinct route values.</summary>
        [Test]
        public void NavigationRoutePreservesRequestedAndResolvedGoals()
        {
            NavigationRoute route = NavigationRoute.Create(PointGoal(new Vector2(5, 2), 0f),
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) }, true);

            Assert.That(route.Goal.TargetBounds.Center, Is.EqualTo(new Vector2(5, 2)));
            Assert.That(route.Endpoint, Is.EqualTo(Vector2.right));
        }

        /// <summary>Verifies semantic route factories and segment replacement preserve route-owned state.</summary>
        [Test]
        public void NavigationRouteFactoriesDescribeCompletenessAndPreserveStateWhenReplacingSegments()
        {
            NavigationGoalRequest goal = PointGoal(new Vector2(5f, 2f), 0f);
            NavigationRoute complete = NavigationRoute.Complete(goal, new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });
            NavigationRoute partial = NavigationRoute.Partial(goal, new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });
            NavigationRoute replaced = partial.WithSegments(new[] { new FlyRouteSegment(Vector2.zero, Vector2.right) });

            Assert.That(complete.ReachesGoal, Is.True);
            Assert.That(partial.ReachesGoal, Is.False);
            Assert.That(replaced.Start, Is.EqualTo(partial.Start));
            Assert.That(replaced.Goal, Is.EqualTo(partial.Goal));
            Assert.That(replaced.Endpoint, Is.EqualTo(partial.Endpoint));
            Assert.That(replaced.ReachesGoal, Is.False);
            Assert.That(replaced.Segments[0], Is.TypeOf<FlyRouteSegment>());
        }

        /// <summary>Verifies named planning-result factories keep route availability separate from termination.</summary>
        [Test]
        public void NavigationPlanResultFactoriesPreserveTerminationAndRouteAvailability()
        {
            NavigationRoute route = NavigationRoute.Complete(PointGoal(Vector2.right, 0f),
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });

            Assert.That(NavigationPlanResult.ResultProduced(route).Termination,
                Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(NavigationPlanResult.ResultProduced(route).Route, Is.SameAs(route));
            Assert.That(NavigationPlanResult.SearchExhausted().Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted));
            Assert.That(NavigationPlanResult.SearchExhausted().Route, Is.Null);
            Assert.That(NavigationPlanResult.BudgetReached().Termination,
                Is.EqualTo(NavigationPlanTermination.BudgetReached));
            Assert.That(NavigationPlanResult.BudgetReached().Route, Is.Null);
            Assert.That(NavigationPlanResult.NoResult.Termination,
                Is.EqualTo(NavigationPlanTermination.NoResult));
            Assert.That(NavigationPlanResult.NoResult.Route, Is.Null);
            Assert.That(() => NavigationPlanResult.SearchExhausted().WithRoute(route),
                Throws.InstanceOf<InvalidOperationException>());
            Assert.That(() => NavigationPlanResult.BudgetReached().WithRoute(route),
                Throws.InstanceOf<InvalidOperationException>());
            Assert.That(() => NavigationPlanResult.NoResult.WithRoute(route),
                Throws.InstanceOf<InvalidOperationException>());
        }

        /// <summary>Verifies named search updates expose their status and route payload.</summary>
        [Test]
        public void NavigationSearchUpdateFactoriesSetExpectedTerminalState()
        {
            NavigationRoute route = NavigationRoute.Complete(PointGoal(Vector2.right, 0f),
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });
            NavigationSearchUpdate pending = NavigationSearchUpdate.Pending(3);
            NavigationSearchUpdate completed = NavigationSearchUpdate.CompletedRoute(route, 4, 5);
            NavigationSearchUpdate exhausted = NavigationSearchUpdate.Exhausted(4, 5);
            NavigationSearchUpdate budget = NavigationSearchUpdate.BudgetReached(4, 5);

            Assert.That(pending.Status, Is.EqualTo(NavigationSearchStatus.Pending));
            Assert.That(pending.WorkUnits, Is.Zero);
            Assert.That(pending.ExpandedNodes, Is.EqualTo(3));
            Assert.That(completed.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(completed.Route, Is.SameAs(route));
            Assert.That(exhausted.Status, Is.EqualTo(NavigationSearchStatus.Exhausted));
            Assert.That(exhausted.Route, Is.Null);
            Assert.That(budget.Status, Is.EqualTo(NavigationSearchStatus.BudgetReached));
            Assert.That(budget.Route, Is.Null);
        }

        /// <summary>Verifies named transition factories preserve their graph identity and edge fields.</summary>
        [Test]
        public void NavigationTransitionFactoriesDescribeJumpAndFlyEdges()
        {
            GroundRouteSegment segment = new(Vector2.zero, Vector2.right);
            NavigationTransition completedJump = NavigationTransition.CompletedJump(Vector2.right, segment, 2f);
            NavigationTransition landing = NavigationTransition.JumpLanding(NavigationNodeIdentity.Ground(7),
                Vector2.right, default, segment, 3f, false);
            NavigationTransition fly = NavigationTransition.FlyMove(new Vector2Int(3, 4), Vector2.right,
                segment, 5f, true);

            Assert.That(completedJump.Destination, Is.EqualTo(NavigationNodeIdentity.Jump(-1)));
            Assert.That(completedJump.CompletesGoal, Is.True);
            Assert.That(landing.Destination, Is.EqualTo(NavigationNodeIdentity.Ground(7)));
            Assert.That(landing.Cost, Is.EqualTo(3f));
            Assert.That(fly.Destination, Is.EqualTo(NavigationNodeIdentity.Fly(new Vector2Int(3, 4))));
            Assert.That(fly.CompletesGoal, Is.True);
        }

        /// <summary>Verifies rectangular Proximity goals accept overlap and compute the shortest AABB gap.</summary>
        [Test]
        public void ProximityGoalUsesRectangularDistance()
        {
            TestNavigationWorld world = UnitWorld();
            NavigationGoalRequest goal = ProximityGoal(AABB.FromCenterAndSize(5f, 3f, 2f, 4f), 0.5f);

            Assert.That(goal.DistanceToCenteredBody(
                AABB.FromCenterAndSize(new Vector2(1f, 0f), new Vector2(2f, 2f))), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(world.IsGoalComplete(goal, AABB.FromCenterAndSize(new Vector2(4f, 2f), new Vector2(2f, 2f))), Is.True);
            Assert.That(PointGoal(new Vector2(3f, 4f), 1f).DistanceToCenteredBody(
                AABB.FromCenterAndSize(new Vector2(1f, 4f), Vector2.one)),
                Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(() => NavigationGoalRequest.Proximity(
                AABB.FromCenterAndSize(new Vector2(float.NaN, 0f), Vector2.one), DistanceMetric.Euclidean, 0f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => PointGoal(Vector2.zero, float.PositiveInfinity), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies all axial AABB metrics handle overlap, boundaries, and negative coordinates.</summary>
        [Test]
        public void NavigationGoalDistanceSupportsAllMetrics()
        {
            AABB first = AABB.FromCenterAndSize(-5f, -2f, 2f, 2f);
            AABB second = AABB.FromCenterAndSize(1f, 3f, 2f, 2f);
            Assert.That(DistanceMetric.Euclidean.DistanceToBody(first, second), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(DistanceMetric.Manhattan.DistanceToBody(first, second), Is.EqualTo(7f));
            Assert.That(DistanceMetric.Chebyshev.DistanceToBody(first, second), Is.EqualTo(4f));
            Assert.That(DistanceMetric.Manhattan.DistanceToBody(first, first), Is.EqualTo(0f));
            Assert.That(() => DistanceMetric.Euclidean.DistanceToBody(new AABB(float.NaN, 0f, float.NaN, 1f), first), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => ((DistanceMetric)99).DistanceToBody(first, second), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies an explicit goal request keeps its geometry, metric and line-of-sight fields.</summary>
        [Test]
        public void NavigationGoalRequestPreservesGeometryMetricAndLineOfSight()
        {
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(-2f, 0f)), DistanceMetric.Manhattan, 0.25f, true);
            TestNavigationWorld world = new(new AABBInt(-4, -4, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);

            Assert.That(request.DistanceMetric, Is.EqualTo(DistanceMetric.Manhattan));
            Assert.That(request.RequiresLineOfSight, Is.True);
            Assert.That(request.Geometry, Is.EqualTo(NavigationGoalGeometry.Proximity));
            Assert.That(request.ArrivalTolerance, Is.EqualTo(0.25f));
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(-1.75f, 0f), Vector2.one)), Is.True);
        }

        /// <summary>Verifies exact request identity is component-exact and consistent with its hash code.</summary>
        [Test]
        public void NavigationGoalRequestEqualityIsExactAndHashConsistent()
        {
            AABB bounds = AABB.FromCenterAndSize(-2f, 3f, 2f, 4f);
            NavigationGoalRequest baseline = NavigationGoalRequest.Proximity(
                bounds, DistanceMetric.Euclidean, 0.5f);
            NavigationGoalRequest identical = NavigationGoalRequest.Proximity(
                bounds, DistanceMetric.Euclidean, 0.5f);

            Assert.That(baseline.Equals(identical), Is.True);
            Assert.That(baseline == identical, Is.True);
            Assert.That(baseline.GetHashCode(), Is.EqualTo(identical.GetHashCode()));
            Assert.That(baseline, Is.EqualTo(identical));

            Assert.That(baseline.Equals(NavigationGoalRequest.GroundRange(bounds, 0.5f)), Is.False);
            Assert.That(baseline.Equals(NavigationGoalRequest.Proximity(bounds, DistanceMetric.Manhattan, 0.5f)), Is.False);
            Assert.That(baseline.Equals(NavigationGoalRequest.Proximity(bounds, DistanceMetric.Euclidean, 0.5f, true)), Is.False);
            Assert.That(baseline.Equals(NavigationGoalRequest.Proximity(bounds, DistanceMetric.Euclidean, 0.6f)), Is.False);
            Assert.That(baseline.Equals(NavigationGoalRequest.Retreat(bounds, DistanceMetric.Euclidean, 0.5f)), Is.False);
            Assert.That(baseline.Equals(NavigationGoalRequest.Proximity(
                AABB.FromCenterAndSize(-2.0001f, 3f, 2f, 4f), DistanceMetric.Euclidean, 0.5f)), Is.False,
                "Exact request identity must not use Unity's approximate Vector2 comparison.");
            Assert.That(baseline.HasCompatibleSemantics(NavigationGoalRequest.Proximity(
                AABB.FromCenterAndSize(-2.0001f, 3f, 2f, 4f), DistanceMetric.Euclidean, 0.5f)), Is.True,
                "A moved target stays semantically compatible; only the exact identity changes.");
        }

        /// <summary>Verifies the continuous lower-center anchor of an AABB is never rounded to integers.</summary>
        [Test]
        public void AabbLowerCenterKeepsContinuousCoordinates()
        {
            AABB centered = AABB.FromCenterAndSize(2.5f, 3.25f, 1.5f, 1.5f);
            Assert.That(centered.LowerCenter, Is.EqualTo(new Vector2(2.5f, 2.5f)));

            AABB offset = new(new Vector2(2.3f, 7.4f), new Vector2(3.9f, 9.6f));
            Assert.That(offset.LowerCenter, Is.EqualTo(new Vector2(3.1f, 7.4f)));

            AABB zeroSize = AABB.Point(new Vector2(0.35f, -0.75f));
            Assert.That(zeroSize.Size, Is.EqualTo(Vector2.zero));
            Assert.That(zeroSize.LowerCenter, Is.EqualTo(new Vector2(0.35f, -0.75f)));
            Assert.That(zeroSize.Contains(new Vector2(0.35f, -0.75f)), Is.True);
            Assert.That(zeroSize.Equals(AABB.Point(0.35f, -0.75f)), Is.True);
            Assert.That(zeroSize.GetHashCode(),
                Is.EqualTo(AABB.Point(0.35f, -0.75f).GetHashCode()));
        }

        [Test]
        public void NavigationGoalSemanticsIgnoreTargetExtentSamplingNoiseButPreserveExactIdentity()
        {
            AABB firstBounds = AABB.FromCenterAndSize(5f, 3f, 1.79999971f, 1.5999999f);
            AABB sampledBounds = AABB.FromCenterAndSize(5f, 3f, 1.79999971f, 1.60000038f);
            NavigationGoalRequest first = NavigationGoalRequest.GroundRange(firstBounds, 1f, true);
            NavigationGoalRequest sampled = NavigationGoalRequest.GroundRange(sampledBounds, 1f, true);

            Assert.That(first.HasCompatibleSemantics(sampled), Is.True);
            Assert.That(first.Equals(sampled), Is.False);

            NavigationGoalRequest moved = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(new Vector2(5f, 6f), firstBounds.Size), 1f, true);
            Assert.That(first.HasCompatibleSemantics(moved), Is.True);
            Assert.That(first.IsReusableFor(moved), Is.False);
        }

        [Test]
        public void NavigationGoalSemanticsRejectRealTargetExtentChange()
        {
            float epsilon = NavigationWorldQueries.GeometryEpsilon;
            // Every extent below is exact in float arithmetic, so the sampling-noise boundary is
            // deterministic instead of depending on rounding of a center/size round trip.
            Vector2 min = new(4f, 0f);
            NavigationGoalRequest first = ProximityGoal(new AABB(min, new Vector2(6f, 0f)), 0.2f);
            NavigationGoalRequest belowBoundary = ProximityGoal(new AABB(min, new Vector2(6f, epsilon * 0.5f)), 0.2f);
            NavigationGoalRequest atBoundary = ProximityGoal(new AABB(min, new Vector2(6f, epsilon)), 0.2f);
            NavigationGoalRequest changed = ProximityGoal(new AABB(min, new Vector2(6f, epsilon * 2f)), 0.2f);

            Assert.That(first.HasCompatibleSemantics(belowBoundary), Is.True);
            Assert.That(first.HasCompatibleSemantics(atBoundary), Is.True,
                "An extent difference exactly at the sampling-noise epsilon must still be tolerated.");
            Assert.That(first.HasCompatibleSemantics(changed), Is.False);
        }

        /// <summary>Verifies Approach and Retreat use one threshold with opposite completion directions.</summary>
        [Test]
        public void SharedReachDistanceUsesOppositeApproachAndRetreatDirections()
        {
            TestNavigationWorld world = new(new AABBInt(-8, -8, 8, 8), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest retreat = NavigationGoalRequest.Retreat(AABB.Point(Vector2.zero), DistanceMetric.Euclidean, 2f);
            NavigationGoalRequest approach = NavigationGoalRequest.Proximity(AABB.Point(Vector2.zero), DistanceMetric.Euclidean, 2f);

            Assert.That(retreat.IsRetreat, Is.True);
            Assert.That(retreat.ArrivalTolerance, Is.Zero);
            Assert.That(retreat.RetreatDistance, Is.EqualTo(2f));
            Assert.That(world.IsGoalComplete(approach, AABB.FromCenterAndSize(new Vector2(1.99f, 0f), Vector2.zero)), Is.True,
                "Approach must complete at or below the shared reachDistance.");
            Assert.That(world.IsGoalComplete(approach, AABB.FromCenterAndSize(new Vector2(2.01f, 0f), Vector2.zero)), Is.False);
            Assert.That(world.IsGoalComplete(retreat, AABB.FromCenterAndSize(new Vector2(2.01f, 0f), Vector2.zero)), Is.True,
                "Retreat must complete at or above the shared reachDistance.");
            Assert.That(world.IsGoalComplete(retreat, AABB.FromCenterAndSize(new Vector2(1.99f, 0f), Vector2.zero)), Is.False);
            Assert.That(world.IsGoalCompleteAlong(retreat,
                AABB.FromCenterAndSize(new Vector2(1f, 0f), Vector2.zero),
                AABB.FromCenterAndSize(new Vector2(2.01f, 0f), Vector2.zero)), Is.True);
        }

        /// <summary>Verifies LOS completion is evaluated against the immutable map snapshot, not dynamic physics.</summary>
        [Test]
        public void LineOfSightGoalRejectsSolidSnapshotCell()
        {
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(2f, 0f)), DistanceMetric.Euclidean, 1f, true);
            TestNavigationWorld clearWorld = new(
                new AABBInt(0, -2, 4, 2), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            TestNavigationWorld blockedWorld = new(
                new AABBInt(0, -2, 4, 2), new[] { new Vector2Int(1, 0) }, Array.Empty<Vector2Int>());

            Assert.That(clearWorld.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(0.5f, 0f), Vector2.one)), Is.True);
            Assert.That(blockedWorld.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(0.5f, 0f), Vector2.one)), Is.False,
                "Satisfied goal geometry must not complete while the required line of sight is blocked.");
            Assert.That(request.GeometryCompletionDistance(AABB.FromCenterAndSize(new Vector2(0.5f, 0f), Vector2.one)),
                Is.LessThanOrEqualTo(request.CompletionTolerance));
        }

        /// <summary>Verifies LOS sweeps clip geometry before sampling even beyond the former global sample cap.</summary>
        [Test]
        public void LineOfSightSweepFindsNarrowCompletionIntervalAcrossLongSegment()
        {
            const float targetX = 8192.25f;
            AABBInt bounds = new(0, 0, 9001, 2);
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(targetX, 0.5f)), DistanceMetric.Euclidean, 0f, true);
            Vector2 bodySize = new(0.1f, 0.1f);
            Vector2 start = new(0.25f, 0.5f);
            Vector2 end = new(9000.25f, 0.5f);

            TestNavigationWorld clear = new(bounds, Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            TestNavigationWorld blocked = new(bounds, new[] { new Vector2Int(8192, 0) }, Array.Empty<Vector2Int>());

            Assert.That(clear.IsGoalCompleteAlong(request, AABB.FromCenterAndSize(start, bodySize),
                AABB.FromCenterAndSize(end, bodySize)), Is.True);
            Assert.That(blocked.IsGoalCompleteAlong(request, AABB.FromCenterAndSize(start, bodySize),
                AABB.FromCenterAndSize(end, bodySize)), Is.False);
        }

        /// <summary>Locks the serialized product-level movement and distance enum values.</summary>
        [Test]
        public void MovementGoalEnumsKeepAuthoredValues()
        {
            Assert.That((int)MovementGoal.Default, Is.Zero);
            Assert.That((int)MovementGoal.Confront, Is.EqualTo(1));
            Assert.That((int)MovementGoal.Proximity, Is.EqualTo(2));
            Assert.That((int)MovementGoal.FiringPosition, Is.EqualTo(3));
            Assert.That((int)DistanceMetric.Euclidean, Is.Zero);
            Assert.That((int)DistanceMetric.Manhattan, Is.EqualTo(1));
            Assert.That((int)DistanceMetric.Chebyshev, Is.EqualTo(2));
        }

        /// <summary>Verifies guidance uses the raw target center without changing completion geometry.</summary>
        [TestCase(DistanceMetric.Euclidean, 5f)]
        [TestCase(DistanceMetric.Manhattan, 7f)]
        [TestCase(DistanceMetric.Chebyshev, 4f)]
        public void GuidanceDistanceUsesGoalMetricForCenteredGoals(DistanceMetric metric, float expected)
        {
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), metric, 0.1f);

            Assert.That(goal.GuidanceDistance(AABB.FromCenterAndSize(new Vector2(1f, 0f), Vector2.one)), Is.EqualTo(expected).Within(0.0001f));
        }

        /// <summary>Verifies Ground Range guidance remains Euclidean to the raw target AABB center.</summary>
        [Test]
        public void GroundRangeGuidanceUsesRawTargetCenter()
        {
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.1f);

            Assert.That(goal.GuidanceDistance(AABB.FromCenterAndSize(new Vector2(1f, 0f), Vector2.one)), Is.EqualTo(5f).Within(0.0001f));
        }

        /// <summary>Verifies Fly-style bounds replacement preserves every non-bounds goal identity field.</summary>
        [Test]
        public void WithTargetBoundsPreservesGoalIdentity()
        {
            AABB originalBounds = AABB.FromCenterAndSize(2f, 3f, 2f, 4f);
            AABB replacementBounds = AABB.FromCenterAndSize(2f, 5f, 2f, 1f);
            NavigationGoalRequest original = NavigationGoalRequest.Proximity(
                originalBounds, DistanceMetric.Chebyshev, 0.75f, true);

            NavigationGoalRequest replacement = original.WithTargetBounds(replacementBounds);

            Assert.That(replacement.TargetBounds, Is.EqualTo(replacementBounds));
            Assert.That(replacement.Geometry, Is.EqualTo(original.Geometry));
            Assert.That(replacement.DistanceMetric, Is.EqualTo(original.DistanceMetric));
            Assert.That(replacement.RequiresLineOfSight, Is.EqualTo(original.RequiresLineOfSight));
            Assert.That(replacement.ArrivalTolerance, Is.EqualTo(original.ArrivalTolerance));
        }

        /// <summary>Verifies Proximity requests select the metric used by bound body completion and swept-segment distances.</summary>
        [TestCase(DistanceMetric.Euclidean, 2.9154759f, 1.8027756f)]
        [TestCase(DistanceMetric.Manhattan, 4f, 2.5f)]
        [TestCase(DistanceMetric.Chebyshev, 2.5f, 1.5f)]
        public void ProximityRequestUsesSelectedMetricForBodyAndSweepDistance(
            DistanceMetric metric, float expectedBodyDistance, float expectedSegmentDistance)
        {
            TestNavigationWorld world = new(new AABBInt(-8, -8, 8, 8), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), metric, 0.1f);

            Assert.That(request.DistanceMetric, Is.EqualTo(metric));
            Assert.That(world.GetGoalCompletionDistance(request, AABB.FromCenterAndSize(new Vector2(1f, 0f), Vector2.one)),
                Is.EqualTo(expectedBodyDistance).Within(0.0001f));
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(1f, 0f), Vector2.one)), Is.False);
            Assert.That(request.GeometrySweptCompletionDistance(
                AABB.FromLowerCenter(new Vector2(0f, 0f), Vector2.one),
                AABB.FromLowerCenter(new Vector2(2f, 0f), Vector2.one)),
                Is.EqualTo(expectedSegmentDistance).Within(0.0001f));
        }

        /// <summary>Verifies bound Proximity sweep completion finds an interior closest point for every metric.</summary>
        [TestCase(DistanceMetric.Euclidean)]
        [TestCase(DistanceMetric.Manhattan)]
        [TestCase(DistanceMetric.Chebyshev)]
        public void ProximitySweepCompletesAtInteriorClosestPoint(DistanceMetric metric)
        {
            TestNavigationWorld world = new(new AABBInt(-8, -8, 8, 8), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), metric, 1f);

            float sweepDistance = goal.GeometrySweptCompletionDistance(
                AABB.FromLowerCenter(new Vector2(0f, 0f), Vector2.one),
                AABB.FromLowerCenter(new Vector2(10f, 0f), Vector2.one));

            Assert.That(sweepDistance, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sweepDistance, Is.LessThanOrEqualTo(goal.ArrivalTolerance));
            Assert.That(world.IsGoalCompleteAlong(goal, AABB.FromCenterAndSize(new Vector2(0f, 2f), Vector2.one),
                AABB.FromCenterAndSize(new Vector2(10f, 2f), Vector2.one)), Is.True,
                "A body crossing the acceptance region between two samples must still complete.");
            Assert.That(world.IsGoalCompleteAlong(goal, AABB.FromCenterAndSize(new Vector2(-10f, 2f), Vector2.one),
                AABB.FromCenterAndSize(new Vector2(-1f, 2f), Vector2.one)), Is.False);
        }

        /// <summary>Verifies the legacy point factory keeps Euclidean compatibility.</summary>
        [Test]
        public void PointFactoryUsesEuclideanCompatibilityMetric()
        {
            NavigationGoalRequest goal = PointGoal(new Vector2(5f, 3f), 0.1f);

            Assert.That(goal.DistanceMetric, Is.EqualTo(DistanceMetric.Euclidean));
            Assert.That(UnitWorld().GetGoalCompletionDistance(goal, AABB.FromCenterAndSize(new Vector2(1f, 0f), Vector2.one)),
                Is.EqualTo(Mathf.Sqrt(18.5f)).Within(0.0001f));
        }

        /// <summary>Verifies Ground Range lower-center point and segment distances reject non-finite coordinates.</summary>
        [Test]
        public void GroundRangeLowerCenterDistanceQueriesValidateInputs()
        {
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.1f);

            Assert.That(() => goal.DistanceToLowerCenterGoal(new Vector2(float.NaN, 0f), 1f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => goal.DistanceToLowerCenterGoal(new Vector2(float.PositiveInfinity, 0f), 1f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => goal.DistanceToLowerCenterGoalSegment(Vector2.zero,
                new Vector2(float.NegativeInfinity, 0f), 1f), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => goal.DistanceToLowerCenterGoalSegment(new Vector2(float.NaN, 0f),
                Vector2.one, 1f), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies the shared scalar validator rejects every invalid width category.</summary>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void GroundRangeLowerCenterDistanceRejectsInvalidWidth(float invalidWidth)
        {
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.1f);

            Assert.That(() => goal.DistanceToLowerCenterGoal(Vector2.zero, invalidWidth),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies Ground Range lower-center containment validates both body-size components before using width.</summary>
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void GroundRangeLowerCenterBodyRejectsInvalidHeight(float invalidHeight)
        {
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.1f);

            Assert.That(() => goal.DistanceToLowerCenterBody(
                AABB.FromLowerCenter(Vector2.zero, new Vector2(1f, invalidHeight))), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies goal geometry and request construction reject every malformed input category.</summary>
        [Test]
        public void GoalGeometryValidationRejectsMalformedInput()
        {
            AABB bounds = AABB.FromCenterAndSize(-2f, 3f, 2f, 4f);
            Assert.That(() => NavigationGoalRequest.Proximity(
                new AABB(float.NaN, 0f, float.NaN, 1f), DistanceMetric.Euclidean, 0f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRequest.Proximity(bounds, (DistanceMetric)99, 0f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRequest.GroundRange(bounds, float.NaN),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRequest.Retreat(bounds, DistanceMetric.Euclidean, float.PositiveInfinity),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies the public Ground Range factory rejects malformed bounds directly.</summary>
        [Test]
        public void GroundRangeRequestFactoryValidatesBounds()
        {
            // AABB exposes settable corners, so an inverted box must still be rejected at the goal boundary.
            AABB inverted = new(new Vector2(-0.5f, -0.5f), new Vector2(0.5f, 0.5f));
            inverted.Min = new Vector2(2f, -0.5f);
            Assert.That(() => NavigationGoalRequest.GroundRange(inverted, 0f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRequest.GroundRange(
                new AABB(float.NaN, 0f, float.NaN, 1f), 0f),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies the shared scalar validator rejects every invalid arrival tolerance category.</summary>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void GroundRangeRequestFactoryRejectsInvalidTolerance(float invalidTolerance)
        {
            Assert.That(() => NavigationGoalRequest.GroundRange(AABB.Point(Vector3.zero), invalidTolerance), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies public distance queries reject non-finite positions and malformed body sizes.</summary>
        [Test]
        public void NavigationDistanceQueriesValidateInputs()
        {
            NavigationGoalRequest goal = PointGoal(Vector2.zero, 0f);
            Assert.That(() => goal.DistanceToCenteredBody(
                AABB.FromCenterAndSize(new Vector2(float.NaN, 0f), Vector2.one)), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => goal.DistanceToCenteredBody(
                AABB.FromCenterAndSize(Vector2.zero, new Vector2(-1f, 1f))), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => goal.DistanceToLowerCenterBody(
                AABB.FromLowerCenter(Vector2.zero, new Vector2(1f, float.PositiveInfinity))), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRequest.Proximity(AABB.Point(Vector3.zero), (DistanceMetric)99, 0f), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>
        /// Verifies Ground Walk acceptance is the authored horizontal gap plus a fixed one world unit of
        /// foot-height difference: both boundaries are inclusive and everything beyond them is rejected.
        /// </summary>
        [Test]
        public void GroundWalkGoalUsesLowerCenterAndFixedFootHeightAcceptance()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest request = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 9f, 2f, 2f), 0.5f);

            Assert.That(request.TargetBounds.LowerCenter, Is.EqualTo(new Vector2(5f, 8f)));
            Assert.That(request.DistanceToLowerCenterGoal(new Vector2(6.9f, 9f), 0.8f), Is.EqualTo(0f).Within(0.0001f));
            Vector2 bodySize = new(0.8f, 1.5f);
            Vector2 acceptedCenter = new(6.9f, 8.75f);
            Vector2 rejectedCenter = new(7.4f, 8.75f);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(acceptedCenter, bodySize)), Is.True);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(6.9f, 8.75f), bodySize)), Is.True);
            Assert.That(world.IsGoalCompleteAlong(request, AABB.FromCenterAndSize(new Vector2(7.4f, 8.75f), bodySize),
                AABB.FromCenterAndSize(acceptedCenter, bodySize)), Is.True,
                "A fast body that crosses the acceptance band between two samples must still complete.");
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(rejectedCenter, bodySize)), Is.False,
                "Ground Range arrival tolerance must not be applied outside its authored acceptance bounds.");
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(7.4f, 8.75f), bodySize)), Is.False);
            Assert.That(world.IsGoalCompleteAlong(request, AABB.FromCenterAndSize(rejectedCenter, bodySize),
                AABB.FromCenterAndSize(rejectedCenter + Vector2.up * 0.1f, bodySize)), Is.False);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(7.4f, 8.75f), bodySize)), Is.False,
                "A narrow body must not inherit the wider body's horizontal acceptance.");
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(7.4f, 8.75f), new Vector2(2f, 1.5f))), Is.True,
                "Horizontal acceptance must expand by the supplied body half-width.");

            // Vertical acceptance is exactly one world unit of foot-height difference on each side.
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(5f, 7.75f), bodySize)), Is.True);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(5f, 9.75f), bodySize)), Is.True);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(5f, 7.65f), bodySize)), Is.False);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(5f, 9.85f), bodySize)), Is.False);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(new Vector2(7.4f, 9.75f), bodySize)), Is.False,
                "Horizontal and vertical acceptance are independent bounds, so both must hold.");

            Assert.That(request.DistanceToLowerCenterGoal(new Vector2(5f, 9f), 0.8f),
                Is.EqualTo(request.DistanceToLowerCenterGoal(new Vector2(5f, 9f), 4f)).Within(0.0001f));
            Assert.That(request.DistanceToLowerCenterGoalSegment(new Vector2(0f, 8f), new Vector2(5f, 8f), 0.8f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>Verifies centered goal completion does not inherit planner comparison tolerance.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void CenteredGoalRejectsPointOutsideAuthoredArrivalTolerance(bool requiresLineOfSight)
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 8, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = Vector2.one;
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(new Vector2(5f, 1f)), DistanceMetric.Euclidean, 0.5f, requiresLineOfSight);
            Vector2 outside = new(3.9999f, 1f);

            Assert.That(world.GetGoalCompletionDistance(goal, AABB.FromCenterAndSize(outside, bodySize)),
                Is.GreaterThan(goal.ArrivalTolerance));
            Assert.That(world.IsGoalComplete(goal, AABB.FromCenterAndSize(outside, bodySize)), Is.False);
            Assert.That(world.IsGoalCompleteAlong(goal, AABB.FromCenterAndSize(outside, bodySize),
                AABB.FromCenterAndSize(outside, bodySize)), Is.False);
        }

        /// <summary>
        /// Verifies the Ground Walk foot-height band is fixed goal geometry: it is stated in world
        /// units and never derived from the terrain model of the world that evaluates it.
        /// </summary>
        [Test]
        public void GroundWalkAcceptanceIsFixedGoalGeometry()
        {
            NavigationGoalRequest request = NavigationGoalRequest.GroundRange(AABB.Point(new Vector2(2.5f, 3f)), 0.25f);
            TestNavigationWorld world = new(new AABBInt(0, 0, 8, 8), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            TestNavigationWorld fineTerrainWorld = new(new AABBInt(0, 0, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            Vector2 bodySize = new(0.8f, 1.5f);

            Assert.That(request.GetLowerCenterAcceptanceBounds(bodySize.x).Size.y, Is.EqualTo(2f).Within(0.0001f));

            // 0.6 world units above the target feet: inside the fixed one-unit band.
            Vector2 insideBand = new(2.5f, 4.35f);
            Vector2 outsideBand = new(2.5f, 4.76f);
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(insideBand, bodySize)), Is.True);
            Assert.That(fineTerrainWorld.IsGoalComplete(request, AABB.FromCenterAndSize(insideBand, bodySize)), Is.True,
                "The foot-height band must not depend on the world's terrain resolution.");
            Assert.That(world.IsGoalComplete(request, AABB.FromCenterAndSize(outsideBand, bodySize)), Is.False);
            Assert.That(fineTerrainWorld.IsGoalComplete(request, AABB.FromCenterAndSize(outsideBand, bodySize)), Is.False,
                "The foot-height band must not depend on the world's terrain resolution.");
        }

        /// <summary>Verifies a route segment suffix enumerates only real segments and rejects an invalid start.</summary>
        [Test]
        public void NavigationRouteSuffixEnumerationStaysInBounds()
        {
            NavigationRouteSegment first = new GroundRouteSegment(Vector2.zero, Vector2.right);
            NavigationRouteSegment second = new GroundRouteSegment(Vector2.right, new Vector2(2f, 0f));
            NavigationRoute route = NavigationRoute.Complete(PointGoal(new Vector2(2f, 0f), 0f),
                new[] { first, second });

            List<NavigationRouteSegment> whole = new();
            foreach (NavigationRouteSegment segment in route.GetRouteSegments(0)) whole.Add(segment);
            Assert.That(whole, Is.EqualTo(new[] { first, second }));

            List<NavigationRouteSegment> suffix = new();
            foreach (NavigationRouteSegment segment in route.GetRouteSegments(1)) suffix.Add(segment);
            Assert.That(suffix, Is.EqualTo(new[] { second }));

            List<NavigationRouteSegment> empty = new();
            foreach (NavigationRouteSegment segment in route.GetRouteSegments(route.Count)) empty.Add(segment);
            Assert.That(empty, Is.Empty, "A suffix may start at the route's end and stay empty.");

            Assert.That(() => route.GetRouteSegments(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => route.GetRouteSegments(route.Count + 1), Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        /// <summary>Verifies same-center goals with different extents cannot reuse a plan.</summary>
        [Test]
        public void GoalReuseRequiresMatchingTargetExtents()
        {
            NavigationGoalRequest baseline = ProximityGoal(AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.5f);
            NavigationGoalRequest differentExtents = ProximityGoal(AABB.FromCenterAndSize(5f, 3f, 4f, 2f), 0.5f);
            NavigationGoalRequest movedSameExtents = ProximityGoal(AABB.FromCenterAndSize(5.25f, 3f, 2f, 2f), 0.5f);

            Assert.That(baseline.IsReusableFor(differentExtents), Is.False);
            Assert.That(baseline.IsReusableFor(movedSameExtents), Is.True);
        }

        /// <summary>
        /// Verifies Ground Walk reuse bounds horizontal drift by the larger of the target-motion
        /// tolerance and the goal's arrival tolerance, and level drift by the target-motion tolerance
        /// alone, neither being the fixed foot-height acceptance rule.
        /// </summary>
        [Test]
        public void GroundWalkReuseSeparatesHorizontalToleranceFromLevelChange()
        {
            NavigationGoalRequest baseline = NavigationGoalRequest.GroundRange(AABB.Point(new Vector2(5f, 9f)), 3f);
            NavigationGoalRequest smallLevelMove = NavigationGoalRequest.GroundRange(AABB.Point(new Vector2(5f, 10f)), 3f);
            NavigationGoalRequest largeLevelMove = NavigationGoalRequest.GroundRange(AABB.Point(new Vector2(5f, 10.0002f)), 3f);
            NavigationGoalRequest horizontalMove = NavigationGoalRequest.GroundRange(AABB.Point(new Vector2(8f, 9f)), 3f);
            NavigationGoalRequest farHorizontalMove = NavigationGoalRequest.GroundRange(AABB.Point(new Vector2(9.5f, 9f)), 3f);

            Assert.That(baseline.IsReusableFor(smallLevelMove), Is.True);
            Assert.That(baseline.IsReusableFor(largeLevelMove), Is.False);
            Assert.That(baseline.IsReusableFor(horizontalMove), Is.True,
                "A move inside the goal's own arrival tolerance stays reusable.");
            Assert.That(baseline.IsReusableFor(farHorizontalMove), Is.False,
                "A move beyond both the tolerance and the arrival bound is not reusable.");
        }

        /// <summary>Verifies empty routes and segment continuity are checked at the factory boundary.</summary>
        [Test]
        public void NavigationRouteEnforcesContinuityAndEmptyRouteRules()
        {
            Vector2 microscopicOffset = new(0.000005f, 0f);
            NavigationGoalRequest goal = PointGoal(Vector2.right, 0f);
            Assert.That(() => NavigationRoute.Create(goal, Array.Empty<NavigationRouteSegment>(), true),
                Throws.InstanceOf<ArgumentException>(),
                "Only an explicit frame declaration can make a zero-length route.");
            Assert.DoesNotThrow(() => NavigationRoute.Create(goal,
                new NavigationRouteSegment[] { new GroundRouteSegment(new Vector2(0f, 0f), new Vector2(1f, 0f)) }, true));
            Assert.That(() => NavigationRoute.Create(goal,
                new NavigationRouteSegment[]
                {
                    new GroundRouteSegment(Vector2.zero, Vector2.right),
                    new GroundRouteSegment(Vector2.up, Vector2.up),
                }, true), Throws.InstanceOf<ArgumentException>(),
                "Segments must form a continuous world-space chain.");
            Assert.That(() => NavigationRoute.Create(goal,
                new NavigationRouteSegment[]
                {
                    new GroundRouteSegment(Vector2.zero, Vector2.right),
                    new FlyRouteSegment(microscopicOffset, Vector2.right),
                }, true), Throws.InstanceOf<ArgumentException>(),
                "A route cannot mix coordinate frames.");
            Assert.That(() => NavigationRoute.Create(goal,
                new NavigationRouteSegment[] { new GroundRouteSegment(Vector2.zero, Vector2.right),
                    new FlyRouteSegment(Vector2.right + microscopicOffset, Vector2.up) }, true), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies each segment family declares the coordinate frame of its positions.</summary>
        [Test]
        public void NavigationRouteSegmentFamiliesDeclareTheirCoordinateFrame()
        {
            Assert.That(new GroundRouteSegment(Vector2.zero, Vector2.right).CoordinateFrame,
                Is.EqualTo(NavigationRouteCoordinateFrame.GroundAnchor));
            Assert.That(new JumpRouteSegment(Vector2.zero, Vector2.right).CoordinateFrame,
                Is.EqualTo(NavigationRouteCoordinateFrame.GroundAnchor));
            Assert.That(new FallRouteSegment(Vector2.zero, Vector2.right, new Vector2(1f, -1f)).CoordinateFrame,
                Is.EqualTo(NavigationRouteCoordinateFrame.GroundAnchor));
            Assert.That(new DropThroughRouteSegment(Vector2.zero, Vector2.right).CoordinateFrame,
                Is.EqualTo(NavigationRouteCoordinateFrame.GroundAnchor));
            Assert.That(new FlyRouteSegment(Vector2.zero, Vector2.right).CoordinateFrame,
                Is.EqualTo(NavigationRouteCoordinateFrame.BodyCenter));
        }

        /// <summary>Verifies a route rejects segments that speak in two coordinate frames.</summary>
        [Test]
        public void NavigationRouteRejectsMixedCoordinateFrames()
        {
            NavigationGoalRequest goal = PointGoal(new Vector2(2f, 0f), 0f);

            Assert.That(() => NavigationRoute.Complete(goal,
                new NavigationRouteSegment[]
                {
                    new GroundRouteSegment(Vector2.zero, Vector2.right),
                    new FlyRouteSegment(Vector2.right, new Vector2(2f, 0f)),
                }), Throws.InstanceOf<ArgumentException>(),
                "A continuous, correctly spanned chain must still be rejected when its frames differ.");
        }

        /// <summary>Verifies a zero-length route keeps its declared position and coordinate frame.</summary>
        [Test]
        public void NavigationRouteEmptyRouteKeepsZeroLengthPositionAndDeclaredFrame()
        {
            NavigationGoalRequest goal = PointGoal(new Vector2(5f, 2f), 0f);
            Vector2 position = new(5f, 2f);
            NavigationRoute route = NavigationRoute.Empty(position, goal,
                NavigationRouteCoordinateFrame.BodyCenter, true);

            Assert.That(route.Count, Is.Zero);
            Assert.That(route.Start, Is.EqualTo(position));
            Assert.That(route.Endpoint, Is.EqualTo(route.Start));
            Assert.That(route.CoordinateFrame, Is.EqualTo(NavigationRouteCoordinateFrame.BodyCenter));
            Assert.That(route.ReachesGoal, Is.True);
            AABB template = AABB.FromLowerCenter(Vector2.zero, new Vector2(0.8f, 1.5f));
            Assert.That(route.ResolveEndpointBody(template).Center, Is.EqualTo(position),
                "A body-center route does not raise its endpoint.");
            Assert.That(route.ResolveEndpointBody(template).SizeX, Is.EqualTo(template.SizeX).Within(0.0001f));
            Assert.That(route.ResolveEndpointBody(template).SizeY, Is.EqualTo(template.SizeY).Within(0.0001f));
        }

        /// <summary>Verifies endpoint conversion produces the body each route frame declares.</summary>
        [Test]
        public void NavigationRouteResolvesEndpointBodyFromItsSegmentFrame()
        {
            NavigationGoalRequest goal = PointGoal(new Vector2(2f, 1f), 0f);
            Vector2 bodySize = new(0.8f, 1.5f);
            Vector2 endpoint = new(2f, 1f);
            AABB template = AABB.FromCenterAndSize(Vector2.zero, bodySize);
            NavigationRoute groundRoute = NavigationRoute.Complete(goal,
                new NavigationRouteSegment[] { new GroundRouteSegment(Vector2.zero, endpoint) });
            NavigationRoute flyRoute = NavigationRoute.Complete(goal,
                new NavigationRouteSegment[] { new FlyRouteSegment(Vector2.zero, endpoint) });

            Assert.That(groundRoute.CoordinateFrame, Is.EqualTo(NavigationRouteCoordinateFrame.GroundAnchor));
            Assert.That(flyRoute.CoordinateFrame, Is.EqualTo(NavigationRouteCoordinateFrame.BodyCenter));

            AABB resolvedGround = groundRoute.ResolveEndpointBody(template);
            Assert.That(resolvedGround.LowerCenter, Is.EqualTo(endpoint),
                "A ground-anchored endpoint is the resolved body's lower center.");
            Assert.That(resolvedGround.SizeX, Is.EqualTo(bodySize.x).Within(0.0001f));
            Assert.That(resolvedGround.SizeY, Is.EqualTo(bodySize.y).Within(0.0001f));
            Assert.That(resolvedGround.Approximately(AABB.FromLowerCenter(endpoint, bodySize)), Is.True);

            AABB resolvedFly = flyRoute.ResolveEndpointBody(template);
            Assert.That(resolvedFly.Center, Is.EqualTo(endpoint),
                "A body-center endpoint is already the resolved body's center.");
            Assert.That(resolvedFly.SizeX, Is.EqualTo(bodySize.x).Within(0.0001f));
            Assert.That(resolvedFly.SizeY, Is.EqualTo(bodySize.y).Within(0.0001f));
            Assert.That(resolvedFly.Approximately(AABB.FromCenterAndSize(endpoint, bodySize)), Is.True);

            // Every route position, not only the endpoint, is resolved through the route's single frame.
            Assert.That(groundRoute.ResolveBodyAt(groundRoute.Start, template).LowerCenter, Is.EqualTo(groundRoute.Start));
            Assert.That(flyRoute.ResolveBodyAt(flyRoute.Start, template).Center, Is.EqualTo(flyRoute.Start));
        }

        /// <summary>Verifies planning-target reuse measures each geometry's own target position.</summary>
        [Test]
        public void PlanningTargetReuseUsesEachGeometrysTargetPosition()
        {
            NavigationGoalRequest baseline = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.5f);
            NavigationGoalRequest sameTarget = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.5f);
            NavigationGoalRequest levelMoved = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 4f, 2f, 2f), 0.5f);
            NavigationGoalRequest centered = ProximityGoal(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.5f);

            Assert.That(baseline.IsSamePlanningTarget(sameTarget), Is.True);
            Assert.That(baseline.IsSamePlanningTarget(levelMoved), Is.False,
                "Ground Range compares the continuous lower edge of its target, not its center.");
            Assert.That(baseline.IsSamePlanningTarget(centered), Is.False,
                "Different completion geometry is never the same planning target.");
        }

        /// <summary>Verifies lower-center construction produces the body its anchor and size describe.</summary>
        [Test]
        public void AabbLowerCenterConstructionProducesBodyBounds()
        {
            Vector2 lowerCenter = new(2f, 3f);
            Vector2 bodySize = new(0.8f, 1.5f);
            AABB body = AABB.FromLowerCenter(lowerCenter, bodySize);

            Assert.That(body.Min, Is.EqualTo(new Vector2(1.6f, 3f)));
            Assert.That(body.Max, Is.EqualTo(new Vector2(2.4f, 4.5f)));
            Assert.That(body.LowerCenter, Is.EqualTo(lowerCenter));
            Assert.That(body.SizeX, Is.EqualTo(bodySize.x).Within(0.0001f));
            Assert.That(body.SizeY, Is.EqualTo(bodySize.y).Within(0.0001f));
            Assert.That(body.Center, Is.EqualTo(new Vector2(2f, 3.75f)));
        }

        /// <summary>Verifies center construction keeps the meaning it had before the lower-center factory.</summary>
        [Test]
        public void AabbCenterConstructionKeepsItsCenter()
        {
            Vector2 center = new(2f, 3f);
            Vector2 bodySize = new(0.8f, 1.5f);
            AABB body = AABB.FromCenterAndSize(center, bodySize);

            Assert.That(body.Min, Is.EqualTo(new Vector2(1.6f, 2.25f)));
            Assert.That(body.Max, Is.EqualTo(new Vector2(2.4f, 3.75f)));
            Assert.That(body.Center, Is.EqualTo(center));
            Assert.That(body.SizeX, Is.EqualTo(bodySize.x).Within(0.0001f));
            Assert.That(body.SizeY, Is.EqualTo(bodySize.y).Within(0.0001f));
            Assert.That(body.LowerCenter, Is.EqualTo(new Vector2(2f, 2.25f)));
        }

        /// <summary>
        /// Verifies every goal geometry measures the same physical body whichever way that body was
        /// constructed, so an equivalent pose is never a different completion answer.
        /// </summary>
        [Test]
        public void GoalCompletionIsEquivalentAcrossEquivalentBodyPoses()
        {
            TestNavigationWorld world = UnitWorld();
            Vector2 bodySize = new(0.8f, 1.5f);
            Vector2 target = new(5f, 1f);
            Vector2 lowerCenter = new(5f, 1f);
            AABB fromLowerCenter = AABB.FromLowerCenter(lowerCenter, bodySize);
            AABB fromCenter = AABB.FromCenterAndSize(fromLowerCenter.Center, bodySize);
            Assert.That(fromLowerCenter.Approximately(fromCenter), Is.True, "The two constructions must describe one body.");

            NavigationGoalRequest[] goals =
            {
                NavigationGoalRequest.GroundRange(AABB.Point(target), 0.1f),
                NavigationGoalRequest.Proximity(AABB.Point(target), DistanceMetric.Euclidean, 0.1f),
                NavigationGoalRequest.Retreat(AABB.Point(target), DistanceMetric.Euclidean, 4f),
            };

            for (int index = 0; index < goals.Length; index++)
            {
                NavigationGoalRequest goal = goals[index];
                Assert.That(goal.GeometryCompletionDistance(fromLowerCenter),
                    Is.EqualTo(goal.GeometryCompletionDistance(fromCenter)).Within(0.0001f));
                Assert.That(goal.GuidanceDistance(fromLowerCenter),
                    Is.EqualTo(goal.GuidanceDistance(fromCenter)).Within(0.0001f));
                Assert.That(goal.DistanceToCenteredBody(fromLowerCenter),
                    Is.EqualTo(goal.DistanceToCenteredBody(fromCenter)).Within(0.0001f));
                Assert.That(world.IsGoalComplete(goal, fromLowerCenter),
                    Is.EqualTo(world.IsGoalComplete(goal, fromCenter)));
            }
        }

        /// <summary>Verifies a swept body measures the same interval whichever way its poses were constructed.</summary>
        [Test]
        public void GoalSweptCompletionIsEquivalentAcrossEquivalentBodyPoses()
        {
            TestNavigationWorld world = UnitWorld();
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(new Vector2(6f, 1f)), 0.1f);
            AABB startAnchor = AABB.FromLowerCenter(new Vector2(2f, 1f), bodySize);
            AABB endAnchor = AABB.FromLowerCenter(new Vector2(6f, 1f), bodySize);
            AABB startCenter = AABB.FromCenterAndSize(startAnchor.Center, bodySize);
            AABB endCenter = AABB.FromCenterAndSize(endAnchor.Center, bodySize);

            Assert.That(goal.GeometrySweptCompletionDistance(startAnchor, endAnchor),
                Is.EqualTo(goal.GeometrySweptCompletionDistance(startCenter, endCenter)).Within(0.0001f));
            Assert.That(world.IsGoalCompleteAlong(goal, startAnchor, endAnchor),
                Is.EqualTo(world.IsGoalCompleteAlong(goal, startCenter, endCenter)));
            Assert.That(world.IsGoalCompleteAlong(goal, startCenter, endCenter), Is.True,
                "The sweep crosses the acceptance band.");
        }

        /// <summary>Verifies route reuse still compares the goal's own target geometry after the anchor removal.</summary>
        [Test]
        public void RouteReuseComparesGoalTargetGeometryWithoutAnAnchor()
        {
            NavigationGoalRequest planned = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5f, 3f, 2f, 2f), 0.5f);
            NavigationGoalRequest drifted = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(5.05f, 3f, 2f, 2f), 0.5f);
            NavigationGoalRequest moved = NavigationGoalRequest.GroundRange(
                AABB.FromCenterAndSize(9f, 3f, 2f, 2f), 0.5f);

            Assert.That(planned.IsReusableFor(drifted), Is.True,
                "A target inside the reuse tolerance keeps its route.");
            Assert.That(planned.IsReusableFor(moved), Is.False,
                "A target outside the reuse tolerance must re-plan.");
        }

        /// <summary>Verifies navigation parameter constructors reject malformed and negative values.</summary>
        [Test]
        public void NavigationParameterConstructorsValidateValues()
        {
            Assert.DoesNotThrow(() => new WalkNavigationParameters(new Vector2(1, 2), 5, new Vector2(0, -9.81f), 1, 0, 2, 3, 0.02f));
            Assert.That(() => new WalkNavigationParameters(new Vector2(0, 2), 5, Vector2.down, 1, 0, 2, 3, 0.02f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new WalkNavigationParameters(new Vector2(1, 2), -1, Vector2.down, 1, 0, 2, 3, 0.02f),
                Throws.InstanceOf<ArgumentException>());
            Assert.DoesNotThrow(() => new JumpNavigationParameters(new Vector2(1, 2), new Vector2(0, -9.81f), 1, 0, 2, 3, 0.02f));
            Assert.That(() => new JumpNavigationParameters(new Vector2(1, 2), new Vector2(float.NaN, -1), 1, 0, 2, 3, 0.02f),
                Throws.InstanceOf<ArgumentException>());
            Assert.DoesNotThrow(() => new FlyNavigationParameters(new Vector2(1, 2)));
            Assert.That(() => new FlyNavigationParameters(new Vector2(float.NaN, 2)),
                Throws.InstanceOf<ArgumentException>());
            Assert.DoesNotThrow(() => new FlyNavigationParameters(new Vector2(1, 2), 0f, true));
            Assert.That(() => new FlyNavigationParameters(new Vector2(1, 2), -1f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new FlyNavigationParameters(new Vector2(1, 2), 1f, false),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies approach budget geometry observes the closest point inside a segment.</summary>
        [Test]
        public void RetreatApproachDistanceCountsInteriorApproach()
        {
            Assert.That(RetreatNavigationGeometry.SegmentApproachDistance(
                new Vector2(-3f, 0f), new Vector2(3f, 0f), Vector2.zero), Is.EqualTo(3f).Within(0.0001f));
            Assert.That(RetreatNavigationGeometry.RouteApproachDistance(
                new Vector2(-3f, 0f), Vector2.zero,
                new NavigationRouteSegment[] { new FlyRouteSegment(new Vector2(-3f, 0f), new Vector2(3f, 0f)) }),
                Is.EqualTo(3f).Within(0.0001f));
            Assert.That(RetreatNavigationGeometry.RouteApproachDistance(
                new Vector2(-3f, 0f), Vector2.zero,
                new NavigationRouteSegment[]
                {
                    new FlyRouteSegment(new Vector2(-3f, 0f), Vector2.zero),
                    new FlyRouteSegment(Vector2.zero, new Vector2(3f, 0f)),
                }), Is.EqualTo(3f).Within(0.0001f),
                "Approach is charged on the way in, but the later departure does not refund or re-charge it.");
        }

        /// <summary>Creates the shared empty test world used by goal-only contract checks.</summary>
        private static TestNavigationWorld UnitWorld()
            => new(new AABBInt(-16, -16, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());

        private static NavigationGoalRequest PointGoal(Vector2 point, float tolerance)
            => NavigationGoalRequest.Proximity(AABB.Point(point), DistanceMetric.Euclidean, tolerance);

        private static IEnumerable<NavigationTransitionWork> ArtificialTransitions(NavigationSearchNode node)
        {
            if (!node.Identity.Equals(NavigationNodeIdentity.Ground(-1))) yield break;
            yield return NavigationTransitionWork.WorkUnit;
            yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                1, Vector2.right, default, new GroundRouteSegment(Vector2.zero, Vector2.right),
                1f, true));
        }

        private static IEnumerable<NavigationTransitionWork> ArtificialTailTransitions(NavigationSearchNode node)
        {
            if (node.Identity.Equals(NavigationNodeIdentity.Ground(-1)))
            {
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    1, Vector2.right, default, new GroundRouteSegment(Vector2.zero, Vector2.right),
                    1f, false));
                yield break;
            }

            if (node.Identity.Equals(NavigationNodeIdentity.Ground(1)))
            {
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    2, new Vector2(2f, 0f), default,
                    new GroundRouteSegment(Vector2.right, new Vector2(2f, 0f)), 1f, false));
                yield break;
            }

            if (node.Identity.Equals(NavigationNodeIdentity.Ground(2)))
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    3, new Vector2(3f, 0f), default,
                    new GroundRouteSegment(new Vector2(2f, 0f), new Vector2(3f, 0f)),
                    1f, true));
        }

        /// <summary>Verifies a solid edge lying on a body box face is contact, while an edge inside the box blocks the body.</summary>
        [Test]
        public void SolidEdgeOnBodyBoxFaceIsContactRatherThanOccupancy()
        {
            NavigationWorldSnapshot world = CreateGroundWorld(
                new NavigationShapeData(24, 0, NavigationShapeType.Edge,
                    new[] { new Vector2(0f, 1f), new Vector2(4f, 1f) }, 0f, NavigationSurfaceKind.Solid, true),
                new NavigationShapeData(25, 0, NavigationShapeType.Edge,
                    new[] { new Vector2(6f, 1f), new Vector2(6f, 3f) }, 0f, NavigationSurfaceKind.Solid, true));
            AABB restingOnEdge = AABB.FromMinAndSize(1f, 1f, 1f, 1f);
            AABB leaningOnEdge = AABB.FromMinAndSize(6f, 1f, 1f, 1f);
            AABB overlappingEdge = AABB.FromMinAndSize(1f, 0.5f, 1f, 1f);

            Assert.That(world.IsBodyClear(restingOnEdge, 0f), Is.True);
            Assert.That(world.IsBodyClear(leaningOnEdge, 0f), Is.True);
            Assert.That(world.IsBodyClear(overlappingEdge, 0f), Is.False);
        }

        private static NavigationWorldSnapshot CreateGroundWorld(params NavigationShapeData[] shapes)
            => NavigationWorldSnapshot.Create(AABB.FromMinAndSize(0, 0, 8, 4),
                shapes, Array.Empty<NavigationRegionData>());

        private static NavigationGoalRequest ProximityGoal(AABB bounds, float tolerance)
            => NavigationGoalRequest.Proximity(bounds, DistanceMetric.Euclidean, tolerance);

        private static JumpTrajectorySolution CreateSolution(Vector2 start, Vector2 landing, float height, float speed)
        {
            Assert.That(JumpTrajectory.TrySolve(CreateInput(start, landing, height, speed), out JumpTrajectorySolution solution), Is.True);
            return solution;
        }

        private static JumpTrajectoryInput CreateInput(Vector2 start, Vector2 landing, float height, float unusedSpeed)
            => new(start, landing, new Vector2(0, -9.81f), 1, 0, height, 0.02f);

    }
}
