using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class And : Arithmetic
    {
        public VariableReference<bool> a;
        public VariableReference<bool> b;

        public VariableReference<bool> result;

        public override State Execute()
        {
            bool result = a && b;
            if (this.result.HasReference)
            {
                this.result.SetValue(result);
            }

            return StateOf(result);
        }
    }
}
