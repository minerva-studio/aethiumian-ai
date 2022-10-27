using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class GetValue : Arithmetic
    {
        VariableReference a;
        VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Int || a.Type == VariableType.Float)
                {
                    result.Value = a.NumericValue;
                    End(true);
                }
                else if (a.Type == VariableType.Bool)
                {
                    result.Value = a.BoolValue;
                    End(true);
                }
                else if (a.Type == VariableType.String)
                {
                    result.Value = a.StringValue;
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