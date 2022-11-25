using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    public sealed class Cross : Arithmetic
    {
        // only Vector3 can do cross product
        public VariableField<Vector3> a;
        public VariableField<Vector3> b;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                result.Value = Vector3.Cross(a, b);
                End(true);
            }
            catch (Exception)
            {
                End(false);
                throw;
            }

        }
    }
}