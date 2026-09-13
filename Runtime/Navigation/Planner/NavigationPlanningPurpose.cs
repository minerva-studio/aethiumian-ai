#if UNITY_EDITOR || DEVELOPMENT_BUILD
#endif

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Identifies the delivery contract of a navigation request. Initial routes may publish
    /// a verified executable prefix; endpoint continuations must accumulate a complete tail
    /// or reach an explicit exhausted/budget terminal state before publishing.
    /// </summary>
    public enum NavigationPlanningPurpose
    {
        InitialRoute,
        EndpointContinuation,
    }
}
