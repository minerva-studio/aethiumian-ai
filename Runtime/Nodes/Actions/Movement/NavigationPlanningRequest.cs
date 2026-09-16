using Aethiumian.AI.Navigation;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Immutable planning context and its separately releasable cancellation resource.</summary>
    internal sealed class NavigationPlanningRequest
    {
        private CancellationTokenSource cancellation;
        private int staleTicks;
        private const int StaleRefreshTicks = 8;

        internal NavigationPlanningRequest(NavigationPlanningOperation operation, Vector2 start,
            NavigationGoalRequest goal, NavigationPlanningPurpose purpose,
            NavigationRouteSegment committedSegment, CancellationTokenSource cancellation)
        {
            Operation = operation;
            Start = start;
            Goal = goal;
            Purpose = purpose;
            CommittedSegment = committedSegment;
            this.cancellation = cancellation;
        }

        /// <summary>The asynchronous result owned by this request.</summary>
        public NavigationPlanningOperation Operation { get; }
        /// <summary>The execution anchor captured when planning began.</summary>
        public Vector2 Start { get; }
        /// <summary>The immutable goal captured for this request.</summary>
        public NavigationGoalRequest Goal { get; }
        /// <summary>Whether this request starts movement or supplies its continuation.</summary>
        public NavigationPlanningPurpose Purpose { get; }
        /// <summary>The action this continuation follows, or null for an initial route.</summary>
        public NavigationRouteSegment CommittedSegment { get; }

        /// <summary>Advances or clears the request-local stale interval and reports when refresh is due.</summary>
        internal bool AdvanceStaleness(bool stale)
        {
            staleTicks = stale ? System.Math.Min(staleTicks + 1, StaleRefreshTicks) : 0;
            return staleTicks >= StaleRefreshTicks;
        }
        internal void Release(bool cancel)
        {
            CancellationTokenSource resource = cancellation;
            cancellation = null;
            if (resource == null) return;
            try
            {
                if (cancel) resource.Cancel();
            }
            finally
            {
                resource.Dispose();
            }
        }
    }
}
