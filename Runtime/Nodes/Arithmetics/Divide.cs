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
                if (!TryResolveOperationMode(a, b, result, out var mode))
                {
                    return State.Failed;
                }

                if (mode == ArithmeticMode.Int)
                {
                    int componentCount = result.Type.ComponentCount();
                    result.SetComponentwiseValue(ComponentwiseInt4.Divide(a.IntComponentwiseValue, b.IntComponentwiseValue, componentCount));
                }
                else
                {
                    Vector4 left = a.ComponentwiseValue;
                    Vector4 right = b.ComponentwiseValue;
                    result.SetComponentwiseValue(new Vector4(left.x / right.x, left.y / right.y, left.z / right.z, left.w / right.w));
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
