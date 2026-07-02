#nullable enable
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests
{
    public sealed class BehaviourTreeStartWhenInitializedTests
    {
        [UnityTest]
        public IEnumerator StartWhenInitialized_QueuedBeforeReady_StartsAfterInitialization()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var head = TreeTestFixture.CreateNode<YieldingProbeFlow>("Head");

            using var fixture = TreeTestFixture.Create(head);
            fixture.Tree.StartWhenInitialized();

            if (!fixture.Tree.IsInitialized)
            {
                Assert.That(fixture.Tree.IsRunning, Is.False);
            }

            yield return fixture.WaitUntilReady();

            var runtimeHead = fixture.GetRuntimeNode(head);
            Assert.That(fixture.Tree.IsRunning, Is.True);
            Assert.That(fixture.Tree.CurrentStage.Node, Is.SameAs(runtimeHead));
            Assert.That(runtimeHead.runCount, Is.EqualTo(1));
            Assert.That(runtimeHead.executeThreadId, Is.EqualTo(mainThreadId));
        }

        [UnityTest]
        public IEnumerator StartWhenInitialized_ReadyTree_StartsImmediately()
        {
            var head = TreeTestFixture.CreateNode<YieldingProbeFlow>("Head");

            using var fixture = TreeTestFixture.Create(head);
            yield return fixture.WaitUntilReady();

            fixture.Tree.StartWhenInitialized();

            var runtimeHead = fixture.GetRuntimeNode(head);
            Assert.That(fixture.Tree.IsRunning, Is.True);
            Assert.That(fixture.Tree.CurrentStage.Node, Is.SameAs(runtimeHead));
            Assert.That(runtimeHead.runCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StartWhenInitialized_MultipleRequests_StartOnlyOnce()
        {
            var head = TreeTestFixture.CreateNode<YieldingProbeFlow>("Head");

            using var fixture = TreeTestFixture.Create(head);
            fixture.Tree.StartWhenInitialized();
            fixture.Tree.StartWhenInitialized();

            yield return fixture.WaitUntilReady();

            var runtimeHead = fixture.GetRuntimeNode(head);
            Assert.That(fixture.Tree.IsRunning, Is.True);
            Assert.That(runtimeHead.runCount, Is.EqualTo(1));

            fixture.Tree.StartWhenInitialized();
            Assert.That(runtimeHead.runCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator StartWhenInitialized_InitializationFailure_DoesNotStart()
        {
            LogAssert.Expect(LogType.Exception, new Regex("Invalid behaviour tree, no head was found"));

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = UUID.Empty;

            GameObject gameObject = new("StartWhenInitializedFailureTest");
            try
            {
                TestBehaviour script = gameObject.AddComponent<TestBehaviour>();
                BehaviourTree tree = new(data, gameObject, script);
                tree.StartWhenInitialized();

                float deadline = Time.realtimeSinceStartup + 5f;
                while (!tree.IsError && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(tree.IsError, Is.True);
                Assert.That(tree.IsRunning, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Serializable]
        private sealed class YieldingProbeFlow : Flow
        {
            public int runCount;
            public int executeThreadId;

            public override State Execute()
            {
                // Yield keeps the stack alive so the test can inspect the active stage.
                executeThreadId = Thread.CurrentThread.ManagedThreadId;
                runCount++;
                return State.Yield;
            }

            public override void Initialize()
            {
                runCount = 0;
                executeThreadId = 0;
            }
        }

        private sealed class TestBehaviour : MonoBehaviour
        {
        }
    }
}
