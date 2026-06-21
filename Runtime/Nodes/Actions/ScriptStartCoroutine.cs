using System;
using System.Collections;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class ScriptStartCoroutine : Action
    {
        public enum AfterExecuteAction
        {
            waitUntilEnd,
            @continue,
        }

        public string methodName;
        public AfterExecuteAction afterExecuteAction;
        Coroutine coroutine;

        public override void Start()
        {
            Call();
        }

        IEnumerator Execution()
        {
            yield return AIComponent.StartCoroutine(methodName);
            yield return null;
            if (afterExecuteAction == AfterExecuteAction.waitUntilEnd) End(true);
        }


        private void Call()
        {
            coroutine = AIComponent.StartCoroutine(Execution());
            if (afterExecuteAction == AfterExecuteAction.@continue) End(true);
        }
    }
}