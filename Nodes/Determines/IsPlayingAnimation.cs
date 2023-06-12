using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class IsPlayingAnimation : Determine
    {
        public VariableField<string> stageName;
        UnityEngine.Animator animator;
        UnityEngine.Animator Animator => animator ? animator : animator = gameObject.GetComponent<UnityEngine.Animator>();

        public override bool GetValue()
        {
            return Animator.GetCurrentAnimatorStateInfo(0).IsName(stageName);
        }
    }
}