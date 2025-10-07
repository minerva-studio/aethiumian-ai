using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Do Variable addition")]
    [Serializable]
    public sealed class Add : Arithmetic
    {
        [Readable]
        public VariableField a;
        [Readable]
        public VariableField b;
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (a.Type == VariableType.String || b.Type == VariableType.String)
                {
                    result.SetValue(a.StringValue + b.StringValue);
                }
                else if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.SetValue(a.IntValue + b.IntValue);
                }
                else if (a.IsNumericLike && b.IsNumericLike) result.SetValue(a.NumericValue + b.NumericValue);
                else if (a.IsVector && b.IsVector) result.SetValue(a.VectorValue + b.VectorValue);
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
