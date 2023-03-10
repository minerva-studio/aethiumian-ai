using Amlos.AI.Variables;
using System;
using System.Text;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Multiply : Arithmetic
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
            if (a.Type == VariableType.String && b.Type == VariableType.Float)
            {
                return State.Failed;
            }
            else if (a.Type == VariableType.Float && b.Type == VariableType.String)
            {
                return State.Failed;
            }
            // Vector-Vector multiplication should use Dot or Cross
            // However we would allow you to do it for mutiplying the vector components
            //if (a.IsVector && b.IsVector)
            //{
            //    return State.Failed;
            //    return;
            //}
            try
            {
                if (a.Type == VariableType.String && b.Type == VariableType.Int)
                {
                    var newString = new StringBuilder(a.StringValue.Length * b.IntValue).Insert(0, a.StringValue, b.IntValue).ToString();
                    result.Value = newString;
                }
                else if (a.Type == VariableType.Int && b.Type == VariableType.String)
                {
                    var newString = new StringBuilder(b.StringValue.Length * a.IntValue).Insert(0, b.StringValue, a.IntValue).ToString();
                    result.Value = newString;
                }
                else if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.Value = a.IntValue * b.IntValue;
                }
                else if (a.IsNumeric && b.IsNumeric) result.Value = a.NumericValue * b.NumericValue;
                else if (a.IsVector && b.IsNumeric) result.Value = a.VectorValue * b.NumericValue;
                else if (a.IsNumeric && b.IsVector) result.Value = a.NumericValue * b.VectorValue;
                return State.Success;

            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }
        }
    }

}
