using Aethiumian.AI.Variables;
using System;
using UnityEngine;
using Ator = UnityEngine.Animator;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// directly control the animation played
    /// </summary>
    [NodeTip("Play animation of given state name")]
    [Serializable]
    [RequireComponent(typeof(Ator))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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