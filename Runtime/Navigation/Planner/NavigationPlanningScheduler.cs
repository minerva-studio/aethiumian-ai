using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns a finite queue consumed by Map-lifetime background planning tasks.</summary>
    public sealed class NavigationPlanningScheduler : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly ProfilerMarker PlanningMarker = new("AethiumianAI.Navigation.BackgroundPlanning");
        private static readonly ProfilerMarker PathFindingMarker = new("Aethiumian.AI/PathFinding");
#endif
        private readonly int capacity;
        private readonly ConcurrentQueue<ScheduledWork> queue = new();
        private readonly SemaphoreSlim signal = new(0);
        private readonly CancellationTokenSource shutdown = new();
        private readonly ConcurrentDictionary<NavigationPlanningOperation, ScheduledWork> active = new();
        private readonly Task[] consumers;
        private int queuedCount;
        private int started;
        private int disposed;

        /// <summary>
        /// Gets the fixed number of consumers owned by this scheduler.
        /// </summary>
        public int WorkerCount => consumers.Length;

        /// <summary>Creates a finite Map-owned queue without starting planning before world publication.</summary>
        public NavigationPlanningScheduler(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            int count = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 2));
            consumers = new Task[count];
        }

        /// <summary>Starts the fixed consumers after the immutable world snapshot has been published.</summary>
        public void Start()
        {
            ThrowIfDisposed();
            if (Interlocked.Exchange(ref started, 1) != 0) return;
            for (int i = 0; i < consumers.Length; i++) consumers[i] = Task.Run(ConsumeLoopAsync);
        }

        /// <summary>Queues already-created pure planning work without retaining its Unity owner.</summary>
        public NavigationPlanningOperation PlanWork(INavigationPlanningWork work, CancellationToken cancellationToken = default, NavigationPlanningOperation operation = null)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            ThrowIfDisposed();
            if (cancellationToken.IsCancellationRequested)
            {
                work.Dispose();
                return NavigationPlanningOperation.CreateCancelled();
            }
            int count = Interlocked.Increment(ref queuedCount);
            if (count > capacity)
            {
                Interlocked.Decrement(ref queuedCount);
                work.Dispose();
                throw new InvalidOperationException("The navigation planning queue is full.");
            }
            NavigationPlanningOperation result = operation ?? new NavigationPlanningOperation();
            if (operation == null) result.RegisterCancellation(cancellationToken);
            queue.Enqueue(new ScheduledWork(work, result, cancellationToken, shutdown.Token));
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
                    Interlocked.Decrement(ref queuedCount);
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
            try
            {
                if (scheduled.IsCancellationRequested)
                {
                    scheduled.Operation.TryFinalizeCancellation();
                    return;
                }
                using (INavigationPlanningWork work = scheduled.TakeWork())
                {
                    NavigationPlanResult result;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    using (PlanningMarker.Auto())
                    using (PathFindingMarker.Auto())
#endif
                        result = work.Execute(scheduled.CancellationToken);
                    if (scheduled.Operation.TryPrepareCompletion(result, out _))
                    {
                        scheduled.Operation.ReleaseCancellationRegistration();
                        scheduled.Operation.PublishPreparedCompletion();
                    }
                }
            }
            catch (Exception exception)
            {
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

            public ScheduledWork(INavigationPlanningWork work, NavigationPlanningOperation operation, CancellationToken requestCancellation, CancellationToken runtimeCancellation)
            {
                this.work = work;
                Operation = operation;
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation, runtimeCancellation);
            }

            public bool IsCancellationRequested => cancellation.IsCancellationRequested || Operation.IsCancellationRequested;
            public CancellationToken CancellationToken => cancellation.Token;


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
