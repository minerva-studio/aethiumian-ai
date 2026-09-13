using Aethiumian.AI.Navigation;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Immutable planning context and its separately releasable cancellation resource.</summary>
    internal sealed class NavigationPlanningRequest
    {
        private CancellationTokenSource cancellation;

        internal NavigationPlanningRequest(NavigationPlanningOperation operation, Vector2 start,
            NavigationGoalRegion goal, NavigationPlanningPurpose purpose,
            NavigationRouteSegment committedSegment, CancellationTokenSource cancellation)
        {
            Operation = operation;
            Start = start;
            GoalRegion = goal;
            Purpose = purpose;
            CommittedSegment = committedSegment;
            this.cancellation = cancellation;
        }

        /// <summary>The asynchronous result owned by this request.</summary>
        public NavigationPlanningOperation Operation { get; }
        /// <summary>The execution anchor captured when planning began.</summary>
        public Vector2 Start { get; }
        /// <summary>The immutable target and world captured for this request.</summary>
        public NavigationGoalRegion GoalRegion { get; }
        /// <summary>Whether this request starts movement or supplies its continuation.</summary>
        public NavigationPlanningPurpose Purpose { get; }
        /// <summary>The action this continuation follows, or null for an initial route.</summary>
        public NavigationRouteSegment CommittedSegment { get; }

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
