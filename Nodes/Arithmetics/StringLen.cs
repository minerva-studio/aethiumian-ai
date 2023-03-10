using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class StringLen : Arithmetic
    {
        public VariableField<string> a;

        public VariableReference result;

        public override State Execute()
        {
            try
            {
                result.Value = a.StringValue.Length;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return State.Success;
        }
    }
}