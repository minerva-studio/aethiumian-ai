using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Absolute : Arithmetic
    {
        [NumericOrVector]
        [Readable]
        public VariableReference a;

        [Writable]
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
                var baseValue = a.Vector2Value;
                var value = new Vector2(Mathf.Abs(baseValue.x), Mathf.Abs(baseValue.y));
                result.SetValue(value);
                return State.Success;
            }
            else if (a.Type == VariableType.Vector3)
            {
                var baseValue = a.Vector3Value;
                var value = new Vector3(Mathf.Abs(baseValue.x), Mathf.Abs(baseValue.y), Mathf.Abs(baseValue.z));
                result.SetValue(value);
                return State.Success;
            }
            else return State.Failed;
        }
    }
}
