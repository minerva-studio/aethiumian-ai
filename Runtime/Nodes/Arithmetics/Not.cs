using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Not : Arithmetic
    {
        [Readable]
        public VariableReference a;

        [Writable]
        public VariableReference<bool> result;

        public override State Execute()
        {
            var result = !a.BoolValue;
            if (this.result.HasReference) this.result.SetValue(result);
            return StateOf(result);
        }
    }
}
