using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Arccosine : Arithmetic
    {
        [NumericTypeLimit]
        public VariableField a;
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (!a.IsNumericLike)
                    return State.Failed;

                float numericValue = a.NumericValue;
                if (numericValue > 1 || numericValue < -1)
                    return State.Failed;

                result.Value = Mathf.Acos(numericValue);
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }

        }
    }
}