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
                    result.SetValue(Mathf.Tan(a.FloatValue));
                    return State.Success;
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
