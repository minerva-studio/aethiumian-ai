using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    public sealed class StringLen : Arithmetic
    {
        [Readable]
        public VariableField<string> a;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                result.SetValue(a.StringValue.Length);
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return State.Success;
        }
    }
}