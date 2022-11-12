using UnityEngine;
using System;
namespace Amlos.AI
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    public class Dot : Arithmetic
    {
        public VariableField a;
        public VariableField b;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Vector2 && b.Type == VariableType.Vector2)
                {
                    result.Value = Vector2.Dot(a.VectorValue, b.VectorValue);
                }
                else if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
                {
                    result.Value = Vector3.Dot(a.VectorValue, b.VectorValue);
                }
                else
                {
                    // Vector3 dot Vector2 or vise versa is undifined
                    End(false);
                }

                End(true);
            }
            catch (System.Exception)
            {
                End(false);
                throw;
            }

        }
    }
}