using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Check variable is null")]
    [Serializable]
    public sealed class IsNull : Determine
    {
        [Readable]
        [Constraint(VariableType.Generic, VariableType.UnityObject)]
        public VariableReference variable;

        public override Exception IsValidNode()
        {
            if (!variable.HasValue)
            {
                return InvalidNodeException.VariableIsRequired(nameof(variable), this);
            }
            return null;
        }

        public override bool GetValue()
        {
            object value = variable.Value;
            return value is UnityEngine.Object obj ? !obj : value == null;
        }
    }
}
