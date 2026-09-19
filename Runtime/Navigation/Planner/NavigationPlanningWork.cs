using System;
using System.Threading;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Represents detached planner work executed by a scheduler worker.
    /// Implementations must be safe for background execution, must not retain Unity scene owners,
    /// and must not call Unity APIs that require the main thread.
    /// </summary>
    public interface INavigationPlanningWork : IDisposable
    {
        /// <summary>Executes the planner while observing cancellation at planner-owned loop boundaries.</summary>
        NavigationPlanResult Execute(CancellationToken cancellationToken);
    }
}
