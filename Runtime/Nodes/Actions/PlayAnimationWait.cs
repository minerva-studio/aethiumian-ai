using Amlos.AI.Variables;
using System;
using UnityEngine;
using Ator = UnityEngine.Animator;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// directly control the animation played
    /// </summary>
    [NodeTip("Change parameters of an animator")]
    [Serializable]
    [RequireComponent(typeof(Ator))]
    public sealed class PlayAnimationWait : Action
    {
        [Readable]
        public VariableField<string> stateName;
        [Readable]
        public VariableField<int> layer = 0;


        Ator animator;
        Ator AnimatorComponent => animator ? animator : animator = gameObject.GetComponent<Ator>();

        public override void Awake()
        {
            if (!AnimatorComponent)
            {
                End(false);
                return;
            }
            animator.Play(stateName, layer);
        }

        public override void FixedUpdate()
        {
#if UNITY_6000_0_OR_NEWER
            if (animator.updateMode != AnimatorUpdateMode.Fixed)
#else
            if (animator.updateMode != AnimatorUpdateMode.AnimatePhysics)
#endif

            {
                return;
            }
            if (!animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
            {
                End(true);
            }
        }

        public override void Update()
        {
            if (animator.updateMode != AnimatorUpdateMode.Normal)
            {
                return;
            }
            if (!animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName))
            {
                End(true);
            }
        }
    }
}