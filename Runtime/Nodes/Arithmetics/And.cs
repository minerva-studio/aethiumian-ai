using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class And : Arithmetic
    {
        [Readable]
        public VariableReference<bool> a;
        [Readable]
        public VariableReference<bool> b;

        [Writable]
        public VariableReference<bool> result;

        public override State Execute()
        {
            bool result = a && b;
            if (this.result.HasReference) this.result.SetValue(result);

            return StateOf(result);
        }
    }
}
