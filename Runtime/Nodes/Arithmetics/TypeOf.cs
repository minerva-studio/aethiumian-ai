using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Get variable's type")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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
