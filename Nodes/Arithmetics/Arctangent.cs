using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Arctangent : Arithmetic
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
                    result.Value = Mathf.Atan(a.NumericValue);
                    return State.Success;
                }
                else
                    return State.Failed;
            }
            catch (System.Exception)
            {
                return State.Failed;
            }

        }
    }
}
