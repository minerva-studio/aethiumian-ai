using System;

namespace Amlos.AI
{
    /// <summary>
    /// Shares a field when a node is instantiated for runtime execution.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class RuntimeSharedAttribute : Attribute
    {
    }
}
