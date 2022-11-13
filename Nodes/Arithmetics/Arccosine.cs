using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Arccosine : Arithmetic
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
                    if (a.NumericValue > 1 || a.NumericValue < -1)
                        End(false);
                    else
                    {
                        result.Value = Mathf.Acos(a.NumericValue);
                        End(true);
                    }
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