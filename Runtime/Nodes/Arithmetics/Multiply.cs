using Aethiumian.AI.Variables;
using System;
using System.Text;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Multiplies two numeric values or vectors.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Multiply : ComponentwiseArithmetic
    {
        [Readable]
        public VariableField a;
        [Readable]
        public VariableField b;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
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
                    result.SetValue(newString);
                    return State.Success;
                }
                else if (a.Type == VariableType.Int && b.Type == VariableType.String)
                {
                    var newString = new StringBuilder(b.StringValue.Length * a.IntValue).Insert(0, b.StringValue, a.IntValue).ToString();
                    result.SetValue(newString);
                    return State.Success;
                }

                if (!TryResolveOperationMode(a, b, result, out var mode))
                {
                    return State.Failed;
                }

                if (mode == ArithmeticMode.Int)
                {
                    result.SetComponentwiseValue(a.IntComponentwiseValue * b.IntComponentwiseValue);
                }
                else
                {
                    result.SetComponentwiseValue(Vector4.Scale(a.ComponentwiseValue, b.ComponentwiseValue));
                }

                return State.Success;
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }
        }
    }

}
