using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Arctangent : Arithmetic
    {
        public VariableField a;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
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
