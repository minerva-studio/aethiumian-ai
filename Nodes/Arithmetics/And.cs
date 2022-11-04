using System;

namespace Amlos.AI
{
    [Serializable]
    public class And : Arithmetic
    {
        public VariableReference<bool> a;
        public VariableReference<bool> b;

        public override void Execute()
        {
            End(a && b);
        }
    }
}
