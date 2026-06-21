using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Check variable is null")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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
