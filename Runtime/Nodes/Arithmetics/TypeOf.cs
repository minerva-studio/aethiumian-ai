using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Get variable's type")]
    [Serializable]
    public sealed class TypeOf : Arithmetic
    {
        [Readable]
        public VariableReference variable;
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            if (!variable.HasValue)
            {
                throw InvalidNodeException.VariableIsRequired(nameof(variable), this);
            }
            if (result.HasReference) result.SetValue(variable.Value?.GetType());
            return StateOf(variable.Value != null);
        }
    }
}
