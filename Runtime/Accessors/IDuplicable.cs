namespace Amlos.AI.Accessors
{
    /// <summary>
    /// Marks a runtime value as supporting the deep clone used by duplicate flows.
    /// </summary>
    public interface IDuplicable
    {
        /// <summary>
        /// Creates an independent deep clone of this value.
        /// </summary>
        /// <returns>A duplicated value equivalent to the source.</returns>
        object Duplicate();
    }
}
