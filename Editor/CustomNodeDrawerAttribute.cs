using Amlos.AI;
using Minerva.Module;
using System;
using UnityEditor;

namespace Amlos.Editor
{
    [System.AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class CustomNodeDrawerAttribute : Attribute
    {
        public Type type;

        public CustomNodeDrawerAttribute(Type type)
        {
            this.type = type;
        }
    }
}