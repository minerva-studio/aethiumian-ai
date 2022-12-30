using System;

namespace Amlos.AI
{
    [NodeTip("Get variable's type")]
    [Serializable]
    public sealed class TypeOf : Arithmetic
    {
        public VariableReference variable;
        public VariableReference result;

        public override void Execute()
        {
            if (!variable.HasRuntimeValue)
            {
                throw InvalidNodeException.VariableIsRequired(nameof(variable));
            }
            if (result.HasRuntimeReference) result.Value = variable.Value?.GetType();
            End(variable.Value != null);
        }
    }
}
