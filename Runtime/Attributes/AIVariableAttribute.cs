using Minerva.Module;
using System;

namespace Amlos.AI
{
    [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class AIVariableAttribute : Attribute
    {
        public string name;
        public UUID? uuid;

        public AIVariableAttribute()
        {
        }

        public AIVariableAttribute(string name = null, UUID? uuid = null)
        {
            this.name = name;
            this.uuid = uuid;
        }
    }
}
