using System;

namespace Amlos.AI
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

        public override void Execute()
        {
            try
            {
                result.Value = a.VectorValue.normalized;
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