using Amlos.AI.Variables;
using System;
using UnityEngine;
namespace Amlos.AI
{
    [Serializable]
    public sealed class SquareRoot : Arithmetic
    {
        [TypeLimit(VariableType.Float, VariableType.Int)]
        public VariableField a;
        public VariableReference result;

        public override void Execute()
        {
            if (a.Type == VariableType.Bool)
            {
                End(false);
                return;
            }
            if (a.Type == VariableType.String)
            {
                End(false);
                return;
            }

            else if (a.NumericValue < 0)
            {
                End(false);
                return;
            }
            try
            {
                if (a.Type == VariableType.Int)
                {
                    result.Value = Mathf.Sqrt(a.NumericValue);
                    End(true);
                }
                else if (a.Type == VariableType.Float)
                {
                    result.Value = Mathf.Sqrt(a.NumericValue);
                    End(true);
                }
            }
            catch (System.Exception)
            {
                End(false);
                throw;
            }
        }
    }
}
