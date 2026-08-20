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
                if (a.Type == VariableType.Vector2 && b.Type == VariableType.Vector2)
                {
                    result.SetValue(Vector2.Dot(a.Vector2Value, b.Vector2Value));
                }
                else if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
                {
                    result.SetValue(Vector3.Dot(a.Vector3Value, b.Vector3Value));
                }
                else if (a.Type == VariableType.Vector4 && b.Type == VariableType.Vector4)
                {
                    result.SetValue(Vector4.Dot(a.Vector4Value, b.Vector4Value));
                }
                else
                {
                    // Vector3 dot Vector2 or vise versa is undifined
                    return State.Failed;
                }

                return State.Success;
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
