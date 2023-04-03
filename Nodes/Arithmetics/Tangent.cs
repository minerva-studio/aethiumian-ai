using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Tangent : Arithmetic
    {
        [Numeric]
        public VariableField a;

        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
                {
                    result.Value = Mathf.Tan(a.NumericValue);
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
