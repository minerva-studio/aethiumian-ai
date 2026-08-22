using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Calculates the dot product of two vectors.")]
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Dot : Arithmetic
    {
        [Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.Vector4)]
        [Readable]
        public VariableField a;

        [Constraint(VariableType.Vector2, VariableType.Vector3, VariableType.Vector4)]
        [Readable]
        public VariableField b;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                if (HasNaN(a) || HasNaN(b))
                    return State.Failed;

                if (a.Type == VariableType.Vector2 && b.Type == VariableType.Vector2)
                {
                    float value = Vector2.Dot(a.Vector2Value, b.Vector2Value);
                    return result.SetValue(value, failOnNaN) ? State.Success : State.Failed;
                }
                else if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
                {
                    float value = Vector3.Dot(a.Vector3Value, b.Vector3Value);
                    return result.SetValue(value, failOnNaN) ? State.Success : State.Failed;
                }
                else if (a.Type == VariableType.Vector4 && b.Type == VariableType.Vector4)
                {
                    float value = Vector4.Dot(a.Vector4Value, b.Vector4Value);
                    return result.SetValue(value, failOnNaN) ? State.Success : State.Failed;
                }
                else
                {
                    // Vector3 dot Vector2 or vise versa is undifined
                    return State.Failed;
                }

            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
