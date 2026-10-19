using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests.Inspector
{
    /// <summary>Verifies the user-facing search and noise filters used by the event log.</summary>
    public sealed class AIInspectorEventLogTests
    {
        [UnityTest]
        public IEnumerator InlineBoolean_ExceptionUsesTheExecutingStackWithoutSchedulingTheAdapter()
        {
            using var fixture = TreeTestFixture.Create(TreeTestFixture.CreateNode<Sequence>("Root"));
            yield return fixture.WaitUntilReady();
            var stack = fixture.Tree.CreateStack(BehaviourTree.StackType.Main, "Inline");
            var boolean = new Aethiumian.AI.Nodes.Boolean { name = "Condition", uuid = UUID.NewUUID(), behaviourTree = fixture.Tree, boolean = null };
            stack.Initialize();
            LogAssert.Expect(LogType.Error, "Exception occurred at node [Condition]");
            LogAssert.Expect(LogType.Exception, new Regex(".*"));

            stack.ReturnInlineBoolean(boolean);

            var record = fixture.Tree.StackEvents.Single();
            Assert.That(record.EventType, Is.EqualTo(BehaviourTree.NodeCallStack.EventType.Exception));
            Assert.That(record.NodeUUID, Is.EqualTo(boolean.uuid));
            Assert.That(record.Context.Policy, Is.EqualTo("ReturnFailed"));
            Assert.That(record.Context.Severity, Is.EqualTo(BehaviourTree.NodeCallStack.EventSeverity.Warning));
            Assert.That(boolean.callStack, Is.Null);
        }

        [UnityTest]
        public IEnumerator EscapingException_DeduplicatesWithinOneInvocationButKeepsLaterOccurrences()
        {
            using var fixture = TreeTestFixture.Create(TreeTestFixture.CreateNode<Sequence>("Root"));
            yield return fixture.WaitUntilReady();
            var stack = fixture.Tree.CreateStack(BehaviourTree.StackType.Main, "Exceptions");
            var exception = new System.InvalidOperationException("Repeated instance");
            var recordEscaping = typeof(BehaviourTree).GetMethod("RecordEscapingException", BindingFlags.Instance | BindingFlags.NonPublic);
            stack.RecordException(null, exception, "Stop", "Tree: Fault");
            recordEscaping.Invoke(fixture.Tree, new object[] { stack, exception, "Tick", 0L });
            Assert.That(fixture.Tree.StackEvents.Count, Is.EqualTo(1));
            recordEscaping.Invoke(fixture.Tree, new object[] { stack, exception, "Tick", 1L });
            Assert.That(fixture.Tree.StackEvents.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator EscapingException_UpgradesAnEarlierHandledWarningToAnError()
        {
            using var fixture = TreeTestFixture.Create(TreeTestFixture.CreateNode<Sequence>("Root"));
            yield return fixture.WaitUntilReady();
            var stack = fixture.Tree.CreateStack(BehaviourTree.StackType.Main, "Exceptions");
            var exception = new System.InvalidOperationException("Escalated failure");
            stack.RecordException(null, exception, "Stop", "Continue cleanup", severity: BehaviourTree.NodeCallStack.EventSeverity.Warning);
            var recordEscaping = typeof(BehaviourTree).GetMethod("RecordEscapingException", BindingFlags.Instance | BindingFlags.NonPublic);
            recordEscaping.Invoke(fixture.Tree, new object[] { stack, exception, "Tick", 0L });
            Assert.That(fixture.Tree.StackEvents.Count, Is.EqualTo(2));
            Assert.That(fixture.Tree.StackEvents.Last().Context.Severity, Is.EqualTo(BehaviourTree.NodeCallStack.EventSeverity.Error));
        }

        [UnityTest]
        public IEnumerator InlineCondition_ThrowPreservesBranchOwnershipAndConditionPhase()
        {
            using var fixture = TreeTestFixture.Create(TreeTestFixture.CreateNode<Sequence>("Root"));
            yield return fixture.WaitUntilReady();
            fixture.Data.nodeErrorHandle = NodeErrorSolution.Throw;
            var stack = fixture.Tree.CreateStack(BehaviourTree.StackType.Branch, "Conditional branch");
            var boolean = new Aethiumian.AI.Nodes.Boolean { name = "Condition", uuid = UUID.NewUUID(), behaviourTree = fixture.Tree };
            var exception = new System.InvalidOperationException("Condition failed");

            Assert.Throws<System.InvalidOperationException>(() => boolean.HandleException(exception, "Condition", stack));

            var record = fixture.Tree.StackEvents.Single();
            Assert.That(record.StackType, Is.EqualTo(BehaviourTree.StackType.Branch));
            Assert.That(record.NodeUUID, Is.EqualTo(boolean.uuid));
            Assert.That(record.Context.Phase, Is.EqualTo("Condition"));
            Assert.That(record.Context.Policy, Does.StartWith("Throw"));
            Assert.That(record.Context.Severity, Is.EqualTo(BehaviourTree.NodeCallStack.EventSeverity.Error));
        }

        [Test]
        public void Redirect_UnwindDoesNotInheritAnEarlierForcedReturnResult()
        {
            var stack = new BehaviourTree.NodeCallStack();
            var records = Observe(stack);
            var parent = new Sequence { name = "Parent", uuid = UUID.NewUUID() };
            var child = new Sequence { name = "Child", uuid = UUID.NewUUID() };
            var source = new Sequence { name = "Timeout", uuid = UUID.NewUUID() };
            stack.Initialize();
            stack.Start(parent);
            stack.Push(child);
            stack.Interrupt(child, true, source);
            stack.Break(null, source);

            var pops = records.Where(record => record.EventType == BehaviourTree.NodeCallStack.EventType.Pop).ToArray();
            Assert.That(pops[0].Result, Is.EqualTo(State.Success));
            Assert.That(pops[0].Context.Reason, Is.EqualTo("ForcedReturn"));
            Assert.That(pops[1].Result, Is.Null);
            Assert.That(pops[1].Context.Reason, Is.EqualTo("Unwind"));
            Assert.That(pops.All(record => record.Context.Severity == BehaviourTree.NodeCallStack.EventSeverity.Debug), Is.True);
            Assert.That(records.Where(record => record.EventType == BehaviourTree.NodeCallStack.EventType.Start || record.EventType == BehaviourTree.NodeCallStack.EventType.Redirect).All(record => record.Context.Severity == BehaviourTree.NodeCallStack.EventSeverity.Info), Is.True);
            Assert.That(records.Where(record => record.EventType == BehaviourTree.NodeCallStack.EventType.Push).All(record => record.Context.Severity == BehaviourTree.NodeCallStack.EventSeverity.Debug), Is.True);
            Assert.That(pops.All(record => record.Context.CauseEventId != 0 && record.SourceUUID == source.uuid), Is.True);
            Assert.That(records.Single(record => record.SequenceId == pops[1].Context.CauseEventId).Context.Reason, Is.EqualTo("Unwind"));
        }

        [Test]
        public void Clear_RecordsPhysicalExitsWithoutStopOrPopCallbacks()
        {
            var stack = new BehaviourTree.NodeCallStack();
            var records = Observe(stack);
            var node = new Sequence { uuid = UUID.NewUUID() };
            bool stopped = false;
            bool popped = false;
            node.OnInterrupted += () => stopped = true;
            stack.OnNodePopStack += _ => popped = true;
            stack.Initialize();
            stack.Start(node);
            stack.Clear();

            Assert.That(stack.Count, Is.Zero);
            Assert.That(stopped || popped, Is.False);
            Assert.That(records.Last().Context.Reason, Is.EqualTo("Clear"));
            Assert.That(records.Last().Result, Is.Null);
            Assert.That(AIInspector.FindStackEventPair(records, records.Last().SequenceId), Is.EqualTo(1));
        }

        [Test]
        public void End_RecordsCompletionAfterCleanupAndPreventsReentrantEnd()
        {
            var stack = new BehaviourTree.NodeCallStack();
            var records = Observe(stack);
            var node = new Sequence { uuid = UUID.NewUUID() };
            node.OnInterrupted += stack.End;
            stack.Initialize();
            stack.Start(node);
            stack.End();

            Assert.That(stack.Count, Is.Zero);
            Assert.That(records.Last().EventType, Is.EqualTo(BehaviourTree.NodeCallStack.EventType.End));
            Assert.That(records.Last().StackState, Is.EqualTo(BehaviourTree.NodeCallStack.StackState.End));
            Assert.That(records.Last().Context.Severity, Is.EqualTo(BehaviourTree.NodeCallStack.EventSeverity.Info));
            Assert.That(records.Count(record => record.EventType == BehaviourTree.NodeCallStack.EventType.End), Is.EqualTo(1));
            Assert.That(records[^2].Context.Reason, Is.EqualTo("Unwind"));
        }

        [Test]
        public void Redirect_RejectedTargetEmitsNoPhysicalExit()
        {
            var stack = new BehaviourTree.NodeCallStack();
            var records = Observe(stack);
            stack.Initialize();
            stack.Start(new Sequence { uuid = UUID.NewUUID() });
            Assert.That(stack.Interrupt(new Sequence { uuid = UUID.NewUUID() }, false), Is.False);
            Assert.That(records.Last().Context.Accepted, Is.False);
            Assert.That(records.Last().Context.Severity, Is.EqualTo(BehaviourTree.NodeCallStack.EventSeverity.Warning));
            Assert.That(records.Last().Context.RequestedResult, Is.EqualTo(State.Failed));
            Assert.That(records.Last().Result, Is.Null);
            Assert.That(stack.Count, Is.EqualTo(1));
        }

        [Test]
        public void Filters_CombineStructuredSearchWithExactEventTypes()
        {
            var context = new BehaviourTree.NodeCallStack.EventContext("Unwind", new Sequence { name = "Timeout" }, 42);
            var record = new BehaviourTree.StackEventRecord(43, 120, 2f, "Combat", 7, BehaviourTree.StackType.Main, "Main Stack", BehaviourTree.NodeCallStack.EventType.Pop, null, null, BehaviourTree.NodeCallStack.StackState.Ready, "", context);
            Assert.That(AIInspector.MatchesStackEvent(record, new[] { "Unwind", "Timeout", "42" }, 1 << (int)BehaviourTree.NodeCallStack.EventType.Pop, 0, 0, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(record, System.Array.Empty<string>(), 1 << (int)BehaviourTree.NodeCallStack.EventType.Push, 0, 0, null), Is.False);
        }

        /// <summary>Captures the same immutable snapshots as the tree event sink, without a scene fixture.</summary>
        private static List<BehaviourTree.StackEventRecord> Observe(BehaviourTree.NodeCallStack stack)
        {
            var records = new List<BehaviourTree.StackEventRecord>();
            stack.OnStackEvent += (owner, type, node, result, detail, context) =>
            {
                long id = records.Count + 1;
                records.Add(new BehaviourTree.StackEventRecord(id, 120, 2f, "Combat", 7, BehaviourTree.StackType.Main, "Main Stack", type, node, result, owner.State, detail, context));
                return id;
            };
            return records;
        }

        [Test]
        public void Search_MatchesWordsAcrossFieldsWithoutCaseSensitivity()
        {
            var record = Record(BehaviourTree.NodeCallStack.EventType.Exception, detail: "Target lost\nAttack cancelled");

            Assert.That(AIInspector.MatchesStackEvent(record, new[] { "TARGET", "main", "exception" }, -1, 0, 0, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(record, new[] { "target", "missing" }, -1, 0, 0, null), Is.False);
        }

        [Test]
        public void EventTypes_MultipleSelectionsDoNotClassifyResults()
        {
            int types = (1 << (int)BehaviourTree.NodeCallStack.EventType.Pop) | (1 << (int)BehaviourTree.NodeCallStack.EventType.Redirect);
            Assert.That(AIInspector.MatchesStackEvent(Record(BehaviourTree.NodeCallStack.EventType.Pop, State.Success), System.Array.Empty<string>(), types, 0, 0, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(Record(BehaviourTree.NodeCallStack.EventType.Pop, State.Failed), System.Array.Empty<string>(), types, 0, 0, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(Record(BehaviourTree.NodeCallStack.EventType.Redirect), System.Array.Empty<string>(), types, 0, 0, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(Record(BehaviourTree.NodeCallStack.EventType.Exception), System.Array.Empty<string>(), types, 0, 0, null), Is.False);
            Assert.That(AIInspector.MatchesStackEvent(Record(BehaviourTree.NodeCallStack.EventType.Push), System.Array.Empty<string>(), types, 0, 0, null), Is.False);
        }

        [Test]
        public void EventTypes_AllAndNoneApplyToEveryRuntimeEventType()
        {
            foreach (BehaviourTree.NodeCallStack.EventType type in System.Enum.GetValues(typeof(BehaviourTree.NodeCallStack.EventType)))
            {
                Assert.That(AIInspector.MatchesStackEvent(Record(type), System.Array.Empty<string>(), -1, 0, 0, null), Is.True);
                Assert.That(AIInspector.MatchesStackEvent(Record(type), System.Array.Empty<string>(), 0, 0, 0, null), Is.False);
            }
        }

        [Test]
        public void Severity_IntersectsTypeSelectionWithoutTreatingFailedReturnsAsWarnings()
        {
            var context = new BehaviourTree.NodeCallStack.EventContext(phase: "Execute", policy: "Fault", severity: BehaviourTree.NodeCallStack.EventSeverity.Error);
            var error = new BehaviourTree.StackEventRecord(1, 120, 2f, "Combat", 7, BehaviourTree.StackType.Main, "Main", BehaviourTree.NodeCallStack.EventType.Exception, null, null, BehaviourTree.NodeCallStack.StackState.Calling, "Failure", context);
            int exceptions = 1 << (int)BehaviourTree.NodeCallStack.EventType.Exception;
            int errors = (int)BehaviourTree.NodeCallStack.EventSeverity.Error + 1;
            int warnings = (int)BehaviourTree.NodeCallStack.EventSeverity.Warning + 1;
            int debug = (int)BehaviourTree.NodeCallStack.EventSeverity.Debug + 1;
            Assert.That(AIInspector.MatchesStackEvent(error, new[] { "error" }, exceptions, errors, 0, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(error, System.Array.Empty<string>(), exceptions, warnings, 0, null), Is.False);
            Assert.That(AIInspector.MatchesStackEvent(error, System.Array.Empty<string>(), 1 << (int)BehaviourTree.NodeCallStack.EventType.Pop, errors, 0, null), Is.False);
            var failed = Record(BehaviourTree.NodeCallStack.EventType.Pop, State.Failed);
            Assert.That(AIInspector.MatchesStackEvent(failed, System.Array.Empty<string>(), -1, debug, 0, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(failed, System.Array.Empty<string>(), -1, warnings, 0, null), Is.False);
        }

        [Test]
        public void Filters_CombineSearchEventAndStack()
        {
            var record = Record(BehaviourTree.NodeCallStack.EventType.Exception, detail: "Target lost");

            Assert.That(AIInspector.MatchesStackEvent(record, new[] { "target" }, 1 << (int)BehaviourTree.NodeCallStack.EventType.Exception, 0, 1, null), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(record, new[] { "target" }, 1 << (int)BehaviourTree.NodeCallStack.EventType.Exception, 0, 2, null), Is.False);
            Assert.That(AIInspector.MatchesStackEvent(record, new[] { "target" }, 1 << (int)BehaviourTree.NodeCallStack.EventType.Push, 0, 1, null), Is.False);
        }

        [Test]
        public void Search_FindsFrameAndStackIdentity()
        {
            Assert.That(AIInspector.MatchesStackEvent(Record(BehaviourTree.NodeCallStack.EventType.Redirect), new[] { "120", "7" }, -1, 0, 0, null), Is.True);
        }

        [Test]
        public void SelectedNode_MatchesRecordedIdentityAndExcludesStackEvents()
        {
            var node = new Sequence { name = "Attack", uuid = UUID.NewUUID() };
            var sameIdentity = new Sequence { uuid = node.uuid };
            var otherNode = new Sequence { uuid = UUID.NewUUID() };
            var record = new BehaviourTree.StackEventRecord(1, 120, 2f, "Combat", 7, BehaviourTree.StackType.Main, "Main Stack", BehaviourTree.NodeCallStack.EventType.Push, node, null, BehaviourTree.NodeCallStack.StackState.Ready, "", default);

            Assert.That(AIInspector.MatchesStackEvent(record, System.Array.Empty<string>(), -1, 0, 0, sameIdentity), Is.True);
            Assert.That(AIInspector.MatchesStackEvent(record, System.Array.Empty<string>(), -1, 0, 0, otherNode), Is.False);
            Assert.That(AIInspector.MatchesStackEvent(Record(BehaviourTree.NodeCallStack.EventType.Start), System.Array.Empty<string>(), -1, 0, 0, node), Is.False);
        }

        [Test]
        public void Pairing_UsesNestingAndUniqueIdentityForRepeatedSameFrameExecutions()
        {
            var parent = new Sequence { uuid = UUID.NewUUID() };
            var child = new Sequence { uuid = UUID.NewUUID() };
            var events = new[]
            {
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 1, node: parent),
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 2, node: child),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 3, node: child),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 4, node: parent),
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 5, node: parent),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 6, node: parent)
            };

            Assert.That(AIInspector.FindStackEventPair(events, 1), Is.EqualTo(3));
            Assert.That(AIInspector.FindStackEventPair(events, 3), Is.EqualTo(1));
            Assert.That(AIInspector.FindStackEventPair(events, 4), Is.EqualTo(0));
            Assert.That(AIInspector.FindStackEventPair(events, 6), Is.EqualTo(4));
        }

        [Test]
        public void Pairing_SeparatesInterleavedStacksEvenForTheSameNode()
        {
            var node = new Sequence { uuid = UUID.NewUUID() };
            var events = new[]
            {
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 1, stackId: 7, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 2, stackId: 8, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 3, stackId: 7, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 4, stackId: 8, node: node)
            };

            Assert.That(AIInspector.FindStackEventPair(events, 1), Is.EqualTo(2));
            Assert.That(AIInspector.FindStackEventPair(events, 4), Is.EqualTo(1));
        }

        [Test]
        public void Pairing_RedirectStillMatchesItsCleanupPops()
        {
            var node = new Sequence { uuid = UUID.NewUUID() };
            var events = new[]
            {
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 1, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Redirect, sequenceId: 2, detail: "Unwind", node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Redirect, sequenceId: 3, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 4, node: node)
            };

            Assert.That(AIInspector.FindStackEventPair(events, 1), Is.EqualTo(3));
            Assert.That(AIInspector.FindStackEventPair(events, 4), Is.EqualTo(0));
        }

        [Test]
        public void Pairing_DoesNotCrossStackRestartOrGuessWhenRecordsAreMissing()
        {
            var node = new Sequence { uuid = UUID.NewUUID() };
            var events = new[]
            {
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 1, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Start, sequenceId: 2, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 3, node: node),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 4, node: node)
            };

            Assert.That(AIInspector.FindStackEventPair(events, 1), Is.EqualTo(-1));
            Assert.That(AIInspector.FindStackEventPair(events, 4), Is.EqualTo(2));
            Assert.That(AIInspector.FindStackEventPair(new[] { events[3] }, 4), Is.EqualTo(-1));
            Assert.That(AIInspector.FindStackEventPair(events, 99), Is.EqualTo(-1));
        }

        [Test]
        public void Pairing_NodeIdentityMismatchRemainsUnmatched()
        {
            var events = new[]
            {
                Record(BehaviourTree.NodeCallStack.EventType.Push, sequenceId: 1, node: new Sequence { uuid = UUID.NewUUID() }),
                Record(BehaviourTree.NodeCallStack.EventType.Pop, sequenceId: 2, node: new Sequence { uuid = UUID.NewUUID() })
            };

            Assert.That(AIInspector.FindStackEventPair(events, 1), Is.EqualTo(-1));
            Assert.That(AIInspector.FindStackEventPair(events, 2), Is.EqualTo(-1));
        }

        /// <summary>Creates a recorded event without a live scene or running AI.</summary>
        private static BehaviourTree.StackEventRecord Record(BehaviourTree.NodeCallStack.EventType type, State? result = null, string detail = "", long sequenceId = 1, int stackId = 7, TreeNode node = null)
        {
            return new BehaviourTree.StackEventRecord(sequenceId, 120, 2f, "Combat", stackId, BehaviourTree.StackType.Main, "Main Stack", type, node, result, BehaviourTree.NodeCallStack.StackState.Ready, detail, new BehaviourTree.NodeCallStack.EventContext(reason: type == BehaviourTree.NodeCallStack.EventType.Pop ? "Return" : null));
        }
    }
}
