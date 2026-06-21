using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("Interrupt the host node by condition and return the configured result")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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

        private Boolean BooleanCondition => behaviourTree?.GetNode(condition) as Boolean;
        private bool IsBooleanFastPath => BooleanCondition != null;

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
            if (behaviourTree.GetNode(condition) != null)
            {
                return SetNextExecute(condition);
            }

            return HandleException(InvalidNodeException.ReferenceIsRequired(nameof(condition), this));
        }

        public override void Initialize()
        {
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

            Boolean booleanCondition = BooleanCondition;
            if (booleanCondition != null && IsTimerReady)
            {
                // Boolean is a pure value adapter, so it can be evaluated without scheduling a service stack.
                ResetTimer();
                if (EvaluateBooleanCondition(booleanCondition))
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
