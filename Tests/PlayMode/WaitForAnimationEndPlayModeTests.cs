#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Animator = UnityEngine.Animator;

namespace Aethiumian.AI.PlayMode.Tests
{
    public sealed class WaitForAnimationEndPlayModeTests
    {
        [UnityTest]
        public IEnumerator CurrentModeCompletesAfterNonLoopingStateFinishes()
        {
            AnimatorFixture fixture = new();
            TestRun run = null;
            try
            {
                run = fixture.CreateRun(AnimatorFixture.OneShotPath, WaitForAnimationEnd.AnimationState.current);
                yield return Start(run);
                yield return WaitForResult(run, true);
            }
            finally
            {
                run?.Dispose();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator StageNameWaitsForTargetThenCompletesAtNaturalEnd()
        {
            AnimatorFixture fixture = new();
            TestRun run = null;
            try
            {
                run = fixture.CreateRun(AnimatorFixture.IdlePath, WaitForAnimationEnd.AnimationState.stageName, AnimatorFixture.OneShotPath);
                yield return Start(run);
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.That(run.AI.IsRunning, Is.True, "The action must wait while the configured target has not started.");
                Assert.That(run.Animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(Animator.StringToHash(AnimatorFixture.IdlePath)));

                run.Animator.SetTrigger(AnimatorFixture.PlayTargetTrigger);
                yield return WaitUntil(() =>
                {
                    AnimatorStateInfo state = run.Animator.GetCurrentAnimatorStateInfo(0);
                    return state.fullPathHash == Animator.StringToHash(AnimatorFixture.OneShotPath)
                        && !run.Animator.IsInTransition(0);
                }, 2f, "The configured target state did not become active.");
                Assert.That(run.AI.IsRunning, Is.True,
                    "Entering the configured target is not an interruption of that target.");
                yield return WaitForResult(run, true);
            }
            finally
            {
                run?.Dispose();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator StageNameAcceptsNestedFullStatePaths()
        {
            AnimatorFixture fixture = new();
            TestRun run = null;
            try
            {
                run = fixture.CreateRun(AnimatorFixture.IdlePath,
                    WaitForAnimationEnd.AnimationState.stageName, AnimatorFixture.NestedAttackPath);
                yield return Start(run);
                run.Animator.Play(Animator.StringToHash(AnimatorFixture.NestedAttackPath), 0, 0f);
                run.Animator.Update(0f);
                yield return WaitForResult(run, true);
            }
            finally
            {
                run?.Dispose();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator LoopingStateWaitsThroughWrapAndCompletesWhenCrossFadedAway()
        {
            AnimatorFixture fixture = new();
            TestRun run = null;
            try
            {
                run = fixture.CreateRun(AnimatorFixture.LoopPath, WaitForAnimationEnd.AnimationState.current);
                yield return Start(run);
                yield return WaitUntil(() =>
                {
                    AnimatorStateInfo state = run.Animator.GetCurrentAnimatorStateInfo(0);
                    return state.fullPathHash == Animator.StringToHash(AnimatorFixture.LoopPath) && state.normalizedTime >= 1.1f;
                }, 3f, "The looping state did not complete one loop.");

                Assert.That(run.AI.IsRunning, Is.True, "A looping state must not complete from normalizedTime alone.");
                run.Animator.CrossFade(Animator.StringToHash(AnimatorFixture.OtherPath), 0.15f, 0);
                yield return WaitForResult(run, true);
            }
            finally
            {
                run?.Dispose();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SelfTransitionCompletesTheAction()
        {
            AnimatorFixture fixture = new();
            TestRun run = null;
            try
            {
                run = fixture.CreateRun(AnimatorFixture.LoopPath, WaitForAnimationEnd.AnimationState.current);
                yield return Start(run);
                Assert.That(run.AI.IsRunning, Is.True);
                run.Animator.SetTrigger(AnimatorFixture.ReplayTrigger);
                yield return WaitForResult(run, true);
            }
            finally
            {
                run?.Dispose();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator SamplesUsingTheAnimatorUpdateMode()
        {
            AnimatorFixture fixture = new();
            try
            {
                foreach (AnimatorUpdateMode updateMode in new[]
                         { AnimatorUpdateMode.Normal, AnimatorUpdateMode.Fixed, AnimatorUpdateMode.UnscaledTime })
                {
                    TestRun run = fixture.CreateRun(AnimatorFixture.OneShotPath, WaitForAnimationEnd.AnimationState.current);
                    run.Animator.updateMode = updateMode;
                    try
                    {
                        yield return Start(run);
                        yield return WaitForResult(run, true);
                    }
                    finally
                    {
                        run.Dispose();
                    }

                    yield return null;
                }
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator InvalidAnimatorAndStageInputsFailImmediately()
        {
            AnimatorFixture fixture = new();
            try
            {
                TestRun missingAnimator = fixture.CreateRun(null, WaitForAnimationEnd.AnimationState.current, addAnimator: false);
                yield return AssertImmediateFailure(missingAnimator);

                TestRun missingController = fixture.CreateRun(null, WaitForAnimationEnd.AnimationState.current);
                yield return AssertImmediateFailure(missingController);

                TestRun noCurrentState = fixture.CreateRun(null, WaitForAnimationEnd.AnimationState.current, useEmptyController: true);
                yield return AssertImmediateFailure(noCurrentState);

                TestRun emptyStagePath = fixture.CreateRun(AnimatorFixture.IdlePath,
                    WaitForAnimationEnd.AnimationState.stageName, string.Empty);
                yield return AssertImmediateFailure(emptyStagePath);

                TestRun invalidStagePath = fixture.CreateRun(AnimatorFixture.IdlePath,
                    WaitForAnimationEnd.AnimationState.stageName, "Idle");
                yield return AssertImmediateFailure(invalidStagePath);

                TestRun missingFullStagePath = fixture.CreateRun(AnimatorFixture.IdlePath,
                    WaitForAnimationEnd.AnimationState.stageName, "Base Layer.DoesNotExist");
                yield return AssertImmediateFailure(missingFullStagePath);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        private static IEnumerator Start(TestRun run)
        {
            yield return WaitUntil(() => run.AI.BehaviourTree != null && run.AI.BehaviourTree.IsInitialized,
                5f, "The behaviour tree did not initialize.");
            Assert.That(run.AI.BehaviourTree.IsFaulted, Is.False);
            if (run.Animator && !string.IsNullOrWhiteSpace(run.InitialStatePath))
            {
                run.Animator.Play(Animator.StringToHash(run.InitialStatePath), 0, 0f);
                run.Animator.Update(0f);
            }
            run.AI.Start(false);
            yield return null;
        }

        private static IEnumerator AssertImmediateFailure(TestRun run)
        {
            try
            {
                yield return Start(run);
                yield return WaitForResult(run, false);
            }
            finally
            {
                run.Dispose();
            }
        }

        private static IEnumerator WaitForResult(TestRun run, bool expectedResult)
        {
            yield return WaitUntil(() => !run.AI.IsRunning, 3f, "The animation action did not finish before its real-time deadline.");
            Assert.That(run.AI.BehaviourTree.MainStack.ReturnValue, Is.EqualTo(expectedResult));
            Assert.That(run.AI.BehaviourTree.IsFaulted, Is.False);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!predicate() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private sealed class TestRun : IDisposable
        {
            public GameObject Host { get; }
            public AI AI { get; }
            public Animator Animator { get; }
            public BehaviourTreeData Data { get; }
            public string InitialStatePath { get; }

            public TestRun(GameObject host, AI ai, Animator animator, BehaviourTreeData data, string initialStatePath)
            {
                Host = host;
                AI = ai;
                Animator = animator;
                Data = data;
                InitialStatePath = initialStatePath;
            }

            public void Dispose()
            {
                if (Host != null) UnityEngine.Object.DestroyImmediate(Host);
                if (Data != null) UnityEngine.Object.DestroyImmediate(Data);
            }
        }

        private sealed class AnimatorFixture : IDisposable
        {
            public const string IdlePath = "Base Layer.Idle";
            public const string OneShotPath = "Base Layer.OneShot";
            public const string NestedAttackPath = "Base Layer.Combat.NestedAttack";
            public const string LoopPath = "Base Layer.Looping";
            public const string OtherPath = "Base Layer.Other";
            public const string PlayTargetTrigger = "PlayTarget";
            public const string ReplayTrigger = "Replay";

            private readonly string folder;
            private readonly AnimatorController controller;
            private readonly AnimatorController emptyController;

            public AnimatorFixture()
            {
                folder = $"Assets/__WaitForAnimationEndTests_{Guid.NewGuid():N}";
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));

                AnimationClip idleClip = CreateClip("Idle", true);
                AnimationClip oneShotClip = CreateClip("OneShot", false);
                AnimationClip loopClip = CreateClip("Looping", true);
                AnimationClip otherClip = CreateClip("Other", true);

                controller = AnimatorController.CreateAnimatorControllerAtPath($"{folder}/WaitForAnimationEnd.controller");
                controller.AddParameter(PlayTargetTrigger, AnimatorControllerParameterType.Trigger);
                controller.AddParameter(ReplayTrigger, AnimatorControllerParameterType.Trigger);

                AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
                AnimatorState idle = AddState(stateMachine, "Idle", idleClip);
                AnimatorState oneShot = AddState(stateMachine, "OneShot", oneShotClip);
                AnimatorState looping = AddState(stateMachine, "Looping", loopClip);
                AddState(stateMachine, "Other", otherClip);
                AnimatorStateMachine combat = stateMachine.AddStateMachine("Combat");
                AddState(combat, "NestedAttack", oneShotClip);
                stateMachine.defaultState = idle;

                AnimatorStateTransition startTarget = idle.AddTransition(oneShot);
                startTarget.hasExitTime = false;
                startTarget.duration = 0f;
                startTarget.AddCondition(AnimatorConditionMode.If, 0f, PlayTargetTrigger);

                AnimatorStateTransition selfTransition = looping.AddTransition(looping);
                selfTransition.hasExitTime = false;
                selfTransition.duration = 0.15f;
                selfTransition.canTransitionToSelf = true;
                selfTransition.AddCondition(AnimatorConditionMode.If, 0f, ReplayTrigger);

                emptyController = AnimatorController.CreateAnimatorControllerAtPath($"{folder}/Empty.controller");
                AssetDatabase.SaveAssets();
            }

            public TestRun CreateRun(string defaultPath, WaitForAnimationEnd.AnimationState mode,
                string stagePath = null, bool addAnimator = true, bool useEmptyController = false)
            {
                WaitForAnimationEnd action = new()
                {
                    uuid = UUID.NewUUID(),
                    animation = mode,
                };
                if (mode == WaitForAnimationEnd.AnimationState.stageName)
                    action.stageName = stagePath;

                BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
                data.noActionMaximumDurationLimit = true;
                data.headNodeUUID = action.uuid;
                data.nodes.Add(action);

                GameObject host = new($"WaitForAnimationEnd_{mode}");
                Animator animator = null;
                if (addAnimator)
                {
                    animator = host.AddComponent<Animator>();
                    animator.runtimeAnimatorController = useEmptyController
                        ? emptyController
                        : defaultPath == null ? null : controller;
                }

                AI ai = host.AddComponent<AI>();
                ai.awakeStart = false;
                ai.autoRestart = false;
                ai.Data = data;
                return new TestRun(host, ai, animator, data, defaultPath);
            }

            public void Dispose()
            {
                if (AssetDatabase.IsValidFolder(folder) && !AssetDatabase.DeleteAsset(folder))
                    throw new InvalidOperationException($"Failed to delete Animator test fixture folder '{folder}'.");
                AssetDatabase.Refresh();
            }

            private AnimationClip CreateClip(string name, bool loop)
            {
                AnimationClip clip = new() { name = name, frameRate = 30f };
                clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x",
                    AnimationCurve.Linear(0f, 0f, 0.6f, 1f));
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = loop;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AssetDatabase.CreateAsset(clip, $"{folder}/{name}.anim");
                return clip;
            }

            private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, AnimationClip clip)
            {
                AnimatorState state = stateMachine.AddState(name);
                state.motion = clip;
                return state;
            }
        }
    }
}
#endif
