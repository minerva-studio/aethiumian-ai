using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{

    [NodeTip("Do node subtraction")]
    [Serializable]
    public sealed class Subtract : Arithmetic
    {
        public VariableField a;
        public VariableField b;
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
                    result.Value = a.IntValue - b.IntValue;
                }
                else if (a.IsNumeric && b.IsNumeric) result.Value = a.NumericValue - b.NumericValue;
                else if (a.IsVector && b.IsVector) result.Value = a.VectorValue - b.VectorValue;
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
