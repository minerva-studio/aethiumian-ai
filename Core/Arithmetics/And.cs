using System;

namespace Amlos.AI
{
    [Serializable]
    public class And : Arithmetic
    {
        VariableReference<bool> a;
        VariableReference<bool> b;

        public override void Execute()
        {
            End(a && b);
        }
    }
}
