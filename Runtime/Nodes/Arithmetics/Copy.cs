using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
    [NodeTip("Copy value of one variable to another")]
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
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(from)));
            }

            if (!to.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(to)));
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
