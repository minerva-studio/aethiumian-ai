using System;
using System.Threading;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#endif

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Represents one detached planner executed to completion by one background consumer.
    /// </summary>
    internal interface INavigationPlanningWork : IDisposable
    {
        /// <summary>Executes the planner while observing cancellation at planner-owned loop boundaries.</summary>
        NavigationPlanResult Execute(CancellationToken cancellationToken);
    }

    /// <summary>
    /// Detached worker completion notification consumed by the Map-owned main-thread drain.
    /// </summary>
    internal readonly struct NavigationPlanningCompletion
    {
        public readonly NavigationPlanningOperation Operation;

        /// <summary>Creates one completion notification without capturing a Unity owner.</summary>
        public NavigationPlanningCompletion(NavigationPlanningOperation operation)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }
    }
}
