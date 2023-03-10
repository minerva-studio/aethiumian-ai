using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Return the boolean value from a variable")]
    public sealed class Boolean : Arithmetic
    {
        public VariableReference boolean;

        public override void Execute()
        {
            bool value;
            try
            {
                value = boolean.BoolValue;
            }
            catch (Exception)
            {
                value = false;
            }
            End(value);
        }
    }

}
