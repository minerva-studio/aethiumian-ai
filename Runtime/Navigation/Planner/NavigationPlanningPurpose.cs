#if UNITY_EDITOR || DEVELOPMENT_BUILD
#endif

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Identifies whether planning begins at the initial anchor or the endpoint of a committed action.
    /// It does not change result delivery: Route planning publishes only a complete route or its
    /// exhausted/budget terminal result, while NextAction is explicitly a single local result.
    /// </summary>
    public enum NavigationPlanningPurpose
    {
        InitialRoute,
        EndpointContinuation,
    }
}
