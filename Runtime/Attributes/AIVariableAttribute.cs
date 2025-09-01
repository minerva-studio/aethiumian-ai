using Amlos.AI.Variables;
using Minerva.Module;
using System;

namespace Amlos.AI
{
    [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class AIVariableAttribute : Attribute
    {
        private readonly string name;
        private readonly UUID uuid;

        public string Name => name;
        public UUID UUID => uuid;

        public AIVariableAttribute(string name)
        {
            this.name = name;
            this.uuid = VariableUtility.CreateStableUUID(name);
        }
    }
}
