using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
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
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
                {
                    result.SetValue(Mathf.Tan(a.NumericValue));
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
