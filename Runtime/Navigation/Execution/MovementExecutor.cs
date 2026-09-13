using System;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Owns action lifetime and stall timing. Concrete executors borrow bodies and runtimes;
    /// the node selects recovery policy after receiving a terminal result.
    /// </summary>
    public abstract class MovementExecutor : IDisposable
    {
        /// <summary>Internal progress observations; never exposed as node policy instructions.</summary>
        protected enum ProgressObservation
        {
            /// <summary>This phase is not monitored and clears idle time.</summary>
            NotMonitored,
            /// <summary>A new best progress observation clears idle time.</summary>
            Advanced,
            /// <summary>This phase expects progress but made none this tick.</summary>
            Waiting
        }

        private readonly float maximumIdleDuration;
        private float stallTimer;
        private bool disposed;

        /// <summary>Whether an action has been prepared and has not ended.</summary>
        protected bool IsExecuting { get; private set; }

        /// <summary>Captures a finite non-negative timeout. Zero disables stall observation.</summary>
        protected MovementExecutor(float maximumIdleDuration = 0f)
        {
            Validate.NonNegativeFinite(maximumIdleDuration, nameof(maximumIdleDuration));
            this.maximumIdleDuration = maximumIdleDuration;
        }

        /// <summary>
        /// Advances one prepared action. Physical terminal outcomes take precedence over timeout.
        /// Ordinary failures are returned; programming exceptions are cleaned up and rethrown.
        /// </summary>
        public ExecutionResult Tick(float deltaTime)
        {
            ThrowIfDisposed();
            if (!IsExecuting) throw new InvalidOperationException("No active movement execution.");
            Validate.PositiveFinite(deltaTime, nameof(deltaTime));
            try
            {
                ExecutionResult result = Tick_Internal(deltaTime);
                if (result.Status == ExecutionStatus.Running && maximumIdleDuration > 0f)
                {
                    // Scheduling belongs here: derived executors never advance the clock.
                    ProgressObservation progress = ObserveProgress();
                    stallTimer = progress == ProgressObservation.Waiting ? stallTimer + deltaTime : 0f;
                    if (stallTimer >= maximumIdleDuration)
                        result = ExecutionResult.Failure(ExecutionFailureReason.Stalled);
                }
                if (result.Status != ExecutionStatus.Running) CancelExecution();
                return result;
            }
            catch
            {
                // A cleanup error must not replace the original execution/observation error.
                try { CancelExecution(); } catch { }
                throw;
            }
        }

        /// <summary>
        /// Clears idle time and concrete progress samples after an intentional pause.
        /// Does not restart trajectory time or write velocity. Inactive is a no-op; disposed is rejected.
        /// </summary>
        public void ResetProgressBaseline()
        {
            ThrowIfDisposed();
            stallTimer = 0f;
            if (IsExecuting) ResetProgressBaselineCore();
        }

        /// <summary>Releases the current action once and permanently prevents reuse.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelExecution();
        }

        /// <summary>Releases the previous action before derived code installs new action data or leases.</summary>
        protected void BeginExecution()
        {
            ThrowIfDisposed();
            CancelExecution();
            IsExecuting = true;
        }

        /// <summary>Idempotently ends the action without destroying borrowed dependencies or resetting velocity.</summary>
        protected void CancelExecution()
        {
            stallTimer = 0f;
            if (!IsExecuting) return;
            // Mark inactive first so a throwing cleanup cannot cause a second release.
            IsExecuting = false;
            ReleaseExecutionResources();
        }

        /// <summary>Rejects preparation after disposal before derived code changes action data.</summary>
        protected void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(GetType().Name);
        }

        /// <summary>Executes one physics step with an already validated positive, finite time step.</summary>
        protected abstract ExecutionResult Tick_Internal(float deltaTime);
        /// <summary>Called once after a running step when monitoring is enabled; defines meaningful progress.</summary>
        protected virtual ProgressObservation ObserveProgress() => ProgressObservation.NotMonitored;
        /// <summary>Forgets action-specific progress samples after a pause.</summary>
        protected virtual void ResetProgressBaselineCore() { }
        /// <summary>Releases resources owned by this action, including temporary collision leases.</summary>
        protected virtual void ReleaseExecutionResources() { }
    }
}
