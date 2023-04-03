using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// author: Wendi Cai
    /// </summary>
    [Serializable]
    [NodeTip("Get the magnitude of the vector")]
    public sealed class Magitude : Arithmetic
    {
        [Vector]
        public VariableField a;
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                result.Value = a.VectorValue.magnitude;
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
