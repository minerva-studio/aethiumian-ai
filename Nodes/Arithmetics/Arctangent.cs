using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public sealed class Arctangent : Arithmetic
    {
        [NumericTypeLimit]
        public VariableField a;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.IsNumeric)
                {
                    result.Value = Mathf.Atan(a.NumericValue);
                    End(true);
                }
                else
                    End(false);
            }
            catch (System.Exception)
            {
                End(false);
                throw;
            }

        }
    }
}
