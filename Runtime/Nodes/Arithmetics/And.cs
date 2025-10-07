using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
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
