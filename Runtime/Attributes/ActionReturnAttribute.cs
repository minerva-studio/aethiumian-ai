using System;

namespace Aethiumian.AI
{
    /// <summary>
    /// Attribute to mark methods as valid return types for <see cref="FunctionAction"/> nodes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ActionReturnAttribute : Attribute
    {
    }
}