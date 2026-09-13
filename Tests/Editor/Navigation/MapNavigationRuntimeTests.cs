using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies Map-owned queueing before publication, planning, cancellation, and build failure.</summary>
    public sealed class MapNavigationRuntimeTests
    {
        private static readonly WalkNavigationParameters WalkParameters = new(new Vector2(0.8f, 1f), 4f, new Vector2(0f, -9.81f), 1f, 0f, 2f, 3f, 0.02f);
        private static readonly JumpNavigationParameters JumpParameters = new(new Vector2(0.8f, 1f), new Vector2(0f, -9.81f), 1f, 0f, 2f, 3f, 0.02f);
        private static readonly FlyNavigationParameters FlyParameters = new(new Vector2(0.8f, 0.8f));

        /// <summary>Execution filters use the explicit configuration before and after world publication.</summary>
        [Test]
        public void TerrainFilterUsesCapturedLayersBeforeWorldPublication()
        {
            NavigationPhysicsLayers layers = new(1 << 2, 1 << 4);
            using MapNavigationRuntime runtime = new(8, 128, 128, layers);
            Assert.That(runtime.IsReady, Is.False);
            Assert.That(runtime.IsDisposed, Is.False);
            Assert.That(runtime.PhysicsLayers.SolidLayers.value, Is.EqualTo(1 << 2));
            Assert.That(runtime.PhysicsLayers.OneWayLayers.value, Is.EqualTo(1 << 4));
            ContactFilter2D filter = runtime.CreateTerrainFilter();
            Assert.That(filter.layerMask.value, Is.EqualTo((1 << 2) | (1 << 4)));
            Assert.That(filter.useLayerMask, Is.True);
            Assert.That(filter.useTriggers, Is.False);

            runtime.PublishWorld(CreateOpenWorld());
            Assert.That(runtime.CreateTerrainFilter().layerMask.value, Is.EqualTo(filter.layerMask.value));
            runtime.Dispose();
            Assert.That(runtime.IsDisposed, Is.True);
            Assert.That(runtime.IsReady, Is.False);
            Assert.Throws<ObjectDisposedException>(() => runtime.CreateTerrainFilter());
        }

        /// <summary>The compatibility constructor never supplies project-specific physics layers.</summary>
        [Test]
        public void PlanningOnlyConstructorUsesEmptyPhysicsLayers()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            Assert.That(runtime.CreateTerrainFilter().layerMask.value, Is.Zero);
        }

        /// <summary>Resolves runtime-owned bindings and rejects missing sources or disposed runtimes.</summary>
        [Test]
        public void SourceBindingsResolveWithinRuntimeAndRejectDisposedRuntime()
        {
            GameObject firstHost = new("runtime-source-first");
            GameObject secondHost = new("runtime-source-second");

            try
            {
                Collider2D firstCollider = firstHost.AddComponent<BoxCollider2D>();
                Collider2D secondCollider = secondHost.AddComponent<BoxCollider2D>();

                using MapNavigationRuntime first = CreateRuntime();
                using MapNavigationRuntime second = CreateRuntime();

                NavigationSurfaceId surface = new(7, 0);
                NavigationSurfaceId missingSurface = new(99, 0);

                first.PublishWorld(
                    CreateOpenWorld(),
                    new Dictionary<int, Collider2D> { [7] = firstCollider });
                second.PublishWorld(
                    CreateOpenWorld(),
                    new Dictionary<int, Collider2D> { [7] = secondCollider });

                // The same local source ID resolves through each runtime's own bindings.
                Assert.That(first.TryResolveSource(surface, out Collider2D resolved), Is.True);
                Assert.That(resolved, Is.SameAs(firstCollider));

                Assert.That(second.TryResolveSource(surface, out resolved), Is.True);
                Assert.That(resolved, Is.SameAs(secondCollider));

                Assert.That(first.TryResolveSource(missingSurface, out resolved), Is.False);
                Assert.That(resolved, Is.Null);

                first.Dispose();

                Assert.That(first.TryResolveSource(surface, out resolved), Is.False);
                Assert.That(resolved, Is.Null);

                // Disposing one owner must not invalidate another owner's bindings.
                Assert.That(second.IsDisposed, Is.False);
                Assert.That(second.TryResolveSource(surface, out resolved), Is.True);
                Assert.That(resolved, Is.SameAs(secondCollider));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstHost);
                UnityEngine.Object.DestroyImmediate(secondHost);
            }
        }

        /// <summary>Verifies horizontal endpoint tolerance includes its minimum and one-step contact margin.</summary>
        [Test]
        public void HorizontalEndpointToleranceUsesMinimumAndContactMargin()
        {
            float contactMargin = Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon;
            Assert.That(GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(0f, 0.02f),
                Is.EqualTo(0.2f));
            Assert.That(GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(20f, 0.02f),
                Is.EqualTo(0.4f + contactMargin).Within(0.000001f));
            Assert.That(GroundTraversalEndpointPolicy.GetHorizontalTransitionTolerance(5f, 0.02f),
                Is.EqualTo(0.1f + contactMargin).Within(0.000001f));
            Assert.That(() =>
                GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(float.NaN, 0.02f),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() =>
                GroundTraversalEndpointPolicy.GetHorizontalCompletionTolerance(1f, -0.02f),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies requests remain pending until the immutable world is published.</summary>
        [Test]
        public void PlanningWaitsForPublishedWorld()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            NavigationPlanningOperation operation = runtime.PlanFlyAsync(new Vector2(1.5f, 2.5f), Goal(new Vector2(3.5f, 2.5f)), FlyParameters);
            Assert.That(operation.IsCompleted, Is.False);
            Assert.That(operation.IsCompleted, Is.False);

            runtime.PublishWorld(CreateOpenWorld());
            WaitForCompletion(operation);
            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(operation.Result, Is.Not.Null);
        }

        /// <summary>Verifies Retreat goals are not rejected merely because the target bounds are outside the finite world.</summary>
        [Test]
        public void RetreatGoalOutsideWorldStillUsesRuntimeCompletionPredicate()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(
                new Bounds(new Vector3(100f, 2.5f), Vector3.zero), DistanceMetric.Euclidean, 1f);

            NavigationPlanningOperation operation = runtime.PlanFlyAsync(
                new Vector2(1.5f, 2.5f), goal, FlyParameters);
            WaitForCompletion(operation);

            Assert.That(operation.Outcome, Is.EqualTo(NavigationPlanningOutcome.RouteFound));
            Assert.That(operation.Result, Is.Not.Null);
            Assert.That(operation.Result.Count, Is.Zero);
        }

        /// <summary>Verifies all locomotion requests share the finite FIFO planner budget.</summary>
        [Test]
        public void PublishedWorldSupportsAllPlanningKinds()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());
            NavigationPlanningOperation walk = runtime.PlanWalkAsync(new Vector2(0.5f, 1f), Goal(new Vector2(3.5f, 1f)), WalkParameters);
            NavigationPlanningOperation jump = runtime.PlanJumpAsync(new Vector2(0.5f, 1f), Goal(new Vector2(3.5f, 1f)), JumpParameters);
            NavigationPlanningOperation fly = runtime.PlanFlyAsync(new Vector2(1.5f, 2.5f), Goal(new Vector2(4.5f, 2.5f)), FlyParameters);

            WaitForCompletion(walk);
            WaitForCompletion(jump);
            WaitForCompletion(fly);
            Assert.That(walk.IsCompleted, Is.True);
            Assert.That(jump.IsCompleted, Is.True);
            Assert.That(fly.IsCompleted, Is.True);
            Assert.That(walk.Result, Is.Not.Null);
            Assert.That(jump.Result, Is.Not.Null);
            Assert.That(fly.Result, Is.Not.Null);
        }

        /// <summary>Verifies Smart Walk rebinds its raw target against the explicit published cell size.</summary>
        [Test]
        public void SmartWalkBindsGoalAtScheduleTime()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 6, 5),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                    new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0) },
                Array.Empty<Vector2Int>(), 0.5f);
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(world);

            NavigationGoalRequest captured = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(2.25f, 0.5f, 0f), new Vector3(2f, 0f, 0f)), 0.1f);
            NavigationGoalRegion boundCaptured = NavigationGoalRegion.Bind(captured, world);
            WalkNavigationParameters parameters = new(new Vector2(0.2f, 0.4f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 2f, 3f, 0.02f);
            Assert.That(world.TryResolveGroundSupport(new Vector2(2.25f, 0.5f), parameters.BodySize,
                out _, out _, out _), Is.True);
            Assert.That(boundCaptured.ContainsLowerCenterGoal(new Vector2(2.25f, 0.5f), parameters.BodySize.x), Is.True);
            NavigationPlanningOperation operation = runtime.PlanWalkAsync(
                new Vector2(2.25f, 0.5f), captured, parameters);
            WaitForCompletion(operation);

            Assert.That(operation.Outcome, Is.EqualTo(NavigationPlanningOutcome.RouteFound));
            Assert.That(operation.Result, Is.Not.Null);
            Assert.That(operation.Result.GoalRegion.IsGroundWalk, Is.True);
            Assert.That(operation.Result.GoalRegion.CellSize, Is.EqualTo(0.5f));
            Assert.That(operation.Result.GoalRegion.Center.y, Is.EqualTo(0.5f));
        }

        /// <summary>Verifies an unresolved Smart Walk start is retried by the worker rather than memoized as a terminal failure.</summary>
        [Test]
        public void UnresolvedSmartWalkIsNotMemoizedAsExhaustedFailure()
        {
            CountingNavigationWorld world = new(new RectInt(0, 0, 6, 5));
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(world);
            WalkNavigationParameters parameters = new(new Vector2(0.8f, 1f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(3.5f, 1f), Vector3.zero), 0.1f);
            Vector2 start = new(1.5f, 1f);

            NavigationPlanningOperation first = runtime.PlanWalkAsync(start, goal, parameters);
            WaitForCompletion(first);
            Assert.That(first.Outcome, Is.EqualTo(NavigationPlanningOutcome.NoPath));
            Assert.That(first.Result, Is.Null);
            Assert.That(world.SupportQueryCount, Is.EqualTo(2),
                "Prepared parameters are shared by the request boundary, failure key, and worker core.");

            NavigationPlanningOperation second = runtime.PlanWalkAsync(start, goal, parameters);
            WaitForCompletion(second);
            Assert.That(second.Outcome, Is.EqualTo(NavigationPlanningOutcome.NoPath));
            Assert.That(second.Result, Is.Null);
            Assert.That(world.SupportQueryCount, Is.EqualTo(4),
                "The second unresolved request must enter the worker instead of hitting failed-request memoization.");
        }

        /// <summary>Verifies planning support resolves canonical NavWorld cells across an integer boundary.</summary>
        [Test]
        public void PlanningGroundSupportUsesCanonicalSupportCell()
        {
            Vector2Int canonicalCell = new(1, 8);
            Vector2Int otherCell = new(1, 5);
            TestNavigationWorld world = new(
                new RectInt(0, 0, 3, 12),
                Array.Empty<Vector2Int>(),
                new[] { canonicalCell, otherCell },
                new Dictionary<Vector2Int, float>
                {
                    [canonicalCell] = 8.999982f,
                    [otherCell] = 5.999982f,
                });
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(world);

            Vector2 observedLowerCenter = new(1.5f, 9.004980f);
            Assert.That(runtime.GetPlanningStartCell(observedLowerCenter).y, Is.EqualTo(9));
            Assert.That(runtime.TryResolvePlanningGroundSupport(
                observedLowerCenter,
                new Vector2(0.8f, 0.8f),
                out Vector2 snappedLowerCenter,
                out NavigationSupport support), Is.True);
            Assert.That(NavigationWorldQueries.WorldToCell(world, support.Position), Is.EqualTo(canonicalCell));
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(snappedLowerCenter.y, Is.EqualTo(8.999982f).Within(0.0001f));

            Vector2 otherPlatform = new(1.5f, 6.004980f);
            Assert.That(runtime.TryResolvePlanningGroundSupport(
                otherPlatform,
                new Vector2(0.8f, 0.8f),
                out Vector2 otherSnappedLowerCenter,
                out NavigationSupport otherSupport), Is.True);
            Vector2Int otherSupportCell = NavigationWorldQueries.WorldToCell(world, otherSupport.Position);
            Assert.That(otherSupportCell, Is.EqualTo(otherCell));
            Assert.That(otherSupportCell, Is.Not.EqualTo(canonicalCell));
            Assert.That(otherSupport.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(otherSnappedLowerCenter.y, Is.EqualTo(5.999982f).Within(0.0001f));
        }

        /// <summary>Verifies repeated ground requests reuse snapshot-owned validated jump geometry.</summary>
        [Test]
        public void RepeatedGroundProfileReusesJumpGeometry()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            NavigationPlanningOperation first = runtime.PlanJumpAsync(
                new Vector2(0.5f, 1f), Goal(new Vector2(3.5f, 1f)), JumpParameters);
            WaitForCompletion(first);
            Assert.That(first.IsCompleted, Is.True);

            NavigationPlanningOperation second = runtime.PlanJumpAsync(
                new Vector2(0.5f, 1f), Goal(new Vector2(2.5f, 1f)), JumpParameters);
            WaitForCompletion(second);
            Assert.That(second.IsCompleted, Is.True);
            Assert.That(second.Result, Is.Not.Null,
                "Cached trajectories must still be rescored for the new goal.");
        }

        /// <summary>Verifies trajectory validation is not reused for a different launch offset in one support cell.</summary>
        [Test]
        public void DifferentLaunchOffsetDoesNotReuseCachedTrajectory()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            NavigationPlanningOperation first = runtime.PlanJumpAsync(
                new Vector2(0.25f, 1f), Goal(new Vector2(3.5f, 1f)), JumpParameters);
            Complete(runtime, first);
            NavigationPlanningOperation second = runtime.PlanJumpAsync(
                new Vector2(0.75f, 1f), Goal(new Vector2(2.5f, 1f)), JumpParameters);
            Complete(runtime, second);

            Assert.That(second.Result, Is.Not.Null,
                "A changed launch offset must still produce a valid route after revalidating trajectory geometry.");
        }

        /// <summary>Verifies Smart and Simple Walk reuse goal-independent local ground topology.</summary>
        [Test]
        public void WalkModesReuseLocalGroundTopologyAcrossGoals()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());
            Vector2 start = new(0.5f, 1f);

            NavigationPlanningOperation smart = runtime.PlanWalkAsync(start, Goal(new Vector2(3.5f, 1f)), WalkParameters);
            Complete(runtime, smart);
            NavigationPlanningOperation simple = runtime.PlanWalkStepAsync(start, Goal(new Vector2(4.5f, 1f)), WalkParameters);
            Complete(runtime, simple);

            Assert.That(simple.Result, Is.Not.Null,
                "A topology cache hit must still produce a route for the new goal.");
        }

        /// <summary>Verifies Walk policy filtering does not split the shared jump landing topology.</summary>
        [Test]
        public void WalkAndJumpShareLandingTopology()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());
            Vector2 start = new(0.5f, 1f);

            NavigationPlanningOperation jump = runtime.PlanJumpAsync(start, Goal(new Vector2(3.5f, 1f)), JumpParameters);
            Complete(runtime, jump);
            NavigationPlanningOperation walk = runtime.PlanWalkStepAsync(start, Goal(new Vector2(5.5f, 1f)), WalkParameters);
            Complete(runtime, walk);

            Assert.That(walk.Result, Is.Not.Null);
        }

        /// <summary>Verifies detached jump work prepares and validates candidates in one background execution.</summary>
        [Test]
        public void JumpCandidatePreparationCompletesInDetachedPlannerWork()
        {
            List<Vector2Int> floor = new();
            for (int x = 0; x < 41; x++) floor.Add(new Vector2Int(x, 0));
            TestNavigationWorld world = new(new RectInt(0, 0, 41, 8), floor, Array.Empty<Vector2Int>());
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(token => planner.Plan(
                new Vector2(20.5f, 1f), NavigationGoalRegion.Bind(
                    Goal(new Vector2(35.5f, 1f)), world), JumpParameters,
                token, new NavigationPlanningDiagnostics()));

            NavigationRoute route = work.Execute(CancellationToken.None).Route;
            Assert.That(route, Is.Not.Null);
        }

        /// <summary>Verifies cached local topology prevents repeating a completed fall-column scan.</summary>
        [Test]
        public void RepeatedWalkSupportReusesFallScan()
        {
            List<Vector2Int> solids = new();
            for (int x = 0; x < 5; x++) solids.Add(new Vector2Int(x, 0));
            solids.Add(new Vector2Int(1, 3));
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(new TestNavigationWorld(new RectInt(0, 0, 5, 5), solids, Array.Empty<Vector2Int>()));
            Vector2 start = new(1.5f, 4f);

            NavigationPlanningOperation first = runtime.PlanWalkStepAsync(start, Goal(new Vector2(4.5f, 1f)), WalkParameters);
            Complete(runtime, first);
            NavigationPlanningOperation second = runtime.PlanWalkStepAsync(start, Goal(new Vector2(0.5f, 1f)), WalkParameters);
            Complete(runtime, second);

            Assert.That(first.Result, Is.Not.Null);
            Assert.That(second.Result, Is.Not.Null);
        }

        /// <summary>Verifies a budget horizon may retain best effort but never enters the failure cache.</summary>
        [Test]
        public void HorizonNullDoesNotMemoFailure()
        {
            using MapNavigationRuntime runtime = new(4, 1, 1);
            runtime.PublishWorld(CreateOpenWorld());
            WalkNavigationParameters groundedOnly = new(new Vector2(0.8f, 1f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = Goal(new Vector2(5.5f, 1f));

            NavigationPlanningOperation first = runtime.PlanWalkAsync(new Vector2(0.5f, 1f), goal, groundedOnly);
            Complete(runtime, first);
            Assert.That(first.Result, Is.Not.Null);
            Assert.That(first.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.BudgetReached));

            NavigationPlanningOperation second = runtime.PlanWalkAsync(new Vector2(0.5f, 1f), goal, groundedOnly);
            Assert.That(second.IsCompleted, Is.False, "A horizon result must remain eligible for a fresh attempt.");
            Complete(runtime, second);
            Assert.That(second.Result, Is.Not.Null);
            Assert.That(second.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.BudgetReached));
        }

        /// <summary>Verifies an exhausted result with a best-effort route is not negative-cached.</summary>
        [Test]
        public void FailedRequestWithSameSnapshotAndRegionIsDeduplicated()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateBlockedWorld());
            NavigationGoalRequest goal = Goal(new Vector2(4.5f, 2.5f));
            NavigationPlanningOperation first = runtime.PlanFlyAsync(new Vector2(1.5f, 2.5f), goal, FlyParameters);
            WaitForCompletion(first);

            Assert.That(first.IsCompleted, Is.True);
            Assert.That(first.Result, Is.Not.Null);
            Assert.That(first.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.SearchExhausted));

            NavigationPlanningOperation second = runtime.PlanFlyAsync(new Vector2(1.5f, 2.5f), goal, FlyParameters);
            Assert.That(second.IsCompleted, Is.False);
            WaitForCompletion(second);
            Assert.That(second.Result, Is.Not.Null);
            Assert.That(second.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.SearchExhausted));
        }

        /// <summary>Verifies failed-request memoization keeps distance metrics distinct.</summary>
        [Test]
        public void FailedRequestMemoizationSeparatesDistanceMetric()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = new(new RectInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            Bounds target = new(new Vector3(1.1f, 1.1f), Vector3.zero);
            Vector2 start = new(0.5f, 0.5f);

            NavigationPlanningOperation manhattan = runtime.PlanFlyAsync(start,
                NavigationGoalRequest.Proximity(target, DistanceMetric.Manhattan, 0.25f), FlyParameters);
            WaitForCompletion(manhattan);
            Assert.That(manhattan.Result, Is.Null);
            Assert.That(manhattan.Outcome, Is.EqualTo(NavigationPlanningOutcome.NoPath));

            NavigationPlanningOperation chebyshev = runtime.PlanFlyAsync(start,
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f), FlyParameters);
            WaitForCompletion(chebyshev);
            Assert.That(chebyshev.Result, Is.Not.Null);
            Assert.That(chebyshev.Result.Segments, Is.Empty,
                "The Chebyshev goal is already complete even though the Manhattan goal failed.");
        }

        /// <summary>Verifies a failed Retreat request with one budget cannot poison a later budget.</summary>
        [Test]
        public void FailedRetreatRequestMemoizationSeparatesApproachBudget()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = CreateOpenWorld();
            runtime.PublishWorld(world);
            Vector2 start = new(0.5f, 2.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(
                new Bounds(new Vector3(1.5f, 2.5f), Vector3.zero), DistanceMetric.Euclidean, 3f);

            NavigationPlanningOperation constrained = runtime.PlanFlyAsync(
                start, goal, new FlyNavigationParameters(new Vector2(0.8f, 0.8f), 0.1f));
            WaitForCompletion(constrained);
            Assert.That(constrained.Result, Is.Null);
            Assert.That(constrained.Outcome, Is.EqualTo(NavigationPlanningOutcome.NoPath));

            NavigationPlanningOperation relaxed = runtime.PlanFlyAsync(
                start, goal, new FlyNavigationParameters(new Vector2(0.8f, 0.8f), 1f));
            WaitForCompletion(relaxed);
            Assert.That(relaxed.Result, Is.Not.Null,
                "A larger approach budget must not hit the failed-request cache entry for the smaller budget.");
            Assert.That(relaxed.Result.GoalRegion.IsRetreat, Is.True);
        }

        /// <summary>Verifies failed-request memoization keeps line-of-sight requirements distinct.</summary>
        [Test]
        public void FailedRequestMemoizationSeparatesLineOfSightRequirement()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = new(new RectInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            Bounds target = new(new Vector3(1.1f, 1.1f), Vector3.zero);
            Vector2 start = new(0.5f, 0.5f);

            NavigationPlanningOperation requiresLineOfSight = runtime.PlanFlyAsync(start,
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f, true), FlyParameters);
            WaitForCompletion(requiresLineOfSight);
            Assert.That(requiresLineOfSight.Result, Is.Null);
            Assert.That(requiresLineOfSight.Outcome, Is.EqualTo(NavigationPlanningOutcome.NoPath));

            NavigationPlanningOperation noLineOfSight = runtime.PlanFlyAsync(start,
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f), FlyParameters);
            WaitForCompletion(noLineOfSight);
            Assert.That(noLineOfSight.Result, Is.Not.Null);
            Assert.That(noLineOfSight.Result.Segments, Is.Empty,
                "The no-LOS goal is already complete even though the LOS goal failed.");
        }

        /// <summary>Verifies only Smart Walk memoizes an exhausted terminal endpoint for the exact request identity.</summary>
        [Test]
        public void SmartWalkMemoizesExactTerminalEndpointOnly()
        {
            using MapNavigationRuntime runtime = new(4, 128, 64);
            List<Vector2Int> solids = new()
            {
                new(0, 0), new(1, 0), new(2, 0), new(3, 0), new(4, 0), new(5, 0),
                new(3, 1), new(3, 2), new(3, 3), new(3, 4),
            };
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 5), solids, Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            WalkNavigationParameters groundedOnly = new(new Vector2(0.8f, 1f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(4.5f, 1f), Vector3.zero), 0f);
            Vector2 start = new(0.5f, 1f);

            NavigationPlanningOperation first = runtime.PlanWalkAsync(start, goal, groundedOnly,
                CancellationToken.None, NavigationPlanningPurpose.EndpointContinuation);
            WaitForCompletion(first);
            Assert.That(first.Result, Is.Not.Null);
            Assert.That(first.Result.SearchComplete, Is.True);
            Assert.That(first.Result.GoalRegion.IsGroundWalk, Is.True);

            NavigationPlanningOperation second = runtime.PlanWalkAsync(start, goal, groundedOnly);
            WaitForCompletion(second);
            Assert.That(second.Result, Is.Not.Null,
                "Replanning from the original start must still return the same best-effort approach route.");
            Assert.That(second.Result.ResolvedGoal, Is.EqualTo(first.Result.ResolvedGoal));

            NavigationPlanningOperation terminalStart = runtime.PlanWalkAsync(
                first.Result.ResolvedGoal, goal, groundedOnly);
            Assert.That(terminalStart.IsCompleted, Is.False,
                "A best-effort route must not become a negative-cache entry.");
            WaitForCompletion(terminalStart);
            Assert.That(terminalStart.Result, Is.Null);
            Assert.That(terminalStart.PlanResult.Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted));

            NavigationPlanningOperation repeatedTerminalStart = runtime.PlanWalkAsync(
                first.Result.ResolvedGoal, goal, groundedOnly);
            Assert.That(repeatedTerminalStart.IsCompleted, Is.True,
                "Only the exact exhausted request may use the negative cache.");
            Assert.That(repeatedTerminalStart.Result, Is.Null);

            NavigationGoalRequest lineOfSightGoal = NavigationGoalRequest.GroundRange(
                new Bounds(new Vector3(4.5f, 1f), Vector3.zero), 0f, true);
            NavigationPlanningOperation lineOfSightTerminal = runtime.PlanWalkAsync(
                first.Result.ResolvedGoal, lineOfSightGoal, groundedOnly);
            Assert.That(lineOfSightTerminal.IsCompleted, Is.False,
                "A changed LOS requirement must not hit the terminal memo for another goal identity.");
            WaitForCompletion(lineOfSightTerminal);
            Assert.That(lineOfSightTerminal.Result, Is.Not.Null,
                "The changed request must retain its best-effort route rather than being treated as cached failure.");

            NavigationPlanningOperation withinEndpoint = runtime.PlanWalkAsync(
                first.Result.ResolvedGoal - Vector2.right * 0.19f, goal, groundedOnly);
            Assert.That(withinEndpoint.IsCompleted, Is.False,
                "A nearby start is a separate request and must not inherit another start's failure.");
            WaitForCompletion(withinEndpoint);

            NavigationPlanningOperation outsideEndpoint = runtime.PlanWalkAsync(
                first.Result.ResolvedGoal - Vector2.right * 0.21f, goal, groundedOnly);
            WaitForCompletion(outsideEndpoint);
            Assert.That(outsideEndpoint.Result, Is.Not.Null,
                "A start beyond the shared horizontal endpoint region must replan.");

            NavigationPlanningOperation differentStart = runtime.PlanWalkAsync(new Vector2(0.9f, 1f), goal, groundedOnly);
            WaitForCompletion(differentStart);
            Assert.That(differentStart.Result, Is.Not.Null,
                "A terminal memo must retain the endpoint completion region rather than sharing a whole support cell.");

            NavigationPlanningOperation simple = runtime.PlanWalkStepAsync(start, goal, groundedOnly);
            WaitForCompletion(simple);
            Assert.That(simple.Result, Is.Not.Null,
                "Simple Walk must not consume Smart Walk terminal memo state.");
        }

        /// <summary>Verifies finite-world rejection accounts for the moving body before failing an exterior goal.</summary>
        [Test]
        public void ExteriorGoalEarlyFailUsesBodyExpandedWorldBoundary()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            NavigationPlanningOperation touching = runtime.PlanFlyAsync(
                new Vector2(0.5f, 2.5f), Goal(new Vector2(-0.1f, 2.5f)), FlyParameters);
            Assert.That(touching.IsCompleted, Is.False);

            NavigationPlanningOperation separated = runtime.PlanFlyAsync(
                new Vector2(0.5f, 2.5f), Goal(new Vector2(-10f, 2.5f)), FlyParameters);
            Assert.That(separated.IsCompleted, Is.True);
        }

        /// <summary>Verifies body-expanded early rejection preserves airborne and partially exterior targets.</summary>
        [Test]
        public void ExteriorGoalEarlyFailPreservesAirborneAndPartialRegions()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            NavigationPlanningOperation airborne = runtime.PlanJumpAsync(
                new Vector2(0.5f, 1f), Goal(new Vector2(2.5f, 3.5f)), JumpParameters);
            Assert.That(airborne.IsCompleted, Is.False);

            Bounds partialBounds = new(new Vector3(-0.25f, 2.5f, 0f), new Vector3(1f, 1f, 0f));
            NavigationPlanningOperation partial = runtime.PlanFlyAsync(new Vector2(0.5f, 2.5f),
                NavigationGoalRequest.Proximity(partialBounds, DistanceMetric.Euclidean, 0f), FlyParameters);
            Assert.That(partial.IsCompleted, Is.False);
        }

        /// <summary>Verifies point goals map to one cell, including exact and cross-boundary coordinates.</summary>
        [Test]
        public void PointGoalCellBoundsIncludePointCellAndTrackBoundaryCrossing()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = CreateOpenWorld();
            runtime.PublishWorld(world);

            RectInt exactBoundary = runtime.GetGoalRegionCellBounds(NavigationGoalRegion.Bind(Goal(new Vector2(1f, 1f)), world));
            Assert.That(exactBoundary, Is.EqualTo(new RectInt(1, 1, 1, 1)));

            RectInt beforeBoundary = runtime.GetGoalRegionCellBounds(
                NavigationGoalRegion.Bind(Goal(new Vector2(0.999f, 1f)), world));
            RectInt afterBoundary = runtime.GetGoalRegionCellBounds(
                NavigationGoalRegion.Bind(Goal(new Vector2(1.001f, 1f)), world));
            Assert.That(beforeBoundary.xMin, Is.EqualTo(0));
            Assert.That(afterBoundary.xMin, Is.EqualTo(1));
            Assert.That(afterBoundary.width, Is.EqualTo(1));
        }

        /// <summary>Verifies cancellation is finalized during the wait-for-world phase.</summary>
        [Test]
        public void CancellationWorksBeforeWorldPublication()
        {
            using CancellationTokenSource cancellation = new();
            using MapNavigationRuntime runtime = CreateRuntime();
            NavigationPlanningOperation operation = runtime.PlanFlyAsync(Vector2.one, Goal(Vector2.right), FlyParameters, cancellation.Token);
            cancellation.Cancel();
            Assert.That(operation.IsCompleted, Is.True);
            Assert.That(operation.IsCancelled, Is.True);
        }

        /// <summary>Verifies a failed build fails existing and subsequent requests with the same exception.</summary>
        [Test]
        public void FailedWorldTerminatesWaitingAndFutureRequests()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            NavigationPlanningOperation waiting = runtime.PlanFlyAsync(Vector2.one, Goal(Vector2.right), FlyParameters);
            InvalidOperationException failure = new("capture failed");
            runtime.FailWorld(failure);
            NavigationPlanningOperation later = runtime.PlanFlyAsync(Vector2.one, Goal(Vector2.right), FlyParameters);

            Assert.That(waiting.Exception, Is.SameAs(failure));
            Assert.That(later.Exception, Is.SameAs(failure));
            Assert.That(waiting.IsCompleted, Is.True);
            Assert.That(later.IsCompleted, Is.True);
        }

        /// <summary>Verifies disposal cancels queued work and rejects later use.</summary>
        [Test]
        public void DisposeCancelsQueuedOperations()
        {
            MapNavigationRuntime runtime = CreateRuntime();
            NavigationPlanningOperation operation = runtime.PlanFlyAsync(Vector2.one, Goal(Vector2.right), FlyParameters);
            runtime.Dispose();
            Assert.That(operation.IsCancelled, Is.True);
            Assert.That(runtime.IsReady, Is.False);
            Assert.Throws<ObjectDisposedException>(() => runtime.PlanFlyAsync(Vector2.zero, Goal(Vector2.one), FlyParameters));
            Assert.Throws<ObjectDisposedException>(() => runtime.PlanFlyAsync(Vector2.one, Goal(Vector2.right), FlyParameters));
        }

        /// <summary>Verifies runtime construction and world publication validate their owning inputs.</summary>
        [Test]
        public void ConstructorAndPublicationRejectInvalidInputs()
        {
            Assert.That(() => new MapNavigationRuntime(2, 0, 1), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => new MapNavigationRuntime(2, 1, 0), Throws.InstanceOf<ArgumentException>());
            using MapNavigationRuntime runtime = new(2, 32, 32);
            Assert.Throws<ArgumentNullException>(() => runtime.PublishWorld(null));
        }

        private static MapNavigationRuntime CreateRuntime()
            => new(4, 64, 64);

        private static void Complete(MapNavigationRuntime runtime, NavigationPlanningOperation operation)
        {
            WaitForCompletion(operation);
        }

        private static void WaitForCompletion(NavigationPlanningOperation operation)
        {
            Assert.That(SpinWait.SpinUntil(() => operation.IsCompleted, TimeSpan.FromSeconds(5)), Is.True,
                "Background navigation planning did not complete within the bounded test timeout.");
        }

        private static TestNavigationWorld CreateOpenWorld()
            => new(new RectInt(0, 0, 6, 5), new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0),
            }, Array.Empty<Vector2Int>());

        private static TestNavigationWorld CreateBlockedWorld()
        {
            List<Vector2Int> solid = new();
            for (int y = 0; y < 5; y++) solid.Add(new Vector2Int(3, y));
            return new TestNavigationWorld(new RectInt(0, 0, 6, 5), solid, Array.Empty<Vector2Int>());
        }

        private static NavigationGoalRequest Goal(Vector2 destination)
            => NavigationGoalRequest.Proximity(new Bounds(destination, Vector3.zero), DistanceMetric.Euclidean, 0f);

        /// <summary>Counts immutable support queries for the unresolved-start failure-cache contract.</summary>
        private sealed class CountingNavigationWorld : INavigationWorld
        {
            private readonly TestNavigationWorld world;
            public Vector2 Origin => world.Origin;
            public float CellSize => world.CellSize;
            public RectInt CellBounds => world.CellBounds;
            public int SupportQueryCount => Volatile.Read(ref supportQueryCount);
            private int supportQueryCount;

            public CountingNavigationWorld(RectInt cellBounds)
            {
                world = new TestNavigationWorld(cellBounds, Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            }

            public bool IsBodyClear(Rect bodyBounds, float tolerance) => world.IsBodyClear(bodyBounds, tolerance);
            public bool IsBodyPathClear(Rect bodyBounds, Vector2 displacement, float tolerance)
                => world.IsBodyPathClear(bodyBounds, displacement, tolerance);
            public bool IsLineOfSightClear(Vector2 start, Vector2 end) => world.IsLineOfSightClear(start, end);
            public bool TryGetSupportBelow(Vector2 position, out NavigationSupport support)
                => world.TryGetSupportBelow(position, out support);
            public bool TryResolveSupport(Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
            {
                Interlocked.Increment(ref supportQueryCount);
                support = default;
                return world.TryResolveSupport(feet, bodySize, snapDistance, out support);
            }
            public IReadOnlyList<NavigationSupportCandidate> GetSupportCandidates(Rect anchorBounds, Vector2 bodySize)
                => world.GetSupportCandidates(anchorBounds, bodySize);
            public void CollectSupportCandidates(Rect anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results)
                => world.CollectSupportCandidates(anchorBounds, bodySize, results);
            public void CollectOneWayCrossings(Vector2 previousFeet, Vector2 currentFeet, float bodyWidth,
                List<NavigationSurfaceCrossing> results)
                => world.CollectOneWayCrossings(previousFeet, currentFeet, bodyWidth, results);
            public bool AreInSameRegion(Vector2 first, Vector2 second) => world.AreInSameRegion(first, second);
        }
    }
}
