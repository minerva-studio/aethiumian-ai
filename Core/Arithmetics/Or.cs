using Amlos.Module;
using System;

namespace Amlos.AI
{
    [Serializable]
    public class Or : Arithmetic
    {
        public VariableReference a;
        public VariableReference b;
        public bool storeResult;
        [DisplayIf(nameof(storeResult))] public VariableReference<bool> result;

        public override void Execute()
        {
            var ret = a.BoolValue || b.BoolValue;
            if (storeResult)
            {
                this.result.Value = ret;
            }
            End(ret);
        }
    }
}
