using Amlos.AI.Variables;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Absolute : Arithmetic
    {
        [NumericOrVector]
        public VariableReference a;

        public VariableReference result;

        public override State Execute()
        {
            if (a.Type == VariableType.Int)
            {
                result.SetValue(Mathf.Abs(a.IntValue));
                return State.Success;
            }
            else if (a.Type == VariableType.Float)
            {
                result.SetValue(Mathf.Abs(a.FloatValue));
                return State.Success;
            }
            else if (a.Type == VariableType.Vector2)
            {
                result.SetValue(VectorUtility.Abs(a.Vector2Value));
                return State.Success;
            }
            else if (a.Type == VariableType.Vector3)
            {
                result.SetValue(VectorUtility.Abs(a.Vector3Value));
                return State.Success;
            }
            else return State.Failed;
        }
    }
}
