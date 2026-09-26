using Aethiumian.AI.Attributes;
using Aethiumian.AI.Variables;
using System;
using UnityEngine;
using UnityAnimator = UnityEngine.Animator;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Waits until an animation finishes playing.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class WaitForAnimationEnd : Action
    {
        public enum AnimationState
        {
            current,
            stageName,
        }

        public AnimationState animation;
        [DisplayIf(nameof(animation), AnimationState.stageName)]
        [Readable] public VariableField<string> stageName;

        [NonSerialized] private UnityAnimator animator;
        [NonSerialized] private AnimatorUpdateMode updateMode;
        [NonSerialized] private int targetStateHash;
        [NonSerialized] private bool targetObserved;

        public override void Awake()
        {
            animator = behaviourTree.gameObject.GetComponent<UnityAnimator>();
            if (!animator || !animator.runtimeAnimatorController || animator.layerCount == 0)
            {
                Fail();
                return;
            }

            updateMode = animator.updateMode;
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            switch (animation)
            {
                case AnimationState.current:
                    targetStateHash = currentState.fullPathHash;
                    if (targetStateHash == 0 || !animator.HasState(0, targetStateHash))
                    {
                        Fail();
                        return;
                    }

                    targetObserved = true;
                    break;

                case AnimationState.stageName:
                    string path = stageName;
                    string layerPrefix = animator.GetLayerName(0) + ".";
                    if (string.IsNullOrWhiteSpace(path)
                        || !path.StartsWith(layerPrefix, StringComparison.Ordinal)
                        || path.Length == layerPrefix.Length)
                    {
                        Fail();
                        return;
                    }

                    targetStateHash = UnityAnimator.StringToHash(path);
                    if (!animator.HasState(0, targetStateHash))
                    {
                        Fail();
                        return;
                    }

                    targetObserved = false;
                    break;

                default:
                    Fail();
                    return;
            }
        }

        public override void Update()
        {
            if (!IsFixedUpdateMode())
                ObserveState();
        }

        public override void FixedUpdate()
        {
            if (IsFixedUpdateMode())
                ObserveState();
        }

        private bool IsFixedUpdateMode()
        {
#if UNITY_6000_0_OR_NEWER
            return updateMode == AnimatorUpdateMode.Fixed;
#else
            return updateMode == AnimatorUpdateMode.AnimatePhysics;
#endif
        }

        private void ObserveState()
        {
            if (!animator || IsComplete)
                return;

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            if (!targetObserved)
            {
                if (currentState.fullPathHash != targetStateHash)
                    return;

                targetObserved = true;
            }

            if (animator.IsInTransition(0) || currentState.fullPathHash != targetStateHash)
            {
                Success();
                return;
            }

            if (!currentState.loop && currentState.normalizedTime >= 1f)
                Success();
        }
    }
}
