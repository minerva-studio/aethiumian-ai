﻿using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeTip("Do Variable addition")]
    [Serializable]
    public sealed class Add : Arithmetic
    {
        [Exclude(VariableType.Bool)]
        public VariableField a;
        [Exclude(VariableType.Bool)]
        public VariableField b;
        public VariableReference result;

        public override State Execute()
        {
            if (a.Type == VariableType.Bool || b.Type == VariableType.Bool)
            {
                return State.Failed;
            }
            try
            {
                if (a.Type == VariableType.String || b.Type == VariableType.String)
                {
                    result.SetValue(a.StringValue + b.StringValue);
                }
                else if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.SetValue(a.IntValue + b.IntValue);
                }
                else if (a.IsNumericLike && b.IsNumericLike) result.SetValue(a.NumericValue + b.NumericValue);
                else if (a.IsVector && b.IsVector) result.SetValue(a.VectorValue + b.VectorValue);
                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
