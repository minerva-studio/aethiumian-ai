using System;

namespace Amlos.AI.Editor
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class CustomNodeDrawerAttribute : Attribute
    {
        public Type type;

        public CustomNodeDrawerAttribute(Type type)
        {
            this.type = type;
        }
    }
}