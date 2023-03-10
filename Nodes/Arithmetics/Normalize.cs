using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// author: Wendi Cai
    /// </summary>
    [Serializable]
    [NodeTip("Get the normalized vector of the input vector")]
    public sealed class Normalize : Arithmetic
    {
        [VectorTypeLimit]
        public VariableField a;

        [TypeExclude(VariableType.Float, VariableType.Int)]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                result.Value = a.VectorValue.normalized;
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
