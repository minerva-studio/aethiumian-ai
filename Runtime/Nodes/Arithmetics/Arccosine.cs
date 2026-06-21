using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    public sealed class Arccosine : Arithmetic
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
                if (!a.IsNumericLike)
                    return State.Failed;

                float numericValue = a.NumericValue;
                if (numericValue > 1 || numericValue < -1)
                    return State.Failed;

                result.SetValue(Mathf.Acos(numericValue));
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }

        }
    }
}