using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("Copy value of one variable to another")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Copy : Arithmetic
    {
        [Readable]
        public VariableField from;
        [Writable]
        public VariableReference to;

        public override State Execute()
        {
            if (!from.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(from), this));
            }

            if (!to.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(to), this));
            }

            try
            {
                to.SetValue(from.Value);
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }

}
