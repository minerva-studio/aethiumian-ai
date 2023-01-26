using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Do Variable addition")]
    [Serializable]
    public sealed class Add : Arithmetic
    {
        [TypeExclude(VariableType.Bool)]
        public VariableField a;
        [TypeExclude(VariableType.Bool)]
        public VariableField b;
        public VariableReference result;

        public override void Execute()
        {
            if (a.Type == VariableType.Bool || b.Type == VariableType.Bool)
            {
                End(false);
                return;
            }
            try
            {
                if (a.Type == VariableType.String || b.Type == VariableType.String)
                {
                    result.Value = a.StringValue + b.StringValue;
                }
                else if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.Value = a.IntValue + b.IntValue;
                }
                else if (a.IsNumeric && b.IsNumeric) result.Value = a.NumericValue + b.NumericValue;
                else if (a.IsVector && b.IsVector) result.Value = a.VectorValue + b.VectorValue;
                End(true);
            }
            catch (Exception e)
            {
                End(false);
                Debug.LogException(e);
            }
        }
    }

}
