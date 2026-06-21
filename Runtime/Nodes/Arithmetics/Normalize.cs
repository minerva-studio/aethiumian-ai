using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// author: Wendi Cai
    /// </summary>
    [Serializable]
    [NodeTip("Get the normalized vector of the input vector")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Normalize : Arithmetic
    {
        [Vector]
        [Readable]
        public VariableField a;

        [Exclude(VariableType.Float, VariableType.Int)]
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                result.SetValue(a.VectorValue.normalized);
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
