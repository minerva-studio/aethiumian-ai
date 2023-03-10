namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Result of execution in the tree node
    /// </summary>
    public enum State
    {
        /// <summary>
        /// Has no return value yet, because it is calling another node
        /// </summary>
        NONE_RETURN = -2,
        /// <summary>
        /// Error state
        /// </summary>
        Error = -1,
        /// <summary>
        /// Result true
        /// </summary>
        Success,
        /// <summary>
        /// Result false
        /// </summary>
        Failed,
        /// <summary>
        /// Wait until next update
        /// </summary>
        WaitUntilNextUpdate,
        /// <summary>
        /// Result wait (usually an action)
        /// </summary>
        Wait,
    }
}