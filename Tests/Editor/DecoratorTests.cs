#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests
{
    public sealed class DecoratorTests
    {
        [TestCase(true, true, State.Success)]
        [TestCase(false, true, State.Success)]
        [TestCase(true, false, State.Failed)]
        [TestCase(false, false, State.Failed)]
        public void Always_ChildResultDoesNotChangeConfiguredResult(
            bool childResult,
            bool configuredResult,
            State expected)
        {
            Always always = new() { returnValue = configuredResult };

            Assert.That(always.ReceiveReturnFromChild(childResult), Is.EqualTo(expected));
        }

        [TestCase(true, State.Failed)]
        [TestCase(false, State.Success)]
        public void Inverter_ReturnsInverseOfChildResult(bool childResult, State expected)
        {
            Inverter inverter = new();

            Assert.That(inverter.ReceiveReturnFromChild(childResult), Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator Always_WithoutChildReturnsConfiguredResult()
        {
            Always always = TreeTestFixture.CreateNode<Always>("Always");
            always.returnValue = false;

            using TreeTestFixture fixture = TreeTestFixture.Create(always);
            yield return fixture.WaitUntilReady();
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
        }

        [UnityTest]
        public IEnumerator Capture_StoresAndForwardsSuccess()
        {
            yield return AssertCapture(childResult: true, initialValue: false);
        }

        [UnityTest]
        public IEnumerator Capture_StoresAndForwardsFailure()
        {
            yield return AssertCapture(childResult: false, initialValue: true);
        }

        [UnityTest]
        public IEnumerator Capture_WithoutResultVariableStillForwardsChildResult()
        {
            Capture capture = TreeTestFixture.CreateNode<Capture>("Capture");
            ResultNode child = TreeTestFixture.CreateNode<ResultNode>("Child");
            child.result = false;
            capture.node = new NodeReference(child.uuid);

            using TreeTestFixture fixture = TreeTestFixture.Create(capture, child);
            yield return fixture.WaitUntilReady();
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
        }

        [UnityTest]
        public IEnumerator Capture_WithoutChildDoesNotWriteResult()
        {
            VariableData result = CreateBoolVariable("Result", true);
            Capture capture = TreeTestFixture.CreateNode<Capture>("Capture");
            capture.result.SetReference(result);

            using TreeTestFixture fixture = TreeTestFixture.Create(capture, new[] { result });
            yield return fixture.WaitUntilReady();
            Capture runtimeCapture = fixture.GetRuntimeNode(capture);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
            Assert.That(runtimeCapture.result.BoolValue, Is.True);
        }

        [UnityTest]
        public IEnumerator Capture_ChildErrorDoesNotWriteResult()
        {
            VariableData result = CreateBoolVariable("Result", true);
            Capture capture = TreeTestFixture.CreateNode<Capture>("Capture");
            ErrorNode child = TreeTestFixture.CreateNode<ErrorNode>("Error");
            capture.node = new NodeReference(child.uuid);
            capture.result.SetReference(result);

            using TreeTestFixture fixture = TreeTestFixture.Create(capture, new[] { result }, child);
            yield return fixture.WaitUntilReady();
            Capture runtimeCapture = fixture.GetRuntimeNode(capture);
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("return invalid state"));
            fixture.Start();

            Assert.That(fixture.Tree.MainStack.IsPaused, Is.True);
            Assert.That(runtimeCapture.result.BoolValue, Is.True);
        }

        [UnityTest]
        public IEnumerator Capture_ChildExceptionDoesNotWriteResult()
        {
            VariableData result = CreateBoolVariable("Result", true);
            Capture capture = TreeTestFixture.CreateNode<Capture>("Capture");
            ThrowingNode child = TreeTestFixture.CreateNode<ThrowingNode>("Throw");
            capture.node = new NodeReference(child.uuid);
            capture.result.SetReference(result);

            using TreeTestFixture fixture = TreeTestFixture.Create(
                capture,
                new[] { result },
                NodeErrorSolution.Pause,
                child);
            yield return fixture.WaitUntilReady();
            Capture runtimeCapture = fixture.GetRuntimeNode(capture);
            LogAssert.Expect(LogType.Error, "Exception occurred at node [Throw]");
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Decorator test exception"));
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("return invalid state"));
            fixture.Start();

            Assert.That(fixture.Tree.MainStack.IsPaused, Is.True);
            Assert.That(runtimeCapture.result.BoolValue, Is.True);
        }

        [UnityTest]
        public IEnumerator Capture_InterruptedChildDoesNotWriteResult()
        {
            VariableData result = CreateBoolVariable("Result", true);
            Capture capture = TreeTestFixture.CreateNode<Capture>("Capture");
            YieldNode child = TreeTestFixture.CreateNode<YieldNode>("Yield");
            capture.node = new NodeReference(child.uuid);
            capture.result.SetReference(result);

            using TreeTestFixture fixture = TreeTestFixture.Create(capture, new[] { result }, child);
            yield return fixture.WaitUntilReady();
            Capture runtimeCapture = fixture.GetRuntimeNode(capture);
            fixture.Start();
            fixture.Tree.End();

            Assert.That(runtimeCapture.result.BoolValue, Is.True);
        }

        private static IEnumerator AssertCapture(bool childResult, bool initialValue)
        {
            VariableData result = CreateBoolVariable("Result", initialValue);
            Capture capture = TreeTestFixture.CreateNode<Capture>("Capture");
            ResultNode child = TreeTestFixture.CreateNode<ResultNode>("Child");
            child.result = childResult;
            capture.node = new NodeReference(child.uuid);
            capture.result.SetReference(result);

            using TreeTestFixture fixture = TreeTestFixture.Create(capture, new[] { result }, child);
            yield return fixture.WaitUntilReady();
            Capture runtimeCapture = fixture.GetRuntimeNode(capture);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.EqualTo(childResult));
            Assert.That(runtimeCapture.result.BoolValue, Is.EqualTo(childResult));
        }

        private static VariableData CreateBoolVariable(string name, bool value)
        {
            VariableData variable = new(name, VariableType.Bool);
            variable.SetDefaultValue(value);
            return variable;
        }

        [Serializable]
        private sealed class ResultNode : TreeNode
        {
            public bool result;

            public override void Initialize()
            {
            }

            public override State Execute()
            {
                return result ? State.Success : State.Failed;
            }
        }

        [Serializable]
        private sealed class ErrorNode : TreeNode
        {
            public override void Initialize()
            {
            }

            public override State Execute()
            {
                return State.Error;
            }
        }

        [Serializable]
        private sealed class YieldNode : TreeNode
        {
            public override void Initialize()
            {
            }

            public override State Execute()
            {
                return State.Yield;
            }
        }

        [Serializable]
        private sealed class ThrowingNode : TreeNode
        {
            public override void Initialize()
            {
            }

            public override State Execute()
            {
                throw new InvalidOperationException("Decorator test exception.");
            }
        }
    }
}
