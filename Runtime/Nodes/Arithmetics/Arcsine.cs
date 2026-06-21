using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
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
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
                {
                    if (a.NumericValue > 1 || a.NumericValue < -1)
                        return State.Failed;
                    else
                    {
                        result.SetValue(Mathf.Asin(a.NumericValue));
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
