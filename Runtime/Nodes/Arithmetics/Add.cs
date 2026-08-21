using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Do Variable addition")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Add : ComponentwiseArithmetic
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
                    return State.Success;
                }
                if (!IsComponentwiseBinaryOperationValid(a, b, result))
                {
                    return State.Failed;
                }

                if (result.Type == VariableType.Int || result.Type == VariableType.Float)
                {
                    if (EffectiveMode == ArithmeticMode.Int)
                    {
                        result.SetValue(a.IntScalarValue + b.IntScalarValue);
                    }
                    else
                    {
                        result.SetValue(a.ScalarValue + b.ScalarValue);
                    }
                }
                else
                {
                    result.SetVectorValue(a.ComponentwiseVectorValue + b.ComponentwiseVectorValue);
                }
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
