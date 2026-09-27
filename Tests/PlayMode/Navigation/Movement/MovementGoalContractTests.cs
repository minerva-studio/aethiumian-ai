using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Aethiumian.AI.Navigation.Diagnostics;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies movement goal completion against package-owned runtime contracts.</summary>
    public sealed class MovementGoalContractTests : MovementNodePackageFixture
    {
        private const float ArrivalErrorBound = 0.2f;
        private const int GoalTickLimit = 240;
        private const float DirectionNoise = 0.25f;

        [TearDown]
        public void ResetReplanDiagnostics()
        {
            MovementReplanDiagnostics.Enabled = false;
            MovementReplanDiagnostics.Reset();
        }

        // An unpublished runtime leaves Plan*Async requests pending, so reaching a goal proves Naive bypasses the planner.
        [UnityTest]
        public IEnumerator NaiveWalkReachesDestinationWithoutPublishedWorld()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            Vector2 start = new(MovementStart.x, 0f);
            Walk node = CreateFixedWalk(start + Vector2.right * 3f);
            node.path = Movement.PathMode.Naive;
            MovementHarness harness = CreateHarness(start, node);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.GreaterThan(0), DescribeHarness(harness));
        }

        // The runtime has no published world; a planner request could not produce this route.
        [UnityTest]
        public IEnumerator NaiveFlyReachesDestinationWithoutPublishedWorld()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(34f, 5f));
            Fly node = CreateFlyTrace(target);
            node.path = Movement.PathMode.Naive;
            MovementHarness harness = CreateHarness(new Vector2(30f, 4f), node);
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SimpleWalkWaitsForPublishedWorld()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            Walk node = CreateFixedWalk(MovementStart + Vector2.right * 3f);
            node.path = Movement.PathMode.Simple;
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            for (int tick = 0; tick < 5; tick++) yield return new WaitForFixedUpdate();

            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True, DescribeHarness(harness));
            Assert.That(((Movement)harness.AI.BehaviourTree.Head).Route.HasValue, Is.False);
        }

        // The runtime has no published world; both completed jumps must use direct routes.
        [UnityTest]
        public IEnumerator NaiveJumpChainsTwoDirectSegmentsWithoutPublishedWorld()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            Jump node = new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Naive,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(MovementStart + Vector2.right * 6f),
                reachDistance = (VariableField<float>)0.3f,
                jumpHeight = (VariableField<float>)2f,
                jumpLength = (VariableField<float>)3f,
                jumpInterval = (VariableField<float>)0.1f,
                speedModifier = (VariableField<float>)1f,
            };
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, 500);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.True, DescribeHarness(harness));
            Assert.That(harness.Source.JumpCount, Is.EqualTo(2), DescribeHarness(harness));
        }

        // The unpublished runtime also rules out a planner route during recovery.
        [UnityTest]
        public IEnumerator NaiveJumpRecoversFromUnexpectedLanding()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            const float platformHeight = 1.5f;
            GameObject platform = CreateGround(platformHeight, 1.5f);
            platform.transform.position = new Vector2(MovementStart.x + 2.25f, platformHeight - 0.25f);
            platform.layer = NavigationPhysicsTestLayers.PlatformLayer;
            BoxCollider2D platformCollider = platform.GetComponent<BoxCollider2D>();
            platformCollider.usedByEffector = true;
            PlatformEffector2D effector = platform.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 90f;
            Physics2D.SyncTransforms();
            Jump node = new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Naive,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(MovementStart + Vector2.right * 6f),
                reachDistance = (VariableField<float>)0.3f,
                jumpHeight = (VariableField<float>)2f,
                jumpLength = (VariableField<float>)3f,
                jumpInterval = (VariableField<float>)0.1f,
                speedModifier = (VariableField<float>)1f,
            };
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            bool stoodOnPlatform = false;
            bool jumpedAgain = false;
            for (int tick = 0; tick < 500 && harness.AI.BehaviourTree.IsRunning; tick++)
            {
                yield return new WaitForFixedUpdate();
                float feetY = harness.Collider.bounds.min.y;
                if (harness.Source.JumpCount == 1 && Mathf.Abs(feetY - platformHeight) < 0.1f
                    && Mathf.Abs(harness.Body.linearVelocityY) < 0.1f)
                    stoodOnPlatform = true;
                if (stoodOnPlatform && harness.Source.JumpCount >= 2) jumpedAgain = true;
            }

            Assert.That(stoodOnPlatform, Is.True, DescribeHarness(harness));
            Assert.That(jumpedAgain, Is.True, DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.True, DescribeHarness(harness));
            Assert.That(harness.Source.JumpCount, Is.GreaterThanOrEqualTo(2), DescribeHarness(harness));
        }

        // A completed route with no published world proves the moved target did not invoke Plan*Async.
        [UnityTest]
        public IEnumerator NaiveFlyRefreshesRouteWhenTargetMoves()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(34f, 5f));
            Fly node = CreateFlyTrace(target);
            node.path = Movement.PathMode.Naive;
            MovementHarness harness = CreateHarness(new Vector2(30f, 4f), node);
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);
            yield return new WaitForFixedUpdate();
            target.transform.position = new Vector2(38f, 5f);
            Physics2D.SyncTransforms();
            Movement movement = (Movement)harness.AI.BehaviourTree.Head;
            bool refreshedBeforeOldTarget = false;
            for (int tick = 0; tick < GoalTickLimit && harness.AI.BehaviourTree.IsRunning; tick++)
            {
                yield return new WaitForFixedUpdate();
                if (harness.Body.position.x >= 34f) break;
                if (movement.ActiveSegment is FlyRouteSegment segment && segment.End.x > 37f)
                {
                    refreshedBeforeOldTarget = true;
                    break;
                }
            }
            Assert.That(refreshedBeforeOldTarget, Is.True, DescribeHarness(harness));
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.True, DescribeHarness(harness));
            Assert.That(harness.Body.position.x, Is.GreaterThan(37f), DescribeHarness(harness));
        }

        // Pause and completion with an unpublished world also exclude pending planner work.
        [UnityTest]
        public IEnumerator NaiveFlyHonorsMovementPause()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(34f, 5f));
            Fly node = CreateFlyTrace(target);
            node.path = Movement.PathMode.Naive;
            MovementHarness harness = CreateHarness(new Vector2(30f, 4f), node, canMove: false);
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);
            for (int tick = 0; tick < 5; tick++) yield return new WaitForFixedUpdate();
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True, DescribeHarness(harness));
            Assert.That(harness.Body.position.x, Is.EqualTo(30f).Within(0.05f));

            harness.Source.CanMove = true;
            yield return WaitForTerminal(harness, GoalTickLimit);
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.True, DescribeHarness(harness));
        }

        // A planner request would remain pending without a published world.
        [UnityTest]
        public IEnumerator NaiveWalkStopsAfterRepeatedNoProgressAtWall()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            CreateGround();
            GameObject wall = CreateGround();
            wall.transform.position = new Vector2(MovementStart.x + 2f, 1f);
            wall.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 3f);
            Physics2D.SyncTransforms();
            Vector2 start = new(MovementStart.x, 0f);
            Walk node = CreateFixedWalk(start + Vector2.right * 4f);
            node.path = Movement.PathMode.Naive;
            node.maxIdleDuration = (VariableField<float>)0f;
            MovementHarness harness = CreateHarness(start, node);
            yield return WaitForTreeCreated(harness);
            MovementReplanDiagnostics.Enabled = true;
            MovementReplanDiagnostics.Reset();
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.False, DescribeHarness(harness));
            Assert.That(MovementReplanDiagnostics.Capture().MovementStalls, Is.Zero, DescribeHarness(harness));
            Assert.That(harness.Body.position.x, Is.LessThan(MovementStart.x + 2f), DescribeHarness(harness));
        }

        // Cancellation completes despite the unpublished runtime having no planner results.
        [UnityTest]
        public IEnumerator NaiveFlyCancellationReleasesExecutor()
        {
            using MapNavigationRuntime runtime = CreateUnpublishedRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(50f, 5f));
            Fly node = CreateFlyTrace(target);
            node.path = Movement.PathMode.Naive;
            MovementHarness harness = CreateHarness(new Vector2(30f, 4f), node);
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);
            yield return new WaitForFixedUpdate();
            harness.AI.End(false);
            yield return new WaitForFixedUpdate();

            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.False, DescribeHarness(harness));
            Assert.That(((Movement)harness.AI.BehaviourTree.Head).Executor, Is.Null);
            Assert.That(runtime.IsDisposed, Is.False);
        }

        [UnityTest]
        public IEnumerator FlyCurrentPointCompletesAndClearsVelocity()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            Vector2 center = new(40.5f, 4f);
            GameObject target = CreateTraceTarget(center);
            MovementHarness harness = CreateHarness(center - Vector2.up * (BodyHeight * 0.5f), CreateFlyTrace(target));
            harness.Body.gravityScale = 0f;
            harness.Body.linearVelocity = new Vector2(4f, -2f);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity, Is.EqualTo(Vector2.zero).Within(0.001f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator FlyHighSpeedCrossingCompletesAndClearsVelocity()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            Vector2 center = new(40.5f, 4f);
            GameObject target = CreateTraceTarget(center);
            MovementHarness harness = CreateHarness(new Vector2(30f, 4f), CreateFlyTrace(target));
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);
            yield return new WaitForFixedUpdate();

            harness.Body.position = new Vector2(center.x - 2f, center.y);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            harness.Body.position = new Vector2(center.x + 2f, center.y);
            harness.Body.linearVelocity = new Vector2(300f, 40f);
            Physics2D.SyncTransforms();
            yield return WaitForTerminal(harness, 4);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity, Is.EqualTo(Vector2.zero).Within(0.001f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator WalkCurrentPointCompletesWithoutMovement()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            MovementHarness harness = CreateHarness(MovementStart, CreateFixedWalk(MovementStart));
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.Zero, DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity.x, Is.EqualTo(0f).Within(0.001f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SimpleNoPathFailsAndPreservesVerticalVelocity()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            Walk node = CreateFixedWalk(new Vector2(MovementStart.x, 14f));
            node.path = Movement.PathMode.Simple;
            MovementHarness harness = CreateHarness(new Vector2(MovementStart.x, 1f), node);
            harness.Body.gravityScale = 0f;
            harness.Body.linearVelocity = new Vector2(3f, 2f);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity.x, Is.EqualTo(0f).Within(0.001f), DescribeHarness(harness));
            Assert.That(harness.Body.linearVelocity.y, Is.GreaterThan(0.05f), DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator SimplePlateauDoesNotOscillate()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(56.5f, 5.9f));
            target.GetComponent<BoxCollider2D>().size = new Vector2(10f, 1f);
            Walk node = CreateTraceWalk(target);
            node.path = Movement.PathMode.Simple;
            MovementHarness harness = CreateHarness(MovementStart, node);
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);

            List<float> directions = new();
            for (int tick = 0; tick < GoalTickLimit && harness.AI.BehaviourTree.IsRunning; tick++)
            {
                yield return new WaitForFixedUpdate();
                float velocityX = harness.Body.linearVelocity.x;
                if (Mathf.Abs(velocityX) >= DirectionNoise) directions.Add(Mathf.Sign(velocityX));
            }

            int reversals = 0;
            for (int index = 1; index < directions.Count; index++)
                if (directions[index] != directions[index - 1]) reversals++;
            Assert.That(reversals, Is.EqualTo(0), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.False, DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator DirectWanderArrivalUsesGroundWalkSurfaceGap()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            MovementHarness harness = CreateHarness(MovementStart, CreateDirectWander(new Vector2(31f, 1f)));
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.Zero, DescribeHarness(harness));
        }

        /// <summary>Verifies Fly Wander final placement uses the destination its route resolved.</summary>
        [UnityTest]
        public IEnumerator FlyWanderSetFinalPositionUsesRouteResolvedEndpoint()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            Vector2 destination = new(33f, 1.9f);
            MovementHarness harness = CreateHarness(MovementStart, CreateDirectFlyWander(destination));
            harness.Body.gravityScale = 0f;
            yield return WaitForTreeCreated(harness);

            var movement = (Fly)harness.AI.BehaviourTree.Head;
            NavigationRoute route = default;
            for (int tick = 0; tick < RuntimeContractTickLimit && harness.AI.BehaviourTree.IsRunning; tick++)
            {
                yield return new WaitForFixedUpdate();
                if (movement.Route.HasValue) route = movement.Route;
            }

            Assert.That(harness.AI.BehaviourTree.IsFaulted, Is.False, DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(route.HasValue, Is.True, DescribeHarness(harness));
            Assert.That(route.CoordinateFrame, Is.EqualTo(NavigationRouteCoordinateFrame.BodyCenter));

            const float placementTolerance = 0.02f;
            Vector2 bodySize = NavigationBodyGeometry.GetWorldAabbSize(harness.Collider);
            AABB resolvedEndpoint = route.ResolveEndpointBody(AABB.FromLowerCenter(Vector2.zero, bodySize));
            Vector2 finalCenter = NavigationBodyGeometry.GetBodyCenter(harness.Collider);
            Assert.That(Vector2.Distance(finalCenter, destination), Is.LessThanOrEqualTo(placementTolerance),
                "Fly final placement must snap the body center onto the sampled Wander destination. " + DescribeHarness(harness));
            Assert.That(Vector2.Distance(finalCenter, resolvedEndpoint.Center), Is.LessThanOrEqualTo(placementTolerance),
                "An executed aerial route must leave the body at the body its resolved route endpoint describes. "
                + DescribeHarness(harness));
        }

        [UnityTest]
        public IEnumerator RegionPolicyGatesPlannerAtCanonicalDestination()
        {
            foreach (NavigationRegionPolicy policy in new[]
                { NavigationRegionPolicy.InRegion, NavigationRegionPolicy.CrossRegion })
            {
                using MapNavigationRuntime runtime = new(8, 4096, 4096);
                runtime.PublishWorld(NavigationWorldSnapshotFixtures.SplitRegions());
                using RuntimeContextScope context = new(runtime);
                RequestProbeWalk node = CreateRegionProbeWalk(new Vector2(70f, 1f));
                node.regionPolicy = policy;
                MovementHarness harness = CreateHarness(MovementStart, node);
                harness.Body.gravityScale = 0f;

                yield return WaitForTreeCreated(harness);
                RequestProbeWalk runtimeNode = (RequestProbeWalk)harness.AI.BehaviourTree.Head;
                Assert.That(runtimeNode.regionPolicy, Is.EqualTo(policy), DescribeHarness(harness));
                for (int tick = 0; tick < RuntimeContractTickLimit
                    && harness.AI.BehaviourTree.IsRunning && runtimeNode.RequestCount == 0; tick++)
                    yield return new WaitForFixedUpdate();

                Assert.That(runtimeNode.RequestCount,
                    Is.EqualTo(policy == NavigationRegionPolicy.InRegion ? 0 : 1),
                    DescribeHarness(harness));
                if (harness.AI.BehaviourTree.IsRunning) harness.AI.End(false);
            }
        }

        [UnityTest]
        public IEnumerator SimpleRetreatCompletesImmediatelyWhenAlreadyBeyondReachDistance()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(36.5f, 1.9f));
            MovementHarness harness = CreateHarness(MovementStart + Vector2.up, CreateSimpleRetreat(target, 3f));
            harness.Body.gravityScale = 0f;
            Vector2 initialBodyPosition = harness.Body.position;
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);

            // "Immediately" is observable as completing without ever moving the body.
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Source.WalkCount, Is.Zero, DescribeHarness(harness));
            Assert.That(harness.Body.position.x, Is.EqualTo(initialBodyPosition.x).Within(0.0001f));
            Assert.That(harness.Body.position.y, Is.EqualTo(initialBodyPosition.y).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator SimpleRetreatNoProgressCompletesAsFailure()
        {
            using MapNavigationRuntime runtime = CreateRuntime();
            using RuntimeContextScope context = new(runtime);
            GameObject target = CreateTraceTarget(new Vector2(33f, 1.9f));
            Fly node = CreateSimpleRetreat(target, 3f);
            node.speed = (VariableField<float>)0f;
            node.maxIdleDuration = (VariableField<float>)0.1f;
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, GoalTickLimit);

            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false), DescribeHarness(harness));
        }

        private static MapNavigationRuntime CreateRuntime()
        {
            MapNavigationRuntime runtime = new(8, 4096, 4096);
            AABB bounds = AABB.FromMinAndSize(-100f, -100f, 200f, 200f);
            runtime.PublishWorld(NavigationWorldSnapshot.Create(
                bounds,
                Array.Empty<NavigationShapeData>(),
                new[] { new NavigationRegionData(bounds, 0) }));
            return runtime;
        }

        private static MapNavigationRuntime CreateUnpublishedRuntime()
            => new(8, 4096, 4096,
                new NavigationPhysicsLayers(NavigationPhysicsTestLayers.GeometryMask, NavigationPhysicsTestLayers.PlatformMask));


        private static Fly CreateFlyTrace(GameObject target, MovementGoal goal = MovementGoal.Default)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.Trace,
                goal = goal,
                tracing = new VariableField(target),
                reachDistance = (VariableField<float>)ArrivalErrorBound,
                speed = (VariableField<float>)10f,
                speedModifier = (VariableField<float>)1f,
            };

        private static Walk CreateFixedWalk(Vector2 destination)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(destination),
                reachDistance = (VariableField<float>)ArrivalErrorBound,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                jumpHeight = (VariableField<float>)5f,
                jumpLength = (VariableField<float>)10f,
                setFinalPosition = (VariableField<bool>)false,
            };

        private static Walk CreateTraceWalk(GameObject target)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.Trace,
                goal = MovementGoal.Default,
                tracing = new VariableField(target),
                reachDistance = (VariableField<float>)ArrivalErrorBound,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                jumpHeight = (VariableField<float>)5f,
                jumpLength = (VariableField<float>)10f,
            };

        private static Walk CreateDirectWander(Vector2 center, float reachDistance = ArrivalErrorBound)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.Wander,
                wanderMode = Movement.WanderMode.AbsoluteCentered,
                centerSpace = Space.World,
                centerOfWander = new VariableField(center),
                wanderDistance = (VariableField<float>)0f,
                reachDistance = (VariableField<float>)reachDistance,
                accelerateRate = (VariableField<float>)1f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
                setFinalPosition = (VariableField<bool>)false,
            };

        private static Fly CreateDirectFlyWander(Vector2 center)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.Wander,
                wanderMode = Movement.WanderMode.AbsoluteCentered,
                centerSpace = Space.World,
                centerOfWander = new VariableField(center),
                wanderDistance = (VariableField<float>)0f,
                reachDistance = (VariableField<float>)ArrivalErrorBound,
                speed = (VariableField<float>)10f,
                speedModifier = (VariableField<float>)1f,
                setFinalPosition = (VariableField<bool>)true,
            };

        private static Fly CreateSimpleRetreat(GameObject target, float reachDistance)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.Retreat,
                tracing = new VariableField(target),
                reachDistance = (VariableField<float>)reachDistance,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
            };

        private static RequestProbeWalk CreateRegionProbeWalk(Vector2 destination)
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(destination),
                reachDistance = (VariableField<float>)0.2f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
            };

        [Serializable]
        public sealed class RequestProbeWalk : Walk
        {
            public int RequestCount { get; private set; }

            protected override bool TryRequestRoute(AABB body, NavigationGoalRequest goal,
                NavigationPlanningExtent extent, NavigationPlanningPurpose purpose,
                CancellationToken cancellation, out NavigationPlanningOperation operation)
            {
                RequestCount++;
                operation = null;
                return false;
            }
        }
    }

}
