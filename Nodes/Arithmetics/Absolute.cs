using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Absolute : Arithmetic
    {
        public VariableReference a;
        public VariableReference result;

        public override void Execute()
        {
            if (a.Type == VariableType.Int)
            {
                result.Value = Mathf.Abs(a.IntValue);
                End(true);
            }
            else if (a.Type == VariableType.Float)
            {
                result.Value = Mathf.Abs(a.FloatValue);
                End(true);
            }
            else
                End(false);
        }
    }
}
