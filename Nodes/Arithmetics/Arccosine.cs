using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Arccosine : Arithmetic
    {
        public VariableField a;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
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