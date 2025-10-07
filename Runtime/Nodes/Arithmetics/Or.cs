using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Or : Arithmetic
    {
        [Readable]
        public VariableReference a;
        [Readable]
        public VariableReference b;

        [Writable]
        public VariableReference<bool> result;

        public override State Execute()
        {
            var result = a.BoolValue || b.BoolValue;
            if (this.result.HasReference) this.result.SetValue(result);
            return StateOf(result);
        }
    }
}
