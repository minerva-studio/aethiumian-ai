using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.PlayMode.Tests
{
    /// <summary>Verifies real scaled-time deadlines and tree-owned lifetime boundaries.</summary>
    public sealed class TimerLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator AIClockFreezesAtPauseAndResumesOnRealFrames()
        {
            VariableData definition = new("AITimer", VariableType.Float);
            definition.Flags |= VariableFlag.Timer;
            YieldAction head = new() { uuid = UUID.NewUUID() };
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = TimeDomain.AI, scaleMode = TimeScaleMode.Unscaled };
            data.variables.Add(definition);
            data.nodes.Add(head);
            data.headNodeUUID = head.uuid;
            GameObject host = new("AITimerLifecycle");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;
            try
            {
                yield return WaitForInitialization(ai);
                ai.Start(false);
                TimerVariable timer = (TimerVariable)ai.BehaviourTree.Variables[definition.UUID];
                timer.SetValue(0.2f);
                yield return new WaitForSecondsRealtime(0.03f);
                float beforePause = timer.Remaining;

                ai.Pause();
                timer.SetValue(0.2f);
                float frozen = timer.Remaining;
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(timer.Remaining, Is.EqualTo(frozen).Within(0.005f));

                ai.End(false);
                ai.Start(false);
                timer.SetValue(0.2f);
                float frozenAfterStartWhilePaused = timer.Remaining;
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(timer.Remaining, Is.EqualTo(frozenAfterStartWhilePaused).Within(0.005f),
                    "Starting while paused must not advance the AI Timer.");

                ai.Resume();
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(timer.Remaining, Is.LessThan(frozenAfterStartWhilePaused).And.GreaterThan(0f));
                Assert.That(beforePause, Is.LessThan(0.2f));
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        [UnityTest]
        public IEnumerator AITimerScaledAndUnscaledRespectTimeScaleZero()
        {
            yield return RunAiTimerScaleCase(TimeScaleMode.Scaled);
            yield return RunAiTimerScaleCase(TimeScaleMode.Unscaled);
        }

        private static IEnumerator RunAiTimerScaleCase(TimeScaleMode scaleMode)
        {
            VariableData definition = new($"AI {scaleMode}", VariableType.Float);
            definition.Flags |= VariableFlag.Timer;
            YieldAction head = new() { uuid = UUID.NewUUID() };
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = TimeDomain.AI, scaleMode = scaleMode };
            data.variables.Add(definition);
            data.nodes.Add(head);
            data.headNodeUUID = head.uuid;
            GameObject host = CreateTimerHost(data, $"AITimer{scaleMode}");
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                AI ai = host.GetComponent<AI>();
                yield return WaitForInitialization(ai);
                ai.Start(false);
                TimerVariable timer = (TimerVariable)ai.BehaviourTree.Variables[definition.UUID];
                timer.SetValue(0.2f);
                Time.timeScale = 0f;
                float atZero = timer.Remaining;
                yield return new WaitForSecondsRealtime(0.08f);

                if (scaleMode == TimeScaleMode.Scaled)
                    Assert.That(timer.Remaining, Is.EqualTo(atZero).Within(0.005f));
                else
                    Assert.That(timer.Remaining, Is.LessThan(atZero - 0.03f));
            }
            finally
            {
                Time.timeScale = oldScale;
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        /// <summary>Verifies Timer Uses Scaled Time And Never Starts From Reading.</summary>
        [UnityTest]
        public IEnumerator TimerUsesScaledTimeAndNeverStartsFromReading()
        {
            TimerVariable timer = new(new BehaviourTreeTimer(default), UUID.NewUUID(), "Timer");
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 1f;
                yield return new WaitForSeconds(0.05f);
                Assert.That(timer.Remaining, Is.Zero);
                timer.SetValue(1f);
                yield return new WaitForSeconds(0.05f);
                Assert.That(timer.Remaining, Is.LessThan(1f).And.GreaterThan(0f));
                Time.timeScale = 0f;
                yield return null;
                float frozen = timer.Remaining;
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(timer.Remaining, Is.EqualTo(frozen));
                Time.timeScale = 1f;
                timer.SetValue(0.03f);
                yield return new WaitForSeconds(0.06f);
                Assert.That(timer.Remaining, Is.Zero);
            }
            finally { Time.timeScale = oldScale; }
        }

        /// <summary>Verifies Timer Survives Pause Restart And End Start But Reload Creates Fresh Instance.</summary>
        [UnityTest]
        public IEnumerator TimerSurvivesPauseRestartAndEndStartButReloadCreatesFreshInstance()
        {
            VariableData definition = new("Timer", VariableType.Float);
            definition.SetDefaultValue(30f);
            definition.Flags |= VariableFlag.Timer;
            Always head = new() { uuid = UUID.NewUUID() };
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.variables.Add(definition);
            data.nodes.Add(head);
            data.headNodeUUID = head.uuid;
            GameObject host = new("TimerLifecycle");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;
            try
            {
                yield return WaitForInitialization(ai);
                TimerVariable timer = (TimerVariable)ai.BehaviourTree.Variables[definition.UUID];
                Assert.That(timer.Remaining, Is.Zero);
                timer.SetValue(10f);
                ai.Start(false);
                ai.Pause();
                yield return new WaitForSeconds(0.05f);
                Assert.That(timer.Remaining, Is.LessThan(10f).And.GreaterThan(0f));
                ai.Resume();
                ai.BehaviourTree.Restart();
                ai.End(false);
                ai.Start(false);
                Assert.That(ai.BehaviourTree.Variables[definition.UUID], Is.SameAs(timer));
                Assert.That(timer.Remaining, Is.GreaterThan(0f));
                ai.Reload(false);
                yield return WaitForInitialization(ai);
                RuntimeVariable fresh = ai.BehaviourTree.Variables[definition.UUID];
                Assert.That(fresh, Is.Not.SameAs(timer));
                Assert.That(fresh.FloatValue, Is.Zero);
                Assert.That(timer.Remaining, Is.GreaterThan(0f));
            }
            finally
            {
                Object.Destroy(host);
                Object.Destroy(data);
            }
        }

        [UnityTest]
        public IEnumerator RootTimerSamplesFromUpdateAndFixedUpdateRemainMonotonic()
        {
            float oldScale = Time.timeScale;
            VariableData[] definitions =
            {
                CreateTimerDefinition("GameScaled", TimeDomain.Game, TimeScaleMode.Scaled),
                CreateTimerDefinition("GameUnscaled", TimeDomain.Game, TimeScaleMode.Unscaled),
                CreateTimerDefinition("AiScaled", TimeDomain.AI, TimeScaleMode.Scaled),
                CreateTimerDefinition("AiUnscaled", TimeDomain.AI, TimeScaleMode.Unscaled),
            };
            ClockSamplingAction head = new()
            {
                uuid = UUID.NewUUID(),
                timerUuids = new[]
                {
                    definitions[0].UUID,
                    definitions[1].UUID,
                    definitions[2].UUID,
                    definitions[3].UUID,
                },
            };
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.headNodeUUID = head.uuid;
            data.variables.AddRange(definitions);
            data.nodes.Add(head);
            GameObject host = new("RootClockFrameSampling");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;

            try
            {
                Time.timeScale = 0.5f;
                yield return WaitForInitialization(ai);
                ai.Start(false);
                foreach (VariableData definition in definitions)
                    ((TimerVariable)ai.BehaviourTree.Variables[definition.UUID]).SetValue(2f);

                yield return null;
                yield return new WaitForFixedUpdate();
                yield return new WaitForSecondsRealtime(0.15f);
                yield return new WaitForFixedUpdate();
                yield return null;

                ClockSamplingAction runtimeHead = (ClockSamplingAction)ai.BehaviourTree.References[head.uuid];
                AssertTimerSamples(runtimeHead, false, "Update");
                AssertTimerSamples(runtimeHead, true, "FixedUpdate");
            }
            finally
            {
                Time.timeScale = oldScale;
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        [UnityTest]
        public IEnumerator RootNaturalEndAndFaultFreezeAiTimer_WhileFinishedSubtreeLeavesParentRunning()
        {
            yield return RunNaturalEndTimerCase();
            yield return RunFaultedTreeTimerCase();
            yield return RunFinishedSubtreeTimerCase();
        }

        /// <summary>Bounds initialization failures so the suite cannot indefinitely occupy the test runner.</summary>
        private static IEnumerator WaitForInitialization(AI ai)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((ai.BehaviourTree == null || !ai.BehaviourTree.IsInitialized) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(ai.BehaviourTree?.IsInitialized, Is.True, "Timer tree initialization did not finish within five real seconds.");
            Assert.That(ai.BehaviourTree.IsFaulted, Is.False);
        }

        [System.Serializable]
        private sealed class YieldAction : Aethiumian.AI.Nodes.Action
        {
        }

        /// <summary>Verifies Countdown Settles On Exit And Does Not Charge Inactive Time.</summary>
        [UnityTest]
        public IEnumerator CountdownSettlesOnExitAndDoesNotChargeInactiveTime()
        {
            VariableData definition = new("Remaining", VariableType.Float);
            definition.SetDefaultValue(2f);
            YieldAction head = new() { uuid = UUID.NewUUID() };
            Countdown countdown = new() { uuid = UUID.NewUUID(), name = "Countdown exit probe" };
            countdown.updatingVariable.SetReference(definition);
            head.AddService(countdown);
            BehaviourTreeData data = CreateTimerTreeData(head, definition, countdown);
            data.timeSettings = new TimeSettings { domain = TimeDomain.Game, scaleMode = TimeScaleMode.Unscaled };
            GameObject host = CreateTimerHost(data, "CountdownExitLifecycle");
            try
            {
                AI ai = host.GetComponent<AI>();
                yield return WaitForInitialization(ai);
                ai.Start(false);
                yield return new WaitForSecondsRealtime(0.05f);
                yield return new WaitForFixedUpdate();
                float beforeEnd = ai.BehaviourTree.Variables[definition.UUID].FloatValue;
                Assert.That(beforeEnd, Is.LessThan(2f));
                ai.End(false);
                yield return new WaitForFixedUpdate();
                float settled = ai.BehaviourTree.Variables[definition.UUID].FloatValue;
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(ai.BehaviourTree.Variables[definition.UUID].FloatValue,
                    Is.EqualTo(settled).Within(0.005f), "Countdown settled more than once after exit.");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        /// <summary>Verifies Timeout and Countdown independently observe all domain and scale combinations.</summary>
        [UnityTest]
        public IEnumerator ServiceConsumersUseTheirConfiguredDomainAndScale()
        {
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0.5f;
                foreach (TimeDomain domain in new[] { TimeDomain.Game, TimeDomain.AI })
                {
                    foreach (TimeScaleMode scaleMode in new[] { TimeScaleMode.Scaled, TimeScaleMode.Unscaled })
                    {
                        yield return RunCountdownCase(domain, scaleMode);
                        yield return RunTimeoutCase(domain, scaleMode);
                        yield return RunPausedTimeoutCase(domain, scaleMode);
                        yield return RunPausedCountdownCase(domain, scaleMode);
                    }
                }
            }
            finally
            {
                Time.timeScale = oldScale;
            }
        }

        /// <summary>Proves that an actual asynchronous FunctionAction continues while AI callbacks are paused.</summary>
        [UnityTest]
        public IEnumerator PauseLeavesAsyncFunctionRunningButDefersTimeoutServiceChecks()
        {
            asyncFunctionStarted = false;
            asyncFunctionPassedFirstAwait = false;
            asyncFunctionCanceled = false;
            FunctionAction action = new() { uuid = UUID.NewUUID(), name = "Async Pause Boundary" };
            action.function.SetMethod(typeof(TimerLifecyclePlayModeTests).GetMethod(
                nameof(ObservePauseBoundaryAsync), BindingFlags.Public | BindingFlags.Static));
            action.parameters.Add(new Parameter(VariableType.Node));

            Timeout timeout = new()
            {
                uuid = UUID.NewUUID(),
                name = "Pause Boundary Timeout",
                time = new VariableField(0.04f),
            };
            action.AddService(timeout);

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = TimeDomain.Game, scaleMode = TimeScaleMode.Unscaled };
            data.headNodeUUID = action.uuid;
            data.nodes.Add(action);
            data.nodes.Add(timeout);
            GameObject host = new("AsyncPauseBoundary");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;

            try
            {
                yield return WaitForInitialization(ai);
                ai.Start(false);
                yield return WaitUntil(() => asyncFunctionStarted, 5f);
                Assert.That(asyncFunctionStarted, Is.True, "The real FunctionAction did not start within the timeout.");

                ai.Pause();
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(asyncFunctionPassedFirstAwait, Is.True,
                    "The real FunctionAction async body did not progress while AI was paused.");
                Assert.That(ai.IsRunning, Is.True,
                    "Timeout service checks must not run while the AI driver is paused.");

                ai.Resume();
                yield return WaitUntil(() => !ai.IsRunning, 5f);
                Assert.That(ai.IsRunning, Is.False, "The expired Timeout did not end after resume.");
                yield return WaitUntil(() => asyncFunctionCanceled, 5f);
                Assert.That(asyncFunctionCanceled, Is.True,
                    "The expired Timeout did not interrupt the still-pending async FunctionAction after resume.");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        private static IEnumerator RunCountdownCase(TimeDomain domain, TimeScaleMode scaleMode)
        {
            VariableData value = new("CountdownValue", VariableType.Float);
            value.SetDefaultValue(1f);
            YieldAction head = new() { uuid = UUID.NewUUID() };
            Countdown countdown = new()
            {
                uuid = UUID.NewUUID(),
                name = $"Countdown {domain} {scaleMode}",
            };
            countdown.updatingVariable.SetReference(value);
            head.AddService(countdown);

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = domain, scaleMode = scaleMode };
            data.headNodeUUID = head.uuid;
            data.variables.Add(value);
            data.nodes.Add(head);
            data.nodes.Add(countdown);
            GameObject host = new("CountdownConsumerCase");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;

            try
            {
                yield return WaitForInitialization(ai);
                ai.Start(false);
                yield return new WaitForFixedUpdate();
                float before = ai.BehaviourTree.Variables[value.UUID].FloatValue;
                double beforeClock = ReadUnityClock(scaleMode);
                yield return new WaitForSecondsRealtime(0.12f);
                yield return new WaitForFixedUpdate();
                float after = ai.BehaviourTree.Variables[value.UUID].FloatValue;
                double afterClock = ReadUnityClock(scaleMode);
                double expectedElapsed = afterClock - beforeClock;
                float observedElapsed = before - after;
                float tolerance = Mathf.Max(Time.fixedUnscaledDeltaTime * 2f, 0.04f);

                Assert.That(expectedElapsed, Is.GreaterThan(0.03d), $"{domain}/{scaleMode} did not observe enough selected time.");
                Assert.That(observedElapsed, Is.EqualTo((float)expectedElapsed).Within(tolerance),
                    $"{domain}/{scaleMode} Countdown used the wrong time option.");
                Assert.That(after, Is.GreaterThan(0f), $"{domain}/{scaleMode} Countdown exhausted unexpectedly.");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        private static IEnumerator RunTimeoutCase(TimeDomain domain, TimeScaleMode scaleMode)
        {
            const float duration = 0.12f;
            YieldAction head = new() { uuid = UUID.NewUUID() };
            Timeout timeout = new()
            {
                uuid = UUID.NewUUID(),
                name = $"Timeout {domain} {scaleMode}",
                time = new VariableField(duration),
            };
            head.AddService(timeout);

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = domain, scaleMode = scaleMode };
            data.headNodeUUID = head.uuid;
            data.nodes.Add(head);
            data.nodes.Add(timeout);
            GameObject host = new("TimeoutConsumerCase");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;

            try
            {
                yield return WaitForInitialization(ai);
                ai.Start(false);
                yield return new WaitForFixedUpdate();
                double registeredAt = ReadUnityClock(scaleMode);
                yield return new WaitForSecondsRealtime(0.04f);
                yield return new WaitForFixedUpdate();
                Assert.That(ai.IsRunning, Is.True, $"{domain}/{scaleMode} Timeout ended before its deadline.");

                yield return new WaitForSecondsRealtime(0.14f);
                yield return WaitUntil(() => !ai.IsRunning, 1f);
                Assert.That(ai.IsRunning, Is.False, $"{domain}/{scaleMode} Timeout did not interrupt the host.");
                double terminatedAt = ReadUnityClock(scaleMode);
                float tolerance = Mathf.Max(Time.fixedUnscaledDeltaTime * 2f, 0.04f);
                Assert.That(terminatedAt - registeredAt, Is.GreaterThanOrEqualTo(duration - tolerance),
                    $"{domain}/{scaleMode} Timeout ended before the configured deadline window.");
                Assert.That(terminatedAt - registeredAt, Is.LessThan(0.4d),
                    $"{domain}/{scaleMode} Timeout termination was not observed in the allotted window (tolerance {tolerance}).");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        private static IEnumerator RunPausedTimeoutCase(TimeDomain domain, TimeScaleMode scaleMode)
        {
            const float duration = 0.20f;
            YieldAction head = new() { uuid = UUID.NewUUID() };
            Timeout timeout = new()
            {
                uuid = UUID.NewUUID(),
                name = $"Paused Timeout {domain} {scaleMode}",
                time = new VariableField(duration),
            };
            head.AddService(timeout);

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = domain, scaleMode = scaleMode };
            data.headNodeUUID = head.uuid;
            data.nodes.Add(head);
            data.nodes.Add(timeout);
            GameObject host = new("PausedTimeoutConsumerCase");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;

            try
            {
                yield return WaitForInitialization(ai);
                ai.Start(false);
                yield return new WaitForSecondsRealtime(0.03f);
                yield return new WaitForFixedUpdate();
                Assert.That(ai.IsRunning, Is.True, $"{domain}/{scaleMode} Timeout was not running before pause.");
                double timerBeforePause = ai.BehaviourTree.Timer.Now;

                ai.Pause();
                yield return new WaitForSecondsRealtime(0.50f);
                Assert.That(ai.IsRunning, Is.True, $"{domain}/{scaleMode} Timeout checks ran during Pause.");
                double timerDuringPause = ai.BehaviourTree.Timer.Now;
                if (domain == TimeDomain.AI)
                {
                    Assert.That(timerDuringPause - timerBeforePause, Is.LessThan(0.02d),
                        $"{domain}/{scaleMode} root timer charged the paused interval.");
                }

                ai.Resume();
                yield return new WaitForFixedUpdate();
                yield return null;
                double timerAfterResume = ai.BehaviourTree.Timer.Now;
                if (domain == TimeDomain.Game)
                {
                    Assert.That(ai.IsRunning, Is.False,
                        $"{domain}/{scaleMode} Timeout did not include the paused Game interval on resume.");
                }
                else
                {
                    Assert.That(timerAfterResume - timerBeforePause, Is.LessThan(duration - 0.03d),
                        $"{domain}/{scaleMode} root timer did not preserve its pre-pause budget.");
                    Assert.That(ai.IsRunning, Is.True,
                        $"{domain}/{scaleMode} Timeout incorrectly charged the paused AI interval.");
                    yield return new WaitForSecondsRealtime(0.03f);
                    Assert.That(ai.IsRunning, Is.True,
                        $"{domain}/{scaleMode} Timeout did not preserve its pre-pause AI budget.");
                    yield return WaitUntil(() => !ai.IsRunning, 1f);
                    Assert.That(ai.IsRunning, Is.False, $"{domain}/{scaleMode} AI Timeout did not end after resumed active time.");
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        private static IEnumerator RunPausedCountdownCase(TimeDomain domain, TimeScaleMode scaleMode)
        {
            VariableData value = new("PausedCountdownValue", VariableType.Float);
            value.SetDefaultValue(1f);
            YieldAction head = new() { uuid = UUID.NewUUID() };
            Countdown countdown = new()
            {
                uuid = UUID.NewUUID(),
                name = $"Paused Countdown {domain} {scaleMode}",
            };
            countdown.updatingVariable.SetReference(value);
            head.AddService(countdown);

            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = domain, scaleMode = scaleMode };
            data.headNodeUUID = head.uuid;
            data.variables.Add(value);
            data.nodes.Add(head);
            data.nodes.Add(countdown);
            GameObject host = new("PausedCountdownConsumerCase");
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;

            try
            {
                yield return WaitForInitialization(ai);
                ai.Start(false);
                yield return new WaitForFixedUpdate();
                yield return new WaitForSecondsRealtime(0.03f);
                yield return new WaitForFixedUpdate();
                float beforePause = ai.BehaviourTree.Variables[value.UUID].FloatValue;
                double beforeClock = ReadUnityClock(scaleMode);

                ai.Pause();
                yield return new WaitForSecondsRealtime(0.50f);
                Assert.That(ai.BehaviourTree.Variables[value.UUID].FloatValue, Is.EqualTo(beforePause).Within(0.01f),
                    $"{domain}/{scaleMode} Countdown changed while paused.");

                ai.Resume();
                yield return new WaitForFixedUpdate();
                yield return null;
                float afterResume = ai.BehaviourTree.Variables[value.UUID].FloatValue;
                double afterClock = ReadUnityClock(scaleMode);
                float observedElapsed = beforePause - afterResume;
                double rawElapsed = afterClock - beforeClock;
                float tolerance = Mathf.Max(Time.fixedUnscaledDeltaTime * 2f, 0.05f);
                Assert.That(rawElapsed, Is.GreaterThan(0.10d), $"{domain}/{scaleMode} pause window was not observable.");
                if (domain == TimeDomain.Game)
                {
                    Assert.That(observedElapsed, Is.EqualTo((float)rawElapsed).Within(tolerance),
                        $"{domain}/{scaleMode} Countdown did not charge the paused Game interval.");
                }
                else
                {
                    Assert.That(observedElapsed, Is.LessThan(tolerance),
                        $"{domain}/{scaleMode} Countdown incorrectly charged the paused AI interval.");
                }
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        private static double ReadUnityClock(TimeScaleMode scaleMode)
        {
            return scaleMode == TimeScaleMode.Unscaled
                ? Time.unscaledTimeAsDouble
                : Time.timeAsDouble;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static VariableData CreateTimerDefinition(string name, TimeDomain domain, TimeScaleMode scaleMode)
        {
            VariableData definition = new(name, VariableType.Float);
            definition.Flags |= VariableFlag.Timer;
            return definition;
        }

        private static IEnumerator RunNaturalEndTimerCase()
        {
            VariableData definition = CreateTimerDefinition("NaturalTimer", TimeDomain.AI, TimeScaleMode.Unscaled);
            Constant head = new() { uuid = UUID.NewUUID(), returnValue = true };
            BehaviourTreeData data = CreateTimerTreeData(head, definition);
            GameObject host = CreateTimerHost(data, "NaturalTimerLifecycle");
            try
            {
                AI ai = host.GetComponent<AI>();
                yield return WaitForInitialization(ai);
                ai.Start(false);
                TimerVariable timer = (TimerVariable)ai.BehaviourTree.Variables[definition.UUID];
                timer.SetValue(1f);
                yield return null;
                float frozen = timer.Remaining;
                Assert.That(ai.BehaviourTree.MainStack.State, Is.EqualTo(BehaviourTree.NodeCallStack.StackState.End));
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(timer.Remaining, Is.EqualTo(frozen).Within(0.005f),
                    "A naturally ended root must freeze its AI Timer.");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        private static IEnumerator RunFaultedTreeTimerCase()
        {
            VariableData definition = CreateTimerDefinition("FaultTimer", TimeDomain.AI, TimeScaleMode.Unscaled);
            ThrowingAction head = new() { uuid = UUID.NewUUID(), name = "Fault timer probe" };
            BehaviourTreeData data = CreateTimerTreeData(head, definition);
            data.nodeErrorHandle = NodeErrorSolution.Fault;
            GameObject host = CreateTimerHost(data, "FaultTimerLifecycle");
            try
            {
                AI ai = host.GetComponent<AI>();
                yield return WaitForInitialization(ai);
                LogAssert.Expect(LogType.Error, new Regex(@"Exception occurred at node \[Fault timer probe\]"));
                LogAssert.Expect(LogType.Exception, new Regex(@"Timer lifecycle fault probe"));
                LogAssert.Expect(LogType.Exception, new Regex(@"return invalid state '\(Error\)'"));
                ai.Start(false);
                TimerVariable timer = (TimerVariable)ai.BehaviourTree.Variables[definition.UUID];
                timer.SetValue(1f);
                yield return null;
                float frozen = timer.Remaining;
                Assert.That(ai.BehaviourTree.IsFaulted, Is.True);
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(timer.Remaining, Is.EqualTo(frozen).Within(0.005f),
                    "A faulted root must freeze its AI Timer.");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
            }
        }

        private static IEnumerator RunFinishedSubtreeTimerCase()
        {
            Constant nestedHead = new() { uuid = UUID.NewUUID(), returnValue = true };
            BehaviourTreeData nestedData = ScriptableObject.CreateInstance<BehaviourTreeData>();
            nestedData.noActionMaximumDurationLimit = true;
            nestedData.headNodeUUID = nestedHead.uuid;
            nestedData.nodes.Add(nestedHead);

            VariableData definition = CreateTimerDefinition("SubtreeTimer", TimeDomain.AI, TimeScaleMode.Unscaled);
            Sequence head = new() { uuid = UUID.NewUUID() };
            Subtree subtree = new()
            {
                uuid = UUID.NewUUID(),
                parent = new NodeReference(head.uuid),
                behaviourTreeData = nestedData,
                variableTable = new VariableTableTranslationBuilder(),
            };
            YieldAction tail = new()
            {
                uuid = UUID.NewUUID(),
                parent = new NodeReference(head.uuid),
            };
            head.events = new[] { new NodeReference(subtree.uuid), new NodeReference(tail.uuid) };
            BehaviourTreeData data = CreateTimerTreeData(head, definition, subtree, tail);
            GameObject host = CreateTimerHost(data, "SubtreeTimerLifecycle");
            try
            {
                AI ai = host.GetComponent<AI>();
                yield return WaitForInitialization(ai);
                ai.Start(false);
                TimerVariable timer = (TimerVariable)ai.BehaviourTree.Variables[definition.UUID];
                timer.SetValue(1f);
                yield return null;
                float before = timer.Remaining;
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(ai.IsRunning, Is.True, "The parent must remain active after its subtree ends.");
                Assert.That(timer.Remaining, Is.LessThan(before),
                    "A finished child subtree must not freeze the parent root clock.");
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                UnityEngine.Object.Destroy(data);
                UnityEngine.Object.Destroy(nestedData);
            }
        }

        private static BehaviourTreeData CreateTimerTreeData(TreeNode head, VariableData definition, params TreeNode[] nodes)
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.timeSettings = new TimeSettings { domain = TimeDomain.AI, scaleMode = TimeScaleMode.Unscaled };
            data.headNodeUUID = head.uuid;
            data.variables.Add(definition);
            data.nodes.Add(head);
            data.nodes.AddRange(nodes);
            return data;
        }

        private static GameObject CreateTimerHost(BehaviourTreeData data, string name)
        {
            GameObject host = new(name);
            AI ai = host.AddComponent<AI>();
            ai.awakeStart = false;
            ai.autoRestart = false;
            ai.Data = data;
            return host;
        }

        private static void AssertTimerSamples(ClockSamplingAction action, bool fixedUpdate, string phase)
        {
            int sampleCount = fixedUpdate ? action.fixedSampleCount : action.updateSampleCount;
            double[] clocks = fixedUpdate ? action.fixedClocks : action.updateClocks;
            float[] remaining = fixedUpdate ? action.fixedRemaining : action.updateRemaining;
            Assert.That(sampleCount, Is.GreaterThan(2), $"{phase} did not execute enough frame callbacks.");
            double previousClock = clocks[0];
            float previousRemaining = remaining[0];
            for (int sample = 1; sample < sampleCount; sample++)
            {
                Assert.That(clocks[sample], Is.GreaterThanOrEqualTo(previousClock - 0.0001d), $"{phase} timer went backwards.");
                Assert.That(remaining[sample], Is.LessThanOrEqualTo(previousRemaining + 0.002f), $"{phase} Timer remaining increased.");
                previousClock = clocks[sample];
                previousRemaining = remaining[sample];
            }
        }

        private static bool asyncFunctionStarted;
        private static bool asyncFunctionPassedFirstAwait;
        private static bool asyncFunctionCanceled;

        [System.Serializable]
        private sealed class ClockSamplingAction : Aethiumian.AI.Nodes.Action
        {
            public Aethiumian.AI.UUID[] timerUuids;
            public double[] updateClocks = new double[128];
            public float[] updateRemaining = new float[128];
            public int updateSampleCount;
            public double[] fixedClocks = new double[128];
            public float[] fixedRemaining = new float[128];
            public int fixedSampleCount;

            public override void Initialize()
            {
                updateSampleCount = 0;
                fixedSampleCount = 0;
            }

            public override void Update() => Capture(updateClocks, updateRemaining, ref updateSampleCount);
            public override void FixedUpdate() => Capture(fixedClocks, fixedRemaining, ref fixedSampleCount);

            private void Capture(double[] clocks, float[] remaining, ref int sampleCount)
            {
                if (sampleCount >= clocks.Length)
                    return;

                TimerVariable timer = (TimerVariable)behaviourTree.Variables[timerUuids[0]];
                clocks[sampleCount] = behaviourTree.Timer.Now;
                remaining[sampleCount] = timer.Remaining;
                sampleCount++;
            }
        }

        [System.Serializable]
        private sealed class ThrowingAction : Aethiumian.AI.Nodes.Action
        {
            public ThrowingAction() => name = "Fault timer probe";

            public override void Start() => throw new System.InvalidOperationException("Timer lifecycle fault probe.");
        }

        public static async Task ObservePauseBoundaryAsync(NodeProgress progress)
        {
            asyncFunctionStarted = true;
            try
            {
                await progress.NextFrameAsync();
                asyncFunctionPassedFirstAwait = true;
                for (int index = 0; index < 30; index++)
                    await progress.NextFrameAsync();
            }
            catch (System.OperationCanceledException)
            {
                asyncFunctionCanceled = true;
            }
        }
    }
}
