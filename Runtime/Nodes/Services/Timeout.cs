using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    [NodeTip("Interrupt the host node after a timeout and return the configured result")]
    public sealed class Timeout : Service
    {
        public enum ReturnResult
        {
            Failed,
            Success
        }

        [Numeric]
        [Readable]
        public VariableField time;

        public ReturnResult result = ReturnResult.Failed;

        private float elapsedTime;
        private bool triggered;

        public override bool IsReady => false;

        public override State Execute()
        {
            return State.Success;
        }

        public override void Initialize()
        {
            ResetTimer();
        }

        public override void OnRegistered()
        {
            ResetTimer();
        }

        public override void OnUnregistered()
        {
            ResetTimer();
        }

        public override void UpdateTimer()
        {
            if (triggered)
            {
                return;
            }

            float duration = time?.NumericValue ?? 0;
            if (duration > 0)
            {
                elapsedTime += Time.fixedDeltaTime;
                if (elapsedTime < duration)
                {
                    return;
                }
            }

            TriggerTimeout();
        }

        private void TriggerTimeout()
        {
            var host = behaviourTree.GetNode(parent);
            var targetStack = host?.callStack;
            if (targetStack == null)
            {
                return;
            }

            triggered = targetStack.Interrupt(host, result == ReturnResult.Success);
        }

        private void ResetTimer()
        {
            elapsedTime = 0;
            triggered = false;
        }
    }
}
