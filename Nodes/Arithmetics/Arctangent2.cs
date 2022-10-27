using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Arctangent2 : Arithmetic
    {
        public VariableField a;
        public VariableField b;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if ((a.Type == VariableType.Int || a.Type == VariableType.Float) && (b.Type == VariableType.Int || b.Type == VariableType.Float))
                {
                    if (b.NumericValue == 0)
                    {
                        if (a.NumericValue > 0)
                        {
                            result.Value = Mathf.PI / 2;
                            End(true);
                        }
                        else if (a.NumericValue < 0)
                        {
                            result.Value = -Mathf.PI / 2;
                            End(true);
                        }
                        else
                            End(false);
                    }
                    else
                    {
                        result.Value = Mathf.Tan(a.NumericValue / b.NumericValue);
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