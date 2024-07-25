using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Not : Arithmetic
    {
        public VariableReference a;

        public VariableReference<bool> result;

        public override State Execute()
        {
            var result = !a.BoolValue;
            if (this.result.HasReference)
            {
                this.result.Value = result;
            }
            return StateOf(result);
        }
    }
}
