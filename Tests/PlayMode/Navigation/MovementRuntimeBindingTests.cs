using System;
using System.Collections;
using System.Text.RegularExpressions;
using Aethiumian.AI.Navigation;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests.Navigation
{
    /// <summary>
    /// Verifies package RuntimeContext binding before movement executor initialization.
    /// </summary>
    public sealed class MovementRuntimeBindingTests : MovementNodePackageFixture
    {
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
                Assert.That(movement.TraversalExecutor, Is.Null);
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

        private static FixedJump CreateSingleFixedJump(Vector2 target)
            => new()
            {
                uuid = UUID.NewUUID(),
                targetMode = FixedJump.JumpTargetMode.Direct,
                target = new VariableField(target),
                jumpHeight = new VariableField(3f),
                jumpLength = 3f,
                offset = Vector2.zero,
            };
    }
}
