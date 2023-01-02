using System;

namespace Amlos.AI
{
    [Serializable]
    public sealed class And : Arithmetic
    {
        public VariableReference<bool> a;
        public VariableReference<bool> b;

        public VariableReference<bool> result;
        public override void Execute()
        {
            bool result = a && b;
            if (this.result.HasReference)
            {
                this.result.Value = result;
            }
            End(result);
        }
    }
}
