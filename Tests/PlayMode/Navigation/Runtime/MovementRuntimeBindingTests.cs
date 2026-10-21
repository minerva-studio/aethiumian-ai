using System.Collections;
using System.Text.RegularExpressions;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Verifies package RuntimeContext binding before movement executor initialization.
    /// </summary>
    public sealed class MovementRuntimeBindingTests : MovementNodePackageFixture
    {
        [Test]
        public void FixedJumpSkipReachedDefaultsToFalse()
        {
            Assert.That((bool)new FixedJump().skipReached, Is.False);
        }

        /// <summary>A failed strict solve must not launch unless best effort was requested.</summary>
        [UnityTest]
        public IEnumerator FixedJumpUnreachableDirectTargetFailsWithoutBestEffort()
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            MovementHarness harness = CreateHarness(MovementStart, CreateSingleFixedJump(MovementStart + Vector2.right * 8f));
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.Source.JumpCount, Is.Zero);
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false));
        }

        /// <summary>Best effort limits horizontal travel and substitutes launch height for an unreachable height.</summary>
        [UnityTest]
        public IEnumerator FixedJumpBestEffortClampsRangeAndFallsBackToLaunchHeight()
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            FixedJump node = CreateSingleFixedJump(MovementStart + new Vector2(8f, 20f));
            node.bestEffort = true;
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.Source.JumpCount, Is.EqualTo(1), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(NavigationBodyGeometry.GetGroundAnchor(harness.Collider).x, Is.EqualTo(33.5f).Within(0.2f));
        }

        /// <summary>A planner with no supported endpoints still permits an explicitly requested best-effort impulse.</summary>
        [UnityTest]
        public IEnumerator FixedJumpBestEffortLaunchesWhenPlannedStepHasNoRoute()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096,
                new NavigationPhysicsLayers(NavigationPhysicsTestLayers.GeometryMask, NavigationPhysicsTestLayers.PlatformMask));
            runtime.PublishWorld(NavigationWorldSnapshotFixtures.Open());
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            FixedJump node = CreateSingleFixedJump(MovementStart + Vector2.right * 2f, FixedJump.JumpTargetMode.PlannedStep);
            node.allowAirborneLaunch = true;
            node.bestEffort = true;
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.Source.JumpCount, Is.EqualTo(1), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
        }

        /// <summary>Attempting a jump does not manufacture success when its unsupported endpoint is missed.</summary>
        [UnityTest]
        public IEnumerator FixedJumpBestEffortStillFailsWithoutTheRequiredLanding()
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            FixedJump node = CreateSingleFixedJump(MovementStart + new Vector2(2f, 1f));
            node.bestEffort = true;
            MovementHarness harness = CreateHarness(MovementStart, node);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.Source.JumpCount, Is.EqualTo(1), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false), DescribeHarness(harness));
        }

        /// <summary>An unavailable world returns false immediately; each later query observes current readiness and inputs.</summary>
        [UnityTest]
        public IEnumerator CanFixedJumpQueriesCurrentWorldAndInputsSynchronously()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096,
                new NavigationPhysicsLayers(NavigationPhysicsTestLayers.GeometryMask, NavigationPhysicsTestLayers.PlatformMask));
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            CanFixedJump query = new()
            {
                uuid = UUID.NewUUID(),
                target = new VariableField(MovementStart + Vector2.right * 2f),
                jumpHeight = 3f,
            };
            MovementHarness harness = CreateHarness(MovementStart, query, canMove: false);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false));
            Assert.That(harness.Source.CurrentMovementState, Is.EqualTo(MovementState.Unspecified));
            runtime.PublishWorld(NavigationWorldSnapshotFixtures.SolidGround(1f));
            yield return WaitUntilGrounded(harness);
            CanFixedJump runtimeQuery = (CanFixedJump)harness.AI.BehaviourTree.Head;
            Assert.That(runtimeQuery.Execute(), Is.EqualTo(State.Success));
            runtimeQuery.target = new VariableField(MovementStart + Vector2.right * 8f);
            Assert.That(runtimeQuery.Execute(), Is.EqualTo(State.Failed));
            Assert.That(harness.Source.CurrentMovementState, Is.EqualTo(MovementState.Unspecified));
        }

        /// <summary>Feasibility remains strict when the planner cannot find any supported next action.</summary>
        [UnityTest]
        public IEnumerator CanFixedJumpRejectsPlannedStepWithoutLandingSupport()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096,
                new NavigationPhysicsLayers(NavigationPhysicsTestLayers.GeometryMask, NavigationPhysicsTestLayers.PlatformMask));
            runtime.PublishWorld(NavigationWorldSnapshotFixtures.Open());
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            CanFixedJump query = new()
            {
                uuid = UUID.NewUUID(),
                targetMode = FixedJump.JumpTargetMode.PlannedStep,
                target = new VariableField(MovementStart + Vector2.right * 2f),
                jumpHeight = 3f,
                allowAirborneLaunch = true,
            };
            MovementHarness harness = CreateHarness(MovementStart, query, canMove: false);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false), DescribeHarness(harness));
            Assert.That(harness.Source.CurrentMovementState, Is.EqualTo(MovementState.Unspecified));
        }

        /// <summary>Direct feasibility rejects an out-of-range target even though best-effort execution could launch.</summary>
        [UnityTest]
        public IEnumerator CanFixedJumpRejectsOutOfRangeDirectTarget()
            => VerifyFixedJumpQuery(FixedJump.JumpTargetMode.Direct, 8f, false);

        /// <summary>PlannedStep feasibility accepts a local jump toward a more distant goal.</summary>
        [UnityTest]
        public IEnumerator CanFixedJumpAcceptsPlannedStepWithoutMoving()
            => VerifyFixedJumpQuery(FixedJump.JumpTargetMode.PlannedStep, 8f, true);

        /// <summary>A feasible direct query completes without requiring movement permission or reporting locomotion.</summary>
        [UnityTest]
        public IEnumerator CanFixedJumpAcceptsDirectTargetWithoutMoving()
            => VerifyFixedJumpQuery(FixedJump.JumpTargetMode.Direct, 2f, true);

        /// <summary>Checks a strict query result and the absence of movement effects through the package runtime fixture.</summary>
        private IEnumerator VerifyFixedJumpQuery(FixedJump.JumpTargetMode mode, float distance, bool expected)
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            CanFixedJump query = new()
            {
                uuid = UUID.NewUUID(),
                targetMode = mode,
                target = new VariableField(MovementStart + Vector2.right * distance),
                jumpHeight = 3f,
                jumpLength = 3f,
            };
            MovementHarness harness = CreateHarness(MovementStart, query, canMove: false);
            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(expected), DescribeHarness(harness));
            CanFixedJump runtimeQuery = (CanFixedJump)harness.AI.BehaviourTree.Head;
            Assert.That(runtimeQuery.Execute(), Is.EqualTo(expected ? State.Success : State.Failed));
            Assert.That(harness.Source.JumpCount, Is.Zero);
            Assert.That(harness.Source.WalkCount, Is.Zero);
            Assert.That(harness.Source.CurrentMovementState, Is.EqualTo(MovementState.Unspecified));
            Assert.That(harness.Body.position.x, Is.EqualTo(MovementStart.x).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator MissingOrDisposedRuntimeFails()
        {
            foreach (bool disposed in new[] { false, true })
            {
                using MapNavigationRuntime runtime = new(8, 128, 128);
                using RuntimeContextScope context = new(disposed ? runtime : null);
                if (disposed) runtime.Dispose();
                LogAssert.Expect(LogType.Error, new Regex("Exception occurred at node"));
                LogAssert.Expect(LogType.Exception,
                    new Regex("requires a live NavigationRuntimeContext.Current"));
                MovementHarness harness = CreateHarness(MovementStart, CreateFixedWalk());
                yield return WaitForTreeCreated(harness);
                yield return WaitForTerminal(harness, RuntimeContractTickLimit);
                var movement = (Walk)harness.AI.BehaviourTree.Head;
                Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false));
                Assert.That(movement.NavigationRuntime, Is.Null);
                Assert.That(movement.Executor, Is.Null);
                Assert.That(harness.Source.WalkCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator FixedJumpWaitsForUnpublishedRuntime()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096);
            using RuntimeContextScope context = new(runtime);
            MovementHarness harness = CreateHarness(MovementStart, CreateSingleFixedJump(new Vector2(32.5f, 1f)));
            yield return WaitForTreeCreated(harness);
            for (int tick = 0; tick < 3; tick++) yield return new WaitForFixedUpdate();

            Assert.That(harness.AI.BehaviourTree.IsRunning, Is.True);
            Assert.That(harness.Source.JumpCount, Is.Zero);
            Assert.That(runtime.IsDisposed, Is.False);
            harness.AI.End(false);
        }

        /// <summary>Verifies a reached Direct target skips its launch when skipping is requested.</summary>
        [UnityTest]
        public IEnumerator FixedJumpSkipReachedCompletesWithoutLaunching()
            => ReachedTargetHonorsSkipContract(FixedJump.JumpTargetMode.Direct, true, false, true);

        /// <summary>Verifies a reached Direct target still launches when skipping is disabled.</summary>
        [UnityTest]
        public IEnumerator FixedJumpReachedTargetStillLaunchesWhenSkipDisabled()
            => ReachedTargetHonorsSkipContract(FixedJump.JumpTargetMode.Direct, false, true, true);

        [UnityTest]
        public IEnumerator FixedJumpInRegionRejectsCrossRegionLandingBeforeLaunch()
        {
            using MapNavigationRuntime runtime = new(8, 4096, 4096);
            runtime.PublishWorld(NavigationWorldSnapshotFixtures.SplitRegions());
            using RuntimeContextScope context = new(runtime);
            FixedJump node = CreateSingleFixedJump(new Vector2(70f, 1f));
            node.bestEffort = true;
            MovementHarness harness = CreateHarness(MovementStart, node);

            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);

            Assert.That(harness.Source.JumpCount, Is.Zero, DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(false),
                DescribeHarness(harness));
        }

        /// <summary>Verifies a reached PlannedStep target uses the same skip contract.</summary>
        [UnityTest]
        public IEnumerator PlannedStepReachedTargetUsesTheSameSkipContract()
            => ReachedTargetHonorsSkipContract(FixedJump.JumpTargetMode.PlannedStep, true, false, true);

        /// <summary>Verifies a reached PlannedStep target still launches when skipping is disabled.</summary>
        [UnityTest]
        public IEnumerator PlannedStepReachedTargetStillLaunchesWhenSkipDisabled()
            => ReachedTargetHonorsSkipContract(FixedJump.JumpTargetMode.PlannedStep, false, true, true);

        /// <summary>PlannedStep accepts a transform-only target, matching Simple Jump's Trace geometry.</summary>
        [UnityTest]
        public IEnumerator PlannedStepAcceptsTransformOnlyTarget()
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            GameObject target = CreateTraceTarget(MovementStart + Vector2.right * 2f);
            Object.DestroyImmediate(target.GetComponent<Collider2D>());
            FixedJump node = CreateSingleFixedJump(Vector2.zero, FixedJump.JumpTargetMode.PlannedStep);
            node.target = new VariableField(target);
            MovementHarness harness = CreateHarness(MovementStart, node);

            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);

            Assert.That(harness.Source.JumpCount, Is.EqualTo(1), DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
        }

        /// <summary>FixedJump executes the same first endpoint selected by Simple Jump for a multi-step goal.</summary>
        [UnityTest]
        public IEnumerator PlannedStepMatchesSimpleJumpFirstEndpoint()
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            Vector2 target = MovementStart + Vector2.right * 7f;
            Jump simple = new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Simple,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(target),
                jumpHeight = (VariableField<float>)3f,
                jumpLength = (VariableField<float>)3f,
                reachDistance = (VariableField<float>)0f,
            };
            MovementHarness simpleHarness = CreateHarness(MovementStart, simple);
            yield return WaitForTreeCreated(simpleHarness);
            Jump runtimeSimple = (Jump)simpleHarness.AI.BehaviourTree.Head;
            for (int tick = 0; tick < RuntimeContractTickLimit && !runtimeSimple.Route.HasValue; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(runtimeSimple.Route.HasValue, Is.True, DescribeHarness(simpleHarness));
            Vector2 firstEndpoint = runtimeSimple.Route[0].End;
            Assert.That(runtimeSimple.Route.ReachesGoal, Is.False);
            simpleHarness.AI.End(false);
            simpleHarness.Host.SetActive(false);

            MovementHarness fixedHarness = CreateHarness(MovementStart,
                CreateSingleFixedJump(target, FixedJump.JumpTargetMode.PlannedStep));
            yield return WaitForTreeCreated(fixedHarness);
            yield return WaitForTerminal(fixedHarness, RuntimeContractTickLimit);
            Assert.That(fixedHarness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(fixedHarness));
            Assert.That(Vector2.Distance(NavigationBodyGeometry.GetGroundAnchor(fixedHarness.Collider), firstEndpoint),
                Is.LessThan(0.2f), "FixedJump must land at Simple Jump's first endpoint, rather than the final target.");
        }

        [UnityTest]
        public IEnumerator FixedJumpDirectAllowsAirborneEndpoint() => VerifyUnsupportedFixedJump(FixedJump.JumpTargetMode.Direct, false);

        [UnityTest]
        public IEnumerator FixedJumpPlannedStepAllowsAirborneEndpoint() => VerifyUnsupportedFixedJump(FixedJump.JumpTargetMode.PlannedStep, false);

        [UnityTest]
        public IEnumerator FixedJumpPlannedStepAllowsAirborneLaunch() => VerifyUnsupportedFixedJump(FixedJump.JumpTargetMode.PlannedStep, true);

        /// <summary>Verifies node wiring for grounded-to-airborne and airborne-to-airborne jumps.</summary>
        private IEnumerator VerifyUnsupportedFixedJump(FixedJump.JumpTargetMode mode, bool allowAirborneLaunch)
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            Vector2 start = MovementStart + (allowAirborneLaunch ? Vector2.up * 2f : Vector2.zero);
            FixedJump node = CreateSingleFixedJump(start + Vector2.one, mode);
            node.allowAirborneLaunch = (VariableField<bool>)allowAirborneLaunch;
            node.allowAirborneEndpoint = (VariableField<bool>)true;
            MovementHarness harness = CreateHarness(start, node);

            yield return WaitForTreeCreated(harness);
            yield return WaitForTerminal(harness, RuntimeContractTickLimit);
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(true), DescribeHarness(harness));
            Assert.That(harness.Collider.IsTouchingLayers(NavigationPhysicsTestLayers.GeometryMask), Is.False, DescribeHarness(harness));
        }

        private IEnumerator ReachedTargetHonorsSkipContract(
            FixedJump.JumpTargetMode mode, bool skipReached, bool expectedLaunch, bool expectedResult)
        {
            using MapNavigationRuntime runtime = CreateGroundRuntime(1f);
            using RuntimeContextScope context = new(runtime);
            CreateGround(1f);
            FixedJump node = CreateSingleFixedJump(MovementStart, mode);
            node.reachDistance = (VariableField<float>)0.05f;
            node.skipReached = (VariableField<bool>)skipReached;
            MovementHarness harness = CreateHarness(MovementStart, node);

            yield return WaitForTreeCreated(harness);
            for (int tick = 0; tick < RuntimeContractTickLimit && harness.AI.BehaviourTree.IsRunning; tick++)
                yield return new WaitForFixedUpdate();

            Assert.That(harness.Source.JumpCount, Is.EqualTo(expectedLaunch ? 1 : 0),
                DescribeHarness(harness));
            Assert.That(harness.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(expectedResult),
                DescribeHarness(harness));
        }

        private static MapNavigationRuntime CreateGroundRuntime(float y)
        {
            MapNavigationRuntime runtime = new(
                8,
                4096,
                4096,
                new NavigationPhysicsLayers(
                    NavigationPhysicsTestLayers.GeometryMask,
                    NavigationPhysicsTestLayers.PlatformMask));
            // A one-way snapshot surface needs an authored PlatformEffector2D source binding before a
            // jump can create its launch lease, so this fixture uses a solid floor.
            runtime.PublishWorld(NavigationWorldSnapshotFixtures.SolidGround(y));
            return runtime;
        }

        private static Walk CreateFixedWalk()
            => new()
            {
                uuid = UUID.NewUUID(),
                path = Movement.PathMode.Smart,
                type = Movement.Behaviour.FixedDestination,
                destination = new VariableField(MovementStart + Vector2.right * 10f),
                reachDistance = (VariableField<float>)0.2f,
                jumpHeight = (VariableField<float>)3f,
                jumpLength = (VariableField<float>)3f,
                speed = (VariableField<float>)5f,
                speedModifier = (VariableField<float>)1f,
            };

        private static FixedJump CreateSingleFixedJump(Vector2 target,
            FixedJump.JumpTargetMode mode = FixedJump.JumpTargetMode.Direct)
            => new()
            {
                uuid = UUID.NewUUID(),
                targetMode = mode,
                target = new VariableField(target),
                jumpHeight = new VariableField(3f),
                jumpLength = 3f,
                reachDistance = (VariableField<float>)0f,
                offset = Vector2.zero,
            };
    }
}
