using Aethiumian.AI.Variables;
using System;
using UnityEngine;

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
                if (!TryResolveOperationMode(a, b, result, out var mode))
                {
                    return State.Failed;
                }

                if (mode == ArithmeticMode.Int)
                {
                    result.SetComponentwiseValue(a.IntComponentwiseValue + b.IntComponentwiseValue);
                }
                else
                {
                    result.SetComponentwiseValue(a.ComponentwiseValue + b.ComponentwiseValue);
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
