using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Executes one child a fixed number of times and succeeds when every repetition succeeds.
    /// </summary>
    [Serializable]
    [NodeTip("Repeat one child a fixed number of times")]
    public sealed class Repeat : Decorator
    {
        /// <summary>
        /// Number of times to execute the decorated child for each invocation.
        /// </summary>
        [Readable]
        public VariableField<int> repeatCount = 1;

        [NonSerialized]
        private int targetCount;

        [NonSerialized]
        private int completedCount;

        /// <summary>
        /// Initializes the authored repeat count and clears runtime progress.
        /// </summary>
        public Repeat()
        {
            repeatCount = 1;
            ResetProgress();
        }

        /// <summary>
        /// Reads and clamps the repeat count once, then starts the first child execution.
        /// </summary>
        protected override State ExecuteWithChild()
        {
            targetCount = Math.Max(0, repeatCount);
            completedCount = 0;

            if (targetCount == 0)
            {
                return State.Success;
            }

            return SetNextExecute(node);
        }

        /// <summary>
        /// Stops on the first failed child; otherwise schedules the next repetition or succeeds.
        /// </summary>
        public override State ReceiveReturnFromChild(bool @return)
        {
            if (!@return)
            {
                return State.Failed;
            }

            completedCount++;
            return completedCount >= targetCount
                ? State.Success
                : SetNextExecute(node);
        }

        /// <summary>
        /// Clears repeat progress before the runtime instance is used again.
        /// </summary>
        public override void Initialize()
        {
            ResetProgress();
        }

        /// <summary>
        /// Resets the per-run counters without changing the authored count field.
        /// </summary>
        private void ResetProgress()
        {
            targetCount = 0;
            completedCount = 0;
        }
    }
}
