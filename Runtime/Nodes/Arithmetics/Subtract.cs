using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{

    [NodeTip("Do node subtraction")]
    [Serializable]
    public sealed class Subtract : Arithmetic
    {
        [Readable]
        public VariableField a;
        [Readable]
        public VariableField b;
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            if (a.Type == VariableType.Bool || b.Type == VariableType.Bool)
            {
                return State.Failed;
            }
            if (a.Type == VariableType.String || b.Type == VariableType.String)
            {
                return State.Failed;
            }
            try
            {
                if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.SetValue(a.IntValue - b.IntValue);
                }
                else if (a.IsNumericLike && b.IsNumericLike) result.SetValue(a.NumericValue - b.NumericValue);
                else if (a.IsVector && b.IsVector) result.SetValue(a.VectorValue - b.VectorValue);
                else return State.Failed;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return State.Success;
        }
    }

}
