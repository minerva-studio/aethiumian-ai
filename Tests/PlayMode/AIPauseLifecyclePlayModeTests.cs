#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
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
            data.timeSettings = new TimeSettings { domain = TimeDomain.AI, scaleMode = TimeScaleMode.Unscaled };
            data.headNodeUUID = prototype.uuid;
            data.nodes.Add(prototype);
            VariableData timerDefinition = new("PauseTimer", VariableType.Float);
            timerDefinition.Flags |= VariableFlag.Timer;
            timerDefinition.TypeReference.SetBaseType(typeof(float));
            data.variables.Add(timerDefinition);

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
                TimerVariable timer = (TimerVariable)ai.BehaviourTree.Variables[timerDefinition.UUID];
                timer.SetValue(1f);
                Assert.That(ai.IsRunning, Is.True);
                int updateCount = runtimeProbe.UpdateCount;
                int lateUpdateCount = runtimeProbe.LateUpdateCount;
                int fixedUpdateCount = runtimeProbe.FixedUpdateCount;

                ai.Pause();
                yield return new WaitForFixedUpdate();
                Assert.That(runtimeProbe.UpdateCount, Is.EqualTo(updateCount));
                Assert.That(runtimeProbe.LateUpdateCount, Is.EqualTo(lateUpdateCount));
                Assert.That(runtimeProbe.FixedUpdateCount, Is.EqualTo(fixedUpdateCount));

                gameObject.SetActive(false);
                yield return null;
                float frozenWhileDisabled = timer.Remaining;
                ai.Resume();
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(timer.Remaining, Is.EqualTo(frozenWhileDisabled).Within(0.01f),
                    "Resume while disabled must not advance the AI Timer.");
                gameObject.SetActive(true);
                yield return new WaitForFixedUpdate();
                Assert.That(ai.IsPaused, Is.False);
                Assert.That(ai.IsRunning, Is.True, "Resume while the driver was disabled must preserve the running tree.");
                Assert.That(timer.Remaining, Is.LessThan(frozenWhileDisabled),
                    "The AI Timer must continue after the driver is enabled again.");

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

        [System.Serializable]
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
