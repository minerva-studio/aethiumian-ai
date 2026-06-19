using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Amlos.AI.Tests
{
    public class BehaviourTreeServiceStackTests
    {
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

            fixture.Tree.FixedUpdate();
            yield return null;

            Assert.That(
                !fixture.Tree.ServiceStacks.ContainsKey(runtimeInterrupt) || fixture.Tree.ServiceStacks[runtimeInterrupt] == null,
                Is.True);
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
        public IEnumerator DeactivateStack_RejectsRunningStack()
        {
            var host = CreateNode<YieldingNode>("Host");

            using var fixture = CreateFixture(host);
            yield return fixture.WaitUntilReady();
            fixture.Tree.Start();

            Assert.Throws<InvalidOperationException>(() => fixture.Tree.DeactivateStack(fixture.Tree.MainStack));
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
            data.nodes.AddRange(nodes);

            var gameObject = new GameObject("BehaviourTreeServiceStackTests");
            var script = gameObject.AddComponent<TestBehaviour>();
            var tree = new BehaviourTree(data, gameObject, script);
            return new TreeFixture(data, gameObject, tree);
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
