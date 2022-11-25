using System;
using System.Collections;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
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

        public override void ExecuteOnce()
        {
            Call();
        }

        IEnumerator Execution()
        {
            yield return Script.StartCoroutine(methodName);
            yield return null;
            if (afterExecuteAction == AfterExecuteAction.waitUntilEnd) End(true);
        }


        private void Call()
        {
            try
            {
                coroutine = Script.StartCoroutine(Execution());
            }
            catch (Exception)
            {
                throw new ArithmeticException("Method " + methodName + $" in script {behaviourTree.Script.GetType().Name} cannot be invoke!");
            }

            if (afterExecuteAction == AfterExecuteAction.@continue) End(true);
        }
    }
}