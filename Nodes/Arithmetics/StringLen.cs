using System;

namespace Amlos.AI
{
    [Serializable]
    public class StringLen : Arithmetic
    {
        public VariableField<string> a;

        public VariableReference result;

        public override void Execute()
        {
            try
            {
                result.Value = a.StringValue.Length;
            }
            catch (Exception)
            {
                End(false);
                throw;
            }

        }
    }
}