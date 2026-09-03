#nullable enable
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;
using Unity.Profiling;

namespace Aethiumian.AI.Diagnostics
{
    /// <summary>
    /// Provides opt-in, allocation-free counters for AI runtime performance baselines.
    /// Counters are process-local diagnostics and are never part of behaviour-tree state.
    /// </summary>
    public static class AIPerformanceDiagnostics
    {
        // Diagnostics are opt-in so a normal player build does not pay counter work.
        private static bool enabled;

        private static long aiUpdates;
        private static long aiLateUpdates;
        private static long aiFixedUpdates;
        private static long treeUpdates;
        private static long treeLateUpdates;
        private static long treeFixedUpdates;
        private static long stackUpdates;
        private static long stackLateUpdates;
        private static long stackFixedUpdates;
        private static long stackTicks;
        private static long actionUpdates;
        private static long actionLateUpdates;
        private static long actionFixedUpdates;
        private static long serviceUpdates;
        private static long pathRequests;
        private static long pathExpandedNodes;
        private static long entityQueries;
        private static long entityCandidates;
        private static long movementTicks;
        private static long navigationSubmissions;
        private static long navigationAccepted;
        private static long navigationQueueRejected;
        private static long navigationWorkerStarted;
        private static long navigationWorkerCompleted;
        private static long navigationQueueDepth;
        private static long navigationQueueDepthPeak;
        private static long navigationQueueWaitNanoseconds;
        private static long navigationPlanningNanoseconds;
        private static long navigationRouteFound;
        private static long navigationNoPath;
        private static long navigationCancelled;
        private static long navigationExceptions;

        /// <summary>Marks the AI driver update path.</summary>
        public static readonly ProfilerMarker AIUpdateMarker = new("Aethiumian.AI/AI.Update");
        /// <summary>Marks the AI driver late-update path.</summary>
        public static readonly ProfilerMarker AILateUpdateMarker = new("Aethiumian.AI/AI.LateUpdate");
        /// <summary>Marks the AI driver fixed-update path.</summary>
        public static readonly ProfilerMarker AIFixedUpdateMarker = new("Aethiumian.AI/AI.FixedUpdate");
        /// <summary>Marks behaviour-tree update execution.</summary>
        public static readonly ProfilerMarker TreeUpdateMarker = new("Aethiumian.AI/BehaviourTree.Update");
        /// <summary>Marks behaviour-tree late-update execution.</summary>
        public static readonly ProfilerMarker TreeLateUpdateMarker = new("Aethiumian.AI/BehaviourTree.LateUpdate");
        /// <summary>Marks behaviour-tree fixed-update execution.</summary>
        public static readonly ProfilerMarker TreeFixedUpdateMarker = new("Aethiumian.AI/BehaviourTree.FixedUpdate");
        /// <summary>Marks one behaviour-tree stack tick.</summary>
        public static readonly ProfilerMarker StackTickMarker = new("Aethiumian.AI/BehaviourTree.Stack.Tick");
        /// <summary>Marks stack action updates.</summary>
        public static readonly ProfilerMarker StackUpdateMarker = new("Aethiumian.AI/BehaviourTree.Stack.Update");
        /// <summary>Marks stack action fixed updates.</summary>
        public static readonly ProfilerMarker StackFixedUpdateMarker = new("Aethiumian.AI/BehaviourTree.Stack.FixedUpdate");
        /// <summary>Marks stack action late updates.</summary>
        public static readonly ProfilerMarker StackLateUpdateMarker = new("Aethiumian.AI/BehaviourTree.Stack.LateUpdate");
        /// <summary>Marks service dispatch.</summary>
        public static readonly ProfilerMarker ServiceUpdateMarker = new("Aethiumian.AI/BehaviourTree.Services");
        /// <summary>Marks entity target-query execution.</summary>
        public static readonly ProfilerMarker EntityQueryMarker = new("Aethiumian.AI/EntityQuery");
        /// <summary>Marks path-finding execution.</summary>
        public static readonly ProfilerMarker PathFindingMarker = new("Aethiumian.AI/PathFinding");
        /// <summary>Marks movement-executor ticks.</summary>
        public static readonly ProfilerMarker MovementMarker = new("Aethiumian.AI/MovementExecutor");

