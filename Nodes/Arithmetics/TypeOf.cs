using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Get variable's type")]
    [Serializable]
    public sealed class TypeOf : Arithmetic
    {
        public VariableReference variable;
        public VariableReference result;

        public override State Execute()
        {
            if (!variable.HasValue)
            {
                throw InvalidNodeException.VariableIsRequired(nameof(variable));
            }
            if (result.HasReference) result.Value = variable.Value?.GetType();
            return StateOf(variable.Value != null);
        }
    }
}
