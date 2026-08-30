#nullable enable
using Aethiumian.AI.Editor.Tests.Support;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests.Execution
{
    public sealed class BehaviourTreeFaultContractTests
    {
        [Test]
        public void FaultPolicy_PreservesSerializedOrdinal()
        {
            Assert.That((int)BehaviourTreeErrorSolution.Fault, Is.EqualTo(0));
            Assert.That((int)BehaviourTreeErrorSolution.Restart, Is.EqualTo(1));
            Assert.That((int)BehaviourTreeErrorSolution.Throw, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RuntimeNodeException_LatchesFault()
        {
            FaultingAction node = TreeTestFixture.CreateNode<FaultingAction>("Fault");
            using TreeTestFixture fixture = CreateFaultFixture(node);
            yield return fixture.WaitUntilReady();

            LogFaultMessages();
            fixture.Start();
            fixture.Tick();

            Assert.That(fixture.Tree.IsFaulted, Is.True);
            Assert.That(fixture.Tree.RuntimeFault, Is.Not.Null);
            Assert.That(fixture.Tree.RuntimeFault!.Source, Is.EqualTo("node:Fault"));
        }

        [UnityTest]
        public IEnumerator FaultedTree_DoesNotAdvanceAfterFault()
        {
            FaultingAction node = TreeTestFixture.CreateNode<FaultingAction>("Fault");
            using TreeTestFixture fixture = CreateFaultFixture(node);
            yield return fixture.WaitUntilReady();

            LogFaultMessages();
            fixture.Start();
            fixture.Tick();
            FaultingAction runtimeNode = fixture.GetRuntimeNode(node);
            int updateCount = runtimeNode.UpdateCount;
            int lateUpdateCount = runtimeNode.LateUpdateCount;
            int fixedUpdateCount = runtimeNode.FixedUpdateCount;

            fixture.Tree.Update();
            fixture.Tree.LateUpdate();
            fixture.Tree.FixedUpdate();

            Assert.That(runtimeNode.UpdateCount, Is.EqualTo(updateCount));
            Assert.That(runtimeNode.LateUpdateCount, Is.EqualTo(lateUpdateCount));
            Assert.That(runtimeNode.FixedUpdateCount, Is.EqualTo(fixedUpdateCount));
            Assert.That(fixture.Tree.IsFaulted, Is.True);
        }

        [UnityTest]
        public IEnumerator FaultedTree_RejectsStartAndStartFromNode_WithoutClearingFault()
        {
            FaultingAction node = TreeTestFixture.CreateNode<FaultingAction>("Fault");
            using TreeTestFixture fixture = CreateFaultFixture(node);
            yield return fixture.WaitUntilReady();

            LogFaultMessages();
            fixture.Start();
            fixture.Tick();
            FaultingAction runtimeNode = fixture.GetRuntimeNode(node);
            fixture.Tree.End();

            fixture.Tree.Start();
            Assert.That(fixture.Tree.IsRunning, Is.False);
            Assert.That(fixture.Tree.StartFromNode(runtimeNode), Is.False);
            Assert.That(fixture.Tree.IsFaulted, Is.True);
        }

        [UnityTest]
        public IEnumerator Restart_ClearsRuntimeFaultAndAllowsExecution()
        {
            FaultingAction node = TreeTestFixture.CreateNode<FaultingAction>("Fault");
            using TreeTestFixture fixture = CreateFaultFixture(node);
            yield return fixture.WaitUntilReady();

            LogFaultMessages();
            fixture.Start();
            fixture.Tick();
            Assert.That(fixture.Tree.IsFaulted, Is.True);

            FaultingAction runtimeNode = fixture.GetRuntimeNode(node);
            int startCountBeforeRestart = runtimeNode.StartCount;
            fixture.Tree.Restart();

            Assert.That(fixture.Tree.IsFaulted, Is.False);
            Assert.That(fixture.Tree.IsRunning, Is.True);
            fixture.Tick();
            Assert.That(runtimeNode.StartCount, Is.EqualTo(startCountBeforeRestart + 1));
            Assert.That(fixture.Tree.IsFaulted, Is.False);
        }

        [UnityTest]
        public IEnumerator StartException_LatchesFaultAndCleansActiveStacks()
        {
            FaultingAction node = TreeTestFixture.CreateNode<FaultingAction>("StartFault");
            StartFaultService service = TreeTestFixture.CreateNode<StartFaultService>("StartService");
            node.AddService(service);
            using TreeTestFixture fixture = TreeTestFixture.Create(
                node,
                Array.Empty<VariableData>(),
                NodeErrorSolution.Pause,
                service);
            yield return fixture.WaitUntilReady();

            Assert.Throws<InvalidOperationException>(() => fixture.Start());

            Assert.That(fixture.Tree.IsFaulted, Is.True);
            Assert.That(fixture.Tree.RuntimeFault!.Source, Is.EqualTo("tree start"));
            Assert.That(fixture.Tree.IsRunning, Is.False);
            Assert.That(fixture.Tree.ActiveStacks, Is.Empty);
            Assert.That(fixture.Tree.ServiceStacks, Is.Empty);
        }

        private static TreeTestFixture CreateFaultFixture(FaultingAction node)
        {
            return TreeTestFixture.Create(
                node,
                Array.Empty<VariableData>(),
                NodeErrorSolution.Pause);
        }

        private static void LogFaultMessages()
        {
            LogAssert.Expect(UnityEngine.LogType.Error, "Exception occurred at node [Fault]");
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("fault test exception"));
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("return invalid state"));
        }

        [Serializable]
        private sealed class FaultingAction : Aethiumian.AI.Nodes.Action
        {
            private bool shouldThrow = true;
            public int StartCount { get; private set; }
            public int UpdateCount { get; private set; }
            public int LateUpdateCount { get; private set; }
            public int FixedUpdateCount { get; private set; }

            public override void Start()
            {
                StartCount++;
                if (shouldThrow)
                {
                    shouldThrow = false;
                    throw new InvalidOperationException("fault test exception");
                }
            }

            public override void Update() => UpdateCount++;
            public override void LateUpdate() => LateUpdateCount++;
            public override void FixedUpdate() => FixedUpdateCount++;
        }

        [Serializable]
        private sealed class StartFaultService : Service
        {
            public override bool IsReady => false;

            public override void Initialize()
            {
            }

            public override State Execute() => State.Success;

            public override void OnRegistered()
            {
                throw new InvalidOperationException("start registration failure");
            }

            public override void UpdateTimer()
            {
            }
        }
    }
}
