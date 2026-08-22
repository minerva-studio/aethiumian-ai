using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the cosine of a numeric input.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Cosine : Arithmetic
    {
        [Numeric]
        [Readable]
        public VariableField a;

        [Numeric]
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (!a.IsNumeric)
                    return State.Failed;

                if (HasNaN(a))
                    return State.Failed;

                float value = Mathf.Cos(a.FloatValue);
                return result.SetValue(value, failOnNaN)
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
