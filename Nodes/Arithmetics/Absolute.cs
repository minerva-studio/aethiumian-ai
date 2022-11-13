using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public class Absolute : Arithmetic
    {
        [NumericOrVectorTypeLimit]
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
            else if (a.Type == VariableType.Vector2)
            {
                result.Value = VectorUtilities.Abs(a.Vector2Value);
                End(true);
            }
            else if (a.Type == VariableType.Vector3)
            {
                result.Value = VectorUtilities.Abs(a.Vector3Value);
                End(true);
            }
            else
                End(false);
        }
    }
}
