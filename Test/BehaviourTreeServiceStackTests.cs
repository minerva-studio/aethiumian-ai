using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AiBoolean = Aethiumian.AI.Nodes.Boolean;

namespace Aethiumian.AI.Tests
{
    public class BehaviourTreeServiceStackTests
    {
        [Test]
        public void WaitUntil_FalseConditionYieldsWithoutImmediateReschedule()
        {
            var wait = new Nodes.WaitUntil();

            Assert.That(wait.ReceiveReturnFromChild(false), Is.EqualTo(State.Yield));
        }

        [Test]
        public void WaitWhile_TrueConditionYieldsWithoutImmediateReschedule()
        {
            var wait = new Nodes.WaitWhile();

            Assert.That(wait.ReceiveReturnFromChild(true), Is.EqualTo(State.Yield));
        }

        [Test]
        public void RuntimeNodes_SetNextExecuteCallsAreTerminal()
        {
            string nodesPath = Path.Combine(Application.dataPath, "Scripts", "AI", "Core", "Runtime", "Nodes");
            var violations = new List<string>();

            foreach (string path in Directory.EnumerateFiles(nodesPath, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(path);
                for (int index = 0; index < lines.Length; index++)
                {
                    string line = lines[index];
                    if (!line.Contains("SetNextExecute(") || !line.Contains(";")) continue;
                    if (line.Contains("return SetNextExecute(")) continue;
                    if (line.Contains("SetNextExecute intentionally non-terminal")) continue;

                    violations.Add($"{ToAssetPath(path)}:{index + 1}: {line.Trim()}");
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "SetNextExecute is a terminal handoff. Return it immediately, or add an explicit opt-out comment if a non-terminal schedule is truly required.");
        }

        [Test]
        public void InstantNodeKinds_DoNotExposeServiceHost()
        {
            Assert.That(new InstantCallProbe(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new InstantDetermineProbe(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new InstantArithmeticProbe(), Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(new AiBoolean(), Is.Not.InstanceOf<IServiceHostNode>());
        }

        [Test]
        public void ServiceHostImplementations_AreTreeNodes()
        {
            var violations = typeof(IServiceHostNode).Assembly.GetTypes()
                .Where(type => typeof(IServiceHostNode).IsAssignableFrom(type))
                .Where(type => type.IsClass)
                .Where(type => !typeof(TreeNode).IsAssignableFrom(type))
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(violations, Is.Empty);
        }

        [Test]
        public void ServiceHostNodeContract_UsesNodeItselfAsIdentity()
        {
            var flow = TreeTestFixture.CreateNode<YieldingNode>("Flow Host");
            var action = TreeTestFixture.CreateNode<ActionHostProbe>("Action Host");

            Assert.That(flow, Is.InstanceOf<IServiceHostNode>());
            Assert.That(action, Is.InstanceOf<IServiceHostNode>());
            Assert.That(((IServiceHostNode)flow).Node, Is.SameAs(flow));
            Assert.That(((IServiceHostNode)action).Node, Is.SameAs(action));
        }

        [UnityTest]
        public IEnumerator ServiceHead_ReturnsServiceNodeForHostedBranch()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var service = TreeTestFixture.CreateNode<ManualReadyService>("Manual Service");
            var child = TreeTestFixture.CreateNode<YieldingNode>("Service Child");
            service.child = new NodeReference(child.uuid);
            AddServiceReference(host, service);
            child.parent = new NodeReference(service.uuid);

            using var fixture = TreeTestFixture.Create(host, service, child);
            yield return fixture.WaitUntilReady();

            var runtimeHost = fixture.GetRuntimeNode<YieldingNode>(host);
            var runtimeService = fixture.GetRuntimeNode<ManualReadyService>(service);
            var runtimeChild = fixture.GetRuntimeNode<YieldingNode>(child);

            Assert.That(runtimeHost.ServiceHead, Is.Null);
            Assert.That(runtimeService.ServiceHead, Is.SameAs(runtimeService));
            Assert.That(runtimeChild.ServiceHead, Is.SameAs(runtimeService));
        }

        [UnityTest]
        public IEnumerator TimeoutService_RegistersWithoutAllocatingStack_AndInterruptsHost()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var timeout = TreeTestFixture.CreateNode<Timeout>("Timeout");
            AddServiceReference(host, timeout);

            using var fixture = TreeTestFixture.Create(host, timeout);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeTimeout = fixture.GetRuntimeNode<Timeout>(timeout);
            Assert.That(fixture.Tree.ServiceStacks.TryGetValue(runtimeTimeout, out var stack), Is.True);
            Assert.That(stack, Is.Null);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));

            fixture.Tick();
            yield return null;

            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeTimeout), Is.False);
        }

