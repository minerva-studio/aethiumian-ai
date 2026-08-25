#nullable enable
using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

using Aethiumian.AI.Editor.Tests.Support;

namespace Aethiumian.AI.Editor.Tests.Execution
{
    public sealed class DecoratorTests
    {
        [Test]
        public void Decorators_AreNotFlowOrServiceHosts()
        {
            Assert.That(new Always(), Is.Not.InstanceOf<Flow>());
            Assert.That(new Always(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new Inverter(), Is.Not.InstanceOf<Flow>());
            Assert.That(new Inverter(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new Capture(), Is.Not.InstanceOf<Flow>());
            Assert.That(new Capture(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new ResultChanged(), Is.Not.InstanceOf<Flow>());
            Assert.That(new ResultChanged(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new Repeat(), Is.Not.InstanceOf<Flow>());
            Assert.That(new Repeat(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new Retry(), Is.Not.InstanceOf<Flow>());
            Assert.That(new Retry(), Is.Not.InstanceOf<IServiceHostNode>());
        }

        [UnityTest]
        public IEnumerator Retry_SucceedsAfterTransientFailures()
        {
            Retry retry = TreeTestFixture.CreateNode<Retry>("Retry");
            retry.maxAttempts = 3;
            ScriptedResultNode child = TreeTestFixture.CreateNode<ScriptedResultNode>("Child");
            child.results = new[] { false, false, true };
            retry.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(retry, child);
            yield return fixture.WaitUntilReady();
            ScriptedResultNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(runtimeChild.executions, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Retry_FailsAfterMaximumAttempts()
        {
            Retry retry = TreeTestFixture.CreateNode<Retry>("Retry");
            retry.maxAttempts = 2;
            ScriptedResultNode child = TreeTestFixture.CreateNode<ScriptedResultNode>("Child");
            child.results = new[] { false, false, true };
            retry.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(retry, child);
            yield return fixture.WaitUntilReady();
            ScriptedResultNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
            Assert.That(runtimeChild.executions, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Retry_RunsASequenceChildForEachAttempt()
        {
            Retry retry = TreeTestFixture.CreateNode<Retry>("Retry");
            retry.maxAttempts = 2;
            Sequence sequence = TreeTestFixture.CreateNode<Sequence>("Sequence");
            ScriptedResultNode first = TreeTestFixture.CreateNode<ScriptedResultNode>("First");
            first.results = new[] { false, true };
            ResultNode second = TreeTestFixture.CreateNode<ResultNode>("Second");
            second.result = true;
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            retry.node = sequence.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(retry, sequence, first, second);
            yield return fixture.WaitUntilReady();
            ScriptedResultNode runtimeFirst = fixture.GetRuntimeNode(first);
            ResultNode runtimeSecond = fixture.GetRuntimeNode(second);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(runtimeFirst.executions, Is.EqualTo(2));
            Assert.That(runtimeSecond.executions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Retry_ZeroAndNegativeAttemptsFailWithoutExecutingChild()
        {
            yield return AssertRetryCountWithoutChildExecution(0);
            yield return AssertRetryCountWithoutChildExecution(-2);
        }

        [UnityTest]
        public IEnumerator Retry_WithoutChildFails()
        {
            Retry retry = TreeTestFixture.CreateNode<Retry>("Retry");
            retry.maxAttempts = 3;

            using TreeTestFixture fixture = TreeTestFixture.Create(retry);
            yield return fixture.WaitUntilReady();
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
        }

        [UnityTest]
        public IEnumerator Retry_SnapshotsAttemptLimitBeforeChildCanChangeVariable()
        {
            VariableData attempts = new("Retry Attempts", VariableType.Int);
            attempts.SetDefaultValue(3);
            Retry retry = TreeTestFixture.CreateNode<Retry>("Retry");
            retry.maxAttempts.SetReference(attempts);
            SetIntNode child = TreeTestFixture.CreateNode<SetIntNode>("Set Attempts");
            child.target.SetReference(attempts);
            child.value = 0;
            retry.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(retry, new[] { attempts }, child);
            yield return fixture.WaitUntilReady();
            SetIntNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(runtimeChild.executions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Repeat_ExecutesSuccessfulChildConfiguredNumberOfTimes()
        {
            Repeat repeat = TreeTestFixture.CreateNode<Repeat>("Repeat");
            repeat.repeatCount = 3;
            ResultNode child = TreeTestFixture.CreateNode<ResultNode>("Child");
            child.result = true;
            repeat.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(repeat, child);
            yield return fixture.WaitUntilReady();
            ResultNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(runtimeChild.executions, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator Repeat_StopsOnFirstFailedChild()
        {
            Repeat repeat = TreeTestFixture.CreateNode<Repeat>("Repeat");
            repeat.repeatCount = 3;
            ResultNode child = TreeTestFixture.CreateNode<ResultNode>("Child");
            child.result = false;
            repeat.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(repeat, child);
            yield return fixture.WaitUntilReady();
            ResultNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
            Assert.That(runtimeChild.executions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Repeat_ZeroAndNegativeCountsSucceedWithoutExecutingChild()
        {
            yield return AssertRepeatCountWithoutChildExecution(0);
            yield return AssertRepeatCountWithoutChildExecution(-2);
        }

        [UnityTest]
        public IEnumerator Repeat_WithoutChildFailsEvenWhenCountIsZero()
        {
            Repeat repeat = TreeTestFixture.CreateNode<Repeat>("Repeat");
            repeat.repeatCount = 0;

            using TreeTestFixture fixture = TreeTestFixture.Create(repeat);
            yield return fixture.WaitUntilReady();
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
        }

        [UnityTest]
        public IEnumerator Repeat_RunsASequenceChildForEveryRepetition()
        {
            Repeat repeat = TreeTestFixture.CreateNode<Repeat>("Repeat");
            repeat.repeatCount = 2;
            Sequence sequence = TreeTestFixture.CreateNode<Sequence>("Sequence");
            ResultNode first = TreeTestFixture.CreateNode<ResultNode>("First");
            ResultNode second = TreeTestFixture.CreateNode<ResultNode>("Second");
            first.result = true;
            second.result = true;
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            repeat.node = sequence.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(repeat, sequence, first, second);
            yield return fixture.WaitUntilReady();
            ResultNode runtimeFirst = fixture.GetRuntimeNode(first);
            ResultNode runtimeSecond = fixture.GetRuntimeNode(second);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(runtimeFirst.executions, Is.EqualTo(2));
            Assert.That(runtimeSecond.executions, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Repeat_SnapshotsCountBeforeChildCanChangeTheVariable()
        {
            VariableData count = new("Repeat Count", VariableType.Int);
            count.SetDefaultValue(3);
            Repeat repeat = TreeTestFixture.CreateNode<Repeat>("Repeat");
            repeat.repeatCount.SetReference(count);
            SetIntNode child = TreeTestFixture.CreateNode<SetIntNode>("Set Count");
            child.target.SetReference(count);
            child.value = 0;
            repeat.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(repeat, new[] { count }, child);
            yield return fixture.WaitUntilReady();
            SetIntNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(runtimeChild.executions, Is.EqualTo(3));
            Assert.That(runtimeChild.target.IntValue, Is.EqualTo(0));
        }

        private static IEnumerator AssertRepeatCountWithoutChildExecution(int count)
        {
            Repeat repeat = TreeTestFixture.CreateNode<Repeat>("Repeat");
            repeat.repeatCount = count;
            ResultNode child = TreeTestFixture.CreateNode<ResultNode>("Child");
            child.result = false;
            repeat.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(repeat, child);
            yield return fixture.WaitUntilReady();
            ResultNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(runtimeChild.executions, Is.EqualTo(0));
        }

        private static IEnumerator AssertRetryCountWithoutChildExecution(int count)
        {
            Retry retry = TreeTestFixture.CreateNode<Retry>("Retry");
            retry.maxAttempts = count;
            ResultNode child = TreeTestFixture.CreateNode<ResultNode>("Child");
            child.result = false;
            retry.node = child.ToReference();

            using TreeTestFixture fixture = TreeTestFixture.Create(retry, child);
            yield return fixture.WaitUntilReady();
            ResultNode runtimeChild = fixture.GetRuntimeNode(child);
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.False);
            Assert.That(runtimeChild.executions, Is.EqualTo(0));
        }

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
            fixture.Tick();

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
            fixture.Tick();

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
            fixture.Tick();
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
        public sealed class ResultNode : TreeNode
        {
            public bool result;
            public int executions;

            public override void Initialize()
            {
            }

            public override State Execute()
            {
                executions++;
                return result ? State.Success : State.Failed;
            }
        }

        [Serializable]
        public sealed class ScriptedResultNode : TreeNode
        {
            public bool[] results = Array.Empty<bool>();
            public int executions;

            public override void Initialize()
            {
                executions = 0;
            }

            public override State Execute()
            {
                int index = executions++;
                bool result = results.Length > 0 && results[Math.Min(index, results.Length - 1)];
                return result ? State.Success : State.Failed;
            }
        }

        [Serializable]
        public sealed class SetIntNode : TreeNode
        {
            public VariableReference<int> target = new();
            public int value;

            public override void Initialize()
            {
            }

            public override State Execute()
            {
                executions++;
                target.SetValue(value);
                return State.Success;
            }

            public int executions;
        }

        [Serializable]
        public sealed class ErrorNode : TreeNode
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
        public sealed class YieldNode : TreeNode
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
        public sealed class ThrowingNode : TreeNode
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
