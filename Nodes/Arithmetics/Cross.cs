using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    public class Cross : Arithmetic
    {
        public VariableField a;
        public VariableField b;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
                {
                    result.Value = Vector3.Cross(a.VectorValue, b.VectorValue);
                }
                else
                {
                    // only Vector3 can do cross product
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