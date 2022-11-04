using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Cosine : Arithmetic
    {
        public VariableField a;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
                {
                    result.Value = Mathf.Cos(a.NumericValue);
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