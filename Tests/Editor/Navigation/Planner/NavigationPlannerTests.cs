using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies planner behavior against the package read-only navigation contract.</summary>
    public sealed partial class NavigationPlannerTests
    {
        private static readonly Vector2 Gravity = new(0f, -9.81f);
        private static readonly Vector2 GroundBodySize = new(0.8f, 1.5f);

        /// <summary>Verifies Fly Retreat stops at any reachable cell that satisfies the distance predicate.</summary>
        [Test]
        public void FlyPlannerRetreatFindsAnyReachableCompletedCell()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(AABB.Point(1.5f, 2.5f), DistanceMetric.Euclidean, 2f);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(AABB.FromCenterAndSize(new Vector2(3.5f, 2.5f), bodySize), goal,
                new FlyNavigationParameters(), out NavigationRoute route), Is.True);
            Assert.That(route, Is.Not.Null);
            Assert.That(world.IsGoalComplete(goal, AABB.FromCenterAndSize(route.Endpoint, bodySize)), Is.True);
            Assert.That(route.Segments.Count, Is.GreaterThan(0));
        }

        /// <summary>Verifies a complete Retreat request does not publish a frontier route at its budget.</summary>
        [Test]
        public void FlyPlannerRetreatBudgetReturnsNoRoute()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(AABB.Point(1.5f, 2.5f), DistanceMetric.Euclidean, 3f);

            NavigationPlanResult result = new FlyNavigationPlanner(world, 1).Plan(
                AABB.FromCenterAndSize(new Vector2(3.5f, 2.5f), bodySize), goal, new FlyNavigationParameters());
            NavigationRoute route = result.Route;
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.BudgetReached));
            Assert.That(route, Is.Null);
        }

        /// <summary>Verifies a finite approach budget prunes the target-facing branch while retaining the escape branch.</summary>
        [Test]
        public void FlyPlannerRetreatRespectsApproachBudget()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            Vector2 start = new(4.5f, 2.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(AABB.Point(6.5f, 2.5f), DistanceMetric.Euclidean, 4f);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(AABB.FromCenterAndSize(start, bodySize), goal,
                new FlyNavigationParameters(0.25f), out NavigationRoute route), Is.True);
            Assert.That(world.IsGoalComplete(goal, AABB.FromCenterAndSize(route.Endpoint, bodySize)), Is.True);
            Assert.That(route.Endpoint.x, Is.LessThan(start.x),
                "The finite approach budget must prevent the route from spending its budget toward the target.");
        }

        /// <summary>Verifies an exhausted approach budget still permits escape that does not approach the target.</summary>
        [Test]
        public void FlyPlannerRetreatWithZeroApproachBudgetOnlyEscapesAway()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            Vector2 start = new(4.5f, 2.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(AABB.Point(6.5f, 2.5f), DistanceMetric.Euclidean, 4f);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(AABB.FromCenterAndSize(start, bodySize), goal,
                new FlyNavigationParameters(0f), out NavigationRoute route), Is.True);
            Assert.That(RetreatNavigationGeometry.RouteApproachDistance(start, goal.TargetBounds.Center, route.Segments),
                Is.EqualTo(0f).Within(NavigationConstant.Epsilon));
            Assert.That(route.Endpoint.x, Is.LessThan(start.x));
        }

        /// <summary>Verifies a fully exhausted retreat search returns no path.</summary>
        [Test]
        public void FlyPlannerRetreatExhaustionReturnsNoPath()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(AABB.Point(0.5f, 0.5f), DistanceMetric.Euclidean, 10f);

            Assert.That(new FlyNavigationPlanner(world, 16).TryPlan(
                AABB.FromCenterAndSize(new Vector2(0.5f, 0.5f), new Vector2(0.8f, 0.8f)), goal,
                new FlyNavigationParameters(), out NavigationRoute route), Is.False);
            Assert.That(route, Is.Null);
        }

        /// <summary>Verifies a GroundMove sweep satisfies a Ground Range while traversing the accepted span.</summary>
        [Test]
        public void GroundPlannerAcceptsGroundMoveSweepCrossing()
        {
            TestNavigationWorld world = GroundWorld(0, 3);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(1f, 1f), 0f);

            Assert.That(new WalkNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), bodySize), goal,
                WalkParameters(jumpHeight: 0f, jumpLength: 0f), out NavigationRoute route), Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(world.IsGoalCompleteAlong(goal,
                AABB.FromCenterAndSize(route.Segments[0].Start + Vector2.up * 0.75f, bodySize),
                AABB.FromCenterAndSize(route.Segments[0].End + Vector2.up * 0.75f, bodySize)), Is.True);
        }

        /// <summary>Verifies a jumping Simple step is not terminal merely because its trajectory crosses the goal.</summary>
        [Test]
        public void SimpleGroundPlannerRequiresJumpLandingCompletion()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 4, 6),
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) }, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(1.5f, 1f), 0f);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork<WalkNavigationPlanner, WalkNavigationParameters>(AABB.FromLowerCenter(new Vector2(0.5f, 1f), bodySize),
                goal,
                planner, new WalkNavigationParameters(0f, Gravity, 1f, 0f, 2f, 2.1f, 0.02f), NavigationPlanningExtent.NextAction);

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(world.IsGoalComplete(goal, route.ResolveEndpointBody(AABB.FromLowerCenter(Vector2.zero, bodySize))), Is.False);
            Assert.That(world.IsGoalCompleteAlong(goal,
                AABB.FromCenterAndSize(route.Segments[0].Start + Vector2.up * 0.75f, bodySize),
                AABB.FromCenterAndSize(route.Segments[0].End + Vector2.up * 0.75f, bodySize)), Is.True);
        }

        /// <summary>Verifies a completing Jump takes precedence over an incomplete local Ground action.</summary>
        [Test]
        public void SimpleGroundPlannerPrefersCompletingJumpOverIncompleteGround()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 5, 6),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(3, 0) }, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(3.5f, 1f), DistanceMetric.Euclidean, 0.1f);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork<WalkNavigationPlanner, WalkNavigationParameters>(AABB.FromLowerCenter(new Vector2(0.5f, 1f), bodySize),
                goal,
                planner, WalkParameters(jumpHeight: 2f, jumpLength: 4f), NavigationPlanningExtent.NextAction);

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(world.IsGoalComplete(goal, route.ResolveEndpointBody(AABB.FromLowerCenter(Vector2.zero, bodySize))), Is.True);
        }

        /// <summary>Verifies continuous ground movement stops safely before terrain that requires another action.</summary>
        [TestCase("wall")]
        [TestCase("cliff")]
        [TestCase("step")]
        [TestCase("bounds")]
        public void SimpleGroundPlannerStopsAtTerrainBoundary(string boundary)
        {
            List<Vector2Int> solid = Floor(0, boundary == "cliff" ? 5 : 12);
            if (boundary == "wall" || boundary == "step") solid.Add(new Vector2Int(5, 1));
            if (boundary == "wall") solid.Add(new Vector2Int(5, 2));
            TestNavigationWorld world = new(new AABBInt(0, 0, boundary == "bounds" ? 5 : 12, 6),
                solid, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(10.5f, 1f), 0.1f);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork<WalkNavigationPlanner, WalkNavigationParameters>(AABB.FromLowerCenter(new Vector2(0.8f, 1f), bodySize),
                goal, planner, WalkParameters(),
                NavigationPlanningExtent.NextAction);

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(route.Endpoint.x, Is.GreaterThan(3.5f).And.LessThan(5f));
            Assert.That(route.Endpoint.y, Is.EqualTo(1f));
            Assert.That(world.CanStandAt(AABB.FromLowerCenter(route.Endpoint, bodySize),
                NavigationWorldQueries.SupportSnapDistance, out _), Is.True);
            Assert.That(world.IsGoalComplete(goal, route.ResolveEndpointBody(AABB.FromLowerCenter(Vector2.zero, bodySize))), Is.False);
        }

        /// <summary>Verifies centered planners do not promote their comparison tolerance into goal legality.</summary>
        [Test]
        public void CenteredPlannersRejectStartOutsideAuthoredArrivalTolerance()
        {
            TestNavigationWorld groundWorld = GroundWorld(0, 3);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest walkGoal = NavigationGoalRequest.Proximity(AABB.Point(1.4001f, 1.75f), DistanceMetric.Euclidean, 0.5f);

            Assert.That(new WalkNavigationPlanner(groundWorld, 32, new GroundJumpSolver(groundWorld)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), bodySize), walkGoal,
                WalkParameters(jumpHeight: 0f, jumpLength: 0f), out NavigationRoute walkRoute), Is.True);
            Assert.That(walkRoute.Count, Is.GreaterThan(0));

            TestNavigationWorld flyWorld = new(new AABBInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 flyBodySize = new(0.6f, 0.6f);
            NavigationGoalRequest flyGoal = NavigationGoalRequest.Proximity(AABB.Point(1.3001f, 1.5f), DistanceMetric.Euclidean, 0.5f, true);
            Assert.That(new FlyNavigationPlanner(flyWorld, 32).TryPlan(
                AABB.FromCenterAndSize(new Vector2(0.5f, 1.5f), flyBodySize), flyGoal,
                new FlyNavigationParameters(), out NavigationRoute flyRoute), Is.True);
            Assert.That(flyRoute.Count, Is.GreaterThan(0));
            Assert.That(flyWorld.IsGoalComplete(flyGoal, AABB.FromCenterAndSize(flyRoute.Endpoint, flyBodySize)), Is.True);
        }

        /// <summary>Verifies Jump does not accept a start that only planner comparison tolerance would admit.</summary>
        [Test]
        public void JumpPlannerRejectsStartOutsideAuthoredArrivalTolerance()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 4, 6),
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) }, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(AABB.Point(1.60005f, 1.75f), DistanceMetric.Euclidean, 0.7f);

            Assert.That(world.IsGoalComplete(goal, AABB.FromCenterAndSize(new Vector2(0.5f, 1.75f), bodySize)), Is.False);
            Assert.That(new JumpNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), bodySize), goal,
                new JumpNavigationParameters(Gravity, 1f, 0f, 2f, 2.1f, 0.02f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(world.IsGoalComplete(goal, route.ResolveEndpointBody(AABB.FromLowerCenter(Vector2.zero, bodySize))), Is.True);
        }

        /// <summary>Verifies default Smart Walk uses GroundRange to snap physics contact gaps to the ground route without requiring globally cheapest mixed-search routes.</summary>
        [TestCase(0.005f)]
        [TestCase(0.01f)]
        [TestCase(0.02f)]
        public void GroundPlannerSnapsPhysicsContactGapToSurface(float gap)
        {
            TestNavigationWorld world = GroundWorld(0, 5);
            Vector2 start = new(0.5f, 1f + gap);
            Vector2 goal = new(4.5f, 1.8f);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRequest goalRegion = NavigationGoalRequest.GroundRange(AABB.Point(goal), 1f);

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(start, bodySize), goalRegion, WalkParameters(), out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(route.Start, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(route.Endpoint.y, Is.EqualTo(1f));
            Assert.That(world.IsGoalComplete(goalRegion, route.ResolveEndpointBody(AABB.FromLowerCenter(Vector2.zero, bodySize))), Is.True);
        }

        /// <summary>Verifies a non-grid real start remains the special request origin while search nodes stay discrete.</summary>
        [Test]
        public void GroundPlannerPreservesNonIntegerRequestStart()
        {
            List<Vector2Int> floor = new() { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2) };
            Dictionary<Vector2Int, float> heights = new();
            foreach (Vector2Int cell in floor) heights[cell] = 2.37f;
            TestNavigationWorld world = new(new AABBInt(0, 0, 3, 5), floor, Array.Empty<Vector2Int>(), heights);
            Vector2 start = new(0.63f, 2.37f);

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(start, GroundBodySize),
                Goal(new Vector2(2.5f, 2.37f), 0.1f), WalkParameters(), out NavigationRoute route), Is.True);
            Assert.That(route.Start.x, Is.EqualTo(start.x).Within(0.0001f));
            Assert.That(route.Start.y, Is.EqualTo(start.y).Within(0.0001f));
        }

        /// <summary>Verifies a small adjacent-ground height drift is accepted by the shared contact policy.</summary>
        [Test]
        public void GroundPlannerAcceptsAdjacentHeightDriftWithinContactTolerance()
        {
            Dictionary<Vector2Int, float> heights = new()
            {
                [new Vector2Int(0, 0)] = 1f,
                [new Vector2Int(1, 0)] = 1.005f
            };
            TestNavigationWorld world = new(new AABBInt(0, 0, 2, 4),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, Array.Empty<Vector2Int>(), heights);
            WalkNavigationParameters parameters = WalkParameters(jumpHeight: 0f, jumpLength: 0f);

            Assert.That(new WalkNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(1.5f, 1.005f), 0.01f), parameters, out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
        }

        /// <summary>Verifies adjacent-ground height drift beyond the shared contact policy remains impassable.</summary>
        [Test]
        public void GroundPlannerRejectsAdjacentHeightDriftBeyondContactTolerance()
        {
            Dictionary<Vector2Int, float> heights = new()
            {
                [new Vector2Int(0, 0)] = 1f,
                [new Vector2Int(1, 0)] = 1.02f
            };
            TestNavigationWorld world = new(new AABBInt(0, 0, 2, 4),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, Array.Empty<Vector2Int>(), heights);
            WalkNavigationParameters parameters = WalkParameters(jumpHeight: 0f, jumpLength: 0f);

            Assert.That(new WalkNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(1.5f, 1.02f), 0.01f), parameters, out _), Is.False);
        }

        /// <summary>Verifies jump trajectory clearance uses the shared ground contact policy.</summary>
        [Test]
        public void JumpPlannerAcceptsAdjacentHeightDriftWithinContactTolerance()
        {
            Dictionary<Vector2Int, float> heights = new()
            {
                [new Vector2Int(0, 0)] = 1f,
                [new Vector2Int(1, 0)] = 1.005f
            };
            TestNavigationWorld world = new(new AABBInt(0, 0, 2, 5),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, Array.Empty<Vector2Int>(), heights);
            JumpNavigationParameters parameters = JumpParameters();

            Assert.That(new JumpNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(1.5f, 1.005f), 0.01f), parameters, out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
        }

        /// <summary>Verifies contact tolerance only relaxes the top boundary, not a real side obstruction.</summary>
        [Test]
        public void LowerCenterSegmentStillRejectsSideCollisionWithContactTolerance()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 3, 4),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 1) }, Array.Empty<Vector2Int>());

            Assert.That(world.IsBodyPathClear(AABB.FromLowerCenter(new Vector2(0.5f, 1f), new Vector2(0.8f, 1.5f)),
                new Vector2(1.5f, 1f) - new Vector2(0.5f, 1f), 0.01f), Is.False);
        }

        /// <summary>Verifies an unresolved walking start does not masquerade as an exhausted search.</summary>
        [Test]
        public void GroundPlannerDoesNotRecordExhaustionWhenStartSupportIsUnresolved()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationPlanningDiagnostics diagnostics = new();
            WalkNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            NavigationPlanResult result = planner.Plan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), GroundBodySize),
                Goal(new Vector2(3.5f, 1f), 0.1f), WalkParameters(), CancellationToken.None, diagnostics);
            Assert.That(result.Route, Is.Null);
            Assert.That(diagnostics.ExpansionCount, Is.Zero);
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
        }

        /// <summary>Verifies occupied geometry without a captured top remains blocking but cannot support standing.</summary>
        [Test]
        public void GroundQueriesRejectSolidCellWithoutSupportHeight()
        {
            Vector2Int occupiedCell = new(1, 1);
            TestNavigationWorld world = new(new AABBInt(0, 0, 3, 4), new[] { occupiedCell }, Array.Empty<Vector2Int>(), new Dictionary<Vector2Int, float>());

            Assert.That(world.TryResolveGroundSupport(AABB.FromLowerCenter(new Vector2(1.5f, 2f), new Vector2(0.8f, 1f)), out _, out _), Is.False);
            Assert.That(world.CanStandAt(AABB.FromLowerCenter(new Vector2(1.5f, 2f), new Vector2(0.8f, 1f)), out _), Is.False);
            Assert.That(world.IsBodyClear(AABB.FromLowerCenter(new Vector2(1.5f, 1.9f), new Vector2(0.8f, 1f)), 0f), Is.False);
        }

        /// <summary>Verifies support snapping does not borrow the request-level arrival tolerance.</summary>
        [Test]
        public void GroundPlannerRejectsGapBeyondSupportSnapDistance()
        {
            TestNavigationWorld world = GroundWorld(0, 5);
            Vector2 start = new(0.5f, 1f + NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon);

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(start, GroundBodySize),
                Goal(new Vector2(4.5f, 1f), 10f), WalkParameters(), out _), Is.False);
        }

        /// <summary>Verifies a blocking solid cell is traversed by a shared ballistic jump.</summary>
        [Test]
        public void GroundPlannerJumpsOverObstacle()
        {
            List<Vector2Int> floor = Floor(0, 5);
            floor.Add(new Vector2Int(2, 1));
            TestNavigationWorld world = new(new AABBInt(0, 0, 6, 7), floor, Array.Empty<Vector2Int>());
            Assert.That(new WalkNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(4.5f, 1f), 0.1f), WalkParameters(jumpHeight: 2.5f, jumpLength: 5f), out NavigationRoute route), Is.True);
            Assert.That(ContainsSegment<JumpRouteSegment>(route), Is.True);
        }

        /// <summary>Verifies every planner preserves the physical start and emits a continuous route.</summary>
        [TestCase("Ground")]
        [TestCase("GroundJump")]
        [TestCase("Fly")]
        public void PlannerRoutePreservesExactStartAndContinuity(string caseName)
        {
            Vector2 start;
            NavigationRoute route;

            switch (caseName)
            {
                case "Ground":
                    {
                        TestNavigationWorld world = GroundWorld(0, 3);
                        start = new Vector2(0.50002f, 1f);
                        Assert.That(new WalkNavigationPlanner(world, 64, new GroundJumpSolver(world)).TryPlan(
                            AABB.FromLowerCenter(start, GroundBodySize),
                            Goal(new Vector2(1.5f, 1f), 0.1f),
                            WalkParameters(jumpHeight: 0f, jumpLength: 0f), out route), Is.True);
                        break;
                    }
                case "GroundJump":
                    {
                        List<Vector2Int> floor = Floor(0, 5);
                        floor.Add(new Vector2Int(2, 1));
                        TestNavigationWorld world = new(new AABBInt(0, 0, 6, 7), floor, Array.Empty<Vector2Int>());
                        start = new Vector2(0.50002f, 1f);
                        Assert.That(new WalkNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(
                            AABB.FromLowerCenter(start, GroundBodySize),
                            Goal(new Vector2(4.5f, 1f), 0.1f),
                            WalkParameters(jumpHeight: 2.5f, jumpLength: 2.1f), out route), Is.True);
                        break;
                    }
                case "Fly":
                    {
                        TestNavigationWorld world = new(new AABBInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
                        start = new Vector2(1.500005f, 1.5f);
                        Assert.That(new FlyNavigationPlanner(world, 64).TryPlan(
                            AABB.FromCenterAndSize(start, new Vector2(0.6f, 0.6f)),
                            Goal(new Vector2(1.5f, 1.5f), 0.1f),
                            new FlyNavigationParameters(), out route), Is.True);
                        break;
                    }
                default:
                    Assert.Fail($"Unknown planner contract case '{caseName}'.");
                    return;
            }

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Count, Is.GreaterThan(0));
            Assert.That(route.Start.Equals(start), Is.True);
            Assert.That(route.Segments[0].Start.Equals(start), Is.True);
            for (int index = 1; index < route.Count; index++)
                Assert.That(route.Segments[index - 1].End.Equals(route.Segments[index].Start), Is.True);
            Assert.That(route.Segments[^1].End.Equals(route.Endpoint), Is.True);
        }

        /// <summary>Verifies Unity-style discrete damping does not disable grounded jump planning.</summary>
        [Test]
        public void GroundPlannerSupportsDampedJumpTrajectory()
        {
            List<Vector2Int> floor = Floor(0, 5);
            floor.Add(new Vector2Int(2, 1));
            TestNavigationWorld world = new(new AABBInt(0, 0, 6, 7), floor, Array.Empty<Vector2Int>());

            Assert.That(new WalkNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(4.5f, 1f), 0.1f),
                WalkParameters(jumpHeight: 2.5f, jumpLength: 5f, linearDamping: 5f),
                out NavigationRoute route), Is.True);
            Assert.That(ContainsSegment<JumpRouteSegment>(route), Is.True);
        }

        /// <summary>Verifies full body clearance rejects a passage beneath a low ceiling.</summary>
        [Test]
        public void GroundPlannerRejectsInsufficientClearance()
        {
            List<Vector2Int> solids = Floor(0, 3);
            solids.AddRange(new[] { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2) });
            TestNavigationWorld world = new(new AABBInt(0, 0, 3, 5), solids, Array.Empty<Vector2Int>());
            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), new Vector2(0.8f, 1.5f)),
                Goal(new Vector2(2.5f, 1f), 0.1f), WalkParameters(), out _), Is.False);
        }

        /// <summary>Verifies only an authored one-way surface enables drop-through.</summary>
        [Test]
        public void GroundPlannerDropsThroughOneWaySurface()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 4, 6), new[] { new Vector2Int(1, 0) }, new[] { new Vector2Int(1, 2) });
            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 3f), GroundBodySize),
                Goal(new Vector2(1.5f, 1f), 0.1f), WalkParameters(jumpHeight: 0, jumpLength: 0), out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<DropThroughRouteSegment>());
        }

        /// <summary>Verifies jump-only planning emits a continuous, executable route to a legal endpoint.</summary>
        [Test]
        public void JumpPlannerBuildsOnlyJumpSegments()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 6, 6), new[] { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(4, 0) }, Array.Empty<Vector2Int>());
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            JumpNavigationParameters parameters = JumpParameters();
            using INavigationPlanningWork work = new PlannerWork<JumpNavigationPlanner, JumpNavigationParameters>(AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(4.5f, 1f), 0.1f), planner,
                parameters, NavigationPlanningExtent.Route);
            NavigationRoute route = work.Execute(CancellationToken.None).Route;
            Assert.That(route, Is.Not.Null, "The jump planner should produce a route through the reachable supports.");
            Assert.That(route.Count, Is.GreaterThan(0));
            for (int i = 0; i < route.Count; i++)
                Assert.That(route.Segments[i], Is.TypeOf<JumpRouteSegment>());
            for (int i = 1; i < route.Count; i++)
                Assert.That(route.Segments[i - 1].End.Equals(route.Segments[i].Start), Is.True);
            Assert.That(route.Segments[^1].End.Equals(route.Endpoint), Is.True);
            Assert.That(route.ReachesGoal, Is.True);
        }

        /// <summary>Verifies a Simple Jump action is successful even when its landing is not the final goal.</summary>
        [Test]
        public void SimpleJumpPlannerProducesNonGoalActionAsResult()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 6, 6),
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(4, 0) }, Array.Empty<Vector2Int>());
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));

            NavigationPlanResult result = planner.PlanSingleStep(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(4.5f, 1f), 0.1f), JumpParameters());

            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(result.Route, Is.Not.Null);
            Assert.That(result.Route.ReachesGoal, Is.False);
        }

        /// <summary>Verifies a jump planner with an unresolved start exits before creating a search frontier.</summary>
        [Test]
        public void JumpPlannerDoesNotRecordExhaustionWhenStartSupportIsUnresolved()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationPlanningDiagnostics diagnostics = new();
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            NavigationPlanResult result = planner.Plan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), GroundBodySize),
                Goal(new Vector2(3.5f, 1f), 0.1f), JumpParameters(), CancellationToken.None, diagnostics);
            Assert.That(result.Route, Is.Null);
            Assert.That(diagnostics.ExpansionCount, Is.Zero);
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
        }

        /// <summary>Verifies a Jump request rejects an authored profile that cannot jump at all.</summary>
        [Test]
        public void JumpPlannerRejectsNonPositiveAuthoredJumpHeight()
        {
            TestNavigationWorld world = GroundWorld(0, 3);
            JumpNavigationParameters noJump = new(Gravity, 1f, 0f, 0f, 0f, 0.02f);
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));

            Assert.Throws<ArgumentException>(() => planner.Plan(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize),
                Goal(new Vector2(2.5f, 1f), 0.1f), noJump));
        }

        /// <summary>Verifies a one-way launch can use a bounded upward arc to reach a much lower support.</summary>
        [Test]
        public void JumpPlannerAllowsLowerLandingBeyondJumpHeight()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 4, 8),
                new[] { new Vector2Int(1, 1) },
                new[] { new Vector2Int(1, 4) });

            Assert.That(new JumpNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 5f), new Vector2(0.8f, 1.5f)),
                Goal(new Vector2(1.5f, 2f), 0.1f),
                new JumpNavigationParameters(Gravity, 1f, 0f, 1f, 2.1f, 0.02f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(route.Segments[0].End.y, Is.EqualTo(2f).Within(0.0001f));
        }

        /// <summary>Verifies a solid launch surface still blocks a direct downward jump.</summary>
        [Test]
        public void JumpPlannerDoesNotPassThroughSolidLaunchSurface()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 4, 8),
                new[] { new Vector2Int(1, 1), new Vector2Int(1, 4) },
                Array.Empty<Vector2Int>());

            Assert.That(new JumpNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 5f), new Vector2(0.8f, 1.5f)),
                Goal(new Vector2(1.5f, 2f), 0.1f),
                new JumpNavigationParameters(Gravity, 1f, 0f, 1f, 2.1f, 0.02f),
                out _), Is.False);
        }

        /// <summary>Verifies aerial planning uses a finite grid and body-size clearance.</summary>
        [Test]
        public void FlyPlannerDetoursAroundSolidGeometry()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 7, 5), new[] { new Vector2Int(3, 2) }, Array.Empty<Vector2Int>());
            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(
                AABB.FromCenterAndSize(new Vector2(1.5f, 2.5f), new Vector2(0.6f, 0.6f)),
                Goal(new Vector2(5.5f, 2.5f), 0.1f), new FlyNavigationParameters(), out NavigationRoute route), Is.True);
            Assert.That(route.Count, Is.GreaterThan(1));
        }

        /// <summary>Verifies a Fly Confront request keeps Ground Range lower-center and LOS semantics.</summary>
        [Test]
        public void FlyPlannerConfrontRetainsGroundRangeGoalContract()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 8, 6), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest confrontRequest = NavigationGoalRequest.GroundRange(
                AABB.Point(5.5f, 2.5f), 0.1f, true);
            Vector2 bodySize = new(0.6f, 0.6f);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(
                AABB.FromCenterAndSize(new Vector2(1.5f, 2.5f), bodySize), confrontRequest,
                new FlyNavigationParameters(), out NavigationRoute route), Is.True);
            Assert.That(route.Goal.IsGroundWalk, Is.True);
            Assert.That(route.Goal.RequiresLineOfSight, Is.True);
            Assert.That(world.IsGoalComplete(route.Goal, AABB.FromCenterAndSize(route.Endpoint, bodySize)), Is.True);
            Assert.That(route.Endpoint.y, Is.EqualTo(2.8f).Within(0.0001f));
        }

        /// <summary>Verifies detached aerial work executes a long unobstructed plan to completion.</summary>
        [Test]
        public void FlyPlannerLongDirectSegmentCompletesAsDetachedWork()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 128, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            FlyNavigationPlanner planner = new(world, 256);
            using INavigationPlanningWork work = new PlannerWork<FlyNavigationPlanner, FlyNavigationParameters>(AABB.FromCenterAndSize(new Vector2(0.5f, 1.5f), new Vector2(0.6f, 0.6f)),
                Goal(new Vector2(127.5f, 1.5f), 0.1f),
                planner,
                new FlyNavigationParameters(), NavigationPlanningExtent.Route);

            NavigationRoute route = work.Execute(CancellationToken.None).Route;
            Assert.That(route, Is.Not.Null);
        }

        /// <summary>Verifies synchronous and detached entry points preserve the same public result contract.</summary>
        [Test]
        public void FlyPlannerSyncAndDetachedWorkProduceEquivalentResultsWithoutDiagnostics()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 16, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            FlyNavigationPlanner planner = new(world, 64);
            Vector2 start = new(1.5f, 1.5f);
            NavigationGoalRequest goal = Goal(new Vector2(12.5f, 1.5f), 0.1f);
            Vector2 bodySize = new(0.6f, 0.6f);
            FlyNavigationParameters parameters = new();

            NavigationPlanResult synchronous = planner.Plan(AABB.FromCenterAndSize(start, bodySize), goal, parameters);
            NavigationPlanningDiagnostics diagnostics = new();
            NavigationPlanResult diagnosed = planner.Plan(AABB.FromCenterAndSize(start, bodySize), goal, parameters,
                CancellationToken.None, diagnostics);
            using INavigationPlanningWork work = new PlannerWork<FlyNavigationPlanner, FlyNavigationParameters>(AABB.FromCenterAndSize(start, bodySize),
                goal, planner, parameters, NavigationPlanningExtent.Route);
            NavigationPlanResult detached = work.Execute(CancellationToken.None);

            Assert.That(synchronous.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(detached.Termination, Is.EqualTo(synchronous.Termination));
            Assert.That(diagnosed.Termination, Is.EqualTo(synchronous.Termination));
            Assert.That(detached.Route, Is.Not.Null);
            Assert.That(diagnosed.Route, Is.Not.Null);
            Assert.That(detached.Route.Endpoint, Is.EqualTo(synchronous.Route.Endpoint));
            Assert.That(diagnosed.Route.Endpoint, Is.EqualTo(synchronous.Route.Endpoint));
        }

        /// <summary>Verifies an external assembly can implement the public generic planner contract.</summary>
        [Test]
        public void ExternalPlannerCanImplementPublicPlanContract()
        {
            TestNavigationWorld world = GroundWorld(0, 2);
            NavigationGoalRequest goal = Goal(new Vector2(0.5f, 1f), 0.1f);
            ExternalPlanner planner = new(world);

            NavigationPlanResult result = planner.Plan(AABB.FromLowerCenter(new Vector2(0.5f, 1f), Vector2.one), goal, 17);

            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(result.Route, Is.Not.Null);
            Assert.That(planner.PrepareCount, Is.EqualTo(1));
            Assert.That(planner.ValidateCount, Is.EqualTo(1));
            Assert.That(planner.CoreCount, Is.EqualTo(1));
            Assert.That(planner.PrepareRouteCount, Is.EqualTo(1));
        }

        /// <summary>Verifies detached aerial work resolves a large goal region.</summary>
        [Test]
        public void FlyPlannerLargeGoalCompletesAsDetachedWork()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 32, 8), new[] { new Vector2Int(16, 3) }, Array.Empty<Vector2Int>());
            FlyNavigationPlanner planner = new(world, 256);
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(
                Box(new Vector2(16.5f, 3.5f), new Vector2(12f, 4f)), DistanceMetric.Euclidean, 0.1f);
            using INavigationPlanningWork work = new PlannerWork<FlyNavigationPlanner, FlyNavigationParameters>(AABB.FromCenterAndSize(new Vector2(1.5f, 2.5f), new Vector2(0.6f, 0.6f)),
                goal,
                planner, new FlyNavigationParameters(),
                NavigationPlanningExtent.Route);

            NavigationRoute route = work.Execute(CancellationToken.None).Route;
            Assert.That(route, Is.Not.Null);
        }

        /// <summary>Verifies a body cannot claim support from a cell touched only at its boundary.</summary>
        [Test]
        public void OneWayQueryIgnoresBodyTouchingCellBoundary()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 2, 3), Array.Empty<Vector2Int>(), new[] { new Vector2Int(0, 0) });

            Assert.That(world.CrossesOneWayDown(AABB.FromLowerCenter(new Vector2(-0.5f, 2f), new Vector2(1f, 0f)),
                new Vector2(-0.5f, 1f) - new Vector2(-0.5f, 2f)), Is.False);
        }

        /// <summary>Verifies a wide body may overhang when its foot center has support.</summary>
        [Test]
        public void StandingQueryAllowsWideBodyOverhang()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 4, 3),
                new[] { new Vector2Int(1, 0) },
                Array.Empty<Vector2Int>());

            Assert.That(world.CanStandAt(AABB.FromLowerCenter(new Vector2(1.5f, 1f), new Vector2(2.8f, 1f)), out bool supportIsOneWay), Is.True);
            Assert.That(supportIsOneWay, Is.False);
        }

        /// <summary>Verifies side contact cannot replace support under the foot center.</summary>
        [Test]
        public void StandingQueryRejectsSideOnlySupport()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 4, 3),
                new[] { new Vector2Int(0, 0) },
                Array.Empty<Vector2Int>());

            Assert.That(world.CanStandAt(AABB.FromLowerCenter(new Vector2(1.25f, 1f), new Vector2(2f, 1f)), out _), Is.False);
        }

        /// <summary>Verifies one-way classification comes only from the center support cell.</summary>
        [Test]
        public void StandingQueryClassifiesOnlyCenterSupportCell()
        {
            TestNavigationWorld oneWayCenter = new(
                new AABBInt(0, 0, 4, 3),
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) },
                new[] { new Vector2Int(1, 0) });
            TestNavigationWorld solidCenter = new(
                new AABBInt(0, 0, 4, 3),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) });

            Assert.That(oneWayCenter.CanStandAt(AABB.FromLowerCenter(new Vector2(1.5f, 1f), new Vector2(2.8f, 1f)), out bool oneWay), Is.True);
            Assert.That(oneWay, Is.True);
            Assert.That(solidCenter.CanStandAt(AABB.FromLowerCenter(new Vector2(1.5f, 1f), new Vector2(2.8f, 1f)), out oneWay), Is.True);
            Assert.That(oneWay, Is.False);
        }

        /// <summary>Verifies the full world AABB still rejects a body wider than an open corridor.</summary>
        [Test]
        public void StandingQueryRejectsNarrowCorridor()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 4),
                new[]
                {
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(2, 1),
                },
                Array.Empty<Vector2Int>());

            Assert.That(world.CanStandAt(AABB.FromLowerCenter(new Vector2(1.5f, 1f), new Vector2(1.2f, 1f)), out _), Is.False);
        }

        /// <summary>Verifies invalid planner inputs fail at their owning boundary.</summary>
        [Test]
        public void PlannerRejectsInvalidArguments()
        {
            TestNavigationWorld world = GroundWorld(0, 2);
            Assert.Throws<ArgumentNullException>(() => new WalkNavigationPlanner(null, 8, null));
            Assert.That(() => new FlyNavigationPlanner(world, 8).TryPlan(
                AABB.FromCenterAndSize(new Vector2(float.NaN, 0f), Vector2.one),
                Goal(Vector2.one, 0.1f), new FlyNavigationParameters(), out _),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies a non-canonical start resolves to the authored one-way support.</summary>
        [Test]
        public void GroundPlannerResolvesPartialCellSupportStart_UsesWorldSupportContract()
        {
            Vector2Int supportCell = new(19, 8);
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 24, 12),
                Array.Empty<Vector2Int>(),
                new[] { supportCell },
                new Dictionary<Vector2Int, float> { [supportCell] = 9f });
            Vector2 start = new(19.079f, 9.005f);

            Assert.That(world.TryResolveSupport(AABB.FromLowerCenter(start, new Vector2(0.8f, 1.5f)),
                NavigationWorldQueries.SupportSnapDistance, out NavigationSupport support), Is.True);
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(support.Position.y, Is.EqualTo(9f).Within(0.0001f));

            NavigationPlanningDiagnostics diagnostics = new();
            WalkNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            NavigationPlanResult result = planner.Plan(
                AABB.FromLowerCenter(start, GroundBodySize),
                Goal(new Vector2(19.7f, 9f), 0.1f), WalkParameters(), CancellationToken.None, diagnostics);
            Assert.That(diagnostics.ExpansionCount, Is.GreaterThan(0));
            Assert.That(result.Termination, Is.Not.EqualTo(NavigationPlanTermination.SearchExhausted));
        }

        /// <summary>Verifies a jump-only planner permits a vertical jump with zero horizontal length.</summary>
        [Test]
        public void JumpPlannerAllowsVerticalJumpWithZeroLength_UsesPlannerContract()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new JumpNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), new Vector2(0.8f, 1.5f)),
                Goal(new Vector2(1.5f, 3f), 0.1f),
                new JumpNavigationParameters(Gravity, 1f, 0f, 3.5f, 0f, 0.02f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(route.Segments[0].Start.x, Is.EqualTo(route.Segments[0].End.x).Within(0.0001f));
            Assert.That(((JumpRouteSegment)route.Segments[0]).MinimumApexHeight, Is.GreaterThanOrEqualTo(3f - 0.0001f));
        }

        /// <summary>Verifies the composite ground planner can hand off a vertical jump route.</summary>
        [Test]
        public void GroundPlannerAllowsVerticalJumpWithZeroLength_UsesPlannerContract()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), GroundBodySize),
                Goal(new Vector2(1.5f, 3f), 0.1f),
                WalkParameters(jumpHeight: 3.5f, jumpLength: 0f), out NavigationRoute route), Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(route.Segments[0].Start.x, Is.EqualTo(route.Segments[0].End.x).Within(0.0001f));
        }

        /// <summary>Verifies Smart Ground binds a vertical goal using the body's foot height.</summary>
        [Test]
        public void SmartGroundPlannerUsesFootHeightForVerticalGoal_UsesPlannerContract()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), GroundBodySize),
                new Vector2(1.5f, 3f), 0.1f,
                WalkParameters(jumpHeight: 3.5f, jumpLength: 0f), out NavigationRoute route), Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
        }

        /// <summary>Verifies an upper landing inside the effective apex maximum remains legal.</summary>
        [Test]
        public void GroundPlannerAcceptsUpperLandingWithinEffectiveApexMaximum_UsesPlannerContract()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), GroundBodySize),
                Goal(new Vector2(1.5f, 3f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f), out NavigationRoute route), Is.True);
            Assert.That(route.Segments.Count, Is.EqualTo(1));
            Assert.That(((JumpRouteSegment)route.Segments[0]).MinimumApexHeight,
                Is.LessThanOrEqualTo(3.24f + 0.0001f));
        }

        /// <summary>Verifies an over-high target resolves to a bounded legal landing.</summary>
        [Test]
        public void GroundPlannerRejectsUpperLandingAboveEffectiveApexMaximum_UsesPlannerContract()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });
            WalkNavigationPlanner planner = new(world, 128, new GroundJumpSolver(world));

            Assert.That(planner.TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), GroundBodySize),
                Goal(new Vector2(1.5f, 3f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f), out _), Is.True);
            Assert.That(planner.TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), GroundBodySize),
                Goal(new Vector2(1.5f, 3.25f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f), out NavigationRoute highTargetRoute), Is.True);
            Assert.That(highTargetRoute, Is.Not.Null);
            Assert.That(highTargetRoute.ReachesGoal, Is.True);
            Assert.That(highTargetRoute.Endpoint.y, Is.LessThan(3.25f));
            Assert.That(world.TryResolveSupport(AABB.FromLowerCenter(highTargetRoute.Endpoint, new Vector2(0.8f, 1.5f)),
                NavigationWorldQueries.SupportSnapDistance, out _), Is.True);
            foreach (NavigationRouteSegment segment in highTargetRoute.Segments)
            {
                if (segment is not JumpRouteSegment jump) continue;
                Assert.That(JumpTrajectory.IsApexHeightAllowed(2.99f, jump.MinimumApexHeight), Is.True);
            }
        }

        /// <summary>Verifies jump segments retain directed one-way crossing provenance.</summary>
        [Test]
        public void JumpPlannerRecordsDirectedSurfaceCrossings_UsesPlannerContract()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 8),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) });

            Assert.That(new JumpNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), new Vector2(0.8f, 1.5f)),
                Goal(new Vector2(1.5f, 4f), 0.1f),
                new JumpNavigationParameters(Gravity, 1f, 0f, 5.5f, 0f, 0.02f),
                out NavigationRoute route), Is.True);

            JumpRouteSegment segment = (JumpRouteSegment)route.Segments[0];
            Assert.That(world.IsGoalComplete(route.Goal,
                AABB.FromCenterAndSize(segment.End + Vector2.up * 0.75f, new Vector2(0.8f, 1.5f))), Is.True);
            Assert.That(segment.SurfaceCrossings, Is.Not.Null);
            Assert.That(segment.MinimumApexHeight, Is.LessThanOrEqualTo(5.5f + 0.0001f));
            Assert.That(segment.SurfaceCrossings, Has.Count.GreaterThan(0));

            List<JumpSurfaceCrossing> firstCrossings = new(segment.SurfaceCrossings);
            Assert.That(new JumpNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                AABB.FromLowerCenter(new Vector2(1.5f, 1f), new Vector2(0.8f, 1.5f)),
                Goal(new Vector2(1.5f, 4f), 0.1f),
                new JumpNavigationParameters(Gravity, 1f, 0f, 5.5f, 0f, 0.02f),
                out _), Is.True);
            CollectionAssert.AreEqual(firstCrossings, segment.SurfaceCrossings);
        }

        /// <summary>
        /// Verifies each planner derives its own start from the body pose it is handed: Walk and Jump
        /// measure from the body's lower center, Fly from the body's center. No entrypoint asks its
        /// caller for a start vector, and a planner never reads the other frame's anchor.
        /// </summary>
        [Test]
        public void PlannersDeriveTheirStartFromTheBodyPose()
        {
            Vector2 bodySize = new(0.8f, 1.5f);
            AABB groundBody = AABB.FromLowerCenter(new Vector2(0.5f, 1f), bodySize);
            TestNavigationWorld groundWorld = GroundWorld(0, 3);

            NavigationGoalRequest walkGoal = NavigationGoalRequest.GroundRange(AABB.Point(1f, 1f), 0f);
            Assert.That(new WalkNavigationPlanner(groundWorld, 32, new GroundJumpSolver(groundWorld)).TryPlan(
                groundBody, walkGoal, WalkParameters(jumpHeight: 0f, jumpLength: 0f),
                out NavigationRoute walkRoute), Is.True);
            Assert.That(walkRoute.Start, Is.EqualTo(groundBody.LowerCenter));
            Assert.That(walkRoute.Start, Is.Not.EqualTo(groundBody.Center),
                "A Walk body must plan from its lower center, not from its center.");

            NavigationGoalRequest jumpGoal = NavigationGoalRequest.GroundRange(AABB.Point(1f, 1f), 0.2f);
            Assert.That(new JumpNavigationPlanner(groundWorld, 32, new GroundJumpSolver(groundWorld)).TryPlan(
                groundBody, jumpGoal, JumpParameters(), out NavigationRoute jumpRoute), Is.True);
            Assert.That(jumpRoute.Start, Is.EqualTo(groundBody.LowerCenter));
            Assert.That(jumpRoute.Start, Is.Not.EqualTo(groundBody.Center),
                "A Jump body must plan from its lower center, not from its center.");

            AABB flyBody = AABB.FromCenterAndSize(new Vector2(3.5f, 2.5f), bodySize);
            TestNavigationWorld flyWorld = new(new AABBInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest flyGoal = NavigationGoalRequest.Proximity(
                AABB.Point(new Vector2(5.5f, 2.5f)), DistanceMetric.Euclidean, 0.1f);
            Assert.That(new FlyNavigationPlanner(flyWorld, 128).TryPlan(
                flyBody, flyGoal, new FlyNavigationParameters(), out NavigationRoute flyRoute), Is.True);
            Assert.That(flyRoute.Start, Is.EqualTo(flyBody.Center));
            Assert.That(flyRoute.Start, Is.Not.EqualTo(flyBody.LowerCenter),
                "A Fly body must plan from its center, not from its lower center.");
        }

        /// <summary>Verifies Fly collision geometry always uses the request AABB size.</summary>
        [Test]
        public void FlyPlannerUsesRequestBodySizeForCollisionClearance()
        {
            TestNavigationWorld world = new(new AABBInt(0, 0, 5, 5),
                new[] { new Vector2Int(2, 1) }, Array.Empty<Vector2Int>());
            FlyNavigationPlanner planner = new(world, 64);
            NavigationGoalRequest goal = Goal(new Vector2(1.5f, 3.5f), 0.1f);
            AABB body = AABB.FromCenterAndSize(new Vector2(1.5f, 1.5f), new Vector2(2f, 2f));

            NavigationPlanResult result = planner.Plan(body, goal, new FlyNavigationParameters());

            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
            Assert.That(result.Route, Is.Null);
        }

        [Test]
        public void FlyGoalResolutionObservesCancellationAfterClearanceQuery()
        {
            using CancellationTokenSource cancellation = new();
            TestNavigationWorld world = new CancellationTriggerWorld(
                new AABBInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>(), cancellation, 2);
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(
                AABB.Point(new Vector2(2.5f, 1.5f)), DistanceMetric.Euclidean, 0.1f);

            Assert.Throws<OperationCanceledException>(() => new FlyNavigationPlanner(world, 32).Plan(
                AABB.FromCenterAndSize(new Vector2(0.5f, 1.5f), new Vector2(0.6f, 0.6f)),
                goal, new FlyNavigationParameters(), cancellation.Token));
        }

        [Test]
        public void SimpleWalkObservesCancellationAtSuccessorWorkBoundary()
        {
            using CancellationTokenSource cancellation = new();
            TestNavigationWorld world = new CancellationTriggerWorld(
                new AABBInt(0, 0, 4, 6), Floor(0, 3), Array.Empty<Vector2Int>(), cancellation, 2);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(AABB.Point(3.5f, 1f), 0f);

            Assert.Throws<OperationCanceledException>(() => new WalkNavigationPlanner(world, 32,
                    new GroundJumpSolver(world)).PlanSingleStep(
                    AABB.FromLowerCenter(new Vector2(0.5f, 1f), GroundBodySize), goal,
                    WalkParameters(jumpHeight: 0f, jumpLength: 0f), cancellation.Token));
        }

        /// <summary>Verifies direct planner entry rejects a body with a zero dimension.</summary>
        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        public void PlannerRejectsNonPositiveRequestBody(float width, float height)
        {
            TestNavigationWorld world = GroundWorld(0, 2);
            FlyNavigationPlanner planner = new(world, 8);

            Assert.That(() => planner.Plan(
                AABB.FromCenterAndSize(Vector2.zero, new Vector2(width, height)),
                Goal(Vector2.one, 0.1f), new FlyNavigationParameters()),
                Throws.InstanceOf<ArgumentException>());
        }

        private static TestNavigationWorld GroundWorld(int firstX, int count)
            => NavigationTestWorlds.Ground(firstX, count);

        private static List<Vector2Int> Floor(int firstX, int count)
            => NavigationTestWorlds.Floor(firstX, count);

        private static WalkNavigationParameters WalkParameters(float jumpHeight = 2f, float jumpLength = 4f, float linearDamping = 0f)
            => new(5f, Gravity, 1f, linearDamping, jumpHeight, jumpLength, 0.02f);

        /// <summary>Creates continuous goal geometry from a Bounds-style center and size.</summary>
        private static AABB Box(Vector2 center, Vector2 size) => new(center - size * 0.5f, center + size * 0.5f);

        private static NavigationGoalRequest Goal(Vector2 destination, float arrivalErrorBound)
            => NavigationGoalRequest.Proximity(AABB.Point(destination), DistanceMetric.Euclidean, arrivalErrorBound);

        private static JumpNavigationParameters JumpParameters()
            => new(Gravity, 1f, 0f, 2f, 2.1f, 0.02f);

        private static bool ContainsSegment<TSegment>(NavigationRoute route) where TSegment : NavigationRouteSegment
        {
            for (int i = 0; i < route.Count; i++) if (route.Segments[i] is TSegment) return true;
            return false;
        }

        /// <summary>A test-only planner defined outside the runtime assembly.</summary>
        private sealed class ExternalPlanner : NavigationPlanner<int>
        {
            public override NavigationActions SupportedActions => 0;

            public ExternalPlanner(INavigationWorld world) : base(world, 1)
            {
            }

            public int PrepareCount { get; private set; }
            public int ValidateCount { get; private set; }
            public int CoreCount { get; private set; }
            public int PrepareRouteCount { get; private set; }

            public override NavigationPlanResult Plan(AABB body, NavigationGoalRequest goal,
                int parameters, CancellationToken cancellationToken = default,
                NavigationPlanningDiagnostics diagnostics = null)
            {
                ValidatePlanInputs(body, cancellationToken);
                PrepareCount++;
                ValidateCount++;
                Assert.That(parameters, Is.EqualTo(17));
                CoreCount++;
                PrepareRouteCount++;
                return NavigationPlanResult.ResultProduced(NavigationRoute.Empty(body.LowerCenter, goal,
                    NavigationRouteCoordinateFrame.GroundAnchor, true));
            }
        }

        private sealed class CancellationTriggerWorld : TestNavigationWorld
        {
            private readonly CancellationTokenSource cancellation;
            private readonly int cancelOnBodyClearCall;
            private int bodyClearCalls;

            public CancellationTriggerWorld(AABBInt bounds, IEnumerable<Vector2Int> solidCells,
                IEnumerable<Vector2Int> oneWayCells, CancellationTokenSource cancellation, int cancelOnBodyClearCall)
                : base(bounds, solidCells, oneWayCells)
            {
                this.cancellation = cancellation;
                this.cancelOnBodyClearCall = cancelOnBodyClearCall;
            }

            public override bool IsBodyClear(AABB bodyBounds, float tolerance)
            {
                bool clear = base.IsBodyClear(bodyBounds, tolerance);
                if (++bodyClearCalls == cancelOnBodyClearCall)
                    cancellation.Cancel();
                return clear;
            }
        }

    }
}
