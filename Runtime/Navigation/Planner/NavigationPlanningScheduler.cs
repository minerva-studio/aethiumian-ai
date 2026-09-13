using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Aethiumian.AI.Diagnostics;
#endif
using Unity.Profiling;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns a finite queue consumed by Map-lifetime background planning tasks.</summary>
    public sealed class NavigationPlanningScheduler : IDisposable
    {
        private readonly int capacity;
        private readonly ConcurrentQueue<ScheduledWork> queue = new();
        private readonly SemaphoreSlim signal = new(0);
        private readonly CancellationTokenSource shutdown = new();
        private readonly ConcurrentDictionary<NavigationPlanningOperation, ScheduledWork> active = new();
        private readonly ConcurrentQueue<NavigationPlanningCompletion> completed = new();
        private readonly Task[] consumers;
        private int queuedCount;
        private int started;
        private int disposed;
        private static readonly ProfilerMarker QueueWaitMarker = new("AethiumianAI.Navigation.QueueWait");
        private static readonly ProfilerMarker PlanningMarker = new("AethiumianAI.Navigation.BackgroundPlanning");

        /// <summary>Gets the fixed number of consumers owned by this scheduler.</summary>
        internal int WorkerCount => consumers.Length;

        /// <summary>Dequeues one detached completion record for the Map-owned main-thread drain.</summary>
        internal bool TryDequeueCompletion(out NavigationPlanningCompletion completion)
            => completed.TryDequeue(out completion);

        /// <summary>Creates a finite Map-owned queue without starting planning before world publication.</summary>
        public NavigationPlanningScheduler(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            int count = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 2));
            consumers = new Task[count];
        }

        /// <summary>Starts the fixed consumers after the immutable world snapshot has been published.</summary>
        internal void Start()
        {
            ThrowIfDisposed();
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            for (int i = 0; i < consumers.Length; i++) consumers[i] = Task.Run(ConsumeLoopAsync);
        }

        /// <summary>Queues already-created pure planning work without retaining its Unity owner.</summary>
        internal NavigationPlanningOperation PlanWork(INavigationPlanningWork work,
            CancellationToken cancellationToken = default, NavigationPlanningOperation operation = null)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            ThrowIfDisposed();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AIPerformanceDiagnostics.RecordNavigationSubmission();
