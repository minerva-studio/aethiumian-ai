using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public sealed class IsPlayingAnimation : Determine
    {
        public VariableField<string> stageName;
        Animator animator;
        Animator Animator => animator ? animator : animator = gameObject.GetComponent<Animator>();

        public override bool GetValue()
        {
            return Animator.GetCurrentAnimatorStateInfo(0).IsName(stageName);
        }
    }
}