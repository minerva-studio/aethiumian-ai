using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Arctangent2 : Arithmetic
    {
        [Numeric]
        [Readable]
        public VariableField y;
        [Numeric]
        [Readable]
        public VariableField x;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (!y.IsNumeric || !x.IsNumeric)
                    return State.Failed;

                if (x.NumericValue == 0)
                {
                    if (y.NumericValue > 0)
                    {
                        result.SetValue(Mathf.PI / 2);
                        return State.Success;
                    }
                    else if (y.NumericValue < 0)
                    {
                        result.SetValue(-Mathf.PI / 2);
                        return State.Success;
                    }
                    else return State.Failed;
                }
                else
                {
                    result.SetValue(Mathf.Atan2(y.NumericValue, x.NumericValue));
                    return State.Success;
                }
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }

        }
    }
}