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
        private static readonly Vector2 WalkBodySize = new(0.8f, 1f);
        private static readonly Vector2 JumpBodySize = new(0.8f, 1f);
        private static readonly Vector2 FlyBodySize = new(0.8f, 0.8f);
        private static readonly WalkNavigationParameters WalkParameters = new(4f, new Vector2(0f, -9.81f), 1f, 0f, 2f, 3f, 0.02f);
        private static readonly JumpNavigationParameters JumpParameters = new(new Vector2(0f, -9.81f), 1f, 0f, 2f, 3f, 0.02f);
        private static readonly FlyNavigationParameters FlyParameters = new();

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
            NavigationPlanningOperation walk = runtime.PlanWalkAsync(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), WalkBodySize),
                Goal(new Vector2(3.5f, 1f)), WalkParameters);
            NavigationPlanningOperation jump = runtime.PlanJumpAsync(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), JumpBodySize),
                Goal(new Vector2(3.5f, 1f)), JumpParameters);
            NavigationPlanningOperation fly = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(new Vector2(1.5f, 2.5f), FlyBodySize),
                Goal(new Vector2(3.5f, 2.5f)), FlyParameters);

            Assert.That(walk.IsCompleted, Is.False);
            Assert.That(jump.IsCompleted, Is.False);
            Assert.That(fly.IsCompleted, Is.False);

            runtime.PublishWorld(CreateOpenWorld());
            WaitForCompletion(walk);
            WaitForCompletion(jump);
            WaitForCompletion(fly);
            Assert.That(walk.IsCompleted, Is.True);
            Assert.That(jump.IsCompleted, Is.True);
            Assert.That(fly.IsCompleted, Is.True);
            Assert.That(walk.Result.HasValue, Is.True);
            Assert.That(jump.Result.HasValue, Is.True);
            Assert.That(fly.Result.HasValue, Is.True);
        }

        /// <summary>Verifies Retreat goals are not rejected merely because the target bounds are outside the finite world.</summary>
        [Test]
        public void RetreatGoalOutsideWorldStillUsesRuntimeCompletionPredicate()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(
                AABB.Point(100f, 2.5f), DistanceMetric.Euclidean, 1f);

            NavigationPlanningOperation operation = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(new Vector2(1.5f, 2.5f), FlyBodySize), goal, FlyParameters);
            WaitForCompletion(operation);

            Assert.That(operation.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(operation.Result.HasValue, Is.True);
            Assert.That(operation.Result.Count, Is.Zero);
        }

        /// <summary>Verifies all locomotion requests share the finite FIFO planner budget.</summary>
        [Test]
        public void PublishedWorldSupportsAllPlanningKinds()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());
            NavigationPlanningOperation walk = runtime.PlanWalkAsync(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), WalkBodySize), Goal(new Vector2(3.5f, 1f)), WalkParameters);
            NavigationPlanningOperation jump = runtime.PlanJumpAsync(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), JumpBodySize), Goal(new Vector2(3.5f, 1f)), JumpParameters);
            NavigationPlanningOperation fly = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(new Vector2(1.5f, 2.5f), FlyBodySize), Goal(new Vector2(4.5f, 2.5f)), FlyParameters);

            WaitForCompletion(walk);
            WaitForCompletion(jump);
            WaitForCompletion(fly);
            Assert.That(walk.IsCompleted, Is.True);
            Assert.That(jump.IsCompleted, Is.True);
            Assert.That(fly.IsCompleted, Is.True);
            Assert.That(walk.Result.HasValue, Is.True);
            Assert.That(jump.Result.HasValue, Is.True);
            Assert.That(fly.Result.HasValue, Is.True);
        }

        /// <summary>Verifies Smart Walk plans a pre-publication request against the published world.</summary>
        [Test]
        public void SmartWalkPlansGoalAgainstPublishedWorldAtScheduleTime()
        {
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 6, 5),
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                    new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0) },
                Array.Empty<Vector2Int>(), 0.5f);
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(world);

            NavigationGoalRequest captured = NavigationGoalRequest.GroundRange(
                new AABB(1.25f, 0.5f, 3.25f, 0.5f), 0.1f);
            Vector2 bodySize = new(0.2f, 0.4f);
            WalkNavigationParameters parameters = new(4f,
                new Vector2(0f, -9.81f), 1f, 0f, 2f, 3f, 0.02f);
            Assert.That(world.TryResolveGroundSupport(
                AABB.FromLowerCenter(new Vector2(2.25f, 0.5f), bodySize), out _, out _), Is.True);
            Assert.That(world.IsGoalComplete(captured,
                AABB.FromCenterAndSize(new Vector2(2.25f, 0.5f) + Vector2.up * (bodySize.y * 0.5f),
                    bodySize)), Is.True);
            NavigationPlanningOperation operation = runtime.PlanWalkAsync(
                AABB.FromLowerCenter(new Vector2(2.25f, 0.5f), bodySize), captured, parameters);
            WaitForCompletion(operation);

            Assert.That(operation.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(operation.Result.HasValue, Is.True);
            Assert.That(operation.Result.Goal, Is.EqualTo(captured));
            Assert.That(operation.Result.Goal.IsGroundWalk, Is.True);
            Assert.That(operation.Result.Goal.TargetBounds.LowerCenter.y, Is.EqualTo(0.5f));
        }

        /// <summary>Verifies unresolved Smart Walk support is queried by the planner once per request.</summary>
        [Test]
        public void UnresolvedSmartWalkSupportIsQueriedOnlyByPlanner()
        {
            CountingNavigationWorld world = new(new AABBInt(0, 0, 6, 5));
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(world);
            WalkNavigationParameters parameters = new(4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = NavigationGoalRequest.GroundRange(
                AABB.Point(3.5f, 1f), 0.1f);
            Vector2 start = new(1.5f, 1f);

            NavigationPlanningOperation first = runtime.PlanWalkAsync(
                AABB.FromLowerCenter(start, new Vector2(0.8f, 1f)), goal, parameters);
            WaitForCompletion(first);
            Assert.That(first.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
            Assert.That(first.Result.HasValue, Is.False);
            Assert.That(world.SupportQueryCount, Is.EqualTo(1),
                "The runtime should not make a second support query outside the planner.");

            NavigationPlanningOperation second = runtime.PlanWalkAsync(
                AABB.FromLowerCenter(start, new Vector2(0.8f, 1f)), goal, parameters);
            WaitForCompletion(second);
            Assert.That(second.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));
            Assert.That(second.Result.HasValue, Is.False);
            Assert.That(world.SupportQueryCount, Is.EqualTo(2),
                "Each request should perform only the planner's support query.");
        }

        /// <summary>Verifies planning support resolves the captured surface across an integer boundary.</summary>
        [Test]
        public void PlanningGroundSupportResolvesCapturedSurface()
        {
            Vector2Int canonicalCell = new(1, 8);
            Vector2Int otherCell = new(1, 5);
            TestNavigationWorld world = new(
                new AABBInt(0, 0, 3, 12),
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
            Assert.That(runtime.TryResolvePlanningGroundSupport(
                AABB.FromLowerCenter(observedLowerCenter, new Vector2(0.8f, 0.8f)),
                out Vector2 snappedLowerCenter,
                out NavigationSupport support), Is.True);
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(snappedLowerCenter.y, Is.EqualTo(8.999982f).Within(0.0001f));

            Vector2 otherPlatform = new(1.5f, 6.004980f);
            Assert.That(runtime.TryResolvePlanningGroundSupport(
                AABB.FromLowerCenter(otherPlatform, new Vector2(0.8f, 0.8f)),
                out Vector2 otherSnappedLowerCenter,
                out NavigationSupport otherSupport), Is.True);
            Assert.That(otherSupport.Surface, Is.Not.EqualTo(support.Surface));
            Assert.That(otherSupport.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(otherSnappedLowerCenter.y, Is.EqualTo(5.999982f).Within(0.0001f));
        }

        /// <summary>Verifies each NextAction planner returns one executable action with real progress.</summary>
        [TestCase("Walk")]
        [TestCase("Jump")]
        [TestCase("Fly")]
        public void NextActionPlanningReturnsOneExecutableAction(string plannerKind)
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            Vector2 start;
            NavigationPlanningOperation operation;
            Type expectedSegmentType;
            switch (plannerKind)
            {
                case "Walk":
                    start = new Vector2(0.5f, 1f);
                    operation = runtime.PlanWalkAsync(AABB.FromLowerCenter(start, WalkBodySize),
                        NavigationGoalRequest.GroundRange(AABB.Point(4.5f, 1f), 0.1f),
                        WalkParameters, NavigationPlanningExtent.NextAction);
                    expectedSegmentType = typeof(GroundRouteSegment);
                    break;
                case "Jump":
                    start = new Vector2(0.5f, 1f);
                    operation = runtime.PlanJumpAsync(AABB.FromLowerCenter(start, JumpBodySize),
                        Goal(new Vector2(3.5f, 1f)), JumpParameters,
                        NavigationPlanningExtent.NextAction);
                    expectedSegmentType = typeof(JumpRouteSegment);
                    break;
                case "Fly":
                    start = new Vector2(1.5f, 2.5f);
                    operation = runtime.PlanFlyAsync(AABB.FromCenterAndSize(start, FlyBodySize),
                        Goal(new Vector2(4.5f, 2.5f)), FlyParameters,
                        NavigationPlanningExtent.NextAction);
                    expectedSegmentType = typeof(FlyRouteSegment);
                    break;
                default:
                    Assert.Fail($"Unknown planner kind '{plannerKind}'.");
                    return;
            }

            Complete(runtime, operation);
            Assert.That(operation.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(operation.Result.HasValue, Is.True);
            Assert.That(operation.Result.Count, Is.EqualTo(1));
            Assert.That(operation.Result[0], Is.TypeOf(expectedSegmentType));
            Assert.That(operation.Result[0].Start.Equals(start), Is.True);
            Assert.That(operation.Result[0].End.Equals(start), Is.False);
        }

        /// <summary>Verifies a complete Route request never publishes a partial route.</summary>
        [Test]
        public void RouteExtentProducesOnlyCompleteRoute()
        {
            using MapNavigationRuntime runtime = new(4, 64, 1);
            runtime.PublishWorld(CreateOpenWorld());
            WalkNavigationParameters groundedOnly = new(4f,
                new Vector2(0f, -9.81f), 1f, 0f, 0f, 0f, 0.02f);
            NavigationGoalRequest goal = Goal(new Vector2(5.5f, 1f));

            NavigationPlanningOperation first = runtime.PlanWalkAsync(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), WalkBodySize), goal, groundedOnly);
            Complete(runtime, first);
            Assert.That(first.Result.HasValue, Is.True, DescribeRoute(first.Result));
            Assert.That(first.Result.ReachesGoal, Is.True, DescribeRoute(first.Result));
            Assert.That(first.Result.Count, Is.GreaterThan(1), DescribeRoute(first.Result));
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
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), WalkBodySize),
                Goal(new Vector2(5.5f, 1f)), WalkParameters,
                NavigationPlanningExtent.Route);
            NavigationPlanningOperation jump = runtime.PlanJumpAsync(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), JumpBodySize),
                Goal(new Vector2(5.5f, 1f)), JumpParameters,
                NavigationPlanningExtent.Route);
            NavigationPlanningOperation fly = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(new Vector2(1.5f, 2.5f), FlyBodySize),
                Goal(new Vector2(4.5f, 2.5f)), FlyParameters,
                NavigationPlanningExtent.Route);

            WaitForCompletion(walk);
            WaitForCompletion(jump);
            WaitForCompletion(fly);
            Assert.That(walk.Result.HasValue, Is.False, DescribeRoute(walk.Result));
            Assert.That(jump.Result.HasValue, Is.False, DescribeRoute(jump.Result));
            Assert.That(fly.Result.HasValue, Is.False, DescribeRoute(fly.Result));
            Assert.That(walk.PlanResult.Termination, Is.Not.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(jump.PlanResult.Termination, Is.Not.EqualTo(NavigationPlanTermination.ResultProduced));
            Assert.That(fly.PlanResult.Termination, Is.Not.EqualTo(NavigationPlanTermination.ResultProduced));
        }

        /// <summary>Verifies Fly planning honors the requested distance metric.</summary>
        [Test]
        public void FlyPlanningHonorsDistanceMetric()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = new(new AABBInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            AABB target = new(new Vector2(1.1f, 1.1f), new Vector2(1.1f, 1.1f));
            Vector2 start = new(0.5f, 0.5f);

            NavigationPlanningOperation manhattan = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(start, FlyBodySize),
                NavigationGoalRequest.Proximity(target, DistanceMetric.Manhattan, 0.25f), FlyParameters);
            WaitForCompletion(manhattan);
            Assert.That(manhattan.Result.HasValue, Is.False);
            Assert.That(manhattan.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));

            NavigationPlanningOperation chebyshev = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(start, FlyBodySize),
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f), FlyParameters);
            WaitForCompletion(chebyshev);
            Assert.That(chebyshev.Result.HasValue, Is.True);
            Assert.That(chebyshev.Result, Is.Empty,
                "The Chebyshev goal is already complete even though the Manhattan goal failed.");
        }

        /// <summary>Verifies Fly retreat planning honors the available approach budget.</summary>
        [Test]
        public void FlyRetreatPlanningHonorsApproachBudget()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = CreateOpenWorld();
            runtime.PublishWorld(world);
            Vector2 start = new(0.5f, 2.5f);
            NavigationGoalRequest goal = NavigationGoalRequest.Retreat(
                AABB.Point(1.5f, 2.5f), DistanceMetric.Euclidean, 3f);

            NavigationPlanningOperation constrained = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(start, new Vector2(0.8f, 0.8f)), goal,
                new FlyNavigationParameters(0.1f));
            WaitForCompletion(constrained);
            Assert.That(constrained.Result.HasValue, Is.False);
            Assert.That(constrained.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.SearchExhausted));
            runtime.ReleaseCompletedOperations();

            NavigationPlanningOperation relaxed = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(start, new Vector2(0.8f, 0.8f)), goal,
                new FlyNavigationParameters(1f));
            WaitForCompletion(relaxed);
            Assert.That(relaxed.Result.HasValue, Is.True,
                "A larger approach budget must not hit the failed-request cache entry for the smaller budget.");
            Assert.That(relaxed.Result.Goal.IsRetreat, Is.True);
        }

        /// <summary>Verifies Fly planning honors the goal's line-of-sight requirement.</summary>
        [Test]
        public void FlyPlanningHonorsLineOfSightRequirement()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            TestNavigationWorld world = new(new AABBInt(0, 0, 1, 1), Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            runtime.PublishWorld(world);
            AABB target = new(new Vector2(1.1f, 1.1f), new Vector2(1.1f, 1.1f));
            Vector2 start = new(0.5f, 0.5f);

            NavigationPlanningOperation requiresLineOfSight = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(start, FlyBodySize),
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f, true), FlyParameters);
            WaitForCompletion(requiresLineOfSight);
            Assert.That(requiresLineOfSight.Result.HasValue, Is.False);
            Assert.That(requiresLineOfSight.PlanResult.Termination, Is.EqualTo(NavigationPlanTermination.NoResult));

            NavigationPlanningOperation noLineOfSight = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(start, FlyBodySize),
                NavigationGoalRequest.Proximity(target, DistanceMetric.Chebyshev, 0.25f), FlyParameters);
            WaitForCompletion(noLineOfSight);
            Assert.That(noLineOfSight.Result.HasValue, Is.True);
            Assert.That(noLineOfSight.Result, Is.Empty,
                "The no-LOS goal is already complete even though the LOS goal failed.");
        }

        /// <summary>Verifies finite-world rejection accounts for the moving body before failing an exterior goal.</summary>
        [Test]
        public void ExteriorGoalEarlyFailUsesBodyExpandedWorldBoundary()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            NavigationPlanningOperation touching = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(new Vector2(0.5f, 2.5f), FlyBodySize),
                Goal(new Vector2(-0.1f, 2.5f)), FlyParameters);
            Assert.That(touching.IsCompleted, Is.False);

            NavigationPlanningOperation separated = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(new Vector2(0.5f, 2.5f), FlyBodySize),
                Goal(new Vector2(-10f, 2.5f)), FlyParameters);
            Assert.That(separated.IsCompleted, Is.True);
        }

        /// <summary>Verifies body-expanded early rejection preserves airborne and partially exterior targets.</summary>
        [Test]
        public void ExteriorGoalEarlyFailPreservesAirborneAndPartialRegions()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            runtime.PublishWorld(CreateOpenWorld());

            NavigationPlanningOperation airborne = runtime.PlanJumpAsync(
                AABB.FromLowerCenter(new Vector2(0.5f, 1f), JumpBodySize),
                Goal(new Vector2(2.5f, 3.5f)), JumpParameters);
            Assert.That(airborne.IsCompleted, Is.False);

            AABB partialBounds = new(new Vector2(-0.75f, 2f), new Vector2(0.25f, 3f));
            NavigationPlanningOperation partial = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(new Vector2(0.5f, 2.5f), FlyBodySize),
                NavigationGoalRequest.Proximity(partialBounds, DistanceMetric.Euclidean, 0f), FlyParameters);
            Assert.That(partial.IsCompleted, Is.False);
        }

        /// <summary>Verifies cancellation is finalized during the wait-for-world phase.</summary>
        [Test]
        public void CancellationWorksBeforeWorldPublication()
        {
            using CancellationTokenSource cancellation = new();
            using MapNavigationRuntime runtime = CreateRuntime();
            NavigationPlanningOperation operation = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(Vector2.one, FlyBodySize), Goal(Vector2.right), FlyParameters,
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
            NavigationPlanningOperation waiting = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(Vector2.one, FlyBodySize), Goal(Vector2.right), FlyParameters);
            InvalidOperationException failure = new("capture failed");
            runtime.FailWorld(failure);
            NavigationPlanningOperation later = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(Vector2.one, FlyBodySize), Goal(Vector2.right), FlyParameters);

            Assert.That(waiting.Exception, Is.SameAs(failure));
            Assert.That(later.Exception, Is.SameAs(failure));
            Assert.That(waiting.IsCompleted, Is.True);
            Assert.That(later.IsCompleted, Is.True);
        }

        /// <summary>Verifies world failure keeps its strict lifecycle contract.</summary>
        [Test]
        public void FailWorldRejectsInvalidLifecycleTransitions()
        {
            MapNavigationRuntime runtime = CreateRuntime();
            try
            {
                Assert.Throws<ArgumentNullException>(() => runtime.FailWorld(null));

                runtime.PublishWorld(CreateOpenWorld());
                Assert.Throws<InvalidOperationException>(() => runtime.FailWorld(new InvalidOperationException("too late")));
            }
            finally
            {
                runtime.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => runtime.FailWorld(new InvalidOperationException("disposed")));
        }

        /// <summary>Verifies disposal cancels queued work and rejects later use.</summary>
        [Test]
        public void DisposeCancelsQueuedOperations()
        {
            MapNavigationRuntime runtime = CreateRuntime();
            NavigationPlanningOperation operation = runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(Vector2.one, FlyBodySize), Goal(Vector2.right), FlyParameters);
            runtime.Dispose();
            Assert.That(operation.IsCancelled, Is.True);
            Assert.That(runtime.IsReady, Is.False);
            Assert.Throws<ObjectDisposedException>(() => runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(Vector2.zero, FlyBodySize), Goal(Vector2.one), FlyParameters));
            Assert.Throws<ObjectDisposedException>(() => runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(Vector2.one, FlyBodySize), Goal(Vector2.right), FlyParameters));
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

        /// <summary>Verifies zero-width or zero-height request bodies are rejected before queueing.</summary>
        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        public void RejectsNonPositivePlanningBody(float width, float height)
        {
            using MapNavigationRuntime runtime = CreateRuntime();

            Assert.That(() => runtime.PlanFlyAsync(
                AABB.FromCenterAndSize(Vector2.zero, new Vector2(width, height)),
                Goal(Vector2.one), FlyParameters),
                Throws.InstanceOf<ArgumentException>());
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
            => new(new AABBInt(0, 0, 6, 5), new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0),
                new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0),
            }, Array.Empty<Vector2Int>());

        private static TestNavigationWorld CreateBlockedWorld()
        {
            List<Vector2Int> solid = new();
            for (int y = 0; y < 5; y++) solid.Add(new Vector2Int(3, y));
            return new TestNavigationWorld(new AABBInt(0, 0, 6, 5), solid, Array.Empty<Vector2Int>());
        }

        private static NavigationGoalRequest Goal(Vector2 destination)
            => NavigationGoalRequest.Proximity(AABB.Point(destination), DistanceMetric.Euclidean, 0f);

        private static string DescribeRoute(NavigationRoute route)
        {
            if (!route.HasValue) return "Route=null";
            string segments = string.Join(", ", route.Select(segment =>
                $"{segment.GetType().Name}:{segment.Start}->{segment.End}"));
            return $"Route.ReachesGoal={route.ReachesGoal}; Route.ResolvedGoal={route.Endpoint}; "
                + $"Route=[{segments}]";
        }

        /// <summary>Counts immutable support queries made by planner requests.</summary>
        private sealed class CountingNavigationWorld : INavigationWorld
        {
            private readonly TestNavigationWorld world;
            public AABB WorldBounds => world.WorldBounds;
            public int SupportQueryCount => Volatile.Read(ref supportQueryCount);
            private int supportQueryCount;

            public CountingNavigationWorld(AABBInt cellBounds)
            {
                world = new TestNavigationWorld(cellBounds, Array.Empty<Vector2Int>(), Array.Empty<Vector2Int>());
            }

            public bool IsBodyClear(AABB bodyBounds, float tolerance) => world.IsBodyClear(bodyBounds, tolerance);
            public bool IsBodyPathClear(AABB bodyBounds, Vector2 displacement, float tolerance)
                => world.IsBodyPathClear(bodyBounds, displacement, tolerance);
            public bool IsLineOfSightClear(Vector2 start, Vector2 end) => world.IsLineOfSightClear(start, end);
            public bool TryGetSupportBelow(Vector2 position, out NavigationSupport support)
                => world.TryGetSupportBelow(position, out support);
            public bool TryResolveSupport(AABB body, float snapDistance, out NavigationSupport support)
            {
                Interlocked.Increment(ref supportQueryCount);
                support = default;
                return world.TryResolveSupport(body, snapDistance, out support);
            }
            public IReadOnlyList<NavigationSupportCandidate> GetSupportCandidates(AABB anchorBounds, Vector2 bodySize)
                => world.GetSupportCandidates(anchorBounds, bodySize);
            public void CollectSupportCandidates(AABB anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results)
                => world.CollectSupportCandidates(anchorBounds, bodySize, results);
            public void CollectOneWayCrossings(AABB previousBody, Vector2 displacement,
                List<NavigationSurfaceCrossing> results)
                => world.CollectOneWayCrossings(previousBody, displacement, results);
            public bool AreInSameRegion(Vector2 first, Vector2 second) => world.AreInSameRegion(first, second);
        }
    }
}
