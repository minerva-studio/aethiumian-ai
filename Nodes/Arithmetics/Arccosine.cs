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
                if (a.IsNumeric)
                {
                    if (a.NumericValue > 1 || a.NumericValue < -1)
                        return State.Failed;
                    else
                    {
                        result.Value = Mathf.Acos(a.NumericValue);
                        return State.Success;
                    }
                }
                else
                    return State.Failed;
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }

        }
    }
}