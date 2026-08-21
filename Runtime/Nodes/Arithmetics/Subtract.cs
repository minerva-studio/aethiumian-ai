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

                if (mode == ArithmeticMode.Int)
                {
                    result.SetComponentwiseValue(a.IntComponentwiseValue - b.IntComponentwiseValue);
                }
                else
                {
                    result.SetComponentwiseValue(a.ComponentwiseValue - b.ComponentwiseValue);
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
