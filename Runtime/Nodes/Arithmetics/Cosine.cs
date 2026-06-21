using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
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

                result.SetValue(Mathf.Cos(a.NumericValue));
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