        /// <summary>Gets or sets whether diagnostic counters are enabled.</summary>
        public static bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        /// <summary>Clears all counters before a new measurement window.</summary>
        public static void Reset()
        {
            aiUpdates = 0;
            aiLateUpdates = 0;
            aiFixedUpdates = 0;
            treeUpdates = 0;
            treeLateUpdates = 0;
            treeFixedUpdates = 0;
            stackUpdates = 0;
            stackLateUpdates = 0;
            stackFixedUpdates = 0;
            stackTicks = 0;
            actionUpdates = 0;
            actionLateUpdates = 0;
            actionFixedUpdates = 0;
            serviceUpdates = 0;
            Interlocked.Exchange(ref pathRequests, 0);
            Interlocked.Exchange(ref pathExpandedNodes, 0);
            entityQueries = 0;
            entityCandidates = 0;
            movementTicks = 0;
            Interlocked.Exchange(ref navigationSubmissions, 0);
            Interlocked.Exchange(ref navigationAccepted, 0);
            Interlocked.Exchange(ref navigationQueueRejected, 0);
            Interlocked.Exchange(ref navigationWorkerStarted, 0);
            Interlocked.Exchange(ref navigationWorkerCompleted, 0);
            Interlocked.Exchange(ref navigationQueueDepth, 0);
            Interlocked.Exchange(ref navigationQueueDepthPeak, 0);
            Interlocked.Exchange(ref navigationQueueWaitNanoseconds, 0);
            Interlocked.Exchange(ref navigationPlanningNanoseconds, 0);
            Interlocked.Exchange(ref navigationRouteFound, 0);
            Interlocked.Exchange(ref navigationNoPath, 0);
            Interlocked.Exchange(ref navigationCancelled, 0);
            Interlocked.Exchange(ref navigationExceptions, 0);
        }

        /// <summary>Captures a read-only snapshot of the current counters.</summary>
        public static Snapshot Capture() => new(
            aiUpdates, aiLateUpdates, aiFixedUpdates,
            treeUpdates, treeLateUpdates, treeFixedUpdates,
            stackUpdates, stackLateUpdates, stackFixedUpdates, stackTicks,
            actionUpdates, actionLateUpdates, actionFixedUpdates, serviceUpdates,
            Interlocked.Read(ref pathRequests), Interlocked.Read(ref pathExpandedNodes), entityQueries, entityCandidates, movementTicks,
            Interlocked.Read(ref navigationSubmissions), Interlocked.Read(ref navigationAccepted),
            Interlocked.Read(ref navigationQueueRejected), Interlocked.Read(ref navigationWorkerStarted),
            Interlocked.Read(ref navigationWorkerCompleted), Interlocked.Read(ref navigationQueueDepth),
            Interlocked.Read(ref navigationQueueDepthPeak), Interlocked.Read(ref navigationQueueWaitNanoseconds),
            Interlocked.Read(ref navigationPlanningNanoseconds), Interlocked.Read(ref navigationRouteFound),
            Interlocked.Read(ref navigationNoPath), Interlocked.Read(ref navigationCancelled),
            Interlocked.Read(ref navigationExceptions));

