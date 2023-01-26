using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public sealed class Arctangent2 : Arithmetic
    {
        [NumericTypeLimit]
        public VariableField y;

        [NumericTypeLimit]
        public VariableField x;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (y.IsNumeric && x.IsNumeric)
                {
                    if (x.NumericValue == 0)
                    {
                        if (y.NumericValue > 0)
                        {
                            result.Value = Mathf.PI / 2;
                            End(true);
                        }
                        else if (y.NumericValue < 0)
                        {
                            result.Value = -Mathf.PI / 2;
                            End(true);
                        }
                        else
                            End(false);
                    }
                    else
                    {
                        result.Value = Mathf.Atan2(y.NumericValue, x.NumericValue);
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