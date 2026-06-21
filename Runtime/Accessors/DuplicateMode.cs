namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Selects the node-level duplicate strategy used by generated and reflection accessors.
    /// </summary>
    public enum DuplicateMode
    {
        /// <summary>
        /// Full deep clone for all supported data, creating new instances for mutable data and preserving Unity Object references.
        /// </summary>
        DeepClone,
        /// <summary>
        /// Instantiate a new node instance. Most field will duplicate according to DeepClone, but some fields with <see cref="RuntimeShared"/> attribute will be shared.
        /// </summary>
        Instantiate,
    }
}