        /// <summary>Records one AI driver update.</summary>
        internal static void RecordAIUpdate() { if (enabled) aiUpdates++; }
        /// <summary>Records one AI driver late update.</summary>
        internal static void RecordAILateUpdate() { if (enabled) aiLateUpdates++; }
        /// <summary>Records one AI driver fixed update.</summary>
        internal static void RecordAIFixedUpdate() { if (enabled) aiFixedUpdates++; }
        /// <summary>Records one behaviour-tree update.</summary>
        internal static void RecordTreeUpdate() { if (enabled) treeUpdates++; }
        /// <summary>Records one behaviour-tree late update.</summary>
        internal static void RecordTreeLateUpdate() { if (enabled) treeLateUpdates++; }
        /// <summary>Records one behaviour-tree fixed update.</summary>
        internal static void RecordTreeFixedUpdate() { if (enabled) treeFixedUpdates++; }
        /// <summary>Records one stack action update.</summary>
        internal static void RecordStackUpdate() { if (enabled) stackUpdates++; }
        /// <summary>Records one stack action late update.</summary>
        internal static void RecordStackLateUpdate() { if (enabled) stackLateUpdates++; }
        /// <summary>Records one stack action fixed update.</summary>
        internal static void RecordStackFixedUpdate() { if (enabled) stackFixedUpdates++; }
        /// <summary>Records one stack tick.</summary>
        internal static void RecordStackTick() { if (enabled) stackTicks++; }
        /// <summary>Records one action update.</summary>
        internal static void RecordActionUpdate() { if (enabled) actionUpdates++; }
        /// <summary>Records one action late update.</summary>
        internal static void RecordActionLateUpdate() { if (enabled) actionLateUpdates++; }
        /// <summary>Records one action fixed update.</summary>
        internal static void RecordActionFixedUpdate() { if (enabled) actionFixedUpdates++; }
        /// <summary>Records one service update pass.</summary>
        internal static void RecordServiceUpdate() { if (enabled) serviceUpdates++; }
        /// <summary>Records one path-planning request.</summary>
        internal static void RecordPathRequest() { if (enabled) Interlocked.Increment(ref pathRequests); }
        /// <summary>Records one expanded path search node.</summary>
        internal static void RecordPathExpandedNode() { if (enabled) Interlocked.Increment(ref pathExpandedNodes); }
        /// <summary>Records one entity query.</summary>
        internal static void RecordEntityQuery() { if (enabled) entityQueries++; }
        /// <summary>Records one entity-query candidate visit.</summary>
        internal static void RecordEntityCandidate() { if (enabled) entityCandidates++; }
        /// <summary>Records one movement-executor tick.</summary>
        internal static void RecordMovementTick() { if (enabled) movementTicks++; }
        /// <summary>Records one scheduler submission attempt, including a queue rejection.</summary>
        internal static void RecordNavigationSubmission() { if (enabled) Interlocked.Increment(ref navigationSubmissions); }
        /// <summary>Records one accepted scheduler work item and its queue depth.</summary>
        internal static void RecordNavigationAccepted(int queueDepth)
        {
            if (!enabled) return;
            Interlocked.Increment(ref navigationAccepted);
            Interlocked.Exchange(ref navigationQueueDepth, queueDepth);
            UpdateMaximum(ref navigationQueueDepthPeak, queueDepth);
        }
        /// <summary>Records one finite-queue rejection without retaining the rejected work.</summary>
        internal static void RecordNavigationQueueRejected() { if (enabled) Interlocked.Increment(ref navigationQueueRejected); }
        /// <summary>Records a dequeue and the resulting queue depth.</summary>
        internal static void RecordNavigationDequeued(int queueDepth)
        {
            if (enabled) Interlocked.Exchange(ref navigationQueueDepth, queueDepth);
        }
        /// <summary>Records worker start and the real enqueue-to-worker wait duration.</summary>
        internal static void RecordNavigationWorkerStarted(long queueWaitNanoseconds)
        {
            if (!enabled) return;
            Interlocked.Increment(ref navigationWorkerStarted);
            Interlocked.Add(ref navigationQueueWaitNanoseconds, queueWaitNanoseconds);
        }
        /// <summary>Records worker completion and the planner execution duration.</summary>
        internal static void RecordNavigationWorkerCompleted(long planningNanoseconds)
        {
            if (!enabled) return;
            Interlocked.Increment(ref navigationWorkerCompleted);
            Interlocked.Add(ref navigationPlanningNanoseconds, planningNanoseconds);
        }
        /// <summary>Records one terminal navigation route outcome.</summary>
        internal static void RecordNavigationRouteFound() { if (enabled) Interlocked.Increment(ref navigationRouteFound); }
        /// <summary>Records one ordinary no-path navigation outcome.</summary>
        internal static void RecordNavigationNoPath() { if (enabled) Interlocked.Increment(ref navigationNoPath); }
        /// <summary>Records one cancelled navigation outcome.</summary>
        internal static void RecordNavigationCancelled() { if (enabled) Interlocked.Increment(ref navigationCancelled); }
        /// <summary>Records one unexpected navigation exception.</summary>
        internal static void RecordNavigationException() { if (enabled) Interlocked.Increment(ref navigationExceptions); }

