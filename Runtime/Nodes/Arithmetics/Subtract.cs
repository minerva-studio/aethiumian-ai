using Aethiumian.AI.Variables;
using System;
using UnityEngine;

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

                int componentCount = result.Type.ComponentCount();
                if (HasNaNOperands(a, b, componentCount))
                {
                    return State.Failed;
                }

                if (mode == ArithmeticMode.Int)
                {
                    result.SetComponentwiseValue(a.IntComponentwiseValue - b.IntComponentwiseValue);
                    return State.Success;
                }

                Vector4 value = a.ComponentwiseValue - b.ComponentwiseValue;
                return result.SetComponentwiseValue(value, failOnNaN)
                    ? State.Success
                    : State.Failed;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }

}
