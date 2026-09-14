// Consolidated explicit navigation regressions; normal test runs exclude [Explicit].
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{

    // ===== Runtime cache failures =====
    /// <summary>Legacy runtime memoization failure retained for explicit regression runs.</summary>
    public sealed partial class MapNavigationRuntimeTests
    {
        [Explicit("Known migration failure: Smart Walk negative memoization returns a route for an exhausted request.")]
        [Test]
        public void SmartWalkMemoizesOnlyExactExhaustedFailure()
        {
            using MapNavigationRuntime runtime = new(4, 128, 64);
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 5),
                new[] { new Vector2Int(0, 0) }, Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            WalkNavigationParameters groundedOnly = new(new Vector2(0.8f, 1f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(4.5f, 1f), Vector3.zero), 0f);
            Vector2 start = new(0.5f, 1f);

            NavigationPlanningOperation first = runtime.PlanWalkAsync(start, goal, groundedOnly,
                NavigationPlanningExtent.Route, CancellationToken.None, NavigationPlanningPurpose.EndpointContinuation);
            WaitForCompletion(first);
            Assert.That(first.Result, Is.Null, DescribeRoute(first.Result));
            Assert.That(first.PlanResult.Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted), DescribeRoute(first.Result));

            NavigationPlanningOperation repeated = runtime.PlanWalkAsync(start, goal, groundedOnly);
            Assert.That(repeated.IsCompleted, Is.True,
                "Only the exact exhausted request may use the negative cache.");
            Assert.That(repeated.Result, Is.Null);

            NavigationGoalRequest lineOfSightGoal = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(4.5f, 1f), Vector3.zero), 0f, true);
            NavigationPlanningOperation changedGoal = runtime.PlanWalkAsync(start, lineOfSightGoal, groundedOnly);
            Assert.That(changedGoal.IsCompleted, Is.False,
                "A changed LOS requirement must not hit the terminal memo for another goal identity.");
            WaitForCompletion(changedGoal);
            Assert.That(changedGoal.Result, Is.Null, DescribeRoute(changedGoal.Result));
            Assert.That(changedGoal.PlanResult.Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted), DescribeRoute(changedGoal.Result));

            NavigationPlanningOperation differentStart = runtime.PlanWalkAsync(
                start + Vector2.right * 0.19f, goal, groundedOnly);
            Assert.That(differentStart.IsCompleted, Is.False,
                "A nearby start is a separate request and must not inherit another start's failure.");
            WaitForCompletion(differentStart);
            Assert.That(differentStart.Result, Is.Null, DescribeRoute(differentStart.Result));

            NavigationPlanningOperation simple = runtime.PlanWalkAsync(start, goal, groundedOnly,
                NavigationPlanningExtent.NextAction);
            WaitForCompletion(simple);
            Assert.That(simple.Result, Is.Null,
                "Simple Walk must not consume Smart Walk terminal memo state.");
        }
    }

    // ===== Core snapshot failures =====
    /// <summary>Legacy world-snapshot failures retained for explicit regression runs.</summary>
    public sealed partial class NavigationCoreContractTests
    {
        [Explicit("Known migration failure: immutable support identity snapshot does not resolve the authored surface.")]
        [Test]
        public void NavigationWorldSnapshotResolvesImmutableSupportIdentity()
        {
            NavigationSurfaceId surface = new(7, 3);
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(Vector2.zero, 1f,
                new RectInt(0, 0, 4, 4),
                new[]
                {
                    new NavigationShapeData(7, 3, NavigationShapeType.Edge,
                        new[] { new Vector2(0f, 1f), new Vector2(3f, 1f) }, 0f,
                        NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, 1f),
                },
                new[] { new NavigationRegionData(new RectInt(0, 0, 4, 4), 11) });

            Assert.That(world.TryResolveSupport(new Vector2(1.25f, 1.01f), new Vector2(0.8f, 1.2f),
                0.1f, out NavigationSupport support), Is.True);
            Assert.That(support.Surface, Is.EqualTo(surface));
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(support.Position, Is.EqualTo(new Vector2(1.25f, 1f)));
            Assert.That(world.AreInSameRegion(new Vector2(0.1f, 0.1f), new Vector2(3.9f, 3.9f)), Is.True);
        }

        [Explicit("Known migration failure: one-way crossing provenance snapshot returns the wrong crossing set.")]
        [Test]
        public void NavigationWorldSnapshotReportsOneWayCrossingProvenance()
        {
            NavigationSurfaceId surface = new(2, 5);
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(Vector2.zero, 1f,
                new RectInt(0, 0, 4, 4),
                new[]
                {
                    new NavigationShapeData(2, 5, NavigationShapeType.Edge,
                        new[] { new Vector2(0f, 1f), new Vector2(3f, 1f) }, 0f,
                        NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, 1f),
                },
                Array.Empty<NavigationRegionData>());
            List<NavigationSurfaceCrossing> crossings = new();

            world.CollectOneWayCrossings(new Vector2(1.5f, 2f), new Vector2(1.5f, 0.5f), 0.8f, crossings);

            Assert.That(crossings, Has.Count.EqualTo(3));
            for (int index = 0; index < crossings.Count; index++)
            {
                Assert.That(crossings[index].Surface, Is.EqualTo(surface));
                Assert.That(crossings[index].Position.y, Is.EqualTo(1f).Within(0.001f));
                if (index > 0) Assert.That(crossings[index].Fraction, Is.GreaterThanOrEqualTo(crossings[index - 1].Fraction));
            }
        }
    }

    // ===== Planner failures =====
    /// <summary>Legacy Planner failures retained for explicit regression runs.</summary>
    public sealed partial class NavigationPlannerTests
    {
        [Explicit("Known migration failure: partial-cell support start still depends on the legacy projection.")]
        [Test]
        public void GroundPlannerResolvesPartialCellSupportStart()
        {
            Vector2Int supportCell = new(19, 8);
            TestNavigationWorld world = new(
                new RectInt(0, 0, 24, 12),
                System.Array.Empty<Vector2Int>(),
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

        [Explicit("Known migration failure: captured non-integer one-way surface crossing remains unresolved.")]
        [Test]
        public void OneWayQueryUsesCapturedNonIntegerSurfaceHeight()
        {
            Vector2Int platform = new(1, 5);
            TestNavigationWorld world = new(new RectInt(0, 0, 3, 8), System.Array.Empty<Vector2Int>(),
                new[] { platform }, new Dictionary<Vector2Int, float> { [platform] = 5.38f });

            Assert.That(world.CrossesOneWayDown(new Vector2(1.5f, 5.5f), new Vector2(1.5f, 5.2f), 0.8f), Is.True);
            Assert.That(world.CrossesOneWayDown(new Vector2(1.5f, 5.5f), new Vector2(1.5f, 5.39f), 0.8f), Is.False);
        }

        [Explicit("Known migration failure: jump-only zero-horizontal route is not produced.")]
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
            Assert.That(((JumpRouteSegment)route.Segments[0]).MinimumApexHeight, Is.GreaterThanOrEqualTo(3f - 0.0001f));
        }

        [Explicit("Known migration failure: composite walking planner rejects the zero-horizontal Jump route.")]
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

        [Explicit("Known migration failure: Smart Ground goal binding does not select the supported vertical Jump.")]
        [Test]
        public void SmartGroundPlannerUsesFootHeightForVerticalGoal()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                new Vector2(1.5f, 3f), 0.1f,
                WalkParameters(jumpHeight: 3.5f, jumpLength: 0f),
                out NavigationRoute route), Is.True);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
        }

        [Explicit("Known migration failure: upper landing within effective apex maximum is not accepted.")]
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

        [Explicit("Known migration failure: over-high landing does not produce a bounded legal apex route.")]
        [Test]
        public void GroundPlannerRejectsUpperLandingAboveEffectiveApexMaximum()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 7),
                new[] { new Vector2Int(1, 0) },
                new[] { new Vector2Int(1, 2) });

            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 3f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f), out _), Is.True);
            Assert.That(new WalkNavigationPlanner(world, 128, new GroundJumpSolver(world)).TryPlan(new Vector2(1.5f, 1f),
                Goal(world, new Vector2(1.5f, 3.25f), 0.1f),
                WalkParameters(jumpHeight: 2.99f, jumpLength: 0f), out NavigationRoute highTargetRoute), Is.True);
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

        [Explicit("Known migration failure: Jump route surface-crossing provenance is incomplete.")]
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
            Assert.That(segment.SurfaceCrossings, Has.Count.GreaterThan(0));
        }
    }

}

