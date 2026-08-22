using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the angle from two numeric inputs.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Arctangent2 : Arithmetic
    {
        [Numeric]
        [Readable]
        public VariableField y;
        [Numeric]
        [Readable]
        public VariableField x;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (!y.IsNumeric || !x.IsNumeric)
                    return State.Failed;

                if (HasNaN(y) || HasNaN(x))
                    return State.Failed;

                float xValue = x.FloatValue;
                float yValue = y.FloatValue;
                if (xValue == 0)
                {
                    if (yValue > 0)
                    {
                        return result.SetValue(Mathf.PI / 2, failOnNaN)
                            ? State.Success
                            : State.Failed;
                    }
                    else if (yValue < 0)
                    {
                        return result.SetValue(-Mathf.PI / 2, failOnNaN)
                            ? State.Success
                            : State.Failed;
                    }
                    else return State.Failed;
                }
                else
                {
                    float resultValue = Mathf.Atan2(yValue, xValue);
                    return result.SetValue(resultValue, failOnNaN)
                        ? State.Success
                        : State.Failed;
                }
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }

        }
    }
}
