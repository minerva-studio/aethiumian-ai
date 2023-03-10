using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
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
                    result.Value = a.StringValue + b.StringValue;
                }
                else if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.Value = a.IntValue + b.IntValue;
                }
                else if (a.IsNumeric && b.IsNumeric) result.Value = a.NumericValue + b.NumericValue;
                else if (a.IsVector && b.IsVector) result.Value = a.VectorValue + b.VectorValue;
                return State.Success;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return State.Failed;
            }
        }
    }

}
