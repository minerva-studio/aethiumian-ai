#nullable enable
using Aethiumian.AI.Editor.Tests.Support;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests.Execution
{
    public sealed class AIPauseFocusedTests
    {
        [Test]
        public void PauseAndResume_AreIdempotent()
        {
            GameObject gameObject = new("AIPauseFocusedTests");
            try
            {
                AI ai = gameObject.AddComponent<AI>();

                ai.Pause();
                ai.Pause();
                Assert.That(ai.IsPaused, Is.True);

                ai.Resume();
                ai.Resume();
                Assert.That(ai.IsPaused, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator PauseAndResume_DoNotChangeFaultState()
        {
            using AIContext context = CreateAIContext();
            while (!context.Tree.IsInitialized && !context.Tree.IsFaulted)
                yield return null;
            Assert.That(context.Tree.IsFaulted, Is.False);
            context.Tree.LatchRuntimeFault(new InvalidOperationException("existing fault"), "test");
            Assert.That(context.Tree.IsFaulted, Is.True);

            context.AI.Pause();
            Assert.That(context.Tree.IsFaulted, Is.True);
            context.AI.Resume();
            Assert.That(context.Tree.IsFaulted, Is.True);
            Assert.That(context.Tree.RuntimeFault!.Source, Is.EqualTo("test"));
        }

        [UnityTest]
        public IEnumerator PausedAI_DoesNotForwardCallbacksOrAutoRestart()
        {
            using AIContext context = CreateAIContext();
            while (!context.Tree.IsInitialized && !context.Tree.IsFaulted)
                yield return null;
            Assert.That(context.Tree.IsFaulted, Is.False);
            ProbeAction probe = context.Probe;
            context.AI.Start(true);
            Assert.That(context.Tree.IsRunning, Is.True);

            InvokeCallback(context.AI, "Update");
            InvokeCallback(context.AI, "LateUpdate");
            InvokeCallback(context.AI, "FixedUpdate");
            int updateCount = probe.UpdateCount;
            int lateUpdateCount = probe.LateUpdateCount;
            int fixedUpdateCount = probe.FixedUpdateCount;

            context.AI.Pause();
            InvokeCallback(context.AI, "Update");
            InvokeCallback(context.AI, "LateUpdate");
            InvokeCallback(context.AI, "FixedUpdate");
            Assert.That(probe.UpdateCount, Is.EqualTo(updateCount));
            Assert.That(probe.LateUpdateCount, Is.EqualTo(lateUpdateCount));
            Assert.That(probe.FixedUpdateCount, Is.EqualTo(fixedUpdateCount));

            context.Tree.End();
            int startCount = probe.StartCount;
            InvokeCallback(context.AI, "FixedUpdate");
            Assert.That(probe.StartCount, Is.EqualTo(startCount));

            context.AI.Resume();
            InvokeCallback(context.AI, "FixedUpdate");
            Assert.That(context.Tree.IsRunning, Is.True);
            Assert.That(probe.StartCount, Is.EqualTo(startCount));
        }

        [UnityTest]
        public IEnumerator PauseNode_WithoutAIOwner_ReturnsFailed()
        {
            Pause pause = TreeTestFixture.CreateNode<Pause>("Pause");
            using TreeTestFixture fixture = TreeTestFixture.Create(pause);
            yield return fixture.WaitUntilReady();

            LogAssert.Expect(LogType.Error, "Pause node requires an AI owner.");
            fixture.Start();
            fixture.Tick();

            Assert.That(fixture.Tree.IsFaulted, Is.False);
            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.EqualTo(false));
        }

        [UnityTest]
        public IEnumerator PauseNode_StopsCurrentTick_AndResumeRetainsTreeState()
        {
            using AIContext context = CreatePauseSequenceContext();
            while (!context.Tree.IsInitialized && !context.Tree.IsFaulted)
                yield return null;
            Assert.That(context.Tree.IsFaulted, Is.False);

            ProbeAction probe = context.RuntimeProbe;
            context.AI.Start(false);
            InvokeCallback(context.AI, "Update");
            Assert.That(context.AI.IsPaused, Is.True);
            Assert.That(probe.StartCount, Is.Zero);
            Assert.That(context.Tree.IsRunning, Is.True);

            context.AI.Resume();
            Assert.That(context.AI.IsPaused, Is.False);
            Assert.That(context.Tree.IsRunning, Is.True);
            Assert.That(context.Tree.IsFaulted, Is.False);
        }

        private static AIContext CreatePauseSequenceContext()
        {
            Pause pause = TreeTestFixture.CreateNode<Pause>("Pause");
            ProbeAction probe = TreeTestFixture.CreateNode<ProbeAction>("Probe");
            Sequence sequence = TreeTestFixture.CreateNode<Sequence>("Sequence");
            sequence.events = new[]
            {
                new Aethiumian.AI.References.NodeReference(pause.uuid),
                new Aethiumian.AI.References.NodeReference(probe.uuid),
            };

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = sequence.uuid;
            data.nodes.Add(sequence);
            data.nodes.Add(pause);
            data.nodes.Add(probe);
            GameObject gameObject = new("AIPauseFocusedTests");
            AI ai = gameObject.AddComponent<AI>();
            ai.Data = data;
            ai.ControlTarget = ai;
            InvokeCallback(ai, "CreateBehaviourTree");
            return new AIContext(ai, probe, data, gameObject);
        }

        private static AIContext CreateAIContext()
        {
            ProbeAction probe = TreeTestFixture.CreateNode<ProbeAction>("Probe");
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = probe.uuid;
            data.nodes.Add(probe);
            GameObject gameObject = new("AIPauseFocusedTests");
            AI ai = gameObject.AddComponent<AI>();
            ai.Data = data;
            ai.ControlTarget = ai;
            InvokeCallback(ai, "CreateBehaviourTree");
            return new AIContext(ai, probe, data, gameObject);
        }

        private static void InvokeCallback(AI ai, string methodName)
        {
            MethodInfo method = typeof(AI).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(typeof(AI).FullName, methodName);
            method.Invoke(ai, null);
        }

        private sealed class AIContext : IDisposable
        {
            public readonly AI AI;
            public readonly ProbeAction Probe;
            public ProbeAction RuntimeProbe => (ProbeAction)Tree.References[Probe.uuid]!;
            private readonly BehaviourTreeData data;
            private readonly GameObject gameObject;
            public BehaviourTree Tree => AI.BehaviourTree;

            public AIContext(AI ai, ProbeAction probe, BehaviourTreeData data, GameObject gameObject)
            {
                AI = ai;
                Probe = probe;
                this.data = data;
                this.gameObject = gameObject;
            }

            public void Dispose()
            {
                if (Tree != null && Tree.IsRunning) Tree.End();
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        [Serializable]
        private sealed class ProbeAction : Aethiumian.AI.Nodes.Action
        {
            public int StartCount { get; private set; }
            public int UpdateCount { get; private set; }
            public int LateUpdateCount { get; private set; }
            public int FixedUpdateCount { get; private set; }

            public override void Start() => StartCount++;
            public override void Update() => UpdateCount++;
            public override void LateUpdate() => LateUpdateCount++;
            public override void FixedUpdate() => FixedUpdateCount++;
        }
    }
}
