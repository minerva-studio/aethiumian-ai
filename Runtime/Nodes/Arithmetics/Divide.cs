using Aethiumian.AI.Variables;
using System;
using UnityEngine;
namespace Aethiumian.AI.Nodes
{
    [NodeTip("Divides one numeric value or vector by another.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Divide : ComponentwiseArithmetic
    {
        [Exclude(VariableType.String)]
        [Readable]
        public VariableField a;

        [Exclude(VariableType.String)]
        [Readable]
        public VariableField b;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
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
                        result.SetValue(a.IntScalarValue / b.IntScalarValue);
                    }
                    else
                    {
                        result.SetValue(a.ScalarValue / b.ScalarValue);
                    }
                }
                else
                {
                    Vector4 left = a.ComponentwiseVectorValue;
                    Vector4 right = b.ComponentwiseVectorValue;
                    result.SetVectorValue(new Vector4(
                        left.x / right.x,
                        left.y / right.y,
                        left.z / right.z,
                        left.w / right.w));
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
