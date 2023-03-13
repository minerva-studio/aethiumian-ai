using Amlos.AI.References;
using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Determine variable's type")]
    [Serializable]
    public sealed class IsTypeOf : Arithmetic
    {
        public VariableReference variable;
        public TypeReference type;

        public override State Execute()
        {
            if (!variable.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(variable)));
            }
            if (!type.HasReferType)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(type)));
            }
            return StateOf(variable.Value.GetType() == type.ReferType);
        }
    }
}
