using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns one detached planner callback and its request-local result.</summary>
    internal sealed class PlannerWork : INavigationPlanningWork
    {
        private Func<CancellationToken, NavigationPlanResult> planner;

        /// <summary>Creates work around a pure planner callback.</summary>
        public PlannerWork(Func<CancellationToken, NavigationPlanResult> planner)
        {
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
        }

        /// <summary>Executes the owned callback exactly once.</summary>
        public NavigationPlanResult Execute(CancellationToken cancellationToken)
        {
            Func<CancellationToken, NavigationPlanResult> owned = Interlocked.Exchange(ref planner, null);
            if (owned == null) throw new InvalidOperationException("Planning work was already executed.");
            cancellationToken.ThrowIfCancellationRequested();
            return owned(cancellationToken);
        }

        /// <summary>Releases an unexecuted callback when queued work is cancelled.</summary>
        public void Dispose() => Interlocked.Exchange(ref planner, null);
    }
}
