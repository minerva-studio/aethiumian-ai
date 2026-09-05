using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("Interrupt the host node after a timeout and return the configured result")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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

        private double registeredAt;
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
            RegisterClockSample();
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

            float duration = time?.NumericValue ?? 0f;
            if (float.IsNaN(duration) || float.IsInfinity(duration))
            {
                throw new InvalidOperationException($"Timeout '{name}' requires a finite duration.");
            }

            if (duration > 0)
            {
                if (behaviourTree.Timer.Now - registeredAt < duration)
                {
                    return;
                }
            }

            TriggerTimeout();
        }

        private void TriggerTimeout()
        {
            // Guard before interrupt callbacks run; those callbacks may synchronously
            // unregister and register this service again.
            triggered = true;
            var host = behaviourTree.GetNode(parent);
            var targetStack = host?.callStack;
            if (targetStack == null)
            {
                triggered = false;
                return;
            }

            targetStack.Interrupt(host, result == ReturnResult.Success);
        }

        private void ResetTimer()
        {
            registeredAt = 0d;
            triggered = false;
        }

        private void RegisterClockSample()
        {
            registeredAt = behaviourTree.Timer.Now;
            triggered = false;
        }
    }
}
