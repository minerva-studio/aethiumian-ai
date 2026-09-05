using System;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Consumes an explicit timer when execution is admitted, regardless of the child result.</summary>
    [Serializable]
    [NodeTip("Starts the timer before running the child; failures and interruption retain it.")]
    public sealed class Throttle : Decorator
    {
        [Readable] public VariableField<float> duration = 1f;
        [Readable, Writable] public VariableReference<float> timer = new();

        /// <summary>Checks the source binding without resetting the timer lifetime.</summary>
        public override void Initialize() => RequireTimer();

        /// <summary>Consumes the timer before handing execution to the child.</summary>
        protected override State ExecuteWithChild() => TryStart() ? SetNextExecute(node) : State.Failed;

        /// <summary>Consumes a ready timer and succeeds when no child is authored.</summary>
        protected override State ExecuteWithoutChild() => TryStart() ? State.Success : State.Failed;

        /// <summary>Propagates the child result without modifying the admitted timer.</summary>
        public override State ReceiveReturnFromChild(bool result) => result ? State.Success : State.Failed;

        /// <summary>Reads the duration only when a new execution is admitted.</summary>
        private bool TryStart()
        {
            TimerVariable source = RequireTimer();
            if (source.Remaining > 0f) return false;
            source.SetValue(duration.FloatValue);
            return true;
        }

        /// <summary>Rejects missing or ordinary Float bindings at the owning node boundary.</summary>
        private TimerVariable RequireTimer() => timer?.RuntimeVariable as TimerVariable
            ?? throw new InvalidOperationException($"Throttle '{name}' requires a TimerVariable binding.");
    }
}
