using Aethiumian.AI.Variables;
using System;
using UnityEngine;
namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class SquareRoot : Arithmetic
    {
        [Readable]
        [Constraint(VariableType.Float, VariableType.Int)]
        public VariableField a;

        [Writable]
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
                    result.SetValue(Mathf.Sqrt(a.NumericValue));
                    return State.Success;
                }
                else if (a.Type == VariableType.Float)
                {
                    result.SetValue(Mathf.Sqrt(a.NumericValue));
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
