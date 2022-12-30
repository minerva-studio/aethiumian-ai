using System;

namespace Amlos.AI
{
    [Serializable]
    public sealed class Or : Arithmetic
    {
        public VariableReference a;
        public VariableReference b;

        public VariableReference<bool> result;

        public override void Execute()
        {
            var ret = a.BoolValue || b.BoolValue;
            if (result.HasRuntimeReference)
            {
                this.result.Value = ret;
            }
            End(ret);
        }
    }
}
