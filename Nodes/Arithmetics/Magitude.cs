using System;

namespace Amlos.AI
{
    /// <summary>
    /// author: Wendi Cai
    /// </summary>
    [Serializable]
    [NodeTip("Get the magnitude of the vector")]
    public class Magitude : Arithmetic
    {
        [VectorTypeLimit]
        public VariableField a;
        public VariableReference result;

        public override void Execute()
        {
            try
            {
                result.Value = a.VectorValue.magnitude;
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