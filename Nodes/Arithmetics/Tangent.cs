using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Tangent : Arithmetic
    {
        public VariableField a;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
                {
                    result.Value = Mathf.Tan(a.NumericValue);
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