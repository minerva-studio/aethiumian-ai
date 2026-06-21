using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Arctangent : Arithmetic
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
                if (!a.IsNumeric)
                    return State.Failed;

                result.SetValue(Mathf.Atan(a.NumericValue));
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
