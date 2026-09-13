using System;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Action lifetime only; completion does not imply the node's overall goal is complete.</summary>
    public enum ExecutionStatus { Running, Completed, Failed }

    /// <summary>Explains ordinary failure without prescribing the owning node's recovery policy.</summary>
    public enum ExecutionFailureReason
    {
        /// <summary>No failure, valid only for running or completed actions.</summary>
        None,
        /// <summary>Support or collision constraints prevent the current step from proceeding.</summary>
        Obstructed,
        /// <summary>A monitored phase exceeded its allowed time without effective progress.</summary>
        Stalled,
        /// <summary>Observed support does not permit completing the intended traversal.</summary>
        UnexpectedSupport,
        /// <summary>The remaining physical execution conditions could not be satisfied.</summary>
        InvalidExecution
    }

    /// <summary>Immutable Tick feedback without progress history or contact coordinates. Default is Running.</summary>
    public readonly struct ExecutionResult
    {
        /// <summary>Whether this action continues, completes, or ends unsuccessfully.</summary>
        public ExecutionStatus Status { get; }
        /// <summary>The explanation for Failed; otherwise always None.</summary>
        public ExecutionFailureReason FailureReason { get; }
        /// <summary>A prepared action which has not ended.</summary>
        public static ExecutionResult Running => default;
        /// <summary>The current action completed its physical contract.</summary>
        public static ExecutionResult Completed => new(ExecutionStatus.Completed, ExecutionFailureReason.None);

        private ExecutionResult(ExecutionStatus status, ExecutionFailureReason reason)
        {
            Status = status;
            FailureReason = reason;
        }

        /// <summary>Creates a normal failure; None and undefined reasons are rejected.</summary>
        public static ExecutionResult Failure(ExecutionFailureReason reason)
        {
            if (reason == ExecutionFailureReason.None || !Enum.IsDefined(typeof(ExecutionFailureReason), reason))
                throw new ArgumentOutOfRangeException(nameof(reason));
            return new ExecutionResult(ExecutionStatus.Failed, reason);
        }
    }
}
