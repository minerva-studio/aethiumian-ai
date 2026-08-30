
namespace Aethiumian.AI
{
    /// <summary>
    /// Author: Wendell
    /// </summary>
    /// <summary>
    /// solution when behaviour tree encounter unexpected exception
    /// </summary>
    public enum BehaviourTreeErrorSolution
    {
        Fault,
        Restart,
        Throw,
    }

    public enum NodeErrorSolution
    {
        False,
        Fault,
        Throw,
    }
}
