namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Selects the node-level duplicate strategy used by generated descriptors.
    /// </summary>
    public enum DuplicateMode
    {
        /// <summary>
        /// Full duplicate for all supported data, creating new instances for mutable data and preserving Unity Object references.
        /// </summary>
        Duplicate,
        /// <summary>
        /// Instantiate a new node instance. Most fields are duplicated, but fields with <see cref="RuntimeSharedAttribute"/> are shared.
        /// </summary>
        Instantiate,
    }
}
