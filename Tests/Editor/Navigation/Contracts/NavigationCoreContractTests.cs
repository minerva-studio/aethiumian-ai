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
            TestNavigationWorld world = new(new RectInt(-2, -2, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(Vector2.right, Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, false, NavigationNodeIdentity.Ground(-1),
                _ => Array.Empty<NavigationTransitionWork>());
            using NavigationSearch search = new NavigationSearch(request);

            Assert.That(() => search.Advance(default, default), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies a positive work budget advances an artificial cross-cell action graph.</summary>
        [Test]
        public void NavigationSearchAdvancesArtificialTransition()
        {
            TestNavigationWorld world = new(new RectInt(-2, -2, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(Vector2.right, Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, false, NavigationNodeIdentity.Ground(-1), node =>
                    ArtificialTransitions(node));
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate update = search.Advance(new NavigationWorkBudget(4, 1000d), default);

            Assert.That(update.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(update.Route, Is.Not.Null);
            Assert.That(update.Route.Segments, Has.Count.EqualTo(1));
        }

        /// <summary>Verifies the node expansion limit does not truncate the active node's successor stream.</summary>
        [Test]
        public void NavigationSearchCompletesActiveNodeAtExpansionLimit()
        {
            TestNavigationWorld world = new(new RectInt(-2, -2, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(Vector2.right, Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 1, false, NavigationNodeIdentity.Ground(-1),
                ArtificialTransitions);
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate firstSlice = search.Advance(new NavigationWorkBudget(1, 1000d), default);
            NavigationSearchUpdate update = search.Advance(new NavigationWorkBudget(1, 1000d), default);

            Assert.That(firstSlice.Status, Is.EqualTo(NavigationSearchStatus.BudgetReached));
            Assert.That(firstSlice.IsTerminal, Is.False);
            Assert.That(update.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(update.ExpandedNodes, Is.EqualTo(1));
        }

        /// <summary>Verifies a multi-step prefix is rebuilt from immutable path history and pauses cleanly.</summary>
        [Test]
        public void NavigationSearchPrefixUsesFirstSegmentAndPauses()
        {
            TestNavigationWorld world = new(new RectInt(-2, -2, 8, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(10f, 0f), Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, true, NavigationNodeIdentity.Ground(-1),
                ArtificialPrefixTransitions);
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate update = search.Advance(new NavigationWorkBudget(3, 1000d), default);
            NavigationSearchUpdate afterPause = search.Advance(new NavigationWorkBudget(3, 1000d), default);

            Assert.That(update.Status, Is.EqualTo(NavigationSearchStatus.ExecutablePrefix));
            Assert.That(update.Route, Is.Not.Null);
            Assert.That(update.Route.Segments, Has.Count.EqualTo(1));
            Assert.That(update.Route.Segments[0].Start, Is.EqualTo(Vector2.zero));
            Assert.That(update.Route.Segments[0].End, Is.EqualTo(Vector2.right));
            Assert.That(update.Route.SearchComplete, Is.False);
            Assert.That(afterPause.Status, Is.EqualTo(NavigationSearchStatus.Pending));
            Assert.That(afterPause.IsTerminal, Is.False);
        }

        /// <summary>Verifies endpoint continuation keeps searching across slices instead of publishing a prefix.</summary>
        [Test]
        public void NavigationSearchContinuationDoesNotPublishPrefix()
        {
            TestNavigationWorld world = new(new RectInt(-2, -2, 8, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(3f, 0f), Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationSearchRequest request = new(world, Vector2.zero, default, goal, Vector2.one,
                NavigationActions.GroundMove, 8, false, NavigationNodeIdentity.Ground(-1),
                ArtificialTailTransitions);
            using NavigationSearch search = new NavigationSearch(request);

            NavigationSearchUpdate first = search.Advance(new NavigationWorkBudget(1, 1000d), default);
            NavigationSearchUpdate second = search.Advance(new NavigationWorkBudget(1, 1000d), default);
            NavigationSearchUpdate third = search.Advance(new NavigationWorkBudget(1, 1000d), default);

            Assert.That(first.Status, Is.EqualTo(NavigationSearchStatus.BudgetReached));
            Assert.That(first.IsTerminal, Is.False);
            Assert.That(second.Status, Is.EqualTo(NavigationSearchStatus.BudgetReached));
            Assert.That(second.IsTerminal, Is.False);
            Assert.That(third.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(third.Route.Segments, Has.Count.EqualTo(3));
        }

        /// <summary>Verifies ground reconnection preserves a validated multi-segment tail.</summary>
        [Test]
        public void GroundRouteReconnectsForwardDriftAndPreservesTail()
        {
            NavigationWorldSnapshot world = CreateGroundWorld(
                new NavigationShapeData(20, 0, NavigationShapeType.Edge,
                    new[] { new Vector2(0f, 1f), new Vector2(7f, 1f) }, 0f,
                    NavigationSurfaceKind.Solid, true));
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(6f, 1f), Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationRoute route = NavigationRoute.Create(new Vector2(1f, 1f), goal, new Vector2(6f, 1f),
                new NavigationRouteSegment[]
                {
                    new GroundRouteSegment(new Vector2(1f, 1f), new Vector2(4f, 1f)),
                    new FlyRouteSegment(new Vector2(4f, 1f), new Vector2(6f, 1f)),
                });

            Assert.That(WalkNavigationPlanner.TryReconnectGroundRoute(
                world, route, new Vector2(1.2f, 1f), new Vector2(0.8f, 0.8f),
                NavigationWorldQueries.SupportSnapDistance, 0.05f,
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
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(5f, 1f), Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationRoute route = NavigationRoute.Create(new Vector2(3f, 1f), goal, new Vector2(5f, 1f),
                new[] { new GroundRouteSegment(new Vector2(3f, 1f), new Vector2(5f, 1f)) });

            Assert.That(WalkNavigationPlanner.TryReconnectGroundRoute(
                world, route, new Vector2(2.81f, 1f), new Vector2(0.8f, 0.8f),
                0.05f, 0.05f, 0.2f, out NavigationRoute reconnected), Is.True);
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
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(6f, 1f), Vector3.zero), DistanceMetric.Euclidean, 0f), world);
            NavigationRoute route = NavigationRoute.Create(new Vector2(4f, 1f), goal, new Vector2(6f, 1f),
                new[] { new GroundRouteSegment(new Vector2(4f, 1f), new Vector2(6f, 1f)) });

            Assert.That(WalkNavigationPlanner.TryReconnectGroundRoute(
                world, route, new Vector2(1.2f, 1f), new Vector2(0.8f, 0.8f),
                NavigationWorldQueries.SupportSnapDistance, 0.05f,
                out _), Is.False);
        }

        /// <summary>Verifies shifted query windows return the same stable candidate identities and coordinates.</summary>
        [Test]
        public void NavigationSupportCandidatesRemainStableAcrossShiftedWindows()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(Vector2.zero, 1f,
                new RectInt(0, 0, 32, 4),
                new[]
                {
                    new NavigationShapeData(1, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(8f, 1f), new Vector2(24f, 1f) }, 0f,
                        NavigationSurfaceKind.Solid, true),
                }, Array.Empty<NavigationRegionData>());
            List<NavigationSupportCandidate> first = new();
            List<NavigationSupportCandidate> second = new();
            world.CollectSupportCandidates(new Rect(11.3004f, 0f, 4f, 3f), new Vector2(0.8f, 0.8f), first);
            world.CollectSupportCandidates(new Rect(11.343741f, 0f, 4f, 3f), new Vector2(0.8f, 0.8f), second);

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
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(Vector2.zero, 1f,
                new RectInt(0, 0, 8, 4),
                new[]
                {
                    new NavigationShapeData(2, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(0f, 1f), new Vector2(7f, 1f) }, 0f,
                        NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, 1f),
                }, Array.Empty<NavigationRegionData>());
            List<NavigationSupportCandidate> directory = new();
            world.CollectSupportCandidates(new Rect(0f, 0f, 8f, 3f), new Vector2(0.8f, 0.8f), directory);
            HashSet<int> expectedIds = new();
            for (int index = 0; index < directory.Count; index++) expectedIds.Add(directory[index].Id);

            for (int iteration = 0; iteration < 16; iteration++)
            {
                List<NavigationSupportCandidate> expansion = new();
                world.CollectSupportCandidates(new Rect(0f, 0f, 8f, 3f), new Vector2(0.8f, 0.8f), expansion);
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
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(Vector2.zero, 1f,
                new RectInt(0, 0, 5, 5),
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
            world.CollectSupportCandidates(new Rect(0f, 0f, 5f, 4f), new Vector2(0.05f, 0.2f), candidates);

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
            Assert.That(execution.TryObserve(Vector2.zero, Vector2.right, Vector2.left,
                2f, 1f, out bool progressed), Is.True);
            Assert.That(progressed, Is.True);
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

        /// <summary>Verifies jump route segments expose geometry without an executable trajectory.</summary>
        [Test]
        public void JumpRouteSegmentCarriesGeometryOnly()
        {
            JumpTrajectorySolution solution = CreateSolution(Vector2.zero, new Vector2(2, 1), 2, 5);
            JumpRouteSegment segment = new(solution.StartPosition, solution.LandingPosition);

            Assert.That(segment.LaunchSupport, Is.EqualTo(solution.StartPosition));
            Assert.That(segment.PlannedLanding, Is.EqualTo(solution.LandingPosition));
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
            NavigationRoute route = NavigationRoute.Create(Vector2.zero, BoundPoint(new Vector2(2, 1), 0f), Vector2.right, source);
            source.Add(new FlyRouteSegment(Vector2.right, new Vector2(2, 1)));

            Assert.That(route.Count, Is.EqualTo(1));
            Assert.That(route.Segments, Is.Not.SameAs(source));
            Assert.Throws<NotSupportedException>(() => ((IList<NavigationRouteSegment>)route.Segments)[0] = null);
        }

        /// <summary>Verifies requested and resolved goals remain distinct route values.</summary>
        [Test]
        public void NavigationRoutePreservesRequestedAndResolvedGoals()
        {
            NavigationRoute route = NavigationRoute.Create(Vector2.zero, BoundPoint(new Vector2(5, 2), 0f), Vector2.right,
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });

            Assert.That(route.RequestedGoal, Is.EqualTo(new Vector2(5, 2)));
            Assert.That(route.ResolvedGoal, Is.EqualTo(Vector2.right));
        }

        /// <summary>Verifies semantic route factories and segment replacement preserve route-owned state.</summary>
        [Test]
        public void NavigationRouteFactoriesDescribeCompletenessAndPreserveStateWhenReplacingSegments()
        {
            NavigationGoalRegion goal = BoundPoint(new Vector2(5f, 2f), 0f);
            NavigationRoute complete = NavigationRoute.Complete(Vector2.zero, goal, Vector2.right,
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });
            NavigationRoute partial = NavigationRoute.Partial(Vector2.zero, goal, Vector2.right,
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });
            NavigationRoute replaced = partial.WithSegments(
                new[] { new FlyRouteSegment(Vector2.zero, Vector2.right) });

            Assert.That(complete.SearchComplete, Is.True);
            Assert.That(partial.SearchComplete, Is.False);
            Assert.That(replaced.Start, Is.EqualTo(partial.Start));
            Assert.That(replaced.GoalRegion, Is.SameAs(partial.GoalRegion));
            Assert.That(replaced.ResolvedGoal, Is.EqualTo(partial.ResolvedGoal));
            Assert.That(replaced.SearchComplete, Is.False);
            Assert.That(replaced.Segments[0], Is.TypeOf<FlyRouteSegment>());
        }

        /// <summary>Verifies named planning-result factories keep route availability separate from termination.</summary>
        [Test]
        public void NavigationPlanResultFactoriesPreserveTerminationAndRouteAvailability()
        {
            NavigationRoute route = NavigationRoute.Complete(Vector2.zero, BoundPoint(Vector2.right, 0f),
                Vector2.right, new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });

            Assert.That(NavigationPlanResult.ResultProduced(route).Termination,
                Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(NavigationPlanResult.ResultProduced(route).Route, Is.SameAs(route));
            Assert.That(NavigationPlanResult.SearchExhausted(route).Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted));
            Assert.That(NavigationPlanResult.SearchExhausted().Route, Is.Null);
            Assert.That(NavigationPlanResult.BudgetReached(route).Termination,
                Is.EqualTo(NavigationPlanTermination.BudgetReached));
            Assert.That(NavigationPlanResult.BudgetReached().Route, Is.Null);
            Assert.That(NavigationPlanResult.NoResult.Termination,
                Is.EqualTo(NavigationPlanTermination.NoResult));
            Assert.That(NavigationPlanResult.NoResult.Route, Is.Null);
        }

        /// <summary>Verifies named search updates are the only owner of their terminal state.</summary>
        [Test]
        public void NavigationSearchUpdateFactoriesSetExpectedTerminalState()
        {
            NavigationRoute route = NavigationRoute.Complete(Vector2.zero, BoundPoint(Vector2.right, 0f),
                Vector2.right, new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) });
            NavigationSearchUpdate pending = NavigationSearchUpdate.Pending(3);
            NavigationSearchUpdate completed = NavigationSearchUpdate.CompletedRoute(route, 4, 5);
            NavigationSearchUpdate prefix = NavigationSearchUpdate.ExecutablePrefix(route, 4, 5);
            NavigationSearchUpdate exhausted = NavigationSearchUpdate.Exhausted(route, 4, 5);
            NavigationSearchUpdate slicedBudget = NavigationSearchUpdate.BudgetReached(null, 4, 5, false);
            NavigationSearchUpdate totalBudget = NavigationSearchUpdate.BudgetReached(route, 4, 5, true);

            Assert.That(pending.Status, Is.EqualTo(NavigationSearchStatus.Pending));
            Assert.That(pending.WorkUnits, Is.Zero);
            Assert.That(pending.ExpandedNodes, Is.EqualTo(3));
            Assert.That(pending.IsTerminal, Is.False);
            Assert.That(completed.Status, Is.EqualTo(NavigationSearchStatus.CompleteRoute));
            Assert.That(completed.Route, Is.SameAs(route));
            Assert.That(completed.IsTerminal, Is.True);
            Assert.That(prefix.Status, Is.EqualTo(NavigationSearchStatus.ExecutablePrefix));
            Assert.That(prefix.IsTerminal, Is.True);
            Assert.That(exhausted.Status, Is.EqualTo(NavigationSearchStatus.Exhausted));
            Assert.That(exhausted.IsTerminal, Is.True);
            Assert.That(slicedBudget.Status, Is.EqualTo(NavigationSearchStatus.BudgetReached));
            Assert.That(slicedBudget.Route, Is.Null);
            Assert.That(slicedBudget.IsTerminal, Is.False);
            Assert.That(totalBudget.Route, Is.SameAs(route));
            Assert.That(totalBudget.IsTerminal, Is.True);
        }

        /// <summary>Verifies named transition factories preserve their graph identity and edge fields.</summary>
        [Test]
        public void NavigationTransitionFactoriesDescribeJumpAndFlyEdges()
        {
            GroundRouteSegment segment = new(Vector2.zero, Vector2.right);
            NavigationTransition completedJump = NavigationTransition.CompletedJump(Vector2.right, segment, 2f);
            NavigationTransition landing = NavigationTransition.JumpLanding(NavigationNodeIdentity.Ground(7),
                Vector2.right, default, segment, 3f, false, 4f);
            NavigationTransition fly = NavigationTransition.FlyMove(new Vector2Int(3, 4), Vector2.right,
                segment, 5f, true, 6f);

            Assert.That(completedJump.Destination, Is.EqualTo(NavigationNodeIdentity.Jump(-1)));
            Assert.That(completedJump.CompletesGoal, Is.True);
            Assert.That(completedJump.ProgressDistance, Is.Zero);
            Assert.That(landing.Destination, Is.EqualTo(NavigationNodeIdentity.Ground(7)));
            Assert.That(landing.Cost, Is.EqualTo(3f));
            Assert.That(landing.ProgressDistance, Is.EqualTo(4f));
            Assert.That(fly.Destination, Is.EqualTo(NavigationNodeIdentity.Fly(new Vector2Int(3, 4))));
            Assert.That(fly.CompletesGoal, Is.True);
            Assert.That(fly.ProgressDistance, Is.EqualTo(6f));
        }

        /// <summary>Verifies rectangular goal regions accept overlap and compute the shortest AABB gap.</summary>
        [Test]
        public void NavigationGoalRegionUsesRectangularDistance()
        {
            NavigationGoalRegion region = BoundProximity(
                new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 4f, 0f)), 0.5f);

            Assert.That(region.DistanceToCenteredBody(new Vector2(1f, 0f), new Vector2(2f, 2f)), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(region.ContainsCenteredBody(new Vector2(4f, 2f), new Vector2(2f, 2f)), Is.True);
            Assert.That(BoundPoint(new Vector2(3f, 4f), 1f).DistanceToCenteredBody(new Vector2(1f, 4f), Vector2.one),
                Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(() => NavigationGoalRequest.Proximity(
                new Bounds(new Vector3(float.NaN, 0f, 0f), Vector3.one), DistanceMetric.Euclidean, 0f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => BoundPoint(Vector2.zero, float.PositiveInfinity), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies all axial AABB metrics handle overlap, boundaries, and negative coordinates.</summary>
        [Test]
        public void NavigationGoalDistanceSupportsAllMetrics()
        {
            Bounds first = new(new Vector3(-5f, -2f, 0f), new Vector3(2f, 2f, 0f));
            Bounds second = new(new Vector3(1f, 3f, 0f), new Vector3(2f, 2f, 0f));
            Assert.That(NavigationGoalRegion.AabbAxialGapDistance(first, second, DistanceMetric.Euclidean), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(NavigationGoalRegion.AabbAxialGapDistance(first, second, DistanceMetric.Manhattan), Is.EqualTo(7f));
            Assert.That(NavigationGoalRegion.AabbAxialGapDistance(first, second, DistanceMetric.Chebyshev), Is.EqualTo(4f));
            Assert.That(NavigationGoalRegion.AabbAxialGapDistance(first, first, DistanceMetric.Manhattan), Is.EqualTo(0f));
            Assert.That(() => NavigationGoalRegion.AabbAxialGapDistance(
                new Bounds(new Vector3(float.NaN, 0f), Vector3.one), first, DistanceMetric.Euclidean),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRegion.AabbAxialGapDistance(
                first, second, (DistanceMetric)99), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies explicit goal requests preserve geometry, metric, LOS, and snapshot identity.</summary>
        [Test]
        public void NavigationGoalRequestBindsExplicitSnapshot()
        {
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(
                new Bounds(new Vector3(-2f, 0f), Vector3.zero), DistanceMetric.Manhattan, 0.25f, true);
            TestNavigationWorld world = new(new RectInt(-4, -4, 8, 8), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRegion region = NavigationGoalRegion.Bind(request, world);

            Assert.That(region.Request.DistanceMetric, Is.EqualTo(DistanceMetric.Manhattan));
            Assert.That(region.RequiresLineOfSight, Is.True);
            Assert.That(region.Snapshot, Is.SameAs(world));
            Assert.That(region.CellSize, Is.EqualTo(0.5f));
            Assert.That(region.GoalKey, Is.EqualTo(new NavigationGoalKey(
                request.TargetBounds, NavigationGoalGeometry.Proximity, DistanceMetric.Manhattan, true, request.ArrivalTolerance, world.CellSize)));
            Assert.That(region.IsComplete(new Vector2(-1.75f, 0f), Vector2.one), Is.True);
        }

        /// <summary>Verifies Approach and Retreat use one threshold with opposite completion directions.</summary>
        [Test]
        public void SharedReachDistanceUsesOppositeApproachAndRetreatDirections()
        {
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion retreat = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Retreat(new Bounds(Vector3.zero, Vector3.zero), DistanceMetric.Euclidean, 2f), world);
            NavigationGoalRegion approach = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(Vector3.zero, Vector3.zero), DistanceMetric.Euclidean, 2f), world);

            Assert.That(retreat.IsRetreat, Is.True);
            Assert.That(retreat.ArrivalErrorBound, Is.Zero);
            Assert.That(retreat.RetreatDistance, Is.EqualTo(2f));
            Assert.That(approach.IsComplete(new Vector2(1.99f, 0f), Vector2.zero), Is.True,
                "Approach must complete at or below the shared reachDistance.");
            Assert.That(approach.IsComplete(new Vector2(2.01f, 0f), Vector2.zero), Is.False);
            Assert.That(retreat.IsComplete(new Vector2(2.01f, 0f), Vector2.zero), Is.True,
                "Retreat must complete at or above the shared reachDistance.");
            Assert.That(retreat.IsComplete(new Vector2(1.99f, 0f), Vector2.zero), Is.False);
            Assert.That(retreat.SweptIsComplete(new Vector2(1f, 0f), new Vector2(2.01f, 0f), Vector2.zero), Is.True);
        }

        /// <summary>Verifies LOS completion is evaluated against the immutable map snapshot, not dynamic physics.</summary>
        [Test]
        public void LineOfSightGoalRejectsSolidSnapshotCell()
        {
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(
                new Bounds(new Vector3(2f, 0f), Vector3.zero), DistanceMetric.Euclidean, 1f, true);
            NavigationGoalRegion clearRegion = NavigationGoalRegion.Bind(request,
                new TestNavigationWorld(new RectInt(0, -2, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>()));
            NavigationGoalRegion blockedRegion = NavigationGoalRegion.Bind(request,
                new TestNavigationWorld(new RectInt(0, -2, 4, 4), new[] { new Vector2Int(1, 0) }, Array.Empty<Vector2Int>()));

            Assert.That(clearRegion.IsComplete(new Vector2(0.5f, 0f), Vector2.one), Is.True);
            Assert.That(blockedRegion.IsComplete(new Vector2(0.5f, 0f), Vector2.one), Is.False);
            Assert.That(blockedRegion.ContainsCenteredBody(new Vector2(0.5f, 0f), Vector2.one), Is.False);
        }

        /// <summary>Verifies LOS sweeps clip geometry before sampling even beyond the former global sample cap.</summary>
        [Test]
        public void LineOfSightSweepFindsNarrowCompletionIntervalAcrossLongSegment()
        {
            const float targetX = 8192.25f;
            RectInt bounds = new(0, 0, 9001, 2);
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(
                new Bounds(new Vector3(targetX, 0.5f), Vector3.zero), DistanceMetric.Euclidean, 0f, true);
            Vector2 bodySize = new(0.1f, 0.1f);
            Vector2 start = new(0.25f, 0.5f);
            Vector2 end = new(9000.25f, 0.5f);

            NavigationGoalRegion clear = NavigationGoalRegion.Bind(request,
                new TestNavigationWorld(bounds, Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>()));
            NavigationGoalRegion blocked = NavigationGoalRegion.Bind(request,
                new TestNavigationWorld(bounds, new[] { new Vector2Int(8192, 0) }, Array.Empty<Vector2Int>()));

            Assert.That(clear.SweptIsComplete(start, end, bodySize), Is.True);
            Assert.That(blocked.SweptIsComplete(start, end, bodySize), Is.False);
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
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion region = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(
                    new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), metric, 0.1f), world);

            Assert.That(region.GuidanceDistance(new Vector2(1f, 0f), Vector2.one), Is.EqualTo(expected).Within(0.0001f));
        }

        /// <summary>Verifies Ground Range guidance remains Euclidean to the raw target AABB center.</summary>
        [Test]
        public void GroundRangeGuidanceUsesRawTargetCenter()
        {
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion region = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(
                    new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), 0.1f), world);

            Assert.That(region.GuidanceDistance(new Vector2(1f, 0f), Vector2.one), Is.EqualTo(5f).Within(0.0001f));
        }

        /// <summary>Verifies Fly-style bounds replacement preserves every non-bounds goal identity field.</summary>
        [Test]
        public void WithTargetBoundsPreservesGoalIdentity()
        {
            Bounds originalBounds = new(new Vector3(2f, 3f, 0f), new Vector3(2f, 4f, 0f));
            Bounds replacementBounds = new(new Vector3(2f, 5f, 0f), new Vector3(2f, 1f, 0f));
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
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRequest request = NavigationGoalRequest.Proximity(
                new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), metric, 0.1f);
            NavigationGoalRegion region = NavigationGoalRegion.Bind(request, world);

            Assert.That(region.Request.DistanceMetric, Is.EqualTo(metric));
            Assert.That(region.CompletionDistance(new Vector2(1f, 0f), Vector2.one),
                Is.EqualTo(expectedBodyDistance).Within(0.0001f));
            Assert.That(region.IsComplete(new Vector2(1f, 0f), Vector2.one), Is.False);
            Assert.That(region.DistanceToLowerCenterBodySegment(new Vector2(0f, 0f), new Vector2(2f, 0f), Vector2.one),
                Is.EqualTo(expectedSegmentDistance).Within(0.0001f));
        }

        /// <summary>Verifies bound Proximity sweep completion finds an interior closest point for every metric.</summary>
        [TestCase(DistanceMetric.Euclidean)]
        [TestCase(DistanceMetric.Manhattan)]
        [TestCase(DistanceMetric.Chebyshev)]
        public void BoundProximitySweepCompletesAtInteriorClosestPoint(DistanceMetric metric)
        {
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRegion region = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(
                    new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), metric, 1f), world);

            float sweepDistance = region.DistanceToLowerCenterBodySegment(
                new Vector2(0f, 0f), new Vector2(10f, 0f), Vector2.one);

            Assert.That(sweepDistance, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(sweepDistance, Is.LessThanOrEqualTo(region.ArrivalErrorBound));
        }

        /// <summary>Verifies the legacy point factory keeps Euclidean compatibility.</summary>
        [Test]
        public void PointFactoryUsesEuclideanCompatibilityMetric()
        {
            NavigationGoalRegion region = BoundPoint(new Vector2(5f, 3f), 0.1f);

            Assert.That(region.Request.DistanceMetric, Is.EqualTo(DistanceMetric.Euclidean));
            Assert.That(region.CompletionDistance(new Vector2(1f, 0f), Vector2.one),
                Is.EqualTo(Mathf.Sqrt(18.5f)).Within(0.0001f));
        }

        /// <summary>Verifies Ground Range lower-center point and segment distances reject non-finite coordinates.</summary>
        [Test]
        public void GroundRangeLowerCenterDistanceQueriesValidateInputs()
        {
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRegion region = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), 0.1f), world);

            Assert.That(() => region.DistanceToLowerCenterGoal(new Vector2(float.NaN, 0f), 1f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => region.DistanceToLowerCenterGoal(new Vector2(float.PositiveInfinity, 0f), 1f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => region.DistanceToLowerCenterGoalSegment(Vector2.zero,
                new Vector2(float.NegativeInfinity, 0f), 1f), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => region.DistanceToLowerCenterGoalSegment(new Vector2(float.NaN, 0f),
                Vector2.one, 1f), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies the shared scalar validator rejects every invalid width category.</summary>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void GroundRangeLowerCenterDistanceRejectsInvalidWidth(float invalidWidth)
        {
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRegion region = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), 0.1f), world);

            Assert.That(() => region.DistanceToLowerCenterGoal(Vector2.zero, invalidWidth),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies Ground Range lower-center containment validates both body-size components before using width.</summary>
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void GroundRangeLowerCenterBodyRejectsInvalidHeight(float invalidHeight)
        {
            TestNavigationWorld world = new(new RectInt(-8, -8, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);
            NavigationGoalRegion region = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), 0.1f), world);

            Assert.That(() => region.ContainsLowerCenterBody(Vector2.zero,
                new Vector2(1f, invalidHeight)), Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies every key field contributes to exact goal identity and bound snapshots require a cell size.</summary>
        [Test]
        public void NavigationGoalKeyDistinguishesAllFields()
        {
            Bounds bounds = new(new Vector3(-2f, 3f), new Vector3(2f, 4f));
            NavigationGoalKey baseline = new(bounds, NavigationGoalGeometry.Proximity, DistanceMetric.Euclidean, false, 0.5f, 1f);
            Assert.That(baseline.Geometry, Is.EqualTo(NavigationGoalGeometry.Proximity));
            Assert.That(baseline, Is.EqualTo(new NavigationGoalKey(bounds, NavigationGoalGeometry.Proximity, DistanceMetric.Euclidean, false, 0.5f, 1f)));
            Assert.That(baseline, Is.Not.EqualTo(new NavigationGoalKey(bounds, NavigationGoalGeometry.GroundRange, DistanceMetric.Euclidean, false, 0.5f, 1f)));
            Assert.That(baseline, Is.Not.EqualTo(new NavigationGoalKey(bounds, NavigationGoalGeometry.Proximity, DistanceMetric.Manhattan, false, 0.5f, 1f)));
            Assert.That(baseline, Is.Not.EqualTo(new NavigationGoalKey(bounds, NavigationGoalGeometry.Proximity, DistanceMetric.Euclidean, true, 0.5f, 1f)));
            Assert.That(baseline, Is.Not.EqualTo(new NavigationGoalKey(bounds, NavigationGoalGeometry.Proximity, DistanceMetric.Euclidean, false, 0.6f, 1f)));
            Assert.That(baseline, Is.Not.EqualTo(new NavigationGoalKey(bounds, NavigationGoalGeometry.Proximity, DistanceMetric.Euclidean, false, 0.5f, 2f)));
            Bounds differentBounds = new(new Vector3(-1f, 3f), new Vector3(2f, 4f));
            Assert.That(baseline, Is.Not.EqualTo(new NavigationGoalKey(differentBounds, NavigationGoalGeometry.Proximity, DistanceMetric.Euclidean, false, 0.5f, 1f)));
            Assert.That(() => NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(bounds, DistanceMetric.Euclidean, 0f), new TestNavigationWorld(
                    new RectInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0f)),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies the public Ground Range factory rejects malformed bounds directly.</summary>
        [Test]
        public void GroundRangeRequestFactoryValidatesBounds()
        {
            Assert.That(() => NavigationGoalRequest.GroundRange(
                new Bounds(Vector3.zero, new Vector3(-1f, 1f, 0f)), 0f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(float.NaN, 0f, 0f), Vector3.one), 0f),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies the shared scalar validator rejects every invalid arrival tolerance category.</summary>
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void GroundRangeRequestFactoryRejectsInvalidTolerance(float invalidTolerance)
        {
            Assert.That(() => NavigationGoalRequest.GroundRange(new Bounds(Vector3.zero, Vector3.zero), invalidTolerance),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies public distance queries reject non-finite positions and malformed body sizes.</summary>
        [Test]
        public void NavigationDistanceQueriesValidateInputs()
        {
            NavigationGoalRegion region = BoundPoint(Vector2.zero, 0f);
            Assert.That(() => region.DistanceToCenteredBody(new Vector2(float.NaN, 0f), Vector2.one),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => region.DistanceToCenteredBody(Vector2.zero, new Vector2(-1f, 1f)),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => region.DistanceToLowerCenterBody(Vector2.zero, new Vector2(1f, float.PositiveInfinity)),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationGoalRequest.Proximity(
                new Bounds(Vector3.zero, Vector3.zero), (DistanceMetric)99, 0f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new NavigationGoalKey(
                new Bounds(Vector3.zero, Vector3.zero), NavigationGoalGeometry.Proximity, DistanceMetric.Euclidean, false, 0f, float.NaN),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies Ground Walk acceptance uses horizontal surface gap and one cell of foot-height tolerance.</summary>
        [Test]
        public void GroundWalkGoalUsesLowerCenterAndCellHeightAcceptance()
        {
            TestNavigationWorld unitWorld = new(new RectInt(0, 0, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest request = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(5f, 9f, 0f), new Vector3(2f, 2f, 0f)), 0.5f);
            NavigationGoalRegion region = NavigationGoalRegion.Bind(request, unitWorld);

            Assert.That(region.Center, Is.EqualTo(new Vector2(5f, 8f)));
            Assert.That(region.DistanceToLowerCenterGoal(new Vector2(6.9f, 9f), 0.8f), Is.EqualTo(0f).Within(0.0001f));
            Vector2 bodySize = new(0.8f, 1.5f);
            Vector2 acceptedCenter = new(6.9f, 8.75f);
            Vector2 rejectedCenter = new(7.4f, 8.75f);
            Assert.That(region.IsComplete(acceptedCenter, bodySize), Is.True);
            Assert.That(region.ContainsLowerCenterBody(new Vector2(6.9f, 8f), bodySize), Is.True);
            Assert.That(region.SweptIsComplete(new Vector2(7.4f, 8.75f), acceptedCenter, bodySize), Is.True);
            Assert.That(region.IsComplete(rejectedCenter, bodySize), Is.False,
                "Ground Range arrival tolerance must not be applied outside its authored acceptance bounds.");
            Assert.That(region.ContainsLowerCenterBody(new Vector2(7.4f, 8f), bodySize), Is.False);
            Assert.That(region.SweptIsComplete(rejectedCenter, rejectedCenter + Vector2.up * 0.1f, bodySize), Is.False);
            Assert.That(region.ContainsLowerCenterGoal(new Vector2(7.4f, 8f), 0.8f), Is.False,
                "A narrow body must not inherit the wider body's horizontal acceptance.");
            Assert.That(region.ContainsLowerCenterGoal(new Vector2(7.4f, 8f), 2f), Is.True,
                "Horizontal acceptance must expand by the supplied body half-width.");
            Assert.That(region.ContainsLowerCenterGoal(new Vector2(5f, 7f), 0.8f), Is.True);
            Assert.That(region.ContainsLowerCenterGoal(new Vector2(5f, 9f), 0.8f), Is.True);
            Assert.That(region.ContainsLowerCenterGoal(new Vector2(5f, 6.9f), 0.8f), Is.False);
            Assert.That(region.ContainsLowerCenterGoal(new Vector2(5f, 9.1f), 0.8f), Is.False);
            Assert.That(region.DistanceToLowerCenterGoal(new Vector2(5f, 9f), 0.8f),
                Is.EqualTo(region.DistanceToLowerCenterGoal(new Vector2(5f, 9f), 4f)).Within(0.0001f));
            Assert.That(region.DistanceToLowerCenterGoalSegment(new Vector2(0f, 8f), new Vector2(5f, 8f), 0.8f),
                Is.EqualTo(0f).Within(0.0001f));
        }

        /// <summary>Verifies centered goal completion does not inherit planner comparison tolerance.</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void CenteredGoalRejectsPointOutsideAuthoredArrivalTolerance(bool requiresLineOfSight)
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 8, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = Vector2.one;
            NavigationGoalRegion region = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(5f, 1f), Vector3.zero),
                    DistanceMetric.Euclidean, 0.5f, requiresLineOfSight), world);
            Vector2 outside = new(3.9999f, 1f);

            Assert.That(region.CompletionDistance(outside, bodySize), Is.GreaterThan(region.ArrivalErrorBound));
            Assert.That(region.IsComplete(outside, bodySize), Is.False);
            Assert.That(region.SweptIsComplete(outside, outside, bodySize), Is.False);
        }

        /// <summary>Verifies explicit snapshots are the only source of Ground Walk cell-height tolerance.</summary>
        [Test]
        public void GroundWalkBinderUsesExplicitSnapshotCellSize()
        {
            NavigationGoalRequest request = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(2.5f, 3f, 0f), Vector3.zero), 0.25f);
            TestNavigationWorld unitWorld = new(new RectInt(0, 0, 8, 8), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 1f);
            TestNavigationWorld halfWorld = new(new RectInt(0, 0, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), 0.5f);

            NavigationGoalRegion unitRegion = NavigationGoalRegion.Bind(request, unitWorld);
            NavigationGoalRegion repeatedRegion = NavigationGoalRegion.Bind(request, unitWorld);
            NavigationGoalRegion halfRegion = NavigationGoalRegion.Bind(request, halfWorld);

            Assert.That(unitRegion.CellSize, Is.EqualTo(1f));
            Assert.That(halfRegion.CellSize, Is.EqualTo(0.5f));
            Assert.That(unitRegion.IsReusableFor(repeatedRegion, 1f), Is.True);
            Assert.That(unitRegion.IsReusableFor(halfRegion, 1f), Is.False);
            Assert.That(unitRegion, Is.Not.SameAs(repeatedRegion));
        }

        /// <summary>Verifies same-center goals with different extents cannot reuse a plan.</summary>
        [Test]
        public void GoalReuseRequiresMatchingTargetExtents()
        {
            NavigationGoalRegion baseline = BoundProximity(
                new Bounds(new Vector3(5f, 3f, 0f), new Vector3(2f, 2f, 0f)), 0.5f);
            NavigationGoalRegion differentExtents = BoundProximity(
                new Bounds(new Vector3(5f, 3f, 0f), new Vector3(4f, 2f, 0f)), 0.5f);
            NavigationGoalRegion movedSameExtents = BoundProximity(
                new Bounds(new Vector3(5.25f, 3f, 0f), new Vector3(2f, 2f, 0f)), 0.5f);

            Assert.That(baseline.IsReusableFor(differentExtents, 1f), Is.False);
            Assert.That(baseline.IsReusableFor(movedSameExtents, 1f), Is.True);
        }

        /// <summary>Verifies Ground Walk replacement separates horizontal tolerance from level changes.</summary>
        [Test]
        public void GroundWalkReplacementUsesBoundsAndCellSizeSemantics()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 16, 16), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion baseline = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(5f, 9f, 0f), Vector3.zero), 3f), world);
            NavigationGoalRegion smallLevelMove = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(5f, 10f, 0f), Vector3.zero), 3f), world);
            NavigationGoalRegion largeLevelMove = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(5f, 10.0002f, 0f), Vector3.zero), 3f), world);
            NavigationGoalRegion horizontalMove = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(8f, 9f, 0f), Vector3.zero), 3f), world);

            Assert.That(baseline.IsReusableFor(smallLevelMove, 3f), Is.True);
            Assert.That(baseline.IsReusableFor(largeLevelMove, 3f), Is.False);
            Assert.That(baseline.IsReusableFor(horizontalMove, 3f), Is.True);
            Assert.That(baseline.IsReusableFor(horizontalMove, 0.1f), Is.False);
        }

        /// <summary>Verifies empty routes and segment continuity are checked at the factory boundary.</summary>
        [Test]
        public void NavigationRouteEnforcesContinuityAndEmptyRouteRules()
        {
            Vector2 microscopicOffset = new(0.000005f, 0f);
            Assert.DoesNotThrow(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.zero, Array.Empty<NavigationRouteSegment>()));
            Assert.DoesNotThrow(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.right,
                new NavigationRouteSegment[] { new GroundRouteSegment(new Vector2(0f, 0f), new Vector2(1f, 0f)) }));
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.up, Array.Empty<NavigationRouteSegment>()),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), microscopicOffset, Array.Empty<NavigationRouteSegment>()),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.right,
                new[] { new GroundRouteSegment(Vector2.up, Vector2.right) }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.right,
                new[] { new GroundRouteSegment(microscopicOffset, Vector2.right) }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.up,
                new NavigationRouteSegment[] { new GroundRouteSegment(Vector2.zero, Vector2.right), new FlyRouteSegment(Vector2.up, Vector2.up) }),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.up,
                new NavigationRouteSegment[] { new GroundRouteSegment(Vector2.zero, Vector2.right),
                    new FlyRouteSegment(Vector2.right + microscopicOffset, Vector2.up) }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.up,
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right) }), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => NavigationRoute.Create(Vector2.zero, BoundPoint(Vector2.right, 0f), Vector2.right,
                new[] { new GroundRouteSegment(Vector2.zero, Vector2.right + microscopicOffset) }),
                Throws.InstanceOf<ArgumentException>());
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

        private static NavigationGoalRegion BoundPoint(Vector2 point, float tolerance)
            => NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(point, Vector3.zero), DistanceMetric.Euclidean, tolerance),
                new TestNavigationWorld(new RectInt(-16, -16, 32, 32), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>()));

        private static IEnumerable<NavigationTransitionWork> ArtificialTransitions(NavigationSearchNode node)
        {
            if (!node.Identity.Equals(NavigationNodeIdentity.Ground(-1))) yield break;
            yield return NavigationTransitionWork.WorkUnit;
            yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                1, Vector2.right, default, new GroundRouteSegment(Vector2.zero, Vector2.right),
                1f, true, 0f));
        }

        private static IEnumerable<NavigationTransitionWork> ArtificialPrefixTransitions(NavigationSearchNode node)
        {
            if (node.Identity.Equals(NavigationNodeIdentity.Ground(-1)))
            {
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    1, Vector2.right, default, new GroundRouteSegment(Vector2.zero, Vector2.right),
                    1f, false, 9f));
                yield break;
            }

            if (node.Identity.Equals(NavigationNodeIdentity.Ground(1)))
            {
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    2, new Vector2(2f, 0f), default,
                    new GroundRouteSegment(Vector2.right, new Vector2(2f, 0f)), 1f, false, 8f));
                yield break;
            }

            if (node.Identity.Equals(NavigationNodeIdentity.Ground(2)))
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    3, new Vector2(3f, 0f), default,
                    new GroundRouteSegment(new Vector2(2f, 0f), new Vector2(3f, 0f)), 1f, false, 7f));
        }

        private static IEnumerable<NavigationTransitionWork> ArtificialTailTransitions(NavigationSearchNode node)
        {
            if (node.Identity.Equals(NavigationNodeIdentity.Ground(-1)))
            {
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    1, Vector2.right, default, new GroundRouteSegment(Vector2.zero, Vector2.right),
                    1f, false, 3f));
                yield break;
            }

            if (node.Identity.Equals(NavigationNodeIdentity.Ground(1)))
            {
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    2, new Vector2(2f, 0f), default,
                    new GroundRouteSegment(Vector2.right, new Vector2(2f, 0f)), 1f, false, 2f));
                yield break;
            }

            if (node.Identity.Equals(NavigationNodeIdentity.Ground(2)))
                yield return NavigationTransitionWork.Edge(NavigationTransition.GroundSuccessor(
                    3, new Vector2(3f, 0f), default,
                    new GroundRouteSegment(new Vector2(2f, 0f), new Vector2(3f, 0f)),
                    1f, true, 0f));
        }

        private static NavigationWorldSnapshot CreateGroundWorld(params NavigationShapeData[] shapes)
            => NavigationWorldSnapshot.Create(Vector2.zero, 1f, new RectInt(0, 0, 8, 4),
                shapes, Array.Empty<NavigationRegionData>());

        private static NavigationGoalRegion BoundProximity(Bounds bounds, float tolerance)
            => NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(bounds, DistanceMetric.Euclidean, tolerance),
                new TestNavigationWorld(new RectInt(-16, -16, 32, 32), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>()));

        private static JumpTrajectorySolution CreateSolution(Vector2 start, Vector2 landing, float height, float speed)
        {
            Assert.That(JumpTrajectory.TrySolve(CreateInput(start, landing, height, speed), out JumpTrajectorySolution solution), Is.True);
            return solution;
        }

        private static JumpTrajectoryInput CreateInput(Vector2 start, Vector2 landing, float height, float unusedSpeed)
            => new(start, landing, new Vector2(0, -9.81f), 1, 0, height, 0.02f);

    }
}
