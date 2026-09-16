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

        /// <summary>Verifies a reached PlannedStep target uses the same skip contract.</summary>
        [UnityTest]
        public IEnumerator PlannedStepReachedTargetUsesTheSameSkipContract()
            => ReachedTargetHonorsSkipContract(FixedJump.JumpTargetMode.PlannedStep, true, false, true);

        /// <summary>Verifies a reached PlannedStep target still launches when skipping is disabled.</summary>
        [UnityTest]
        public IEnumerator PlannedStepReachedTargetStillLaunchesWhenSkipDisabled()
            => ReachedTargetHonorsSkipContract(FixedJump.JumpTargetMode.PlannedStep, false, true, true);

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
