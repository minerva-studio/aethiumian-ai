#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests
{
    public sealed class BehaviourTreeStartFromNodeTests
    {
        [UnityTest]
        public IEnumerator StartFromNode_StartsCurrentRunFromTargetInsteadOfHead()
        {
            var head = TreeTestFixture.CreateNode<CountingResultFlow>("Head");
            var target = TreeTestFixture.CreateNode<YieldingProbeFlow>("Forced Target");

            using var fixture = TreeTestFixture.Create(head, target);
            yield return fixture.WaitUntilReady();

            var runtimeHead = fixture.GetRuntimeNode(head);
            var runtimeTarget = fixture.GetRuntimeNode(target);

            Assert.That(fixture.Tree.StartFromNode(runtimeTarget), Is.True);

            Assert.That(runtimeHead.runCount, Is.EqualTo(0));
            Assert.That(runtimeTarget.runCount, Is.EqualTo(1));
            Assert.That(fixture.Tree.CurrentStage.Node, Is.SameAs(runtimeTarget));
        }

        [UnityTest]
        public IEnumerator StartFromNode_CompletedSuccessRunEndsWithoutRestartingHead()
        {
            var head = TreeTestFixture.CreateNode<CountingResultFlow>("Head");
            var target = TreeTestFixture.CreateNode<CountingResultFlow>("Forced Target");
            target.returnValue = true;

            using var fixture = TreeTestFixture.Create(head, target);
            yield return fixture.WaitUntilReady();

            var runtimeHead = fixture.GetRuntimeNode(head);
            var runtimeTarget = fixture.GetRuntimeNode(target);

            Assert.That(fixture.Tree.StartFromNode(runtimeTarget), Is.True);
            yield return null;

            Assert.That(fixture.Tree.IsRunning, Is.False);
            Assert.That(runtimeTarget.runCount, Is.EqualTo(1));
            Assert.That(runtimeHead.runCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator StartFromNode_CompletedFailedRunEndsWithoutRestartingHead()
        {
            var head = TreeTestFixture.CreateNode<CountingResultFlow>("Head");
            var target = TreeTestFixture.CreateNode<CountingResultFlow>("Forced Target");
            target.returnValue = false;

            using var fixture = TreeTestFixture.Create(head, target);
            yield return fixture.WaitUntilReady();

            var runtimeHead = fixture.GetRuntimeNode(head);
            var runtimeTarget = fixture.GetRuntimeNode(target);

            Assert.That(fixture.Tree.StartFromNode(runtimeTarget), Is.True);
            yield return null;

            Assert.That(fixture.Tree.IsRunning, Is.False);
            Assert.That(runtimeTarget.runCount, Is.EqualTo(1));
            Assert.That(runtimeHead.runCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator StartFromNode_NormalStartAfterForcedRunUsesStandardHead()
        {
            var head = TreeTestFixture.CreateNode<CountingResultFlow>("Head");
            var target = TreeTestFixture.CreateNode<CountingResultFlow>("Forced Target");

            using var fixture = TreeTestFixture.Create(head, target);
            yield return fixture.WaitUntilReady();

            var runtimeHead = fixture.GetRuntimeNode(head);
            var runtimeTarget = fixture.GetRuntimeNode(target);

            Assert.That(fixture.Tree.StartFromNode(runtimeTarget), Is.True);
            yield return null;

            fixture.Tree.Start();
            yield return null;

            Assert.That(runtimeTarget.runCount, Is.EqualTo(1));
            Assert.That(runtimeHead.runCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StartFromNode_InvalidTargetsDoNotChangeRunningStack()
        {
            var head = TreeTestFixture.CreateNode<YieldingProbeFlow>("Head");
            var target = TreeTestFixture.CreateNode<CountingResultFlow>("Forced Target");

            using var fixture = TreeTestFixture.Create(head, target);
            yield return fixture.WaitUntilReady();

            var runtimeHead = fixture.GetRuntimeNode(head);
            fixture.Tree.Start();
            yield return null;

            Assert.That(fixture.Tree.CurrentStage.Node, Is.SameAs(runtimeHead));
            Assert.That(fixture.Tree.StartFromNode((TreeNode)null!), Is.False);
            Assert.That(fixture.Tree.StartFromNode(TreeTestFixture.CreateNode<CountingResultFlow>("External")), Is.False);
            Assert.That(fixture.Tree.StartFromNode(UUID.NewUUID()), Is.False);

            Assert.That(fixture.Tree.IsRunning, Is.True);
            Assert.That(fixture.Tree.CurrentStage.Node, Is.SameAs(runtimeHead));
            Assert.That(runtimeHead.runCount, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator StartFromNode_UninitializedTreeReturnsFalse()
        {
            var head = TreeTestFixture.CreateNode<CountingResultFlow>("Head");

            using var fixture = TreeTestFixture.Create(head);

            if (!fixture.Tree.IsInitialized)
            {
                Assert.That(fixture.Tree.StartFromNode(head.uuid), Is.False);
            }

            yield return fixture.WaitUntilReady();
        }

        [UnityTest]
        public IEnumerator StartFromNode_TargetServicesRegisterAndCleanUpWithForcedRun()
        {
            var head = TreeTestFixture.CreateNode<CountingResultFlow>("Head");
            var target = TreeTestFixture.CreateNode<CountingResultFlow>("Forced Target");
            var service = TreeTestFixture.CreateNode<CountingService>("Target Service");
            target.AddService(service);

            using var fixture = TreeTestFixture.Create(head, target, service);
            yield return fixture.WaitUntilReady();

            var runtimeTarget = fixture.GetRuntimeNode(target);
            var runtimeService = fixture.GetRuntimeNode(service);

            Assert.That(fixture.Tree.StartFromNode(runtimeTarget), Is.True);
            yield return null;

            Assert.That(runtimeService.registeredCount, Is.EqualTo(1));
            Assert.That(runtimeService.unregisteredCount, Is.EqualTo(1));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeService), Is.False);
        }

        [Serializable]
        private sealed class CountingResultFlow : Flow
        {
            public bool returnValue = true;
            public int runCount;

            public override State Execute()
            {
                // Count root execution so tests can distinguish forced and normal starts.
                runCount++;
                return StateOf(returnValue);
            }

            public override void Initialize()
            {
                runCount = 0;
            }
        }

        [Serializable]
        private sealed class YieldingProbeFlow : Flow
        {
            public int runCount;

            public override State Execute()
            {
                // Yield keeps the stack alive long enough to assert the active root.
                runCount++;
                return State.Yield;
            }

            public override void Initialize()
            {
                runCount = 0;
            }
        }

        [Serializable]
        private sealed class CountingService : Service
        {
            public int registeredCount;
            public int unregisteredCount;

            public override bool IsReady => false;

            public override State Execute()
            {
                return State.Success;
            }

            public override void Initialize()
            {
                registeredCount = 0;
                unregisteredCount = 0;
            }

            public override void OnRegistered()
            {
                registeredCount++;
            }

            public override void OnUnregistered()
            {
                unregisteredCount++;
            }

            public override void UpdateTimer()
            {
            }
        }
    }
}
