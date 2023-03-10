
namespace Amlos.AI
{
    /// <summary>
    /// Author: Wendell
    /// </summary>
    /// <summary>
    /// solution when behaviour tree encounter unexpected exception
    /// </summary>
    public enum BehaviourTreeErrorSolution
    {
        Pause,
        Restart,
        Throw,
    }

    public enum NodeErrorSolution
    {
        False,
        Pause,
        Throw,
    }
}
