using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("Interrupt the host node by condition and return the configured result")]
    public sealed class Interrupt : RepeatService
    {
        public enum ReturnResult
        {
            Failed,
            Success
        }

        public NodeReference condition;
        public ReturnResult result = ReturnResult.Failed;

        private bool triggered;

        private bool IsBooleanFastPath => condition?.Node is Boolean;

        // Boolean conditions are polled in UpdateTimer and never allocate a service stack.
        public override bool IsReady => !triggered && !IsBooleanFastPath && IsTimerReady;
        public bool IsTimerReady => base.IsReady;


        public override void ReceiveReturn(bool @return)
        {
            if (!@return) return;

            TriggerInterrupt(endServiceStack: true);
        }

        public override State Execute()
        {
            ResetTimer();
            if (condition is not null && condition.HasReference)
            {
                return SetNextExecute(condition);
            }

            return HandleException(InvalidNodeException.ReferenceIsRequired(nameof(condition), this));
        }

        public override void Initialize()
        {
            behaviourTree.GetNode(ref condition);
            ResetState();
        }

        public override void OnRegistered()
        {
            ResetState();
        }

        public override void OnUnregistered()
        {
            ResetState();
        }

        public override void UpdateTimer()
        {
            if (triggered)
            {
                return;
            }

            base.UpdateTimer();

            if (IsBooleanFastPath && IsTimerReady)
            {
                // Boolean is a pure value adapter, so it can be evaluated without scheduling a service stack.
                ResetTimer();
                if (EvaluateBooleanCondition((Boolean)condition.Node))
                {
                    TriggerInterrupt(endServiceStack: false);
                }
            }

        }

        private bool EvaluateBooleanCondition(Boolean booleanCondition)
        {
            try
            {
                return booleanCondition.ReadValue();
            }
            catch (Exception e)
            {
                return booleanCondition.HandleException(e) == State.Success;
            }
        }

        private void TriggerInterrupt(bool endServiceStack)
        {
            var host = behaviourTree.GetNode(parent);
            var targetStack = host?.callStack;
            if (targetStack == null)
            {
                return;
            }

            if (endServiceStack)
            {
                End();
            }

            triggered = targetStack.Interrupt(host, result == ReturnResult.Success);
        }

        private void ResetState()
        {
            triggered = false;
            ResetTimer();
        }
    }
}
