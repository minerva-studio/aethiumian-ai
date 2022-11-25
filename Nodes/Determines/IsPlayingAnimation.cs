using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public sealed class IsPlayingAnimation : Determine
    {
        public VariableField<string> stageName;
        Animator Animator => Script.GetComponent<Animator>();

        public override bool GetValue()
        {
            return Animator.GetCurrentAnimatorStateInfo(0).IsName(stageName);
        }
    }
}