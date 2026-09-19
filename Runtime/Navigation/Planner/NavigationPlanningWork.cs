using System;
using System.Threading;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Represents one detached planner executed to completion by one background consumer.
    /// </summary>
    public interface INavigationPlanningWork : IDisposable
    {
        /// <summary>Executes the planner while observing cancellation at planner-owned loop boundaries.</summary>
        NavigationPlanResult Execute(CancellationToken cancellationToken);
    }
}
