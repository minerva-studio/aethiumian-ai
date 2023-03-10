﻿using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
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
            if (result.HasReference)
            {
                this.result.Value = ret;
            }
            End(ret);
        }
    }
}
