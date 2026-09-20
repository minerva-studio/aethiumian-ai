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
        private readonly object sync = new();
        private readonly Queue<ScheduledWork> simpleQueue = new();
        private readonly Queue<ScheduledWork> smartQueue = new();
        private readonly SemaphoreSlim simpleSignal = new(0, 1);
        private readonly SemaphoreSlim generalSignal = new(0, 1);
        private readonly int smartCapacity;
        private int consecutiveSimple;
        private readonly CancellationTokenSource shutdown = new();
        private readonly ConcurrentDictionary<NavigationPlanningOperation, ScheduledWork> active = new();
        private readonly Task[] consumers;
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
            // A single waiting slot cannot reserve capacity for both classes.
            int reserved = capacity == 1 ? 0 : Math.Min(capacity - 1, (capacity - 1) / 4 + 1);
            smartCapacity = capacity - reserved;
            int count = Math.Max(2, Environment.ProcessorCount / 2);
            consumers = new Task[count];
        }

        /// <summary>Starts the fixed consumers after the immutable world snapshot has been published.</summary>
        public void Start()
        {
            lock (sync)
            {
                ThrowIfDisposed();
                if (started != 0) return;
                started = 1;
                consumers[0] = Task.Run(() => ConsumeLoopAsync(true));
                for (int i = 1; i < consumers.Length; i++)
                    consumers[i] = Task.Run(() => ConsumeLoopAsync(false));
            }
        }

        /// <summary>
        /// Queues detached work. NextAction uses the Simple lane; Route uses the Smart lane.
        /// Capacity counts waiting requests only. At capacity one, no queue slot is reserved.
        /// </summary>
        public NavigationPlanningOperation PlanWork(INavigationPlanningWork work, CancellationToken cancellationToken = default, NavigationPlanningOperation operation = null, NavigationPlanningExtent extent = NavigationPlanningExtent.Route)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            ThrowIfDisposed();
            if (cancellationToken.IsCancellationRequested)
            {
                work.Dispose();
                return NavigationPlanningOperation.CreateCancelled();
            }
            bool simple = extent == NavigationPlanningExtent.NextAction;
            List<ScheduledWork> cancelled = null;
            NavigationPlanningOperation result = operation ?? new NavigationPlanningOperation();
            if (operation == null) result.RegisterCancellation(cancellationToken);
            bool accepted = false;
            try
            {
                lock (sync)
                {
                    ThrowIfDisposed();
                    if (!HasCapacity(simple))
                    {
                        RemoveCancelled(simpleQueue, ref cancelled);
                        RemoveCancelled(smartQueue, ref cancelled);
                        if (smartQueue.Count == 0) consecutiveSimple = 0;
                    }
                    if (!HasCapacity(simple))
                        throw new InvalidOperationException("The navigation planning queue is full.");
                    ScheduledWork scheduled = new(work, result, cancellationToken, shutdown.Token);
                    (simple ? simpleQueue : smartQueue).Enqueue(scheduled);
                    accepted = true;
                    NotifyConsumers();
                }
                return result;
            }
            finally
            {
                if (cancelled != null)
                    foreach (ScheduledWork item in cancelled) RetirePending(item, null);
                if (!accepted)
                {
                    work.Dispose();
                    if (operation == null) result.ReleaseCancellationRegistration();
                }
            }
        }

        private bool HasCapacity(bool simple)
            => simpleQueue.Count + smartQueue.Count < capacity && (simple || smartQueue.Count < smartCapacity);

        private static void RemoveCancelled(Queue<ScheduledWork> queue, ref List<ScheduledWork> removed)
        {
            int count = queue.Count;
            for (int i = 0; i < count; i++)
            {
                ScheduledWork item = queue.Dequeue();
                if (item.IsCancellationRequested) (removed ??= new()).Add(item);
                else queue.Enqueue(item);
            }
        }

        // Notifications are coalesced. Taking work chains another wakeup when work remains.
        // All producers hold sync; consumers may consume a signal before acquiring sync.
        private void NotifyConsumers()
        {
            if (simpleQueue.Count > 0 && simpleSignal.CurrentCount == 0) simpleSignal.Release();
            if (simpleQueue.Count + smartQueue.Count > 0 && generalSignal.CurrentCount == 0) generalSignal.Release();
        }

        private List<ScheduledWork> DrainQueues()
        {
            List<ScheduledWork> pending = new(simpleQueue.Count + smartQueue.Count);
            while (simpleQueue.Count > 0) pending.Add(simpleQueue.Dequeue());
            while (smartQueue.Count > 0) pending.Add(smartQueue.Dequeue());
            consecutiveSimple = 0;
            return pending;
        }

        private static bool RetirePending(ScheduledWork item, Exception failure)
        {
            item.DisposePendingWork();
            bool finalized = failure == null
                ? item.Operation.TryFinalizeCancellation()
                : item.Operation.TryFail(failure);
            item.Operation.ReleaseCancellationRegistration();
            return finalized;
        }

        /// <summary>Finalizes queued and active requests at a Map-owned failure boundary.</summary>
        public int FailPending(Exception failure)
        {
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            List<ScheduledWork> pending;
            ScheduledWork[] running;
            lock (sync)
            {
                pending = DrainQueues();
                running = new List<ScheduledWork>(active.Values).ToArray();
            }
            int finalized = 0;
            foreach (ScheduledWork item in pending)
                if (RetirePending(item, failure)) finalized++;
            foreach (ScheduledWork item in running)
            {
                item.Cancel();
                if (item.Operation.TryFail(failure)) finalized++;
                item.Operation.ReleaseCancellationRegistration();
            }
            return finalized;
        }

        /// <summary>Cancels work without blocking the caller on running planners.</summary>
        public void Dispose()
        {
            List<ScheduledWork> pending;
            Task[] startedConsumers;
            lock (sync)
            {
                if (disposed != 0) return;
                Volatile.Write(ref disposed, 1);
                pending = DrainQueues();
                startedConsumers = Array.FindAll(consumers, static task => task != null);
            }
            shutdown.Cancel();
            foreach (ScheduledWork item in pending) RetirePending(item, null);
            _ = Task.WhenAll(startedConsumers).ContinueWith(_ =>
            {
                simpleSignal.Dispose();
                generalSignal.Dispose();
                shutdown.Dispose();
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private async Task ConsumeLoopAsync(bool simpleOnly)
        {
            SemaphoreSlim signal = simpleOnly ? simpleSignal : generalSignal;
            try
            {
                while (true)
                {
                    await signal.WaitAsync(shutdown.Token).ConfigureAwait(false);
                    ScheduledWork scheduled;
                    bool ownsOperation;
                    lock (sync)
                    {
                        if (disposed != 0) return;
                        if (smartQueue.Count == 0) consecutiveSimple = 0;
                        if (simpleQueue.Count > 0 && (simpleOnly || smartQueue.Count == 0 || consecutiveSimple < 3))
                        {
                            scheduled = simpleQueue.Dequeue();
                            if (!simpleOnly && smartQueue.Count > 0) consecutiveSimple++;
                        }
                        else if (!simpleOnly && smartQueue.Count > 0)
                        {
                            scheduled = smartQueue.Dequeue();
                            consecutiveSimple = 0;
                        }
                        else continue;
                        // Register before releasing queue ownership so failure/close cannot miss this request.
                        ownsOperation = active.TryAdd(scheduled.Operation, scheduled);
                        NotifyConsumers();
                    }
                    if (ownsOperation) Execute(scheduled);
                    else scheduled.DisposePendingWork();
                }
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        }

        private void Execute(ScheduledWork scheduled)
        {
            try
            {
                if (scheduled.IsCancellationRequested)
                {
                    scheduled.Operation.TryFinalizeCancellation();
                    scheduled.DisposeWork();
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
                DisposeWork();
                DisposeCancellation();
            }

            public void DisposeWork()
            {
                Interlocked.Exchange(ref work, null)?.Dispose();
            }

            public void DisposeCancellation()
            {
                if (Interlocked.Exchange(ref cancellationDisposed, 1) == 0) cancellation.Dispose();
            }
        }
    }
}