        /// <summary>Atomically raises a diagnostic maximum from concurrent worker threads.</summary>
        private static void UpdateMaximum(ref long target, long value)
        {
            long current = Interlocked.Read(ref target);
            while (value > current)
            {
                long observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current) return;
                current = observed;
            }
        }

        /// <summary>Immutable diagnostic counter values for one measurement window.</summary>
        public readonly struct Snapshot
        {
            internal Snapshot(
                long aiUpdates, long aiLateUpdates, long aiFixedUpdates,
                long treeUpdates, long treeLateUpdates, long treeFixedUpdates,
                long stackUpdates, long stackLateUpdates, long stackFixedUpdates, long stackTicks,
                long actionUpdates, long actionLateUpdates, long actionFixedUpdates, long serviceUpdates,
                long pathRequests, long pathExpandedNodes, long entityQueries, long entityCandidates,
                long movementTicks,
                long navigationSubmissions, long navigationAccepted, long navigationQueueRejected,
                long navigationWorkerStarted, long navigationWorkerCompleted, long navigationQueueDepth,
                long navigationQueueDepthPeak, long navigationQueueWaitNanoseconds,
                long navigationPlanningNanoseconds, long navigationRouteFound, long navigationNoPath,
                long navigationCancelled, long navigationExceptions)
            {
                AIUpdates = aiUpdates;
                AILateUpdates = aiLateUpdates;
                AIFixedUpdates = aiFixedUpdates;
                TreeUpdates = treeUpdates;
                TreeLateUpdates = treeLateUpdates;
                TreeFixedUpdates = treeFixedUpdates;
                StackUpdates = stackUpdates;
                StackLateUpdates = stackLateUpdates;
                StackFixedUpdates = stackFixedUpdates;
                StackTicks = stackTicks;
                ActionUpdates = actionUpdates;
                ActionLateUpdates = actionLateUpdates;
                ActionFixedUpdates = actionFixedUpdates;
                ServiceUpdates = serviceUpdates;
                PathRequests = pathRequests;
                PathExpandedNodes = pathExpandedNodes;
                EntityQueries = entityQueries;
                EntityCandidates = entityCandidates;
                MovementTicks = movementTicks;
                NavigationSubmissions = navigationSubmissions;
                NavigationAccepted = navigationAccepted;
                NavigationQueueRejected = navigationQueueRejected;
                NavigationWorkerStarted = navigationWorkerStarted;
                NavigationWorkerCompleted = navigationWorkerCompleted;
                NavigationQueueDepth = navigationQueueDepth;
                NavigationQueueDepthPeak = navigationQueueDepthPeak;
                NavigationQueueWaitNanoseconds = navigationQueueWaitNanoseconds;
                NavigationPlanningNanoseconds = navigationPlanningNanoseconds;
                NavigationRouteFound = navigationRouteFound;
                NavigationNoPath = navigationNoPath;
                NavigationCancelled = navigationCancelled;
                NavigationExceptions = navigationExceptions;
            }

            public long AIUpdates { get; }
            public long AILateUpdates { get; }
            public long AIFixedUpdates { get; }
            public long TreeUpdates { get; }
            public long TreeLateUpdates { get; }
            public long TreeFixedUpdates { get; }
            public long StackUpdates { get; }
            public long StackLateUpdates { get; }
            public long StackFixedUpdates { get; }
            public long StackTicks { get; }
            public long ActionUpdates { get; }
            public long ActionLateUpdates { get; }
            public long ActionFixedUpdates { get; }
            public long ServiceUpdates { get; }
            public long PathRequests { get; }
            public long PathExpandedNodes { get; }
            public long EntityQueries { get; }
            public long EntityCandidates { get; }
            public long MovementTicks { get; }
            public long NavigationSubmissions { get; }
            public long NavigationAccepted { get; }
            public long NavigationQueueRejected { get; }
            public long NavigationWorkerStarted { get; }
            public long NavigationWorkerCompleted { get; }
            public long NavigationQueueDepth { get; }
            public long NavigationQueueDepthPeak { get; }
            public long NavigationQueueWaitNanoseconds { get; }
            public long NavigationPlanningNanoseconds { get; }
            public long NavigationRouteFound { get; }
            public long NavigationNoPath { get; }
            public long NavigationCancelled { get; }
            public long NavigationExceptions { get; }
        }
    }
}
#endif
