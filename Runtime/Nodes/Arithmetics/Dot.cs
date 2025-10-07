using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    public sealed class Dot : Arithmetic
    {
        [Constraint(VariableType.Vector2, VariableType.Vector3)]
        [Readable]
        public VariableField a;

        [Constraint(VariableType.Vector2, VariableType.Vector3)]
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
                    result.SetValue(Vector2.Dot(a.VectorValue, b.VectorValue));
                }
                else if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
                {
                    result.SetValue(Vector3.Dot(a.VectorValue, b.VectorValue));
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
