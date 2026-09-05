using System;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Consumes an explicit timer only when the decorated action succeeds.</summary>
    [Serializable]
    [NodeTip("Runs when the timer is ready, then starts the timer on success.")]
    public sealed class Cooldown : Decorator
    {
        [Readable] public VariableField<float> duration = 1f;
        [Readable, Writable] public VariableReference<float> timer = new();

        /// <summary>Checks the source binding without resetting the timer lifetime.</summary>
        public override void Initialize() => RequireTimer();

        /// <summary>Starts the child only while the shared timer is ready.</summary>
        protected override State ExecuteWithChild() => RequireTimer().Remaining > 0f ? State.Failed : SetNextExecute(node);

        /// <summary>Consumes a ready timer immediately when no child is authored.</summary>
        protected override State ExecuteWithoutChild()
        {
            if (RequireTimer().Remaining > 0f) return State.Failed;
            return ReceiveReturnFromChild(true);
        }

        /// <summary>Reads the duration at successful completion; failures consume nothing.</summary>
        public override State ReceiveReturnFromChild(bool result)
        {
            if (!result) return State.Failed;
            RequireTimer().SetValue(duration.FloatValue);
            return State.Success;
        }

        /// <summary>Rejects missing or ordinary Float bindings at the owning node boundary.</summary>
        private TimerVariable RequireTimer() => timer?.RuntimeVariable as TimerVariable
            ?? throw new InvalidOperationException($"Cooldown '{name}' requires a TimerVariable binding.");
    }
}
