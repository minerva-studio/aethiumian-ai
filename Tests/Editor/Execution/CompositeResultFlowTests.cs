#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

using Aethiumian.AI.Editor.Tests.Support;

namespace Aethiumian.AI.Editor.Tests.Execution
{
    public sealed class CompositeResultFlowTests
    {
        [Test]
        public void EmptyCollections_ReturnBooleanIdentity()
        {
            Assert.That(new Sequence().Execute(), Is.EqualTo(State.Success));
            Assert.That(new Decision { events = Array.Empty<NodeReference>() }.Execute(), Is.EqualTo(State.Failed));
            Assert.That(new Aggregate { resultMode = Aggregate.ResultMode.All }.Execute(), Is.EqualTo(State.Success));
            Assert.That(new Aggregate { resultMode = Aggregate.ResultMode.Any }.Execute(), Is.EqualTo(State.Failed));
            Assert.That(new Aggregate { resultMode = Aggregate.ResultMode.True }.Execute(), Is.EqualTo(State.Success));
            Assert.That(new Aggregate { resultMode = Aggregate.ResultMode.False }.Execute(), Is.EqualTo(State.Failed));
        }

        [UnityTest]
        public IEnumerator Sequence_StopsAfterFirstFailure()
        {
            yield return AssertExecution(
                new Sequence(),
                new[] { true, false, true },
                expectedResult: false,
                expectedOrder: new[] { "Child 1", "Child 2" });
        }

        [UnityTest]
        public IEnumerator Sequence_SucceedsAfterEveryChildSucceeds()
        {
            yield return AssertExecution(
                new Sequence(),
                new[] { true, true, true },
                expectedResult: true,
                expectedOrder: new[] { "Child 1", "Child 2", "Child 3" });
        }

        [UnityTest]
        public IEnumerator Decision_StopsAfterFirstSuccess()
        {
            yield return AssertExecution(
                new Decision(),
                new[] { false, true, false },
                expectedResult: true,
                expectedOrder: new[] { "Child 1", "Child 2" });
        }

        [UnityTest]
        public IEnumerator Decision_FailsAfterEveryChildFails()
        {
            yield return AssertExecution(
                new Decision(),
                new[] { false, false, false },
                expectedResult: false,
                expectedOrder: new[] { "Child 1", "Child 2", "Child 3" });
        }

        [UnityTest]
        public IEnumerator Aggregate_AllRunsEveryChild()
        {
            yield return AssertExecution(
                new Aggregate { resultMode = Aggregate.ResultMode.All },
                new[] { true, false, true },
                expectedResult: false,
                expectedOrder: new[] { "Child 1", "Child 2", "Child 3" });
        }

        [UnityTest]
        public IEnumerator Aggregate_AnyRunsEveryChild()
        {
            yield return AssertExecution(
                new Aggregate { resultMode = Aggregate.ResultMode.Any },
                new[] { false, true, false },
                expectedResult: true,
                expectedOrder: new[] { "Child 1", "Child 2", "Child 3" });
        }

        [UnityTest]
        public IEnumerator Aggregate_TrueRunsEveryChildAndReturnsTrue()
        {
            yield return AssertExecution(
                new Aggregate { resultMode = Aggregate.ResultMode.True },
                new[] { false, false, false },
                expectedResult: true,
                expectedOrder: new[] { "Child 1", "Child 2", "Child 3" });
        }

        [UnityTest]
        public IEnumerator Aggregate_FalseRunsEveryChildAndReturnsFalse()
        {
            yield return AssertExecution(
                new Aggregate { resultMode = Aggregate.ResultMode.False },
                new[] { true, true, true },
                expectedResult: false,
                expectedOrder: new[] { "Child 1", "Child 2", "Child 3" });
        }

        [UnityTest]
        public IEnumerator Aggregate_FixedResultDoesNotSwallowChildError()
        {
            Aggregate aggregate = TreeTestFixture.CreateNode<Aggregate>("Aggregate");
            aggregate.resultMode = Aggregate.ResultMode.True;
            RecordingResultNode first = TreeTestFixture.CreateNode<RecordingResultNode>("Child 1");
            ErrorRecordingNode error = TreeTestFixture.CreateNode<ErrorRecordingNode>("Child Error");
            RecordingResultNode last = TreeTestFixture.CreateNode<RecordingResultNode>("Child 3");
            first.parent = new NodeReference(aggregate.uuid);
            error.parent = new NodeReference(aggregate.uuid);
            last.parent = new NodeReference(aggregate.uuid);
            aggregate.events = new[]
            {
                new NodeReference(first.uuid),
                new NodeReference(error.uuid),
                new NodeReference(last.uuid)
            };

            RecordingResultNode.ExecutionOrder.Clear();
            using TreeTestFixture fixture = TreeTestFixture.Create(aggregate, first, error, last);
            yield return fixture.WaitUntilReady();
            LogAssert.Expect(LogType.Exception, new Regex("return invalid state"));
            fixture.Start();
            fixture.Tick();

            Assert.That(fixture.Tree.MainStack.IsPaused, Is.True);
            Assert.That(RecordingResultNode.ExecutionOrder, Is.EqualTo(new[] { "Child 1", "Child Error" }));
        }

        private static IEnumerator AssertExecution(
            Flow flow,
            IReadOnlyList<bool> results,
            bool expectedResult,
            IReadOnlyList<string> expectedOrder)
        {
            flow.name = flow.GetType().Name;
            flow.uuid = UUID.NewUUID();
            flow.parent = NodeReference.Empty;
            RecordingResultNode[] children = new RecordingResultNode[results.Count];
            NodeReference[] references = new NodeReference[results.Count];
            for (int index = 0; index < results.Count; index++)
            {
                RecordingResultNode child = TreeTestFixture.CreateNode<RecordingResultNode>($"Child {index + 1}");
                child.result = results[index];
                child.parent = new NodeReference(flow.uuid);
                children[index] = child;
                references[index] = new NodeReference(child.uuid);
            }

            switch (flow)
            {
                case Sequence sequence:
                    sequence.events = references;
                    break;
                case Decision decision:
                    decision.events = references;
                    break;
                case Aggregate aggregate:
                    aggregate.events = references;
                    break;
            }

            RecordingResultNode.ExecutionOrder.Clear();
            using TreeTestFixture fixture = TreeTestFixture.Create(flow, children);
            yield return fixture.WaitUntilReady();
            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.EqualTo(expectedResult));
            Assert.That(RecordingResultNode.ExecutionOrder, Is.EqualTo(expectedOrder));
        }

        [Serializable]
        public sealed class RecordingResultNode : TreeNode
        {
            internal static readonly List<string> ExecutionOrder = new();
            public bool result;

            public override void Initialize()
            {
            }

            public override State Execute()
            {
                ExecutionOrder.Add(name);
                return StateOf(result);
            }
        }

        [Serializable]
        public sealed class ErrorRecordingNode : TreeNode
        {
            public override void Initialize()
            {
            }

            public override State Execute()
            {
                RecordingResultNode.ExecutionOrder.Add(name);
                return State.Error;
            }
        }
    }
}
