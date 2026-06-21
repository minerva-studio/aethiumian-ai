using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// author: Kevin Zhou
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Cross : Arithmetic
    {
        // only Vector3 can do cross product
        [Readable]
        public VariableField<Vector3> a;
        [Readable]
        public VariableField<Vector3> b;

        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                result.SetValue(Vector3.Cross(a, b));
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
