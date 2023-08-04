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
    public sealed class PlayAnimation : Call
    {
        public VariableField<string> stateName;
        public VariableField<int> layer = 0;


        Ator animator;
        Ator AnimatorComponent => animator ? animator : animator = gameObject.GetComponent<Ator>();

        public override State Execute()
        {
            if (!AnimatorComponent)
            {
                return State.Failed;
            }
            animator.Play(stateName, layer);
            return State.Success;
        }
    }
}