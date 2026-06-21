using System;

namespace Aethiumian.AI
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class AIInspectorIgnoreAttribute : Attribute
    {
    }
}