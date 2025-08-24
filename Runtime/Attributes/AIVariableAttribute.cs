using Amlos.AI.Variables;
using Minerva.Module;
using System;

namespace Amlos.AI
{
    [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class AIVariableAttribute : Attribute
    {
        public string name;
        public UUID uuid;

        public AIVariableAttribute(string name = null)
        {
            this.name = name;
            this.uuid = VariableUtility.CreateStableUUID(name);
        }
    }
}
