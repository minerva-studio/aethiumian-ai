using Amlos.AI.References;
using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Determine variable's type")]
    [Serializable]
    public sealed class IsTypeOf : Determine
    {
        [Readable]
        public VariableReference variable;
        [Readable]
        public GenericTypeReference type;

        public override Exception IsValidNode()
        {
            if (!variable.HasValue)
            {
                return InvalidNodeException.VariableIsRequired(nameof(variable));
            }
            if (!type.HasReferType)
            {
                return InvalidNodeException.VariableIsRequired(nameof(type));
            }
            return null;
        }

        public override bool GetValue()
        {
            return variable.Value.GetType() == type.ReferType;
        }
    }
}
