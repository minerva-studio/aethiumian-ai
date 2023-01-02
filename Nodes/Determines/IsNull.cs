using System;

namespace Amlos.AI
{
    [NodeTip("Check variable is null")]
    [Serializable]
    public sealed class IsNull : Determine
    {
        [TypeLimit(VariableType.Generic, VariableType.UnityObject)]
        public VariableReference variable;

        public override bool GetValue()
        {
            if (!variable.HasValue)
            {
                throw InvalidNodeException.VariableIsRequired(nameof(variable));
            }
            object value = variable.Value;
            return value is UnityEngine.Object obj ? !obj : value == null;
        }
    }
}
