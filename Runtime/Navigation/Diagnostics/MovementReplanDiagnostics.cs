#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Threading;

namespace Aethiumian.AI.Navigation.Diagnostics
{
    /// <summary>
    /// Temporary opt-in counters for diagnosing Movement replanning churn.
    /// This data is never part of behaviour-tree or gameplay state.
    /// </summary>
    public static class MovementReplanDiagnostics
    {
        #region Temporary Navigation Replan Diagnostics

        private static bool enabled;
        private static long goalChecks;
        private static long goalUnchanged;
        private static long goalChanges;
        private static long goalPendingCancellations;
        private static long goalImmediateRetries;
        private static long anchorMoves;
        private static long continuationReplans;
        private static long rejectedReplans;
        private static long unexpectedLandingReplans;
        private static long exhaustedReplans;
        private static long initialPlans;
        private static long targetChangedReplans;
        private static long routeInvalidationReplans;
        private static long movementSuccesses;
        private static long movementFailures;
        private static long movementStalls;
        private static long navigationRestarts;

        /// <summary>Gets or sets whether temporary Movement counters are active.</summary>
        public static bool Enabled { get => enabled; set => enabled = value; }

        /// <summary>Clears temporary Movement counters before a measurement window.</summary>
        public static void Reset()
        {
            goalChecks = 0;
            goalUnchanged = 0;
            goalChanges = 0;
            goalPendingCancellations = 0;
            goalImmediateRetries = 0;
            anchorMoves = 0;
            continuationReplans = 0;
            rejectedReplans = 0;
            unexpectedLandingReplans = 0;
            exhaustedReplans = 0;
            initialPlans = 0;
            targetChangedReplans = 0;
            routeInvalidationReplans = 0;
            movementSuccesses = 0;
            movementFailures = 0;
            movementStalls = 0;
            navigationRestarts = 0;
        }

        /// <summary>Captures a read-only snapshot of temporary Movement counters.</summary>
        public static Snapshot Capture() => new(
            goalChecks, goalUnchanged, goalChanges, goalPendingCancellations,
            goalImmediateRetries, anchorMoves, continuationReplans,
            rejectedReplans, unexpectedLandingReplans, exhaustedReplans, initialPlans,
            targetChangedReplans, routeInvalidationReplans, movementSuccesses,
            movementFailures, movementStalls, navigationRestarts);

        /// <summary>Records one rolling-navigation goal comparison.</summary>
        public static void RecordGoalCheck(bool changed)
        {
            if (!enabled) return;
            Interlocked.Increment(ref goalChecks);
            if (changed) Interlocked.Increment(ref goalChanges);
            else Interlocked.Increment(ref goalUnchanged);
        }

        /// <summary>Records cancellation of a pending request because its goal changed.</summary>
        public static void RecordGoalPendingCancellation() { if (enabled) Interlocked.Increment(ref goalPendingCancellations); }
        /// <summary>Records a goal-change path that permits an immediate replacement request.</summary>
        public static void RecordGoalImmediateRetry() { if (enabled) Interlocked.Increment(ref goalImmediateRetries); }
        /// <summary>Records a physical navigation-anchor movement.</summary>
        public static void RecordAnchorMove() { if (enabled) Interlocked.Increment(ref anchorMoves); }
        /// <summary>Records replanning after a normal route continuation boundary.</summary>
        public static void RecordContinuationReplan() { if (enabled) Interlocked.Increment(ref continuationReplans); }
        /// <summary>Records replanning after a route segment was rejected.</summary>
        public static void RecordRejectedReplan() { if (enabled) Interlocked.Increment(ref rejectedReplans); }
        /// <summary>Records replanning after an unexpected landing.</summary>
        public static void RecordUnexpectedLandingReplan() { if (enabled) Interlocked.Increment(ref unexpectedLandingReplans); }
        /// <summary>Records replanning after an incomplete route is exhausted.</summary>
        public static void RecordExhaustedReplan() { if (enabled) Interlocked.Increment(ref exhaustedReplans); }
        /// <summary>Records the first planning request of one Movement execution.</summary>
        public static void RecordInitialPlan() { if (enabled) Interlocked.Increment(ref initialPlans); }
        /// <summary>Records replanning caused by a changed navigation goal.</summary>
        public static void RecordTargetChangedReplan() { if (enabled) Interlocked.Increment(ref targetChangedReplans); }
        /// <summary>Records replanning after the active route became unusable.</summary>
        public static void RecordRouteInvalidationReplan() { if (enabled) Interlocked.Increment(ref routeInvalidationReplans); }
        /// <summary>Records successful completion of one Movement execution.</summary>
        public static void RecordMovementSuccess() { if (enabled) Interlocked.Increment(ref movementSuccesses); }
        /// <summary>Records failed completion of one Movement execution.</summary>
        public static void RecordMovementFailure() { if (enabled) Interlocked.Increment(ref movementFailures); }
        /// <summary>Records one traversal watchdog stall termination.</summary>
        public static void RecordMovementStall() { if (enabled) Interlocked.Increment(ref movementStalls); }
        /// <summary>Records one restart from the physically observed navigation state.</summary>
        public static void RecordNavigationRestart() { if (enabled) Interlocked.Increment(ref navigationRestarts); }

        /// <summary>Immutable temporary Movement diagnostic values for one window.</summary>
        public readonly struct Snapshot
        {
            public Snapshot(long goalChecks, long goalUnchanged, long goalChanges,
                long goalPendingCancellations, long goalImmediateRetries, long anchorMoves,
                long continuationReplans, long rejectedReplans, long unexpectedLandingReplans,
                long exhaustedReplans, long initialPlans, long targetChangedReplans,
                long routeInvalidationReplans, long movementSuccesses, long movementFailures,
                long movementStalls, long navigationRestarts)
            {
                GoalChecks = goalChecks;
                GoalUnchanged = goalUnchanged;
                GoalChanges = goalChanges;
                GoalPendingCancellations = goalPendingCancellations;
                GoalImmediateRetries = goalImmediateRetries;
                AnchorMoves = anchorMoves;
                ContinuationReplans = continuationReplans;
                RejectedReplans = rejectedReplans;
                UnexpectedLandingReplans = unexpectedLandingReplans;
                ExhaustedReplans = exhaustedReplans;
                InitialPlans = initialPlans;
                TargetChangedReplans = targetChangedReplans;
                RouteInvalidationReplans = routeInvalidationReplans;
                MovementSuccesses = movementSuccesses;
                MovementFailures = movementFailures;
                MovementStalls = movementStalls;
                NavigationRestarts = navigationRestarts;
            }

            public long GoalChecks { get; }
            public long GoalUnchanged { get; }
            public long GoalChanges { get; }
            public long GoalPendingCancellations { get; }
            public long GoalImmediateRetries { get; }
            public long AnchorMoves { get; }
            public long ContinuationReplans { get; }
            public long RejectedReplans { get; }
            public long UnexpectedLandingReplans { get; }
            public long ExhaustedReplans { get; }
            public long InitialPlans { get; }
            public long TargetChangedReplans { get; }
            public long RouteInvalidationReplans { get; }
            public long MovementSuccesses { get; }
            public long MovementFailures { get; }
            public long MovementStalls { get; }
            public long NavigationRestarts { get; }
        }

        #endregion
    }
}
#endif
