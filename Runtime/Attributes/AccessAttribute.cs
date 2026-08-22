using System;

namespace Aethiumian.AI
{
    public abstract class AccessAttribute : Attribute
    {
        public AccessAttribute()
        {
        }
    }

    /// <summary>
    /// Indicates that the decorated variable is writable. (or should be writable)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
    public class WritableAttribute : AccessAttribute
    {
        public WritableAttribute()
        {
        }
    }

    /// <summary>
    /// Indicates that the decorated variable is readable. (or should be readable)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
    public class ReadableAttribute : AccessAttribute
    {
        public ReadableAttribute()
        {
        }
    }
}
