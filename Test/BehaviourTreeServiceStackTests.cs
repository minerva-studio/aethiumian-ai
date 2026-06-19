using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AiBoolean = Amlos.AI.Nodes.Boolean;

namespace Amlos.AI.Tests
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

        [UnityTest]
        public IEnumerator TimeoutService_RegistersWithoutAllocatingStack_AndInterruptsHost()
        {
            var host = CreateNode<YieldingNode>("Host");
            var timeout = CreateNode<Timeout>("Timeout");
            host.services.Add(new NodeReference(timeout.uuid));

            using var fixture = CreateFixture(host, timeout);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeTimeout = fixture.GetRuntimeNode<Timeout>(timeout);
            Assert.That(fixture.Tree.ServiceStacks.TryGetValue(runtimeTimeout, out var stack), Is.True);
            Assert.That(stack, Is.Null);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));

            fixture.Tree.FixedUpdate();
            yield return null;

            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeTimeout), Is.False);
        }

        [UnityTest]
        public IEnumerator SetNextExecute_BooleanTrueReturnsInlineToParent()
        {
            var host = CreateNode<InlineReturnProbe>("Host");
            var condition = CreateNode<AiBoolean>("Condition");
            var conditionVariable = CreateBoolVariable(condition, "inlineTrue");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);

            using var fixture = CreateFixtureWithVariables(host, new[] { conditionVariable }, host, condition);
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
            var host = CreateNode<InlineReturnProbe>("Host");
            var condition = CreateNode<AiBoolean>("Condition");
            var conditionVariable = CreateBoolVariable(condition, "inlineFalse");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);

            using var fixture = CreateFixtureWithVariables(host, new[] { conditionVariable }, host, condition);
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
        public IEnumerator SetNextExecute_BooleanInlineSkipsBooleanServices()
        {
            var host = CreateNode<InlineReturnProbe>("Host");
            var condition = CreateNode<AiBoolean>("Condition");
            var conditionService = CreateNode<ManualReadyService>("Boolean Service");
            var conditionVariable = CreateBoolVariable(condition, "inlineService");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);
            condition.services.Add(new NodeReference(conditionService.uuid));
            conditionService.parent = new NodeReference(condition.uuid);

            using var fixture = CreateFixtureWithVariables(host, new[] { conditionVariable }, host, condition, conditionService);
            yield return fixture.WaitUntilReady();

            var runtimeCondition = fixture.GetRuntimeNode<AiBoolean>(condition);
            var runtimeService = fixture.GetRuntimeNode<ManualReadyService>(conditionService);
            runtimeCondition.boolean.SetValue(true);
            fixture.Tree.Start();
            yield return null;

            Assert.That(runtimeService.registeredCount, Is.EqualTo(0));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeService), Is.False);
        }

        [UnityTest]
        public IEnumerator SetNextExecute_BooleanWithoutVariableReturnsInlineFalse()
        {
            var host = CreateNode<InlineReturnProbe>("Host");
            var condition = CreateNode<AiBoolean>("Condition");
            host.child = new NodeReference(condition.uuid);
            condition.parent = new NodeReference(host.uuid);

            using var fixture = CreateFixture(host, condition);
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
            var host = CreateNode<YieldingNode>("Host");
            var interrupt = CreateNode<Interrupt>("Interrupt");
            var condition = CreateNode<Constant>("Condition");
            condition.returnValue = true;
            interrupt.interval = 2;
            interrupt.condition = new NodeReference(condition.uuid);
            host.services.Add(new NodeReference(interrupt.uuid));
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = CreateFixture(host, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);

            fixture.Tree.FixedUpdate();

            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);
        }

        [UnityTest]
        public IEnumerator InterruptService_AllocatesStackForNormalConditionWhenIntervalIsReady()
        {
            var host = CreateNode<YieldingNode>("Host");
            var interrupt = CreateNode<Interrupt>("Interrupt");
            var condition = CreateNode<Constant>("Condition");
            condition.returnValue = false;
            interrupt.interval = 1;
            interrupt.condition = new NodeReference(condition.uuid);
            host.services.Add(new NodeReference(interrupt.uuid));
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = CreateFixture(host, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            fixture.Tree.FixedUpdate();

            var cachedStack = fixture.Tree.ServiceStacks[runtimeInterrupt];
            Assert.That(cachedStack, Is.Not.Null);
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(cachedStack), Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptService_BooleanConditionPollsWithoutAllocatingStack()
        {
            var host = CreateNode<YieldingNode>("Host");
            var interrupt = CreateNode<Interrupt>("Interrupt");
            var condition = CreateNode<AiBoolean>("Condition");
            var conditionVariable = CreateBoolVariable(condition, "interruptCondition");
            interrupt.interval = 1;
            interrupt.condition = new NodeReference(condition.uuid);
            host.services.Add(new NodeReference(interrupt.uuid));
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = CreateFixtureWithVariables(host, new[] { conditionVariable }, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            var runtimeCondition = fixture.GetRuntimeNode<AiBoolean>(condition);
            runtimeCondition.boolean.SetValue(false);
            fixture.Tree.FixedUpdate();

            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));

            runtimeCondition.boolean.SetValue(true);
            fixture.Tree.FixedUpdate();
            yield return null;

            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeInterrupt), Is.False);
        }

        [UnityTest]
        public IEnumerator InterruptService_BooleanConditionRespectsIgnoredChildren()
        {
            var host = CreateNode<YieldingNode>("Host");
            var interrupt = CreateNode<Interrupt>("Interrupt");
            var condition = CreateNode<AiBoolean>("Condition");
            var conditionVariable = CreateBoolVariable(condition, "ignoredCondition");
            interrupt.interval = 1;
            interrupt.condition = new NodeReference(condition.uuid);
            interrupt.ignoredChildren = new()
            {
                new RawNodeReference { UUID = host.uuid },
            };
            host.services.Add(new NodeReference(interrupt.uuid));
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = CreateFixtureWithVariables(host, new[] { conditionVariable }, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            var runtimeCondition = fixture.GetRuntimeNode<AiBoolean>(condition);
            runtimeCondition.boolean.SetValue(true);
            fixture.Tree.FixedUpdate();

            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InterruptService_BooleanConditionWithoutVariableHandlesExceptionWithoutAllocatingStack()
        {
            var host = CreateNode<YieldingNode>("Host");
            var interrupt = CreateNode<Interrupt>("Interrupt");
            var condition = CreateNode<AiBoolean>("Condition");
            interrupt.interval = 1;
            interrupt.condition = new NodeReference(condition.uuid);
            host.services.Add(new NodeReference(interrupt.uuid));
            condition.parent = new NodeReference(interrupt.uuid);

            using var fixture = CreateFixture(host, interrupt, condition);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeInterrupt = fixture.GetRuntimeNode<Interrupt>(interrupt);
            LogAssert.Expect(LogType.Error, new Regex(@"Exception occurred at node \[Condition\]"));
            LogAssert.Expect(LogType.Exception, new Regex(@"\[Boolean\] Variable ""boolean"" is required"));
            fixture.Tree.FixedUpdate();

            Assert.That(fixture.Tree.ServiceStacks[runtimeInterrupt], Is.Null);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UpdateService_DeactivatesStackAfterSynchronousSubtreeCompletes()
        {
            var host = CreateNode<YieldingNode>("Host");
            var update = CreateNode<Update>("Update");
            var subtree = CreateNode<Constant>("Subtree");
            subtree.returnValue = true;
            update.interval = 0;
            update.subtreeHead = new NodeReference(subtree.uuid);
            host.services.Add(new NodeReference(update.uuid));
            subtree.parent = new NodeReference(update.uuid);

            using var fixture = CreateFixture(host, update, subtree);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeUpdate = fixture.GetRuntimeNode<Update>(update);
            Assert.That(fixture.Tree.ServiceStacks[runtimeUpdate], Is.Null);

            fixture.Tree.FixedUpdate();

            var cachedStack = fixture.Tree.ServiceStacks[runtimeUpdate];
            Assert.That(cachedStack, Is.Not.Null);
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(cachedStack), Is.False);
            Assert.That(fixture.Tree.ActiveStacks.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator UpdateService_ReusesCachedStackAcrossIntervals()
        {
            var host = CreateNode<YieldingNode>("Host");
            var update = CreateNode<Update>("Update");
            var subtree = CreateNode<Constant>("Subtree");
            subtree.returnValue = true;
            update.interval = 0;
            update.subtreeHead = new NodeReference(subtree.uuid);
            host.services.Add(new NodeReference(update.uuid));
            subtree.parent = new NodeReference(update.uuid);

            using var fixture = CreateFixture(host, update, subtree);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeUpdate = fixture.GetRuntimeNode<Update>(update);
            fixture.Tree.FixedUpdate();
            var firstStack = fixture.Tree.ServiceStacks[runtimeUpdate];

            fixture.Tree.FixedUpdate();
            var secondStack = fixture.Tree.ServiceStacks[runtimeUpdate];

            Assert.That(firstStack, Is.Not.Null);
            Assert.That(secondStack, Is.SameAs(firstStack));
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(secondStack), Is.False);
        }

        [UnityTest]
        public IEnumerator DeactivateIdleStack_RejectsRunningStack()
        {
            var host = CreateNode<YieldingNode>("Host");

            using var fixture = CreateFixture(host);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            Assert.Throws<InvalidOperationException>(() => fixture.Tree.DeactivateIdleStack(fixture.Tree.MainStack));
            Assert.That(fixture.Tree.ActiveStacks.ContainsKey(fixture.Tree.MainStack), Is.True);
        }

        [UnityTest]
        public IEnumerator EndingTree_UnregistersIdleServiceOnce()
        {
            var host = CreateNode<YieldingNode>("Host");
            var service = CreateNode<ManualReadyService>("Manual Service");
            service.ready = false;
            host.services.Add(new NodeReference(service.uuid));

            using var fixture = CreateFixture(host, service);
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
            var host = CreateNode<YieldingNode>("Host");
            var service = CreateNode<ManualReadyService>("Manual Service");
            var child = CreateNode<YieldingNode>("Service Child");
            service.ready = true;
            service.child = new NodeReference(child.uuid);
            host.services.Add(new NodeReference(service.uuid));
            child.parent = new NodeReference(service.uuid);

            using var fixture = CreateFixture(host, service, child);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            var runtimeService = fixture.GetRuntimeNode<ManualReadyService>(service);
            fixture.Tree.FixedUpdate();

            Assert.That(fixture.Tree.ServiceStacks[runtimeService], Is.Not.Null);

            fixture.Tree.End();

            Assert.That(runtimeService.unregisteredCount, Is.EqualTo(1));
            Assert.That(fixture.Tree.ServiceStacks.ContainsKey(runtimeService), Is.False);
        }

        private static T CreateNode<T>(string name) where T : TreeNode, new()
        {
            return new T
            {
                name = name,
                uuid = UUID.NewUUID(),
                parent = NodeReference.Empty,
            };
        }

        private static TreeFixture CreateFixture(TreeNode head, params TreeNode[] nodes)
        {
            var data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = head.uuid;
            data.nodes.Add(head);
            foreach (var node in nodes)
            {
                if (node.uuid != head.uuid)
                {
                    data.nodes.Add(node);
                }
            }

            var gameObject = new GameObject("BehaviourTreeServiceStackTests");
            var script = gameObject.AddComponent<TestBehaviour>();
            var tree = new BehaviourTree(data, gameObject, script);
            return new TreeFixture(data, gameObject, tree);
        }

        private static TreeFixture CreateFixtureWithVariables(TreeNode head, VariableData[] variables, params TreeNode[] nodes)
        {
            var data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = head.uuid;
            data.variables.AddRange(variables);
            data.nodes.Add(head);
            foreach (var node in nodes)
            {
                if (node.uuid != head.uuid)
                {
                    data.nodes.Add(node);
                }
            }

            var gameObject = new GameObject("BehaviourTreeServiceStackTests");
            var script = gameObject.AddComponent<TestBehaviour>();
            var tree = new BehaviourTree(data, gameObject, script);
            return new TreeFixture(data, gameObject, tree);
        }

        private static VariableData CreateBoolVariable(AiBoolean condition, string name)
        {
            var variable = new VariableData(name, VariableType.Bool);
            condition.boolean = new VariableReference();
            condition.boolean.SetReference(variable);
            return variable;
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

        private sealed class TreeFixture : IDisposable
        {
            private readonly BehaviourTreeData data;
            private readonly GameObject gameObject;

            public BehaviourTree Tree { get; }

            public TreeFixture(BehaviourTreeData data, GameObject gameObject, BehaviourTree tree)
            {
                this.data = data;
                this.gameObject = gameObject;
                Tree = tree;
            }

            public IEnumerator WaitUntilReady()
            {
                float timeout = Time.realtimeSinceStartup + 5f;
                while (!Tree.IsInitialized && !Tree.IsError && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }

                Assert.That(Tree.IsError, Is.False);
                Assert.That(Tree.IsInitialized, Is.True);
            }

            public T GetRuntimeNode<T>(TreeNode prototype) where T : TreeNode
            {
                return (T)Tree.References[prototype.uuid];
            }

            public void Dispose()
            {
                Tree.End();
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private sealed class TestBehaviour : MonoBehaviour
        {
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

        [Serializable]
        private sealed class YieldingNode : TreeNode
        {
            public override State Execute()
            {
                return State.Yield;
            }

            public override void Initialize()
            {
            }
        }

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
