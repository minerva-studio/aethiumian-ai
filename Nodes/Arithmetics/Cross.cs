using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
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

        public override State Execute()
        {
            try
            {
                result.Value = Vector3.Cross(a, b);
                return State.Success;
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
