using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies Map-owned queueing before publication, planning, cancellation, and build failure.</summary>
    public sealed partial class MapNavigationRuntimeTests
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
                new AABB(new Vector2(100f, 2.5f), new Vector2(100f, 2.5f)), DistanceMetric.Euclidean, 1f);

            NavigationPlanningOperation operation = runtime.PlanFlyAsync(
                new Vector2(1.5f, 2.5f), goal, FlyParameters);
            WaitForCompletion(operation);

            Assert.That(operation.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
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

        /// <summary>Verifies Smart Walk plans a pre-publication request against the published world.</summary>
        [Test]
        public void SmartWalkPlansGoalAgainstPublishedWorldAtScheduleTime()
        {
            TestNavigationWorld world = new(
                new RectInt(0, 0, 6, 5),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                    new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0) },
                Array.Empty<Vector2Int>(), 0.5f);
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(world);

            NavigationGoalRequest captured = NavigationGoalRequest.GroundRange(
                new AABB(new Vector2(1.25f, 0.5f), new Vector2(3.25f, 0.5f)), 0.1f);
            WalkNavigationParameters parameters = new(new Vector2(0.2f, 0.4f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 2f, 3f, 0.02f);
            Assert.That(world.TryResolveGroundSupport(new Vector2(2.25f, 0.5f), parameters.BodySize,
                out _, out _, out _), Is.True);
            Assert.That(world.IsGoalComplete(captured,
                new Vector2(2.25f, 0.5f) + Vector2.up * (parameters.BodySize.y * 0.5f),
                parameters.BodySize), Is.True);
            NavigationPlanningOperation operation = runtime.PlanWalkAsync(
                new Vector2(2.25f, 0.5f), captured, parameters);
            WaitForCompletion(operation);

            Assert.That(operation.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(operation.Result, Is.Not.Null);
            Assert.That(operation.Result.Goal, Is.EqualTo(captured));
            Assert.That(operation.Result.World, Is.SameAs(world));
            Assert.That(operation.Result.Goal.IsGroundWalk, Is.True);
            Assert.That(operation.Result.World.CellSize, Is.EqualTo(0.5f));
            Assert.That(operation.Result.Goal.Center.y, Is.EqualTo(0.5f));
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
                new AABB(new Vector2(3.5f, 1f), new Vector2(3.5f, 1f)), 0.1f);
            Vector2 start = new(1.5f, 1f);

            NavigationPlanningOperation first = runtime.PlanWalkAsync(start, goal, parameters);
            WaitForCompletion(first);
            Assert.That(first.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
            Assert.That(first.Result, Is.Null);
            Assert.That(world.SupportQueryCount, Is.EqualTo(2),
                "Prepared parameters are shared by the request boundary, failure key, and worker core.");

            NavigationPlanningOperation second = runtime.PlanWalkAsync(start, goal, parameters);
            WaitForCompletion(second);
            Assert.That(second.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
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

        /// <summary>Verifies NextAction planning returns one local executable action for Walk, Jump, and Fly.</summary>
        [Test]
        public void NextActionPlanningReturnsOneLocalActionForWalkJumpAndFly()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            Vector2 walkStart = new(0.5f, 1f);
            NavigationPlanningOperation walk = runtime.PlanWalkAsync(
                walkStart,
                NavigationGoalRequest.GroundRange(new AABB(new Vector2(4.5f, 1f), new Vector2(4.5f, 1f)), 0.1f),
                WalkParameters,
                NavigationPlanningExtent.NextAction);
            NavigationPlanningOperation jump = runtime.PlanJumpAsync(
                new Vector2(0.5f, 1f),
                Goal(new Vector2(3.5f, 1f)),
                JumpParameters,
                NavigationPlanningExtent.NextAction);
            NavigationPlanningOperation fly = runtime.PlanFlyAsync(
                new Vector2(1.5f, 2.5f),
                Goal(new Vector2(4.5f, 2.5f)),
                FlyParameters,
                NavigationPlanningExtent.NextAction);

            Complete(runtime, walk);
            Complete(runtime, jump);
            Complete(runtime, fly);

            Assert.That(walk.Result, Is.Not.Null);
            Assert.That(walk.Result.Segments, Has.Count.EqualTo(1));
            Assert.That(walk.Result.Segments[0], Is.TypeOf<GroundRouteSegment>());
            Assert.That(walk.Result.Segments[0].Start, Is.EqualTo(walkStart));
            Assert.That(walk.Result.Segments[0].End, Is.Not.EqualTo(walkStart));

            Assert.That(jump.Result, Is.Not.Null);
            Assert.That(jump.Result.Segments, Has.Count.EqualTo(1));
            Assert.That(jump.Result.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(jump.Result.Segments[0].Start, Is.EqualTo(new Vector2(0.5f, 1f)));
            Assert.That(jump.Result.Segments[0].End, Is.Not.EqualTo(jump.Result.Segments[0].Start));

            Assert.That(fly.Result, Is.Not.Null);
            Assert.That(fly.Result.Segments, Has.Count.EqualTo(1));
            Assert.That(fly.Result.Segments[0], Is.TypeOf<FlyRouteSegment>());
            Assert.That(fly.Result.Segments[0].Start, Is.EqualTo(new Vector2(1.5f, 2.5f)));
            Assert.That(fly.Result.Segments[0].End, Is.Not.EqualTo(fly.Result.Segments[0].Start));
        }

        /// <summary>Verifies detached jump work prepares and validates candidates in one background execution.</summary>
        [Test]
        public void JumpCandidatePreparationCompletesInDetachedPlannerWork()
        {
            List<Vector2Int> floor = new();
            for (int x = 0; x < 41; x++) floor.Add(new Vector2Int(x, 0));
            TestNavigationWorld world = new(new RectInt(0, 0, 41, 8), floor, Array.Empty<Vector2Int>());
            JumpNavigationPlanner planner = new(world, 64, new GroundJumpSolver(world));
            using INavigationPlanningWork work = new PlannerWork(token => planner.PlanSingleStep(
                new Vector2(20.5f, 1f), Goal(new Vector2(35.5f, 1f)), JumpParameters,
                token));

            NavigationPlanResult result = work.Execute(CancellationToken.None);
            Assert.That(result.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            NavigationRoute route = result.Route;
            Assert.That(route, Is.Not.Null);
            Assert.That(route.Segments, Has.Count.EqualTo(1));
            Assert.That(route.Segments[0], Is.TypeOf<JumpRouteSegment>());
            Assert.That(((JumpRouteSegment)route.Segments[0]).MinimumApexHeight, Is.GreaterThan(0f));
        }

        /// <summary>Verifies a complete Route request never publishes a partial route.</summary>
        [Test]
        public void RouteExtentProducesOnlyCompleteRoute()
        {
            using MapNavigationRuntime runtime = new(4, 64, 1);
            runtime.PublishWorld(CreateOpenWorld());
            WalkNavigationParameters groundedOnly = new(new Vector2(0.8f, 1f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = Goal(new Vector2(5.5f, 1f));

            NavigationPlanningOperation first = runtime.PlanWalkAsync(new Vector2(0.5f, 1f), goal, groundedOnly);
            Complete(runtime, first);
            Assert.That(first.Result, Is.Not.Null, DescribeRoute(first.Result));
            Assert.That(first.Result.ReachesGoal, Is.True, DescribeRoute(first.Result));
            Assert.That(first.Result.Segments, Has.Count.GreaterThan(1), DescribeRoute(first.Result));
            Assert.That(first.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced),
                DescribeRoute(first.Result));
        }

        /// <summary>Verifies incomplete Walk, Jump, and Fly Route requests publish no route.</summary>
        [Test]
        public void RouteExtentPublishesNoRouteWhenCompleteSearchCannotFinish()
        {
            using MapNavigationRuntime runtime = new(4, 1, 1);
            runtime.PublishWorld(CreateBlockedWorld());
            NavigationPlanningOperation walk = runtime.PlanWalkAsync(
                new Vector2(0.5f, 1f), Goal(new Vector2(5.5f, 1f)), WalkParameters,
                NavigationPlanningExtent.Route);
            NavigationPlanningOperation jump = runtime.PlanJumpAsync(
                new Vector2(0.5f, 1f), Goal(new Vector2(5.5f, 1f)), JumpParameters,
                NavigationPlanningExtent.Route);
            NavigationPlanningOperation fly = runtime.PlanFlyAsync(
                new Vector2(1.5f, 2.5f), Goal(new Vector2(4.5f, 2.5f)), FlyParameters,
                NavigationPlanningExtent.Route);

            WaitForCompletion(walk);
            WaitForCompletion(jump);
            WaitForCompletion(fly);
            Assert.That(walk.Result, Is.Null, DescribeRoute(walk.Result));
            Assert.That(jump.Result, Is.Null, DescribeRoute(jump.Result));
            Assert.That(fly.Result, Is.Null, DescribeRoute(fly.Result));
            Assert.That(walk.PlanResult.Termination, Is.Not.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(jump.PlanResult.Termination, Is.Not.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(fly.PlanResult.Termination, Is.Not.EqualTo(NavigationPlanTermination.ResultProduced));
        }

        /// <summary>Verifies failed-request memoization keeps the planning purpose distinct.</summary>
        [Test]
        public void FailedRequestMemoizationSeparatesPlanningPurpose()
        {
            using MapNavigationRuntime runtime = new(4, 128, 64);
            TestNavigationWorld world = new(new RectInt(0, 0, 6, 5),
                new[] { new Vector2Int(0, 0) }, Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            WalkNavigationParameters groundedOnly = new(new Vector2(0.8f, 1f), 4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                new AABB(new Vector2(4.5f, 1f), new Vector2(4.5f, 1f)), 0f);
            Vector2 start = new(0.5f, 1f);

            NavigationPlanningOperation continuation = runtime.PlanWalkAsync(start, goal, groundedOnly,
                NavigationPlanningExtent.Route, default, NavigationPlanningPurpose.EndpointContinuation);
            WaitForCompletion(continuation);
            Assert.That(continuation.Result, Is.Null, DescribeRoute(continuation.Result));
            Assert.That(continuation.PlanResult.Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted), DescribeRoute(continuation.Result));
            runtime.ReleaseCompletedOperations();

            NavigationPlanningOperation initialRoute = runtime.PlanWalkAsync(start, goal, groundedOnly,
                NavigationPlanningExtent.Route, default, NavigationPlanningPurpose.InitialRoute);
            Assert.That(initialRoute.IsCompleted, Is.False,
                "Planning purpose is part of the failure identity, so another purpose must still run the planner.");
            WaitForCompletion(initialRoute);
            Assert.That(initialRoute.PlanResult.Termination,
                Is.EqualTo(NavigationPlanTermination.SearchExhausted), DescribeRoute(initialRoute.Result));

            NavigationPlanningOperation repeated = runtime.PlanWalkAsync(start, goal, groundedOnly,
                NavigationPlanningExtent.Route, default, NavigationPlanningPurpose.EndpointContinuation);
            Assert.That(repeated.IsCompleted, Is.True,
                "The identical exhausted request must reuse its memoized failure.");
        }

        /// <summary>Verifies failed-request memoization keeps distance metrics distinct.</summary>
        [Test]
        public void FailedRequestMemoizationSeparatesDistanceMetric()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = new(new RectInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            AABB target = new(new Vector2(1.1f, 1.1f), new Vector2(1.1f, 1.1f));
            Vector2 start = new(0.5f, 0.5f);

            NavigationPlanningOperation manhattan = runtime.PlanFlyAsync(start,
                NavigationGoalRequest.Proximity(target, DistanceMetric.Manhattan, 0.25f), FlyParameters);
            WaitForCompletion(manhattan);
            Assert.That(manhattan.Result, Is.Null);
            Assert.That(manhattan.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));

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
                new AABB(new Vector2(1.5f, 2.5f), new Vector2(1.5f, 2.5f)), DistanceMetric.Euclidean, 3f);

            NavigationPlanningOperation constrained = runtime.PlanFlyAsync(
                start, goal, new FlyNavigationParameters(new Vector2(0.8f, 0.8f), 0.1f));
            WaitForCompletion(constrained);
            Assert.That(constrained.Result, Is.Null);
            Assert.That(constrained.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.SearchExhausted));
            runtime.ReleaseCompletedOperations();

            NavigationPlanningOperation relaxed = runtime.PlanFlyAsync(
                start, goal, new FlyNavigationParameters(new Vector2(0.8f, 0.8f), 1f));
            WaitForCompletion(relaxed);
            Assert.That(relaxed.Result, Is.Not.Null,
                "A larger approach budget must not hit the failed-request cache entry for the smaller budget.");
            Assert.That(relaxed.Result.Goal.IsRetreat, Is.True);
        }

        /// <summary>Verifies failed-request memoization keeps line-of-sight requirements distinct.</summary>
        [Test]
        public void FailedRequestMemoizationSeparatesLineOfSightRequirement()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = new(new RectInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            AABB target = new(new Vector2(1.1f, 1.1f), new Vector2(1.1f, 1.1f));
            Vector2 start = new(0.5f, 0.5f);

            NavigationPlanningOperation requiresLineOfSight = runtime.PlanFlyAsync(start,
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f, true), FlyParameters);
            WaitForCompletion(requiresLineOfSight);
            Assert.That(requiresLineOfSight.Result, Is.Null);
            Assert.That(requiresLineOfSight.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));

            NavigationPlanningOperation noLineOfSight = runtime.PlanFlyAsync(start,
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f), FlyParameters);
            WaitForCompletion(noLineOfSight);
            Assert.That(noLineOfSight.Result, Is.Not.Null);
            Assert.That(noLineOfSight.Result.Segments, Is.Empty,
                "The no-LOS goal is already complete even though the LOS goal failed.");
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

            AABB partialBounds = new(new Vector2(-0.75f, 2f), new Vector2(0.25f, 3f));
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

            RectInt exactBoundary = runtime.GetGoalCellBounds(Goal(new Vector2(1f, 1f)));
            Assert.That(exactBoundary, Is.EqualTo(new RectInt(1, 1, 1, 1)));

            RectInt beforeBoundary = runtime.GetGoalCellBounds(Goal(new Vector2(0.999f, 1f)));
            RectInt afterBoundary = runtime.GetGoalCellBounds(Goal(new Vector2(1.001f, 1f)));
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
            NavigationPlanningOperation operation = runtime.PlanFlyAsync(Vector2.one, Goal(Vector2.right), FlyParameters,
                NavigationPlanningExtent.Route, cancellation.Token);
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
            => NavigationGoalRequest.Proximity(new AABB(destination, destination), DistanceMetric.Euclidean, 0f);

        private static string DescribeRoute(NavigationRoute route)
        {
            if (route == null) return "Route=null";
            string segments = string.Join(", ", route.Segments.Select(segment =>
                $"{segment.GetType().Name}:{segment.Start}->{segment.End}"));
            return $"Route.ReachesGoal={route.ReachesGoal}; Route.ResolvedGoal={route.ResolvedGoal}; "
                + $"Route.Segments=[{segments}]";
        }

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
