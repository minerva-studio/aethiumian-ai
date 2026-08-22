using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Retries one child after failures and succeeds when an attempt succeeds.
    /// </summary>
    [Serializable]
    [NodeTip("Retry one child up to a fixed number of attempts")]
    public sealed class Retry : Decorator
    {
        /// <summary>
        /// Maximum number of total child attempts for each invocation.
        /// </summary>
        [Readable] public VariableField<int> maxAttempts = 3;

        [NonSerialized]
        private int targetAttempts;

        [NonSerialized]
        private int completedAttempts;

        /// <summary>
        /// Initializes the authored attempt limit and clears runtime progress.
        /// </summary>
        public Retry()
        {
            maxAttempts = 3;
            ResetProgress();
        }

        /// <summary>
        /// Reads and clamps the attempt limit once, then starts the first child attempt.
        /// </summary>
        protected override State ExecuteWithChild()
        {
            targetAttempts = Math.Max(0, maxAttempts);
            completedAttempts = 0;

            if (targetAttempts == 0)
            {
                return State.Failed;
            }

            return SetNextExecute(node);
        }

        /// <summary>
        /// Succeeds on the first successful attempt or schedules another attempt after failure.
        /// </summary>
        public override State ReceiveReturnFromChild(bool @return)
        {
            completedAttempts++;
            if (@return)
            {
                return State.Success;
            }

            return completedAttempts >= targetAttempts
                ? State.Failed
                : SetNextExecute(node);
        }

        /// <summary>
        /// Clears retry progress before the runtime instance is used again.
        /// </summary>
        public override void Initialize()
        {
            ResetProgress();
        }

        /// <summary>
        /// Resets per-run attempt counters without changing the authored limit.
        /// </summary>
        private void ResetProgress()
        {
            targetAttempts = 0;
            completedAttempts = 0;
        }
    }
}
