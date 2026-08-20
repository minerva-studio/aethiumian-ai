using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the arcsine of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Arcsine : Arithmetic
    {
        [Numeric]
        [Readable]
        public VariableField a;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (ArithmeticCompatibility.IsScalar(a.Type))
                {
                    float value = a.FloatValue;
                    if (value > 1 || value < -1)
                        return State.Failed;
                    else
                    {
                        result.SetValue(Mathf.Asin(value));
                        return State.Success;
                    }
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
