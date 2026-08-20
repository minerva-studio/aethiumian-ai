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

                float xValue = x.FloatValue;
                float yValue = y.FloatValue;
                if (xValue == 0)
                {
                    if (yValue > 0)
                    {
                        result.SetValue(Mathf.PI / 2);
                        return State.Success;
                    }
                    else if (yValue < 0)
                    {
                        result.SetValue(-Mathf.PI / 2);
                        return State.Success;
                    }
                    else return State.Failed;
                }
                else
                {
                    result.SetValue(Mathf.Atan2(yValue, xValue));
                    return State.Success;
                }
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }

        }
    }
}
