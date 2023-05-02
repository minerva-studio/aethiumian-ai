using System;
namespace Amlos.AI.Editor
{
    /// <summary>
    /// Attribute for a custom AI Node drawer
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public class CustomNodeDrawerAttribute : Attribute
    {
        public Type type;

        public CustomNodeDrawerAttribute(Type type)
        {
            this.type = type;
        }
    }
}