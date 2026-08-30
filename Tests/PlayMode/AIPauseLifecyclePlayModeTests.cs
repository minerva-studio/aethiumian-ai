#nullable enable
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.PlayMode.Tests
{
    public sealed class AIPauseLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator PauseGatesAutomaticCallbacks_AndResumeAllowsAutoRestart()
        {
            ProbeAction prototype = new()
            {
                name = "PauseLifecycleProbe",
                uuid = Aethiumian.AI.UUID.NewUUID(),
                parent = Aethiumian.AI.References.NodeReference.Empty,
            };
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = prototype.uuid;
            data.nodes.Add(prototype);

            GameObject gameObject = new("AIPauseLifecyclePlayMode");
            AI ai = gameObject.AddComponent<AI>();
            ai.Data = data;
            ai.awakeStart = false;
            ai.autoRestart = true;

            try
            {
                yield return new WaitUntil(() => ai.BehaviourTree != null && ai.BehaviourTree.IsInitialized);
                ai.Start(true);
                yield return null;

                ProbeAction runtimeProbe = (ProbeAction)ai.BehaviourTree!.References[prototype.uuid]!;
                Assert.That(ai.IsRunning, Is.True);
                int updateCount = runtimeProbe.UpdateCount;
                int lateUpdateCount = runtimeProbe.LateUpdateCount;
                int fixedUpdateCount = runtimeProbe.FixedUpdateCount;

                ai.Pause();
                yield return new WaitForFixedUpdate();
                Assert.That(runtimeProbe.UpdateCount, Is.EqualTo(updateCount));
                Assert.That(runtimeProbe.LateUpdateCount, Is.EqualTo(lateUpdateCount));
                Assert.That(runtimeProbe.FixedUpdateCount, Is.EqualTo(fixedUpdateCount));

                ai.End(false);
                yield return new WaitForFixedUpdate();
                Assert.That(ai.IsRunning, Is.False);

                ai.Resume();
                yield return new WaitForFixedUpdate();
                Assert.That(ai.IsRunning, Is.False, "End(false) must keep auto-restart disabled.");

                ai.Start(true);
                ai.Pause();
                yield return new WaitForFixedUpdate();
                Assert.That(ai.IsRunning, Is.True);
                ai.End();
                ai.Resume();
                yield return new WaitForFixedUpdate();
                Assert.That(ai.IsRunning, Is.True, "Resume must reopen the automatic restart gate.");
            }
            finally
            {
                UnityEngine.Object.Destroy(gameObject);
                UnityEngine.Object.Destroy(data);
            }
        }

        [Serializable]
        private sealed class ProbeAction : Aethiumian.AI.Nodes.Action
        {
            public int UpdateCount { get; private set; }
            public int LateUpdateCount { get; private set; }
            public int FixedUpdateCount { get; private set; }

            public override void Update() => UpdateCount++;
            public override void LateUpdate() => LateUpdateCount++;
            public override void FixedUpdate() => FixedUpdateCount++;
        }
    }
}
