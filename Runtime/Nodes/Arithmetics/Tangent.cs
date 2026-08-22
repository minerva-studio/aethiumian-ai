using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the tangent of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Tangent : Arithmetic
    {
        [Numeric]
        public VariableField a;
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (ArithmeticCompatibility.IsScalar(a.Type))
                {
                    if (HasNaN(a))
                        return State.Failed;

                    float value = Mathf.Tan(a.FloatValue);
                    return result.SetValue(value, failOnNaN)
                        ? State.Success
                        : State.Failed;
                }
                else
                    return State.Failed;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
