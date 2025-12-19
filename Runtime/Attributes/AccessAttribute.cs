using System;

namespace Amlos.AI
{
    public abstract class AccessAttribute : Attribute
    {
        public AccessAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class WritableAttribute : AccessAttribute
    {
        public WritableAttribute()
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class ReadableAttribute : AccessAttribute
    {
        public ReadableAttribute()
        {
        }
    }
}