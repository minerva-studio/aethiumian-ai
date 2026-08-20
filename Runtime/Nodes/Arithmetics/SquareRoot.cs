using Aethiumian.AI.Variables;
using System;
using UnityEngine;
namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the square root of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class SquareRoot : Arithmetic
    {
        [Readable]
        [Constraint(VariableType.Float, VariableType.Int)]
        public VariableField a;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            if (!ArithmeticCompatibility.IsScalar(a.Type))
            {
                return State.Failed;
            }

            float value = a.FloatValue;
            if (value < 0)
            {
                return State.Failed;
            }
            try
            {
                if (a.Type == VariableType.Int)
                {
                    result.SetValue(Mathf.Sqrt(value));
                    return State.Success;
                }
                else if (a.Type == VariableType.Float)
                {
                    result.SetValue(Mathf.Sqrt(value));
                    return State.Success;
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
