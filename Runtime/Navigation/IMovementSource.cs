namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Exposes whether an AI control target may issue intentional movement.
    /// </summary>
    public interface IMovementSource
    {
        /// <summary>
        /// Gets whether intentional movement is currently permitted.
        /// </summary>
        bool CanMove { get; }
    }
}