        [UnityTest]
        public IEnumerator TimeoutService_SequenceContinuesToNextValidChild()
        {
            var sequence = TreeTestFixture.CreateNode<Sequence>("Host Sequence");
            var interrupted = TreeTestFixture.CreateNode<YieldingNode>("Interrupted Child");
            var timeout = TreeTestFixture.CreateNode<Timeout>("Timeout");
            var next = TreeTestFixture.CreateNode<CountingResultNode>("Next Child");
            next.returnValue = true;

            sequence.events = new[] { new NodeReference(interrupted.uuid), new NodeReference(next.uuid) };
            interrupted.parent = new NodeReference(sequence.uuid);
            next.parent = new NodeReference(sequence.uuid);
            AddServiceReference(interrupted, timeout);

            using var fixture = TreeTestFixture.Create(sequence, interrupted, timeout, next);
            yield return fixture.WaitUntilReady();

            var runtimeNext = fixture.GetRuntimeNode<CountingResultNode>(next);
            var runtimeTimeout = fixture.GetRuntimeNode<Timeout>(timeout);
            fixture.Tree.Start();
            yield return WaitUntilOrTimeout(() => fixture.Tree.ServiceStacks.ContainsKey(runtimeTimeout));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeTimeout), Is.True);

            fixture.Tick();
            yield return WaitUntilOrTimeout(() => runtimeNext.runCount == 1);

            Assert.That(runtimeNext.runCount, Is.EqualTo(1));
            Assert.That(fixture.Tree.MainStack.Exception, Is.Null);
        }

        [UnityTest]
        public IEnumerator TimeoutService_SequenceNullNextChildPausesWithoutRecursiveExecution()
        {
            var sequence = TreeTestFixture.CreateNode<Sequence>("Host Sequence");
            var interrupted = TreeTestFixture.CreateNode<YieldingNode>("Interrupted Child");
            var timeout = TreeTestFixture.CreateNode<Timeout>("Timeout");

            sequence.events = new[] { new NodeReference(interrupted.uuid), NodeReference.Empty };
            interrupted.parent = new NodeReference(sequence.uuid);
            AddServiceReference(interrupted, timeout);

            using var fixture = TreeTestFixture.Create(sequence, interrupted, timeout);
            yield return fixture.WaitUntilReady();

            var runtimeTimeout = fixture.GetRuntimeNode<Timeout>(timeout);
            fixture.Tree.Start();
            yield return WaitUntilOrTimeout(() => fixture.Tree.ServiceStacks.ContainsKey(runtimeTimeout));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeTimeout), Is.True);

            LogAssert.Expect(LogType.Exception, new Regex(@"Encounter null node"));
            LogAssert.Expect(LogType.Exception, new Regex(@"Node \[Host Sequence\] return invalid state '\(Error\)'"));
            fixture.Tick();
            yield return WaitUntilOrTimeout(() => fixture.Tree.MainStack.IsPaused);

            Assert.That(fixture.Tree.MainStack.IsPaused, Is.True);
            Assert.That(fixture.Tree.MainStack.Exception, Is.Null);
        }

        [UnityTest]
        public IEnumerator SetNextExecute_BooleanTrueReturnsInlineToParent()
        {
            var host = TreeTestFixture.CreateNode<InlineReturnProbe>("Host");
            var condition = TreeTestFixture.CreateNode<AiBoolean>("Condition");
            var conditionVariable = CreateBoolVariable(condition, "inlineTrue");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);

            using var fixture = TreeTestFixture.Create(host, new[] { conditionVariable }, host, condition);
            yield return fixture.WaitUntilReady();

            var runtimeHost = fixture.GetRuntimeNode<InlineReturnProbe>(host);
            var runtimeCondition = fixture.GetRuntimeNode<AiBoolean>(condition);
            runtimeCondition.boolean.SetValue(true);
            fixture.Tree.Start();
            yield return null;

            Assert.That(runtimeHost.receivedReturn, Is.True);
            Assert.That(runtimeHost.receivedValue, Is.True);
            Assert.That(runtimeCondition.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator SetNextExecute_BooleanFalseReturnsInlineToParent()
        {
            var host = TreeTestFixture.CreateNode<InlineReturnProbe>("Host");
            var condition = TreeTestFixture.CreateNode<AiBoolean>("Condition");
            var conditionVariable = CreateBoolVariable(condition, "inlineFalse");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);

            using var fixture = TreeTestFixture.Create(host, new[] { conditionVariable }, host, condition);
            yield return fixture.WaitUntilReady();

            var runtimeHost = fixture.GetRuntimeNode<InlineReturnProbe>(host);
            var runtimeCondition = fixture.GetRuntimeNode<AiBoolean>(condition);
            runtimeCondition.boolean.SetValue(false);
            fixture.Tree.Start();
            yield return null;

            Assert.That(runtimeHost.receivedReturn, Is.True);
            Assert.That(runtimeHost.receivedValue, Is.False);
            Assert.That(runtimeCondition.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator SetNextExecute_BooleanInlineDoesNotHostServices()
        {
            var host = TreeTestFixture.CreateNode<InlineReturnProbe>("Host");
            var condition = TreeTestFixture.CreateNode<AiBoolean>("Condition");
            var conditionService = TreeTestFixture.CreateNode<ManualReadyService>("Boolean Service");
            var conditionVariable = CreateBoolVariable(condition, "inlineService");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);
            conditionService.parent = new NodeReference(condition.uuid);

            using var fixture = TreeTestFixture.Create(host, new[] { conditionVariable }, host, condition, conditionService);
            yield return fixture.WaitUntilReady();

            var runtimeCondition = fixture.GetRuntimeNode<AiBoolean>(condition);
            var runtimeService = fixture.GetRuntimeNode<ManualReadyService>(conditionService);
            runtimeCondition.boolean.SetValue(true);
            fixture.Tree.Start();
            yield return null;

            Assert.That(runtimeCondition, Is.Not.InstanceOf<IServiceHostNode>());
            Assert.That(runtimeService.registeredCount, Is.EqualTo(0));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeService), Is.False);
        }

        [UnityTest]
        public IEnumerator SetNextExecute_BooleanWithoutVariableReturnsInlineFalse()
        {
            var host = TreeTestFixture.CreateNode<InlineReturnProbe>("Host");
            var condition = TreeTestFixture.CreateNode<AiBoolean>("Condition");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);

            using var fixture = TreeTestFixture.Create(host, condition);
            yield return fixture.WaitUntilReady();

            var runtimeHost = fixture.GetRuntimeNode<InlineReturnProbe>(host);
            LogAssert.Expect(LogType.Error, new Regex(@"Exception occurred at node \[Condition\]"));
            LogAssert.Expect(LogType.Exception, new Regex(@"\[Boolean\] Variable ""boolean"" is required"));
            fixture.Tree.Start();
            yield return null;

            Assert.That(runtimeHost.receivedReturn, Is.True);
            Assert.That(runtimeHost.receivedValue, Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptService_DoesNotAllocateStackBeforeIntervalIsReady()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var interrupt = TreeTestFixture.CreateNode<Interrupt>("Interrupt");
            var condition = TreeTestFixture.CreateNode<Constant>("Condition");
            condition.returnValue = true;
            interrupt.interval = 2;
            interrupt.condition = new NodeReference(condition.uuid);
            AddServiceReference(host, interrupt);
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = TreeTestFixture.Create(host, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);

            fixture.Tick();

            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);
        }

        [UnityTest]
        public IEnumerator InterruptService_AllocatesStackForNormalConditionWhenIntervalIsReady()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var interrupt = TreeTestFixture.CreateNode<Interrupt>("Interrupt");
            var condition = TreeTestFixture.CreateNode<Constant>("Condition");
            condition.returnValue = false;
            interrupt.interval = 1;
            interrupt.condition = new NodeReference(condition.uuid);
            AddServiceReference(host, interrupt);
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = TreeTestFixture.Create(host, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            fixture.Tick();

            var cachedStack = fixture.Tree.ServiceStacks[runtimeInterrupt];
            Assert.That(cachedStack, Is.Not.Null);
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(cachedStack), Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptService_BooleanConditionPollsWithoutAllocatingStack()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var interrupt = TreeTestFixture.CreateNode<Interrupt>("Interrupt");
            var condition = TreeTestFixture.CreateNode<AiBoolean>("Condition");
            var conditionVariable = CreateBoolVariable(condition, "interruptCondition");
            interrupt.interval = 1;
            interrupt.condition = new NodeReference(condition.uuid);
            AddServiceReference(host, interrupt);
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = TreeTestFixture.Create(host, new[] { conditionVariable }, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            var runtimeCondition = fixture.GetRuntimeNode<AiBoolean>(condition);
            runtimeCondition.boolean.SetValue(false);
            fixture.Tick();

            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));

            runtimeCondition.boolean.SetValue(true);
            fixture.Tick();
            yield return null;

            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeInterrupt), Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptService_BooleanConditionWithoutVariableHandlesExceptionWithoutAllocatingStack()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var interrupt = TreeTestFixture.CreateNode<Interrupt>("Interrupt");
            var condition = TreeTestFixture.CreateNode<AiBoolean>("Condition");
            interrupt.interval = 1;
            interrupt.condition = new NodeReference(condition.uuid);
            AddServiceReference(host, interrupt);
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = TreeTestFixture.Create(host, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            LogAssert.Expect(LogType.Error, new Regex(@"Exception occurred at node \[Condition\]"));
            LogAssert.Expect(LogType.Exception, new Regex(@"\[Boolean\] Variable ""boolean"" is required"));
            fixture.Tick();

            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UpdateService_DeactivatesStackAfterSynchronousSubtreeCompletes()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var update = TreeTestFixture.CreateNode<Update>("Update");
            var subtree = TreeTestFixture.CreateNode<Constant>("Subtree");
            subtree.returnValue = true;
            update.interval = 0;
            update.subtreeHead = new NodeReference(subtree.uuid);
            AddServiceReference(host, update);
            subtree.parent = new NodeReference(update.uuid);

            using var fixture = TreeTestFixture.Create(host, update, subtree);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeUpdate = fixture.GetRuntimeNode<Update>(update);
            Assert.That(fixture.Tree.ServiceStacks[runtimeUpdate], Is.Null);

            fixture.Tick();

            var cachedStack = fixture.Tree.ServiceStacks[runtimeUpdate];
            Assert.That(cachedStack, Is.Not.Null);
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(cachedStack), Is.False);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UpdateService_ReusesCachedStackAcrossIntervals()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var update = TreeTestFixture.CreateNode<Update>("Update");
            var subtree = TreeTestFixture.CreateNode<Constant>("Subtree");
            subtree.returnValue = true;
            update.interval = 0;
            update.subtreeHead = new NodeReference(subtree.uuid);
            AddServiceReference(host, update);
            subtree.parent = new NodeReference(update.uuid);

            using var fixture = TreeTestFixture.Create(host, update, subtree);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeUpdate = fixture.GetRuntimeNode<Update>(update);
            fixture.Tick();
            var firstStack = fixture.Tree.ServiceStacks[runtimeUpdate];

            fixture.Tick();
            var secondStack = fixture.Tree.ServiceStacks[runtimeUpdate];

            Assert.That(firstStack, Is.Not.Null);
            Assert.That(secondStack, Is.SameAs(firstStack));
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(secondStack), Is.False);
        }

        [UnityTest]
        public IEnumerator DeactivateIdleStack_RejectsRunningStack()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");

            using var fixture = TreeTestFixture.Create(host);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            Assert.Throws<InvalidOperationException>(() => fixture.Tree.DeactivateIdleStack(fixture.Tree.MainStack));
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(fixture.Tree.MainStack), Is.True);
        }

        [UnityTest]
        public IEnumerator NullServices_StartFixedUpdateAndEnd_DoNotRegisterServices()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            host.services = null;

            using var fixture = TreeTestFixture.Create(host);
            yield return fixture.WaitUntilReady();

            var runtimeHost = fixture.GetRuntimeNode<YieldingNode>(host);
            Assert.That(runtimeHost.services, Is.Null);

            fixture.Tree.Start();
            Assert.That(fixture.Tree.ServiceStacks, Is.Empty);

            fixture.Tick();
            Assert.That(fixture.Tree.ServiceStacks, Is.Empty);

            Assert.DoesNotThrow(() => fixture.Tree.End());
            Assert.That(fixture.Tree.ServiceStacks, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EndingTree_UnregistersIdleServiceOnce()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var service = TreeTestFixture.CreateNode<ManualReadyService>("Manual Service");
            service.ready = false;
            AddServiceReference(host, service);

            using var fixture = TreeTestFixture.Create(host, service);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeService = fixture.GetRuntimeNode<ManualReadyService>(service);
            Assert.That(runtimeService.registeredCount, Is.EqualTo(1));
            Assert.That(fixture.Tree.ServiceStacks[runtimeService], Is.Null);

            fixture.Tree.End();

            Assert.That(runtimeService.unregisteredCount, Is.EqualTo(1));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeService), Is.False);
        }

        [UnityTest]
        public IEnumerator EndingTree_UnregistersAllocatedServiceOnce()
        {
            var host = TreeTestFixture.CreateNode<YieldingNode>("Host");
            var service = TreeTestFixture.CreateNode<ManualReadyService>("Manual Service");
            var child = TreeTestFixture.CreateNode<YieldingNode>("Service Child");
            service.ready = true;
            service.child = new NodeReference(child.uuid);
            AddServiceReference(host, service);
            child.parent = new NodeReference(service.uuid);

            using var fixture = TreeTestFixture.Create(host, service, child);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeService = fixture.GetRuntimeNode<ManualReadyService>(service);
            fixture.Tick();

            Assert.That(fixture.Tree.ServiceStacks[runtimeService], Is.Not.Null);

            fixture.Tree.End();

            Assert.That(runtimeService.unregisteredCount, Is.EqualTo(1));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeService), Is.False);
        }

        [UnityTest]
        public IEnumerator TimeoutService_InterruptReturnsSuccess()
        {
            // Arrange: InlineReturnProbe as parent records the forced return value,
            // YieldingNode as child gets interrupted by Timeout configured to return Success.
            var parent = TreeTestFixture.CreateNode<InlineReturnProbe>("Parent");
            var child = TreeTestFixture.CreateNode<YieldingNode>("Child");
            var timeout = TreeTestFixture.CreateNode<Timeout>("Timeout");
            timeout.result = Timeout.ReturnResult.Success;

            parent.child = new NodeReference(child.uuid);
            child.parent = new NodeReference(parent.uuid);
            AddServiceReference(child, timeout);

            using var fixture = TreeTestFixture.Create(parent, child, timeout);
            yield return fixture.WaitUntilReady();

            var runtimeParent = fixture.GetRuntimeNode<InlineReturnProbe>(parent);
            fixture.Start();

            // Act: let the simulation tick until the timeout fires and the forced result propagates.
            yield return fixture.WaitUntil(() => runtimeParent.receivedReturn);

            // Assert: parent received true (Success) from the interrupted child.
            Assert.That(runtimeParent.receivedReturn, Is.True);
            Assert.That(runtimeParent.receivedValue, Is.True);
        }

        [UnityTest]
        public IEnumerator TimeoutService_InterruptReturnsFailed()
        {
            // Arrange: same structure but Timeout configured to return Failed.
            var parent = TreeTestFixture.CreateNode<InlineReturnProbe>("Parent");
            var child = TreeTestFixture.CreateNode<YieldingNode>("Child");
            var timeout = TreeTestFixture.CreateNode<Timeout>("Timeout");
            timeout.result = Timeout.ReturnResult.Failed;

            parent.child = new NodeReference(child.uuid);
            child.parent = new NodeReference(parent.uuid);
            AddServiceReference(child, timeout);

            using var fixture = TreeTestFixture.Create(parent, child, timeout);
            yield return fixture.WaitUntilReady();

            var runtimeParent = fixture.GetRuntimeNode<InlineReturnProbe>(parent);
            fixture.Start();

            yield return fixture.WaitUntil(() => runtimeParent.receivedReturn);

            // Assert: parent received false (Failed) from the interrupted child.
            Assert.That(runtimeParent.receivedReturn, Is.True);
            Assert.That(runtimeParent.receivedValue, Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptService_InterruptReturnsSuccess()
        {
            // Arrange: Interrupt service with a Constant(true) condition,
            // configured to return Success when the condition fires.
            var parent = TreeTestFixture.CreateNode<InlineReturnProbe>("Parent");
            var child = TreeTestFixture.CreateNode<YieldingNode>("Child");
            var interrupt = TreeTestFixture.CreateNode<Interrupt>("Interrupt");
            interrupt.result = Interrupt.ReturnResult.Success;
            interrupt.interval = 0;

            var condition = TreeTestFixture.CreateNode<Constant>("Condition");
            condition.returnValue = true;
            interrupt.condition = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(interrupt.uuid);

            parent.child = new NodeReference(child.uuid);
            child.parent = new NodeReference(parent.uuid);
            AddServiceReference(child, interrupt);

            using var fixture = TreeTestFixture.Create(parent, child, interrupt, condition);
            yield return fixture.WaitUntilReady();

            var runtimeParent = fixture.GetRuntimeNode<InlineReturnProbe>(parent);
            fixture.Start();

            // Act: tick until the interrupt fires and the forced result propagates.
            yield return fixture.WaitUntil(() => runtimeParent.receivedReturn);

            // Assert: parent received true (Success) from the interrupted child.
            Assert.That(runtimeParent.receivedReturn, Is.True);
            Assert.That(runtimeParent.receivedValue, Is.True);
        }

        [UnityTest]
        public IEnumerator InterruptService_InterruptReturnsFailed()
        {
            // Arrange: same structure but Interrupt configured to return Failed.
            var parent = TreeTestFixture.CreateNode<InlineReturnProbe>("Parent");
            var child = TreeTestFixture.CreateNode<YieldingNode>("Child");
            var interrupt = TreeTestFixture.CreateNode<Interrupt>("Interrupt");
            interrupt.result = Interrupt.ReturnResult.Failed;
            interrupt.interval = 0;

            var condition = TreeTestFixture.CreateNode<Constant>("Condition");
            condition.returnValue = true;
            interrupt.condition = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(interrupt.uuid);

            parent.child = new NodeReference(child.uuid);
            child.parent = new NodeReference(parent.uuid);
            AddServiceReference(child, interrupt);

            using var fixture = TreeTestFixture.Create(parent, child, interrupt, condition);
            yield return fixture.WaitUntilReady();

            var runtimeParent = fixture.GetRuntimeNode<InlineReturnProbe>(parent);
            fixture.Start();

            yield return fixture.WaitUntil(() => runtimeParent.receivedReturn);

            // Assert: parent received false (Failed) from the interrupted child.
            Assert.That(runtimeParent.receivedReturn, Is.True);
            Assert.That(runtimeParent.receivedValue, Is.False);
        }

        private static void AddServiceReference(IServiceHostNode host, Service service)
        {
            if (host == null)
            {
                throw new ArgumentException("Host node cannot own services.", nameof(host));
            }

            ServiceHostNodeUtility.AssertHostIsNode(host);
            host.AddService(service);
        }

        private static VariableData CreateBoolVariable(AiBoolean condition, string name)
        {
            var variable = new VariableData(name, VariableType.Bool);
            // Tree initialization parses the serialized default before tests override the runtime value.
            variable.DefaultValue = false.ToString();
            condition.boolean = new VariableReference();
            condition.boolean.SetReference(variable);
            return variable;
        }

        private static IEnumerator WaitUntilOrTimeout(Func<bool> predicate)
        {
            // Stack continuations resume asynchronously, so wait for the observed effect instead of fixed frames.
            float timeout = Time.realtimeSinceStartup + 1f;
            while (!predicate() && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
        }

        private static string ToAssetPath(string path)
        {
            string fullDataPath = Path.GetFullPath(Application.dataPath);
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(fullDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return fullPath;
            }

            string relativePath = fullPath.Substring(fullDataPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return $"Assets/{relativePath}";
        }

        [Serializable]
        private sealed class InlineReturnProbe : Flow
        {
            public NodeReference child;
            public bool receivedReturn;
            public bool receivedValue;

            public override State Execute()
            {
                return SetNextExecute(child);
            }

            public override State ReceiveReturnFromChild(bool @return)
            {
                receivedReturn = true;
                receivedValue = @return;
                return State.Success;
            }

            public override void Initialize()
            {
                behaviourTree.GetNode(ref child);
            }
        }

        [DoNotRelease]
        [Serializable]
        private sealed class YieldingNode : Flow
        {
            public override State Execute()
            {
                return State.Yield;
            }

            public override void Initialize()
            {
            }
        }

        [DoNotRelease]
        [Serializable]
        private sealed class CountingResultNode : TreeNode
        {
            public bool returnValue;
            public int runCount;

            public override State Execute()
            {
                // Count executions so timeout handoff tests can prove the next child actually ran.
                runCount++;
                return StateOf(returnValue);
            }

            public override void Initialize()
            {
                runCount = 0;
            }
        }

        [DoNotRelease]
        [Serializable]
        private sealed class InstantCallProbe : Call
        {
            public override State Execute()
            {
                return State.Success;
            }
        }

        [DoNotRelease]
        [Serializable]
        private sealed class InstantDetermineProbe : Determine
        {
            public override bool GetValue()
            {
                return true;
            }
        }

        [DoNotRelease]
        [Serializable]
        private sealed class InstantArithmeticProbe : Arithmetic
        {
            public override State Execute()
            {
                return State.Success;
            }
        }

        [DoNotRelease]
        [Serializable]
        private sealed class ActionHostProbe : Aethiumian.AI.Nodes.Action
        {
            public override void Start()
            {
                Success();
            }
        }

        [DoNotRelease]
        [Serializable]
        private sealed class ManualReadyService : Service
        {
            public bool ready;
            public NodeReference child;
            public int registeredCount;
            public int unregisteredCount;

            public override bool IsReady => ready;

            public override State Execute()
            {
                return child.HasReference ? SetNextExecute(child) : State.Success;
            }

            public override void Initialize()
            {
                behaviourTree.GetNode(ref child);
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
