using System;
using System.Threading;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Controls whether a request asks for a complete route or one local action.</summary>
    public enum NavigationPlanningExtent
    {
        /// <summary>
        /// Plan next action only, then return the result to the Movement node for execution. The Movement node may request a new planning operation after the action completes, or may end movement if the goal is reached.
        /// </summary>
        NextAction,

        /// <summary>
        /// Plan toward the goal. Route requests publish only a complete route or a terminal
        /// exhausted/budget result.
        /// </summary>
        Route
    }

    /// <summary>
    /// Exposes the polling result of one scheduler-owned planning request.
    /// Terminal publication is lock-free and cancellation never disposes its own callback.
    /// </summary>
    public sealed class NavigationPlanningOperation
    {
        private const int Pending = 0;
        private const int Publishing = 1;
        private const int Completed = 2;

        private int terminalState;
        private int cancellationRequested;
        private int cancelled;
        private NavigationPlanResult planResult;
        private Exception exception;
        private CancellationRegistrationHolder cancellationRegistration;

        /// <summary>Gets whether planning, cancellation, or failure reached a terminal outcome.</summary>
        public bool IsCompleted => Volatile.Read(ref terminalState) == Completed;

        /// <summary>Gets whether this operation completed through cancellation.</summary>
        public bool IsCancelled => Volatile.Read(ref cancelled) != 0;

        /// <summary>Gets the published planning result, or <see cref="NavigationPlanResult.NoResult"/> while pending.</summary>
        public NavigationPlanResult PlanResult
            => IsCompleted && !IsCancelled && Exception == null ? planResult : NavigationPlanResult.NoResult;

        /// <summary>Gets the completed route, or null while pending and for no-route, cancellation, or exception outcomes.</summary>
        public NavigationRoute Result => PlanResult.Route;

        /// <summary>Gets the unexpected planner exception, or null for pending and ordinary terminal outcomes.</summary>
        public Exception Exception => IsCompleted ? Volatile.Read(ref exception) : null;


        /// <summary>Creates a pending operation with no owner-capturing state.</summary>
        public NavigationPlanningOperation() { }

        /// <summary>Registers cancellation without retaining a lock or Unity owner.</summary>
        public void RegisterCancellation(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled) return;
            CancellationRegistrationHolder holder = new();
            Volatile.Write(ref cancellationRegistration, holder);
            holder.Register(cancellationToken, this);
        }

        /// <summary>Creates an operation already finalized as cancelled without queue ownership.</summary>
        internal static NavigationPlanningOperation CreateCancelled()
        {
            NavigationPlanningOperation operation = new();
            operation.TryFinalizeCancellation();
            return operation;
        }

        /// <summary>Creates an operation already finalized with a world-build failure.</summary>
        internal static NavigationPlanningOperation CreateFailed(Exception failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            NavigationPlanningOperation operation = new();
            operation.TryFail(failure);
            return operation;
        }

        /// <summary>Gets whether cancellation has been requested from any thread.</summary>
        public bool IsCancellationRequested => Volatile.Read(ref cancellationRequested) != 0;

        /// <summary>Prepares a completed result before publishing its terminal state.</summary>
        public bool TryPrepareCompletion(NavigationPlanResult result, out bool wasCancelled)
        {
            wasCancelled = false;
            if (Interlocked.CompareExchange(ref terminalState, Publishing, Pending) != Pending) return false;
            if (IsCancellationRequested)
            {
                Volatile.Write(ref cancelled, 1);
                result = NavigationPlanResult.NoResult;
                wasCancelled = true;
            }

            planResult = result;
            Volatile.Write(ref exception, null);
            return true;
        }

        /// <summary>Publishes a prepared result after the result and terminal state are ready.</summary>
        public void PublishPreparedCompletion()
        {
            if (Interlocked.CompareExchange(ref terminalState, Completed, Publishing) != Publishing)
                throw new InvalidOperationException("Navigation planning completion was not prepared.");
        }

        /// <summary>Finalizes cancellation through the non-blocking cancellation callback or an owner cleanup boundary.</summary>
        public bool TryFinalizeCancellation()
        {
            if (Interlocked.CompareExchange(ref terminalState, Publishing, Pending) != Pending) return false;
            Volatile.Write(ref cancelled, 1);
            planResult = NavigationPlanResult.NoResult;
            Volatile.Write(ref exception, null);
            Volatile.Write(ref terminalState, Completed);
            return true;
        }

        /// <summary>Finalizes a planner exception without waiting for cancellation callbacks.</summary>
        public bool TryFail(Exception failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            if (IsCancellationRequested) return TryFinalizeCancellation();
            if (!TryPrepareCompletion(NavigationPlanResult.NoResult, out bool wasCancelled)) return false;
            if (!wasCancelled) Volatile.Write(ref exception, failure);
            PublishPreparedCompletion();
            return true;
        }

        /// <summary>Finalizes an ordinary planning result without a scheduler completion record.</summary>
        public bool TryComplete(NavigationPlanResult result)
        {
            if (IsCancellationRequested) return TryFinalizeCancellation();
            if (!TryPrepareCompletion(result, out _)) return false;
            ReleaseCancellationRegistration();
            PublishPreparedCompletion();
            return true;
        }

        /// <summary>Releases the cancellation registration exactly once at an owner-controlled completion boundary.</summary>
        public void ReleaseCancellationRegistration()
            => Volatile.Read(ref cancellationRegistration)?.Dispose();

        private void RequestCancellation()
        {
            Interlocked.Exchange(ref cancellationRequested, 1);
            TryFinalizeCancellation();
        }

        private sealed class CancellationRegistrationHolder
        {
            private CancellationTokenRegistration registration;
            private int registrationReady;
            private int disposeRequested;

            public void Register(CancellationToken token, NavigationPlanningOperation owner)
            {
                CancellationTokenRegistration value = token.Register(
                    static state => ((NavigationPlanningOperation)state).RequestCancellation(), owner);
                registration = value;
                Volatile.Write(ref registrationReady, 1);
                if (Volatile.Read(ref disposeRequested) != 0) value.Dispose();
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposeRequested, 1) != 0) return;
                if (Volatile.Read(ref registrationReady) != 0) registration.Dispose();
            }
        }
    }
}
