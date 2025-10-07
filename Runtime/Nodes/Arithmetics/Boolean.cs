using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Return the boolean value from a variable")]
    public sealed class Boolean : Arithmetic
    {
        [Readable]
        public VariableReference boolean;

        public override State Execute()
        {
            if (!boolean.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(boolean)));
            }

            bool value;
            try
            {
                value = boolean.BoolValue;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return StateOf(value);
        }
    }

}
