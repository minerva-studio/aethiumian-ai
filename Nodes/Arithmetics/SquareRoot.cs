using Amlos.AI.Variables;
using System;
using UnityEngine;
namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class SquareRoot : Arithmetic
    {
        [TypeLimit(VariableType.Float, VariableType.Int)]
        public VariableField a;
        public VariableReference result;

        public override State Execute()
        {
            if (a.Type == VariableType.Bool)
            {
                return State.Failed;
            }
            if (a.Type == VariableType.String)
            {
                return State.Failed;
            }

            else if (a.NumericValue < 0)
            {
                return State.Failed;
            }
            try
            {
                if (a.Type == VariableType.Int)
                {
                    result.Value = Mathf.Sqrt(a.NumericValue);
                    return State.Success;
                }
                else if (a.Type == VariableType.Float)
                {
                    result.Value = Mathf.Sqrt(a.NumericValue);
                    return State.Success;
                }
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return State.Success;
        }
    }
}
