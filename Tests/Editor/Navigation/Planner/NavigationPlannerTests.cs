using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies planner behavior against the package read-only navigation contract.</summary>
    public sealed class NavigationPlannerTests
    {
        private static readonly Vector2 Gravity = new(0f, -9.81f);

        /// <summary>Verifies the contract maps outside cells to solid.</summary>
        [Obsolete("Legacy occupancy projection; superseded by NavigationPlannerContractTests.WorldOutsideBoundsRejectsBodyClearance.", false)]
        [Explicit("Pending deletion: asserts the retired NavigationCell projection instead of the world-space clearance contract.")]
        [Test]
        public void WorldTreatsOutsideBoundsAsSolid()
        {
            TestNavigationWorld world = new(new RectInt(-2, -1, 4, 3), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Assert.That(world.GetCell(new Vector2Int(-3, -1)), Is.EqualTo(NavigationCell.Solid));
            Assert.That(world.GetCell(new Vector2Int(0, 0)), Is.EqualTo(NavigationCell.Empty));
        }

        /// <summary>Verifies default Smart Walk uses GroundRange to prefer a direct ground step without requiring a globally cheapest mixed-search route.</summary>
        [Obsolete("Legacy planner strategy snapshot; superseded by NavigationPlannerContractTests.WalkPlanner_ReachableGoalProducesACompletingRoute.", false)]
        [Explicit("Pending deletion: requires a direct Ground segment instead of validating the supported reachable-goal outcome.")]
        [Test]
        public void GroundPlannerBuildsDirectPath()
        {
            TestNavigationWorld world = NavigationTestWorlds.Ground(0, 5);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(4.5f, 1f), Vector3.zero), 0.1f), world);

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                new Vector2(0.5f, 1f), goal, WalkParameters(bodySize), out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * (bodySize.y * 0.5f), bodySize), Is.True);
        }

        /// <summary>Verifies Fly Retreat stops at any reachable cell that satisfies the distance predicate.</summary>
        [Test]
        public void FlyPlannerRetreatFindsAnyReachableCompletedCell()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Retreat(new Bounds(new Vector3(1.5f, 2.5f), Vector3.zero), DistanceMetric.Euclidean, 2f), world);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(new Vector2(3.5f, 2.5f), goal,
                new FlyNavigationParameters(bodySize), out NavigationRoute route), Is.True);
            Assert.That(route, Is.Not.Null);
            Assert.That(goal.IsComplete(route.ResolvedGoal, bodySize), Is.True);
            Assert.That(route.Segments, Has.Count.GreaterThan(0));
        }

        /// <summary>Verifies a retreat horizon yields a continuation route rather than claiming success.</summary>
        [Test]
        public void FlyPlannerRetreatPartialRouteIsNotTerminal()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Retreat(new Bounds(new Vector3(1.5f, 2.5f), Vector3.zero), DistanceMetric.Euclidean, 3f), world);

            NavigationPlanResult result = new FlyNavigationPlanner(world, 1).Plan(new Vector2(3.5f, 2.5f), goal, new FlyNavigationParameters(bodySize));
            NavigationRoute route = result.Route;
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.BudgetReached));
            Assert.That(route, Is.Not.Null);
            Assert.That(route.SearchComplete, Is.False);
            Assert.That(goal.IsComplete(route.ResolvedGoal, bodySize), Is.False);
        }

        /// <summary>Verifies a bounded Retreat search prefers the escape boundary over the target center.</summary>
        [Test]
        public void FlyPlannerRetreatPartialRoutePrefersEscapeDirection()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            Vector2 start = new(4.5f, 2.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Retreat(new Bounds(new Vector3(2.5f, 2.5f), Vector3.zero), DistanceMetric.Euclidean, 4f), world);

            Assert.That(new FlyNavigationPlanner(world, 1).TryPlan(start, goal, new FlyNavigationParameters(bodySize), out NavigationRoute route), Is.True);
            Assert.That(route.SearchComplete, Is.False);
            Assert.That(route.ResolvedGoal.x, Is.GreaterThan(start.x),
                "A bounded Retreat search must publish an outward continuation instead of approaching the target center.");
        }

        /// <summary>Verifies a finite approach budget prunes the target-facing branch while retaining the escape branch.</summary>
        [Test]
        public void FlyPlannerRetreatRespectsApproachBudget()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 10, 5), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 0.8f);
            Vector2 start = new(4.5f, 2.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Retreat(new Bounds(new Vector3(6.5f, 2.5f), Vector3.zero), DistanceMetric.Euclidean, 4f), world);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(start, goal, new FlyNavigationParameters(bodySize, 0.25f), out NavigationRoute route), Is.True);
            Assert.That(goal.IsComplete(route.ResolvedGoal, bodySize), Is.True);
            Assert.That(route.ResolvedGoal.x, Is.LessThan(start.x),
                "The finite approach budget must prevent the route from spending its budget toward the target.");
        }

        /// <summary>Verifies a fully exhausted retreat search returns no path.</summary>
        [Test]
        public void FlyPlannerRetreatExhaustionReturnsNoPath()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Retreat(new Bounds(new Vector3(0.5f, 0.5f), Vector3.zero), DistanceMetric.Euclidean, 10f), world);

            Assert.That(new FlyNavigationPlanner(world, 16).TryPlan(new Vector2(0.5f, 0.5f), goal,
                new FlyNavigationParameters(new Vector2(0.8f, 0.8f)), out NavigationRoute route), Is.False);
            Assert.That(route, Is.Null);
        }

        /// <summary>Verifies a GroundMove sweep satisfies a Ground Range while traversing the accepted span.</summary>
        [Test]
        public void GroundPlannerAcceptsGroundMoveSweepCrossing()
        {
            TestNavigationWorld world = GroundWorld(0, 3);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(1f, 1f), Vector3.zero), 0f), world);

            Assert.That(new WalkNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f), goal,
                WalkParameters(bodySize, jumpHeight: 0f, jumpLength: 0f), out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(goal.SweptIsComplete(
                route.Segments[0].Start + Vector2.up * 0.75f,
                route.Segments[0].End + Vector2.up * 0.75f,
                bodySize), Is.True);
        }

        /// <summary>Verifies a jumping Simple step is not terminal merely because its trajectory crosses the goal.</summary>
        [Test]
        public void SimpleGroundPlannerRequiresJumpLandingCompletion()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 4, 6),
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) }, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(1.5f, 1f), Vector3.zero), 0f), world);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.PlanSingleStep(
                new Vector2(0.5f, 1f), goal,
                new WalkNavigationParameters(bodySize, 0f, Gravity, 1f, 0f, 2f, 2.1f, 0.02f), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * 0.75f, bodySize), Is.False);
            Assert.That(goal.SweptIsComplete(
                route.Segments[0].Start + Vector2.up * 0.75f,
                route.Segments[0].End + Vector2.up * 0.75f,
                bodySize), Is.True);
        }

        /// <summary>Verifies Simple Walk reaches a distant goal in one ground action, including off-center starts.</summary>
        [TestCase(0.5f, 8.5f, Explicit = true, Reason = "Pending deletion: fixes a Ground-over-Jump candidate preference with no supported route-order contract.")]
        [TestCase(0.8f, 8.5f, Explicit = true, Reason = "Pending deletion: fixes a Ground-over-Jump candidate preference with no supported route-order contract.")]
        [TestCase(8.2f, 0.5f, Explicit = true, Reason = "Pending deletion: fixes a Ground-over-Jump candidate preference with no supported route-order contract.")]
        [Obsolete("Legacy planner candidate-order snapshot; superseded by outcome-based NavigationPlannerContractTests coverage.", false)]
        [Explicit("Pending deletion: fixes a Ground-over-Jump candidate preference with no supported route-order contract.")]
        public void SimpleGroundPlannerPrefersContinuousGroundOverJump(float startX, float targetX)
        {
            TestNavigationWorld world = GroundWorld(0, 10);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(targetX, 1f), Vector3.zero),
                    DistanceMetric.Euclidean, 0.1f), world);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.PlanSingleStep(
                new Vector2(startX, 1f), goal, WalkParameters(bodySize, jumpHeight: 2f, jumpLength: 4f), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(route.Segments[0].Start, Is.EqualTo(new Vector2(startX, 1f)));
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * 0.75f, bodySize), Is.True);
        }

        /// <summary>Verifies a completing Jump takes precedence over an incomplete local Ground action.</summary>
        [Test]
        public void SimpleGroundPlannerPrefersCompletingJumpOverIncompleteGround()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 5, 6),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(3, 0) }, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(3.5f, 1f), Vector3.zero),
                    DistanceMetric.Euclidean, 0.1f), world);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.PlanSingleStep(
                new Vector2(0.5f, 1f), goal, WalkParameters(bodySize, jumpHeight: 2f, jumpLength: 4f), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * 0.75f, bodySize), Is.True);
        }

        /// <summary>Verifies completed local and Jump candidates use cost as their shared primary tie-breaker.</summary>
        [Obsolete("Legacy planner candidate-order snapshot; superseded by outcome-based NavigationPlannerContractTests coverage.", false)]
        [Explicit("Pending deletion: fixes a private cost tie-break between otherwise legal completing candidates.")]
        [Test]
        public void SimpleGroundPlannerPrefersLowerCostCompletedGroundOverJump()
        {
            TestNavigationWorld world = GroundWorld(0, 10);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(4.5f, 1f), Vector3.zero),
                    DistanceMetric.Euclidean, 0.1f), world);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.PlanSingleStep(
                new Vector2(0.5f, 1f), goal, WalkParameters(bodySize, jumpHeight: 2f, jumpLength: 4f), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * 0.75f, bodySize), Is.True);
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
            TestNavigationWorld world = new(new RectInt(0, 0, boundary == "bounds" ? 5 : 12, 6),
                solid, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(10.5f, 1f), Vector3.zero), 0.1f), world);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.PlanSingleStep(
                new Vector2(0.8f, 1f), goal, WalkParameters(bodySize), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(route.ResolvedGoal.x, Is.GreaterThan(3.5f).And.LessThan(5f));
            Assert.That(route.ResolvedGoal.y, Is.EqualTo(1f));
            Assert.That(world.CanStandAt(route.ResolvedGoal, bodySize,
                WalkParameters(bodySize).SupportSnapDistance, out _), Is.True);
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * 0.75f, bodySize), Is.False);
        }

        /// <summary>Verifies Simple Walk rejects symmetric candidates that improve neither completion nor guidance.</summary>
        [Obsolete("Legacy planner candidate-filter snapshot; no supported caller-visible contract depends on this plateau heuristic.", false)]
        [Explicit("Pending deletion: asserts a private neutral-candidate rejection heuristic instead of route legality or goal completion.")]
        [Test]
        public void SimpleGroundPlannerRejectsNeutralPlateauCandidates()
        {
            TestNavigationWorld world = new(new RectInt(-3, 0, 7, 7),
                new[] { new Vector2Int(-2, 0), new Vector2Int(0, 0), new Vector2Int(2, 0) }, Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(new Vector3(0.5f, 5f), Vector3.zero), 0f), world);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.PlanSingleStep(
                new Vector2(0.5f, 1f), goal, WalkParameters(jumpHeight: 2f, jumpLength: 2.1f), cancellationToken));

            Assert.That(work.Execute(CancellationToken.None).Route, Is.Null);
        }

        /// <summary>Verifies wrong-floor completion plateaus use raw-target guidance without changing legality.</summary>
        [Obsolete("Legacy planner guidance snapshot; no supported caller-visible contract depends on this plateau choice.", false)]
        [Explicit("Pending deletion: asserts a private wrong-floor guidance choice instead of route legality or goal completion.")]
        [Test]
        public void SimpleGroundPlannerUsesGuidanceToImproveWrongFloorPlateau()
        {
            TestNavigationWorld world = new(new RectInt(-3, 0, 7, 7),
                new[] { new Vector2Int(-2, 0), new Vector2Int(0, 0), new Vector2Int(2, 0) }, Array.Empty<Vector2Int>());
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(
                    new Bounds(new Vector3(2.5f, 5f), new Vector3(10f, 0f, 0f)), 0f), world);
            WalkNavigationPlanner planner = new(world, 32, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.PlanSingleStep(
                new Vector2(0.5f, 1f), goal, WalkParameters(jumpHeight: 2f, jumpLength: 2.1f), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;

            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.ResolvedGoal.x, Is.GreaterThan(0.5f));
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * 0.75f, new Vector2(0.8f, 1.5f)), Is.False);
        }

        /// <summary>Verifies centered planners do not promote their comparison tolerance into goal legality.</summary>
        [Test]
        public void CenteredPlannersRejectStartOutsideAuthoredArrivalTolerance()
        {
            TestNavigationWorld groundWorld = GroundWorld(0, 3);
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion walkGoal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(1.4001f, 1.75f), Vector3.zero),
                    DistanceMetric.Euclidean, 0.5f), groundWorld);

            Assert.That(new WalkNavigationPlanner(groundWorld, 32, new GroundJumpSolver(groundWorld)).TryPlan(
                new Vector2(0.5f, 1f), walkGoal,
                WalkParameters(bodySize, jumpHeight: 0f, jumpLength: 0f), out NavigationRoute walkRoute), Is.True);
            Assert.That(walkRoute.Count, Is.GreaterThan(0));

            TestNavigationWorld flyWorld = new(new RectInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 flyBodySize = new(0.6f, 0.6f);
            NavigationGoalRegion flyGoal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(1.3001f, 1.5f), Vector3.zero),
                    DistanceMetric.Euclidean, 0.5f, true), flyWorld);
            Assert.That(new FlyNavigationPlanner(flyWorld, 32).TryPlan(
                new Vector2(0.5f, 1.5f), flyGoal,
                new FlyNavigationParameters(flyBodySize), out NavigationRoute flyRoute), Is.True);
            Assert.That(flyRoute.Count, Is.GreaterThan(0));
            Assert.That(flyGoal.IsComplete(flyRoute.ResolvedGoal, flyBodySize), Is.True);
        }

        /// <summary>Verifies Jump does not accept a start that only planner comparison tolerance would admit.</summary>
        [Test]
        public void JumpPlannerRejectsStartOutsideAuthoredArrivalTolerance()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 4, 6),
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) }, Array.Empty<Vector2Int>());
            Vector2 bodySize = new(0.8f, 1.5f);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(new Vector3(1.60005f, 1.75f), Vector3.zero),
                    DistanceMetric.Euclidean, 0.7f), world);

            Assert.That(goal.IsComplete(new Vector2(0.5f, 1.75f), bodySize), Is.False);
            Assert.That(new JumpNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f), goal,
                new JumpNavigationParameters(bodySize, Gravity, 1f, 0f, 2f, 2.1f, 0.02f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(goal.IsComplete(route.ResolvedGoal + Vector2.up * 0.75f, bodySize), Is.True);
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
            NavigationGoalRegion goalRegion = NavigationGoalRegion.Bind(
                NavigationGoalRequest.GroundRange(new Bounds(goal, Vector3.zero), 1f), world);

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(
                start, goalRegion, WalkParameters(bodySize), out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(route.Start, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(route.ResolvedGoal.y, Is.EqualTo(1f));
            Assert.That(goalRegion.IsComplete(route.ResolvedGoal + Vector2.up * (bodySize.y * 0.5f), bodySize), Is.True);
        }

        /// <summary>Verifies grounded planning uses a captured non-integer support height.</summary>
        [Obsolete("Legacy occupancy projection; superseded by NavigationPlannerContractTests.WorldResolvesAuthoredSolidSupportHeightWithoutCellProjection.", false)]
        [Explicit("Pending deletion: asserts NavigationCell and cell coordinates where world-space support identity and height are the supported contract.")]
        [Test]
        public void GroundPlannerUsesCapturedNonIntegerSupportHeight()
        {
            List<Vector2Int> floor = new() { new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5) };
            Dictionary<Vector2Int, float> heights = new();
            foreach (Vector2Int cell in floor) heights[cell] = 5.38f;
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 8), floor, Array.Empty<Vector2Int>(), heights);

            Assert.That(world.TryResolveGroundSupport(new Vector2(1.5f, 5.38f), new Vector2(0.8f, 1.5f),
                out Vector2 snapped, out Vector2Int supportCell, out NavigationCell support), Is.True);
            Assert.That(supportCell, Is.EqualTo(new Vector2Int(1, 5)));
            Assert.That(support, Is.EqualTo(NavigationCell.Solid));
            Assert.That(snapped.y, Is.EqualTo(5.38f).Within(0.0001f));

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 5.38f),
                Goal(world, new Vector2(2.5f, 5.38f), 0.1f), WalkParameters(), out NavigationRoute route), Is.True);
            Assert.That(route.Start.y, Is.EqualTo(5.38f).Within(0.0001f));
            Assert.That(route.ResolvedGoal.y, Is.EqualTo(5.38f).Within(0.0001f));
        }

        /// <summary>Verifies a non-grid real start remains the special request origin while search nodes stay discrete.</summary>
        [Test]
        public void GroundPlannerPreservesNonIntegerRequestStart()
        {
            List<Vector2Int> floor = new() { new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2) };
            Dictionary<Vector2Int, float> heights = new();
            foreach (Vector2Int cell in floor) heights[cell] = 2.37f;
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 5), floor, Array.Empty<Vector2Int>(), heights);
            Vector2 start = new(0.63f, 2.37f);

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(start,
                Goal(world, new Vector2(2.5f, 2.37f), 0.1f), WalkParameters(), out NavigationRoute route), Is.True);
            Assert.That(route.Start.x, Is.EqualTo(start.x).Within(0.0001f));
            Assert.That(route.Start.y, Is.EqualTo(start.y).Within(0.0001f));
        }

        /// <summary>Verifies a small adjacent-ground height drift is accepted by the captured contact tolerance.</summary>
        [Test]
        public void GroundPlannerAcceptsAdjacentHeightDriftWithinContactTolerance()
        {
            Dictionary<Vector2Int, float> heights = new()
            {
                [new Vector2Int(0, 0)] = 1f,
                [new Vector2Int(1, 0)] = 1.005f
            };
            TestNavigationWorld world = new(new RectInt(0, 0, 2, 4),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, Array.Empty<Vector2Int>(), heights);
            WalkNavigationParameters parameters = WalkParameters(jumpHeight: 0f, jumpLength: 0f)
                .WithGroundContactTolerance(0.01f);

            Assert.That(new WalkNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f),
                Goal(world, new Vector2(1.5f, 1.005f), 0.01f), parameters, out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
        }

        /// <summary>Verifies adjacent-ground height drift beyond contact tolerance remains impassable.</summary>
        [Test]
        public void GroundPlannerRejectsAdjacentHeightDriftBeyondContactTolerance()
        {
            Dictionary<Vector2Int, float> heights = new()
            {
                [new Vector2Int(0, 0)] = 1f,
                [new Vector2Int(1, 0)] = 1.02f
            };
            TestNavigationWorld world = new(new RectInt(0, 0, 2, 4),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, Array.Empty<Vector2Int>(), heights);
            WalkNavigationParameters parameters = WalkParameters(jumpHeight: 0f, jumpLength: 0f)
                .WithGroundContactTolerance(0.01f);

            Assert.That(new WalkNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f),
                Goal(world, new Vector2(1.5f, 1.02f), 0.01f), parameters, out _), Is.False);
        }

        /// <summary>Verifies jump trajectory clearance reuses the same ground contact tolerance.</summary>
        [Test]
        public void JumpPlannerAcceptsAdjacentHeightDriftWithinContactTolerance()
        {
            Dictionary<Vector2Int, float> heights = new()
            {
                [new Vector2Int(0, 0)] = 1f,
                [new Vector2Int(1, 0)] = 1.005f
            };
            TestNavigationWorld world = new(new RectInt(0, 0, 2, 5),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, Array.Empty<Vector2Int>(), heights);
            JumpNavigationParameters parameters = JumpParameters().WithGroundContactTolerance(0.01f);

            Assert.That(new JumpNavigationPlanner(world, 32, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f),
                Goal(world, new Vector2(1.5f, 1.005f), 0.01f), parameters, out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
        }

        /// <summary>Verifies contact tolerance only relaxes the top boundary, not a real side obstruction.</summary>
        [Test]
        public void LowerCenterSegmentStillRejectsSideCollisionWithContactTolerance()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 4),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 1) }, Array.Empty<Vector2Int>());

            Assert.That(world.IsLowerCenterSegmentClear(new Vector2(0.5f, 1f), new Vector2(1.5f, 1f),
                new Vector2(0.8f, 1.5f), 0.2f, 0.01f), Is.False);
        }

        /// <summary>Verifies the partial-cell Sahamusi start resolves against its captured representative surface.</summary>
        [Test]
        public void GroundPlannerResolvesPartialCellSupportStart()
        {
            Vector2Int supportCell = new(19, 8);
            TestNavigationWorld world = new(
                new RectInt(0, 0, 24, 12),
                Array.Empty<Vector2Int>(),
                new[] { supportCell },
                new Dictionary<Vector2Int, float> { [supportCell] = 9f });
            Vector2 start = new(19.079f, 9.005f);
            Assert.That(world.TryResolveGroundSupport(start, new Vector2(0.8f, 1.5f),
                out Vector2 snapped, out _, out NavigationCell support), Is.True);
            Assert.That(support, Is.EqualTo(NavigationCell.OneWay));
            Assert.That(snapped.y, Is.EqualTo(9f).Within(0.0001f));
            NavigationPlanningDiagnostics diagnostics = new();
            WalkNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                start, Goal(world, new Vector2(19.7f, 9f), 0.1f), WalkParameters(), cancellationToken,
                diagnostics));

            NavigationPlanResult result = work.Execute(CancellationToken.None);

            Assert.That(diagnostics.ExpansionCount, Is.GreaterThan(0));
            Assert.That(result.Termination, Is.Not.EqualTo(NavigationPlanTermination.SearchExhausted));
        }

        /// <summary>Verifies an unresolved walking start does not masquerade as an exhausted search.</summary>
        [Test]
        public void GroundPlannerDoesNotRecordExhaustionWhenStartSupportIsUnresolved()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationPlanningDiagnostics diagnostics = new();
            WalkNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                new Vector2(1.5f, 1f), Goal(world, new Vector2(3.5f, 1f), 0.1f), WalkParameters(),
                cancellationToken, diagnostics));

            NavigationPlanResult result = work.Execute(CancellationToken.None);
            Assert.That(result.Route, Is.Null);
            Assert.That(diagnostics.ExpansionCount, Is.Zero);
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
        }

        /// <summary>Verifies occupied geometry without a captured top remains blocking but cannot support standing.</summary>
        [Test]
        public void GroundQueriesRejectSolidCellWithoutSupportHeight()
        {
            Vector2Int occupiedCell = new(1, 1);
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 4),
                new[] { occupiedCell }, Array.Empty<Vector2Int>(), new Dictionary<Vector2Int, float>());

            Assert.That(world.TryGetSupportSurfaceY(occupiedCell, out _), Is.False);
            Assert.That(world.TryResolveGroundSupport(new Vector2(1.5f, 2f), new Vector2(0.8f, 1f),
                out _, out _, out _), Is.False);
            Assert.That(world.CanStandAt(new Vector2(1.5f, 2f), new Vector2(0.8f, 1f), out _), Is.False);
            Assert.That(world.IsLowerCenterBodyClearAt(new Vector2(1.5f, 1.9f), new Vector2(0.8f, 1f)), Is.False);
        }

        /// <summary>Verifies the default test world supplies geometric support for every occupied floor cell.</summary>
        [Obsolete("Legacy occupancy projection; superseded by NavigationPlannerContractTests.GroundFixtureProvidesWorldSpaceGeometricSupport.", false)]
        [Explicit("Pending deletion: asserts NavigationCell occupancy instead of the world-space geometric support contract.")]
        [Test]
        public void DefaultTestWorldProvidesGeometricSupportHeight()
        {
            TestNavigationWorld world = GroundWorld(0, 1);

            Assert.That(world.GetCell(new Vector2Int(0, 0)), Is.EqualTo(NavigationCell.Solid));
            Assert.That(world.TryGetSupportSurfaceY(new Vector2Int(0, 0), out float surfaceY), Is.True);
            Assert.That(surfaceY, Is.EqualTo(1f));
            Assert.That(world.TryResolveGroundSupport(new Vector2(0.5f, 1f), new Vector2(0.8f, 1.5f),
                out Vector2 snapped, out _, out _), Is.True);
            Assert.That(snapped.y, Is.EqualTo(1f));
        }

        /// <summary>Verifies one-way crossing uses captured non-integer surface height.</summary>
        [Test]
        public void OneWayQueryUsesCapturedNonIntegerSurfaceHeight()
        {
            Vector2Int platform = new(1, 5);
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 8), Array.Empty<Vector2Int>(),
                new[] { platform }, new Dictionary<Vector2Int, float> { [platform] = 5.38f });

            Assert.That(world.CrossesOneWayDown(new Vector2(1.5f, 5.5f), new Vector2(1.5f, 5.2f), 0.8f), Is.True);
            Assert.That(world.CrossesOneWayDown(new Vector2(1.5f, 5.5f), new Vector2(1.5f, 5.39f), 0.8f), Is.False);
        }

        /// <summary>Verifies support snapping does not borrow the request-level arrival tolerance.</summary>
        [Test]
        public void GroundPlannerRejectsGapBeyondSupportSnapDistance()
        {
            TestNavigationWorld world = GroundWorld(0, 5);
            Vector2 start = new(
                0.5f,
                1f + NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon);

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(start, Goal(world, new Vector2(4.5f, 1f), 10f), WalkParameters(), out _), Is.False);
        }

        /// <summary>Verifies a blocking solid cell is traversed by a shared ballistic jump.</summary>
        [Test]
        public void GroundPlannerJumpsOverObstacle()
        {
            List<Vector2Int> floor = Floor(0, 5);
            floor.Add(new Vector2Int(2, 1));
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 7), floor, Array.Empty<Vector2Int>());
            Assert.That(new WalkNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f), Goal(world, new Vector2(4.5f, 1f), 0.1f), WalkParameters(jumpHeight: 2.5f, jumpLength: 5f), out NavigationRoute route), Is.True);
            Assert.That(ContainsSegment<JumpRouteSegment>(route), Is.True);
        }

        /// <summary>Verifies a sub-tolerance physical start remains the exact root of a multi-step Walk route.</summary>
        [Test]
        public void GroundPlannerPreservesExactNonCanonicalStartAcrossJumpRoute()
        {
            List<Vector2Int> floor = Floor(0, 5);
            floor.Add(new Vector2Int(2, 1));
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 7), floor, Array.Empty<Vector2Int>());
            Vector2 start = new(0.50002f, 1f);

            Assert.That(new WalkNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(start,
                Goal(world, new Vector2(4.5f, 1f), 0.1f),
                WalkParameters(jumpHeight: 2.5f, jumpLength: 2.1f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Start.Equals(start), Is.True);
            Assert.That(route.Count, Is.GreaterThan(1));
            Assert.That(route.Segments[0].Start.Equals(route.Start), Is.True);
            Assert.That(ContainsSegment<JumpRouteSegment>(route), Is.True);
            for (int index = 1; index < route.Count; index++)
                Assert.That(route.Segments[index - 1].End.Equals(route.Segments[index].Start), Is.True);
            Assert.That(route.Segments[^1].End.Equals(route.ResolvedGoal), Is.True);
        }

        /// <summary>Verifies a microscopic physical start cannot be replaced by a canonical cached ground edge.</summary>
        [Test]
        public void GroundPlannerPreservesExactNonCanonicalStartForGroundMove()
        {
            TestNavigationWorld world = GroundWorld(0, 3);
            Vector2 start = new(0.50002f, 1f);

            Assert.That(new WalkNavigationPlanner(world, 64, new GroundJumpSolver(world)).TryPlan(start,
                Goal(world, new Vector2(1.5f, 1f), 0.1f),
                WalkParameters(jumpHeight: 0f, jumpLength: 0f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Start.Equals(start), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(route.Segments[0].Start.Equals(start), Is.True);
            Assert.That(route.Segments[^1].End.Equals(route.ResolvedGoal), Is.True);
        }

        /// <summary>Verifies Fly emits a real segment for coordinates hidden by Unity's approximate vector operator.</summary>
        [Test]
        public void FlyPlannerConnectsMicroscopicNonZeroDisplacement()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            Vector2 start = new(1.500005f, 1.5f);

            Assert.That(new FlyNavigationPlanner(world, 64).TryPlan(start,
                Goal(world, new Vector2(1.5f, 1.5f), 0.1f),
                new FlyNavigationParameters(new Vector2(0.6f, 0.6f)),
                out NavigationRoute route), Is.True);
            Assert.That(route.Count, Is.EqualTo(1));
            Assert.That(route.Start.Equals(start), Is.True);
            Assert.That(route.Segments[0].Start.Equals(start), Is.True);
            Assert.That(route.Segments[0].End.Equals(route.ResolvedGoal), Is.True);
        }

        /// <summary>Verifies Unity-style discrete damping does not disable grounded jump planning.</summary>
        [Test]
        public void GroundPlannerSupportsDampedJumpTrajectory()
        {
            List<Vector2Int> floor = Floor(0, 5);
            floor.Add(new Vector2Int(2, 1));
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 7), floor, Array.Empty<Vector2Int>());

            Assert.That(new WalkNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f),
                Goal(world, new Vector2(4.5f, 1f), 0.1f),
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
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 5), solids, Array.Empty<Vector2Int>());
            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(0.5f, 1f), Goal(world, new Vector2(2.5f, 1f), 0.1f), WalkParameters(bodySize: new Vector2(0.8f, 1.5f)), out _), Is.False);
        }

        /// <summary>Verifies only an authored one-way surface enables drop-through.</summary>
        [Test]
        public void GroundPlannerDropsThroughOneWaySurface()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 4, 6), new[] { new Vector2Int(1, 0) }, new[] { new Vector2Int(1, 2) });
            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 3f), Goal(world, new Vector2(1.5f, 1f), 0.1f), WalkParameters(jumpHeight: 0, jumpLength: 0), out NavigationRoute route), Is.True);
            Assert.That(route.Segments[0], Is.TypeOf<DropThroughRouteSegment>());
        }

        /// <summary>Verifies jump-only planning emits only jump steps and supports local repeated landings.</summary>
        [Test]
        public void JumpPlannerBuildsOnlyJumpSegments()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 6), new[] { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(4, 0) }, Array.Empty<Vector2Int>());
            NavigationPlanningDiagnostics diagnostics = new();
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                new Vector2(0.5f, 1f), Goal(world, new Vector2(4.5f, 1f), 0.1f), JumpParameters(),
                cancellationToken, diagnostics, false));
            NavigationRoute route = work.Execute(CancellationToken.None).Route;
            Assert.That(route, Is.Not.Null,
                "The jump planner should produce a route through the reachable supports.");
            Assert.That(route.Count, Is.EqualTo(2));
            for (int i = 0; i < route.Count; i++) Assert.That(route.Segments[i], Is.TypeOf<JumpRouteSegment>());
            Assert.That(diagnostics.TerminalCandidateCount, Is.EqualTo(1));
        }

        /// <summary>Verifies a jump planner with an unresolved start exits before creating a search frontier.</summary>
        [Test]
        public void JumpPlannerDoesNotRecordExhaustionWhenStartSupportIsUnresolved()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 4, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationPlanningDiagnostics diagnostics = new();
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                new Vector2(1.5f, 1f), Goal(world, new Vector2(3.5f, 1f), 0.1f), JumpParameters(),
                cancellationToken, diagnostics));

            NavigationPlanResult result = work.Execute(CancellationToken.None);
            Assert.That(result.Route, Is.Null);
            Assert.That(diagnostics.ExpansionCount, Is.Zero);
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
        }

        /// <summary>Verifies jump parameters without usable edges exit before creating a search frontier.</summary>
        [Test]
        public void JumpPlannerDoesNotRecordExhaustionWhenJumpEdgesAreUnavailable()
        {
            TestNavigationWorld world = GroundWorld(0, 3);
            NavigationPlanningDiagnostics diagnostics = new();
            JumpNavigationParameters noEdges = new(new Vector2(0.8f, 1.5f), Gravity, 1f, 0f, 0f, 0f, 0.02f);
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                new Vector2(0.5f, 1f), Goal(world, new Vector2(2.5f, 1f), 0.1f), noEdges,
                cancellationToken, diagnostics));

            NavigationPlanResult result = work.Execute(CancellationToken.None);
            Assert.That(result.Route, Is.Null);
            Assert.That(diagnostics.ExpansionCount, Is.Zero);
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
        }

        /// <summary>Verifies jump-only planning accepts a same-column landing when jump length is zero.</summary>
        [Test]
        public void JumpPlannerAllowsVerticalJumpWithZeroLength()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new JumpNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 3f), 0.1f),
                new JumpNavigationParameters(new Vector2(0.8f, 1.5f), Gravity, 1f, 0f, 3.5f, 0f, 0.02f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(route.Segments[0].Start.x, Is.EqualTo(route.Segments[0].End.x).Within(0.0001f));
            JumpRouteSegment segment = (JumpRouteSegment)route.Segments[0];
            Assert.That(segment.MinimumApexHeight, Is.GreaterThanOrEqualTo(3f - 0.0001f));
        }

        /// <summary>Verifies composite walking planning accepts a same-column landing when jump length is zero.</summary>
        [Test]
        public void GroundPlannerAllowsVerticalJumpWithZeroLength()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 3f), 0.1f),
                WalkParameters(jumpHeight: 3.5f, jumpLength: 0f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(route.Segments[0].Start.x, Is.EqualTo(route.Segments[0].End.x).Within(0.0001f));
        }

        /// <summary>Verifies Smart Walk binds a physical upper goal and does not accept horizontal overlap alone.</summary>
        [Test]
        public void SmartGroundPlannerUsesFootHeightForVerticalGoal()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                new Vector2(1.5f, 3f),
                0.1f,
                WalkParameters(jumpHeight: 3.5f, jumpLength: 0f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
        }

        /// <summary>Verifies positive authored jump height includes the effective apex headroom policy.</summary>
        [Test]
        public void GroundPlannerAcceptsUpperLandingWithinEffectiveApexMaximum()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 3f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(((JumpRouteSegment)route.Segments[0]).MinimumApexHeight,
                Is.LessThanOrEqualTo(3.24f + 0.0001f));
        }

        /// <summary>Verifies an over-high request returns a bounded best-effort route with a legal apex.</summary>
        [Test]
        public void GroundPlannerRejectsUpperLandingAboveEffectiveApexMaximum()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 3f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f),
                out _), Is.True);
            NavigationRoute highTargetRoute;
            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 3.25f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f),
                out highTargetRoute), Is.True);
            Assert.That(highTargetRoute, Is.Not.Null);
            Assert.That(highTargetRoute.SearchComplete, Is.True);
            Assert.That(highTargetRoute.ResolvedGoal.y, Is.LessThan(3.25f));
            Assert.That(world.TryResolveGroundSupport(highTargetRoute.ResolvedGoal, new Vector2(0.8f, 1.5f),
                out _, out _, out _), Is.True);
            foreach (NavigationRouteSegment segment in highTargetRoute.Segments)
            {
                if (segment is not JumpRouteSegment jump) continue;
                Assert.That(JumpTrajectory.IsApexHeightAllowed(2.99f, jump.MinimumApexHeight), Is.True);
            }
        }

        /// <summary>Verifies jump segments retain only the discrete OneWay surfaces crossed by the trajectory.</summary>
        [Test]
        public void JumpPlannerRecordsDirectedSurfaceCrossings()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 8),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 1), new Vector2Int(1, 2) });

            Assert.That(new JumpNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 4f), 0.1f),
                new JumpNavigationParameters(new Vector2(0.8f, 1.5f), Gravity, 1f, 0f, 5.5f, 0f, 0.02f),
                out NavigationRoute route), Is.True);

            JumpRouteSegment segment = (JumpRouteSegment)route.Segments[0];
            Assert.That(route.GoalRegion.IsComplete(
                segment.PlannedLanding + Vector2.up * 0.75f, new Vector2(0.8f, 1.5f)), Is.True);
            Assert.That(segment.SurfaceCrossings, Is.Not.Null);
            Assert.That(segment.MinimumApexHeight, Is.LessThanOrEqualTo(5.5f + 0.0001f));
            if (!ContainsSurfaceCrossing(segment, 2f, JumpSurfaceCrossingKind.Ascending))
                Assert.Fail(DescribeSurfaceCrossings(segment));
            Assert.That(ContainsSurfaceCrossing(segment, 3f, JumpSurfaceCrossingKind.Landing), Is.True,
                DescribeSurfaceCrossings(segment));
        }

        /// <summary>Checks whether a route segment contains one crossing kind at a support height.</summary>
        private static bool ContainsSurfaceCrossing(JumpRouteSegment segment, float surfaceY,
            JumpSurfaceCrossingKind kind)
        {
            for (int index = 0; index < segment.SurfaceCrossings.Count; index++)
            {
                JumpSurfaceCrossing crossing = segment.SurfaceCrossings[index];
                if (crossing.Kind == kind && Mathf.Abs(crossing.Position.y - surfaceY) <= 0.0001f) return true;
            }

            return false;
        }

        /// <summary>Formats planner-owned surface spans for a focused test failure.</summary>
        private static string DescribeSurfaceCrossings(JumpRouteSegment segment)
        {
            List<string> descriptions = new();
            for (int index = 0; index < segment.SurfaceCrossings.Count; index++)
            {
                JumpSurfaceCrossing crossing = segment.SurfaceCrossings[index];
                descriptions.Add($"{crossing.Kind}:{crossing.Position.y:R}:" +
                    $"{crossing.Surface.SourceId}:{crossing.Surface.FeatureId}");
            }

            return $"Surface crossings: {string.Join(", ", descriptions)}";
        }

        /// <summary>Verifies a one-way launch can use a bounded upward arc to reach a much lower support.</summary>
        [Test]
        public void JumpPlannerAllowsLowerLandingBeyondJumpHeight()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 4, 8),
                new[] { new Vector2Int(1, 1) },
                new[] { new Vector2Int(1, 4) });

            Assert.That(new JumpNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 5f),
                Goal(world, new Vector2(1.5f, 2f), 0.1f),
                new JumpNavigationParameters(new Vector2(0.8f, 1.5f), Gravity, 1f, 0f, 1f, 2.1f, 0.02f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(route.Segments[0].End.y, Is.EqualTo(2f).Within(0.0001f));
        }

        /// <summary>Verifies a solid launch surface still blocks a direct downward jump.</summary>
        [Test]
        public void JumpPlannerDoesNotPassThroughSolidLaunchSurface()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 4, 8),
                new[] { new Vector2Int(1, 1), new Vector2Int(1, 4) },
                Array.Empty<Vector2Int>());

            Assert.That(new JumpNavigationPlanner(world, 256, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 5f),
                Goal(world, new Vector2(1.5f, 2f), 0.1f),
                new JumpNavigationParameters(new Vector2(0.8f, 1.5f), Gravity, 1f, 0f, 1f, 2.1f, 0.02f),
                out _), Is.False);
        }

        /// <summary>Verifies aerial planning uses a finite grid and body-size clearance.</summary>
        [Test]
        public void FlyPlannerDetoursAroundSolidGeometry()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 7, 5), new[] { new Vector2Int(3, 2) }, Array.Empty<Vector2Int>());
            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(new Vector2(1.5f, 2.5f), Goal(world, new Vector2(5.5f, 2.5f), 0.1f), new FlyNavigationParameters(new Vector2(0.6f, 0.6f)), out NavigationRoute route), Is.True);
            Assert.That(route.Count, Is.GreaterThan(1));
        }

        /// <summary>Verifies a Fly Confront request keeps Ground Range lower-center and LOS semantics.</summary>
        [Test]
        public void FlyPlannerConfrontRetainsGroundRangeGoalContract()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 8, 6), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            NavigationGoalRequest confrontRequest = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(5.5f, 2.5f), Vector3.zero), 0.1f, true);
            Vector2 bodySize = new(0.6f, 0.6f);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(new Vector2(1.5f, 2.5f), NavigationGoalRegion.Bind(confrontRequest, world),
                new FlyNavigationParameters(bodySize), out NavigationRoute route), Is.True);
            Assert.That(route.GoalRegion.IsGroundWalk, Is.True);
            Assert.That(route.GoalRegion.RequiresLineOfSight, Is.True);
            Assert.That(route.GoalRegion.IsComplete(route.ResolvedGoal, bodySize), Is.True);
            Assert.That(route.ResolvedGoal.y, Is.EqualTo(2.8f).Within(0.0001f));
        }

        /// <summary>Verifies Fly planning expands a world-unit arrival bound into enough non-unit cells.</summary>
        [Obsolete("Legacy planner grid-resolution snapshot; goal-region completion is covered by NavigationCoreContractTests.", false)]
        [Explicit("Pending deletion: asserts an internal tolerance-to-cell-radius conversion and zero-segment route shape.")]
        [Test]
        public void FlyPlannerConvertsArrivalToleranceToCellRadius()
        {
            List<Vector2Int> solids = new();
            for (int x = 1; x < 9; x++) solids.Add(new Vector2Int(x, 0));
            TestNavigationWorld world = new(new RectInt(0, 0, 9, 1), solids, Array.Empty<Vector2Int>(), 0.3f);

            Assert.That(new FlyNavigationPlanner(world, 128).TryPlan(new Vector2(0.15f, 0.15f),
                Goal(world, new Vector2(1.35f, 0.15f), 1.4f),
                new FlyNavigationParameters(new Vector2(0.1f, 0.1f)),
                out NavigationRoute route), Is.True);
            Assert.That(route.Count, Is.Zero);
            Assert.That(route.ResolvedGoal, Is.EqualTo(new Vector2(0.15f, 0.15f)));
        }

        /// <summary>Verifies detached aerial work executes a long unobstructed plan to completion.</summary>
        [Test]
        public void FlyPlannerLongDirectSegmentCompletesAsDetachedWork()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 128, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            FlyNavigationPlanner planner = new(world, 256);
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                new Vector2(0.5f, 1.5f),
                Goal(world, new Vector2(127.5f, 1.5f), 0.1f),
                new FlyNavigationParameters(new Vector2(0.6f, 0.6f)), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;
            Assert.That(route, Is.Not.Null);
        }

        /// <summary>Verifies synchronous and detached entry points preserve the same public result contract.</summary>
        [Test]
        public void FlyPlannerSyncAndDetachedWorkProduceEquivalentResultsWithoutDiagnostics()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 16, 4), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            FlyNavigationPlanner planner = new(world, 64);
            Vector2 start = new(1.5f, 1.5f);
            NavigationGoalRegion goal = Goal(world, new Vector2(12.5f, 1.5f), 0.1f);
            FlyNavigationParameters parameters = new(new Vector2(0.6f, 0.6f));

            NavigationPlanResult synchronous = planner.Plan(start, goal, parameters);
            NavigationPlanningDiagnostics diagnostics = new();
            NavigationPlanResult diagnosed = planner.Plan(start, goal, parameters,
                CancellationToken.None, diagnostics);
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                start, goal, parameters, cancellationToken));
            NavigationPlanResult detached = work.Execute(CancellationToken.None);

            Assert.That(synchronous.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(detached.Termination, Is.EqualTo(synchronous.Termination));
            Assert.That(diagnosed.Termination, Is.EqualTo(synchronous.Termination));
            Assert.That(detached.Route, Is.Not.Null);
            Assert.That(diagnosed.Route, Is.Not.Null);
            Assert.That(detached.Route.ResolvedGoal, Is.EqualTo(synchronous.Route.ResolvedGoal));
            Assert.That(diagnosed.Route.ResolvedGoal, Is.EqualTo(synchronous.Route.ResolvedGoal));
            Assert.That(diagnostics.ExploredSupportCells, Is.Empty,
                "Direct Fly planning must not create support-cell diagnostics.");
        }

        /// <summary>Verifies an external assembly can implement the public generic planner contract.</summary>
        [Test]
        public void ExternalPlannerCanImplementPublicPlanContract()
        {
            TestNavigationWorld world = GroundWorld(0, 2);
            NavigationGoalRegion goal = Goal(world, new Vector2(0.5f, 1f), 0.1f);
            ExternalPlanner planner = new(world);

            NavigationPlanResult result = planner.Plan(new Vector2(0.5f, 1f), goal, 17);

            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(result.Route, Is.Not.Null);
            Assert.That(planner.PrepareCount, Is.EqualTo(1));
            Assert.That(planner.ValidateCount, Is.EqualTo(1));
            Assert.That(planner.CoreCount, Is.EqualTo(1));
            Assert.That(planner.PrepareRouteCount, Is.EqualTo(1));
        }

        /// <summary>Verifies detached aerial work resolves a large goal region.</summary>
        [Test]
        public void FlyPlannerLargeGoalRegionCompletesAsDetachedWork()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 32, 8), new[] { new Vector2Int(16, 3) }, Array.Empty<Vector2Int>());
            FlyNavigationPlanner planner = new(world, 256);
            NavigationGoalRegion goal = NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(
                    new Bounds(new Vector3(16.5f, 3.5f), new Vector3(12f, 4f)), DistanceMetric.Euclidean, 0.1f), world);
            using INavigationPlanningWork work = new PlannerWork(cancellationToken => planner.Plan(
                new Vector2(1.5f, 2.5f),
                goal, new FlyNavigationParameters(new Vector2(0.6f, 0.6f)), cancellationToken));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;
            Assert.That(route, Is.Not.Null);
        }

        /// <summary>Verifies a body cannot claim support from a cell touched only at its boundary.</summary>
        [Test]
        public void OneWayQueryIgnoresBodyTouchingCellBoundary()
        {
            TestNavigationWorld world = new(new RectInt(0, 0, 2, 3), Array.Empty<Vector2Int>(), new[] { new Vector2Int(0, 0) });

            Assert.That(world.CrossesOneWayDown(new Vector2(-0.5f, 2f), new Vector2(-0.5f, 1f), 1f), Is.False);
        }

        /// <summary>Verifies a wide body may overhang when its foot center has support.</summary>
        [Test]
        public void StandingQueryAllowsWideBodyOverhang()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 4, 3),
                new[] { new Vector2Int(1, 0) },
                Array.Empty<Vector2Int>());

            Assert.That(world.CanStandAt(new Vector2(1.5f, 1f), new Vector2(2.8f, 1f), out bool supportIsOneWay), Is.True);
            Assert.That(supportIsOneWay, Is.False);
        }

        /// <summary>Verifies side contact cannot replace support under the foot center.</summary>
        [Test]
        public void StandingQueryRejectsSideOnlySupport()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 4, 3),
                new[] { new Vector2Int(0, 0) },
                Array.Empty<Vector2Int>());

            Assert.That(world.CanStandAt(new Vector2(1.25f, 1f), new Vector2(2f, 1f), out _), Is.False);
        }

        /// <summary>Verifies one-way classification comes only from the center support cell.</summary>
        [Test]
        public void StandingQueryClassifiesOnlyCenterSupportCell()
        {
            TestNavigationWorld oneWayCenter = new(
                new RectInt(0, 0, 4, 3),
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) },
                new[] { new Vector2Int(1, 0) });
            TestNavigationWorld solidCenter = new(
                new RectInt(0, 0, 4, 3),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) });

            Assert.That(oneWayCenter.CanStandAt(new Vector2(1.5f, 1f), new Vector2(2.8f, 1f), out bool oneWay), Is.True);
            Assert.That(oneWay, Is.True);
            Assert.That(solidCenter.CanStandAt(new Vector2(1.5f, 1f), new Vector2(2.8f, 1f), out oneWay), Is.True);
            Assert.That(oneWay, Is.False);
        }

        /// <summary>Verifies the full world AABB still rejects a body wider than an open corridor.</summary>
        [Test]
        public void StandingQueryRejectsNarrowCorridor()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 4),
                new[]
                {
                    new Vector2Int(1, 0),
                    new Vector2Int(0, 1),
                    new Vector2Int(2, 1),
                },
                Array.Empty<Vector2Int>());

            Assert.That(world.CanStandAt(new Vector2(1.5f, 1f), new Vector2(1.2f, 1f), out _), Is.False);
        }

        /// <summary>Verifies invalid planner inputs fail at their owning boundary.</summary>
        [Test]
        public void PlannerRejectsInvalidArguments()
        {
            TestNavigationWorld world = GroundWorld(0, 2);
            Assert.Throws<ArgumentNullException>(() => new WalkNavigationPlanner(null, 8, null));
            Assert.That(() => new FlyNavigationPlanner(world, 8).TryPlan(new Vector2(float.NaN, 0f), Goal(world, Vector2.one, 0.1f), new FlyNavigationParameters(Vector2.one), out _),
                Throws.InstanceOf<ArgumentException>());
        }

        private static TestNavigationWorld GroundWorld(int firstX, int count)
            => NavigationTestWorlds.Ground(firstX, count);

        private static List<Vector2Int> Floor(int firstX, int count)
            => NavigationTestWorlds.Floor(firstX, count);

        private static WalkNavigationParameters WalkParameters(Vector2? bodySize = null, float jumpHeight = 2f, float jumpLength = 4f, float linearDamping = 0f)
            => new(bodySize ?? new Vector2(0.8f, 1.5f), 5f, Gravity, 1f, linearDamping, jumpHeight, jumpLength, 0.02f);

        private static NavigationGoalRegion Goal(INavigationWorld world, Vector2 destination, float arrivalErrorBound)
            => NavigationGoalRegion.Bind(
                NavigationGoalRequest.Proximity(new Bounds(destination, Vector3.zero), DistanceMetric.Euclidean, arrivalErrorBound), world);

        private static JumpNavigationParameters JumpParameters()
            => new(new Vector2(0.8f, 1.5f), Gravity, 1f, 0f, 2f, 2.1f, 0.02f);

        private static bool ContainsSegment<TSegment>(NavigationRoute route) where TSegment : NavigationRouteSegment
        {
            for (int i = 0; i < route.Count; i++) if (route.Segments[i] is TSegment) return true;
            return false;
        }

        /// <summary>A test-only planner defined outside the runtime assembly.</summary>
        private sealed class ExternalPlanner : NavigationPlanner<int>
        {
            public ExternalPlanner(INavigationWorld world) : base(world, 1)
            {
            }

            public int PrepareCount { get; private set; }
            public int ValidateCount { get; private set; }
            public int CoreCount { get; private set; }
            public int PrepareRouteCount { get; private set; }

            public override NavigationPlanResult Plan(Vector2 start, NavigationGoalRegion goalRegion,
                int parameters, CancellationToken cancellationToken = default,
                NavigationPlanningDiagnostics diagnostics = null, bool allowExecutablePrefix = false)
            {
                ValidatePlanInputs(start, goalRegion, cancellationToken);
                PrepareCount++;
                ValidateCount++;
                Assert.That(parameters, Is.EqualTo(17));
                CoreCount++;
                PrepareRouteCount++;
                return NavigationPlanResult.ResultProduced(NavigationRoute.Complete(start, goalRegion, start,
                    Array.Empty<NavigationRouteSegment>()));
            }
        }

    }
}
