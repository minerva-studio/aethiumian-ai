using System;

namespace Amlos.AI
{
    [NodeTip("Get variable's type")]
    [Serializable]
    public sealed class IsTypeOf : ComparableDetermine<object>
    {
        public VariableReference variable;

        public override object GetValue()
        {
            if (!variable.HasRuntimeValue || result.HasRuntimeReference)
            {
                throw InvalidNodeException.VariableIsRequired(nameof(variable));
            }
            return variable.Value?.GetType();
        }
    }
}
