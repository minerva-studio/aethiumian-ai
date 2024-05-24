using System;

namespace Amlos.AI
{
    /// <summary>
    /// Disabling a node to be called during service call phase
    /// </summary>
    [System.AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class DisableServiceCallAttribute : Attribute
    {
        public DisableServiceCallAttribute()
        {
        }
    }
}
