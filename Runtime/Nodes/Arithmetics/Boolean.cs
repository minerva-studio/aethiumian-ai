using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Return the boolean value from a variable")]
    public sealed class Boolean : Arithmetic
    {
        [Readable]
        public VariableReference boolean;

        public bool ReadValue()
        {
            if (boolean == null || !boolean.HasValue)
            {
                throw InvalidNodeException.VariableIsRequired(nameof(boolean), this);
            }

            return boolean.BoolValue;
        }

        public override State Execute()
        {
            try
            {
                return StateOf(ReadValue());
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }

}
