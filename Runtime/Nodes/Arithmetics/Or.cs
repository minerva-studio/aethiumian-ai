using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Or : Arithmetic
    {
        public VariableReference a;
        public VariableReference b;

        public VariableReference<bool> result;

        public override State Execute()
        {
            var result = a.BoolValue || b.BoolValue;
            if (this.result.HasReference) this.result.SetValue(result);
            return StateOf(result);
        }
    }
}
