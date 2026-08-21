using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{

    [NodeTip("Do node subtraction")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Subtract : ComponentwiseArithmetic
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
                if (!IsComponentwiseBinaryOperationValid(a, b, result))
                {
                    return State.Failed;
                }

                if (result.Type == VariableType.Int || result.Type == VariableType.Float)
                {
                    if (EffectiveMode == ArithmeticMode.Int)
                    {
                        result.SetValue(a.IntScalarValue - b.IntScalarValue);
                    }
                    else
                    {
                        result.SetValue(a.ScalarValue - b.ScalarValue);
                    }
                }
                else
                {
                    result.SetVectorValue(a.ComponentwiseVectorValue - b.ComponentwiseVectorValue);
                }
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return State.Success;
        }
    }

}