#endif
            if (cancellationToken.IsCancellationRequested)
            {
                work.Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AIPerformanceDiagnostics.RecordNavigationCancelled();
#endif
                return NavigationPlanningOperation.CreateCancelled();
            }
            int count = Interlocked.Increment(ref queuedCount);
            if (count > capacity)
            {
                Interlocked.Decrement(ref queuedCount);
                work.Dispose();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AIPerformanceDiagnostics.RecordNavigationQueueRejected();
#endif
                throw new InvalidOperationException("The navigation planning queue is full.");
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AIPerformanceDiagnostics.RecordNavigationAccepted(count);
#endif
            NavigationPlanningOperation result = operation ?? new NavigationPlanningOperation();
            if (operation == null) result.RegisterCancellation(cancellationToken);
            queue.Enqueue(new ScheduledWork(work, result, cancellationToken, shutdown.Token, Stopwatch.GetTimestamp()));
            signal.Release();
            return result;
        }

        /// <summary>Finalizes queued and active requests at a Map-owned failure boundary.</summary>
        public int FailPending(Exception failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            int finalized = 0;
            while (queue.TryDequeue(out ScheduledWork queued))
            {
                Interlocked.Decrement(ref queuedCount);
                queued.DisposePendingWork();
                if (queued.Operation.TryFail(failure)) finalized++;
                queued.Operation.ReleaseCancellationRegistration();
            }
            foreach (KeyValuePair<NavigationPlanningOperation, ScheduledWork> pair in active)
            {
                pair.Value.Cancel();
                if (pair.Key.TryFail(failure)) finalized++;
                pair.Key.ReleaseCancellationRegistration();
            }
            return finalized;
        }

        /// <summary>Cancels work and schedules managed resource cleanup after every consumer exits.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            shutdown.Cancel();
            while (queue.TryDequeue(out ScheduledWork queued))
            {
                Interlocked.Decrement(ref queuedCount);
                queued.DisposePendingWork();
                queued.Operation.TryFinalizeCancellation();
                queued.Operation.ReleaseCancellationRegistration();
            }
            foreach (ScheduledWork running in active.Values) running.Cancel();
            for (int i = 0; i < consumers.Length; i++) signal.Release();

            Task[] startedConsumers = Array.FindAll(consumers, static task => task != null);
            SemaphoreSlim ownedSignal = signal;
            CancellationTokenSource ownedShutdown = shutdown;
            _ = Task.WhenAll(startedConsumers).ContinueWith(static (_, state) =>
            {
                (SemaphoreSlim signal, CancellationTokenSource shutdown) resources =
                    ((SemaphoreSlim, CancellationTokenSource))state;
                resources.signal.Dispose();
                resources.shutdown.Dispose();
            }, (ownedSignal, ownedShutdown), CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task ConsumeLoopAsync()
        {
            try
            {
                while (true)
                {
                    await signal.WaitAsync(shutdown.Token).ConfigureAwait(false);
                    if (shutdown.IsCancellationRequested) return;
                    if (!queue.TryDequeue(out ScheduledWork scheduled)) continue;
                    int depth = Interlocked.Decrement(ref queuedCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    AIPerformanceDiagnostics.RecordNavigationDequeued(depth);
#endif
                    Execute(scheduled);
                }
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        }

        private void Execute(ScheduledWork scheduled)
        {
            if (!active.TryAdd(scheduled.Operation, scheduled))
            {
                scheduled.DisposePendingWork();
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool navigationWorkerStarted = false;
            bool navigationCompletionRecorded = false;
            long planningStartTimestamp = 0;
            long planningNanoseconds = 0;
#endif
            try
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                AIPerformanceDiagnostics.RecordNavigationWorkerStarted(
                    ElapsedNanoseconds(scheduled.QueuedTimestamp, Stopwatch.GetTimestamp()));
                navigationWorkerStarted = true;
#endif
                if (scheduled.IsCancellationRequested)
                {
                    scheduled.Operation.TryFinalizeCancellation();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    AIPerformanceDiagnostics.RecordNavigationWorkerCompleted(0);
                    AIPerformanceDiagnostics.RecordNavigationCancelled();
                    navigationCompletionRecorded = true;
#endif
                    return;
                }
                using (QueueWaitMarker.Auto())
                {
                    // Keep the legacy marker for compatibility; the cross-thread queue wait is recorded by diagnostics below.
                    _ = ElapsedMilliseconds(scheduled.QueuedTimestamp, Stopwatch.GetTimestamp());
                }
                using (PlanningMarker.Auto())
                using (INavigationPlanningWork work = scheduled.TakeWork())
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    AIPerformanceDiagnostics.RecordPathRequest();
                    planningStartTimestamp = Stopwatch.GetTimestamp();
                    NavigationPlanResult result = ExecuteMeasuredWork(work, scheduled.CancellationToken);
                    planningNanoseconds = ElapsedNanoseconds(planningStartTimestamp, Stopwatch.GetTimestamp());
#else
                    NavigationPlanResult result = work.Execute(scheduled.CancellationToken);
#endif
                    if (scheduled.Operation.TryPrepareCompletion(result, out bool wasCancelled))
                    {
                        if (!wasCancelled)
                        {
                            completed.Enqueue(new NavigationPlanningCompletion(scheduled.Operation));
                        }

                        scheduled.Operation.ReleaseCancellationRegistration();
                        scheduled.Operation.PublishPreparedCompletion();
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    AIPerformanceDiagnostics.RecordNavigationWorkerCompleted(planningNanoseconds);
                    if (wasCancelled || scheduled.Operation.IsCancelled)
                        AIPerformanceDiagnostics.RecordNavigationCancelled();
                    else if (scheduled.Operation.Exception != null)
                        AIPerformanceDiagnostics.RecordNavigationException();
                    else if (result.Route != null)
                        AIPerformanceDiagnostics.RecordNavigationRouteFound();
                    else
                        AIPerformanceDiagnostics.RecordNavigationNoPath();
                    navigationCompletionRecorded = true;
#endif
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (navigationWorkerStarted && !navigationCompletionRecorded)
                {
                    if (planningStartTimestamp != 0)
                        planningNanoseconds = ElapsedNanoseconds(planningStartTimestamp, Stopwatch.GetTimestamp());
                    AIPerformanceDiagnostics.RecordNavigationWorkerCompleted(planningNanoseconds);
                    if (scheduled.IsCancellationRequested)
                        AIPerformanceDiagnostics.RecordNavigationCancelled();
                    else
                        AIPerformanceDiagnostics.RecordNavigationException();
                    navigationCompletionRecorded = true;
                }
#endif
                if (scheduled.IsCancellationRequested) scheduled.Operation.TryFinalizeCancellation();
                else scheduled.Operation.TryFail(exception);
            }
            finally
            {
                scheduled.Operation.ReleaseCancellationRegistration();
                active.TryRemove(scheduled.Operation, out _);
                scheduled.DisposeCancellation();
            }
        }

        private static double ElapsedMilliseconds(long start, long end) => (end - start) * 1000d / Stopwatch.Frequency;

        /// <summary>Converts a stopwatch interval to nanoseconds for worker lifecycle diagnostics.</summary>
        private static long ElapsedNanoseconds(long start, long end)
            => (long)((end - start) * (1_000_000_000d / Stopwatch.Frequency));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Executes one detached planner under the shared path-finding profiler marker.</summary>
        private static NavigationPlanResult ExecuteMeasuredWork(INavigationPlanningWork work, CancellationToken cancellationToken)
        {
            using (AIPerformanceDiagnostics.PathFindingMarker.Auto())
                return work.Execute(cancellationToken);
        }
#endif

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(NavigationPlanningScheduler));
        }

        private sealed class ScheduledWork
        {
            private INavigationPlanningWork work;
            private readonly CancellationTokenSource cancellation;
            private int cancellationDisposed;
            public NavigationPlanningOperation Operation { get; }
            public long QueuedTimestamp { get; }
            public bool IsCancellationRequested => cancellation.IsCancellationRequested || Operation.IsCancellationRequested;
            public CancellationToken CancellationToken => cancellation.Token;

            public ScheduledWork(INavigationPlanningWork work, NavigationPlanningOperation operation,
                CancellationToken requestCancellation, CancellationToken runtimeCancellation, long queuedTimestamp)
            {
                this.work = work;
                Operation = operation;
                QueuedTimestamp = queuedTimestamp;
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation, runtimeCancellation);
            }

            public INavigationPlanningWork TakeWork()
            {
                INavigationPlanningWork result = Interlocked.Exchange(ref work, null);
                return result ?? throw new InvalidOperationException("Planning work was already consumed.");
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancellationDisposed) != 0) return;
                try { cancellation.Cancel(); }
                catch (ObjectDisposedException) { }
            }

            public void DisposePendingWork()
            {
                Interlocked.Exchange(ref work, null)?.Dispose();
                DisposeCancellation();
            }

            public void DisposeCancellation()
            {
                if (Interlocked.Exchange(ref cancellationDisposed, 1) == 0) cancellation.Dispose();
            }
        }
    }
}
