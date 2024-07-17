using Amlos.AI.Variables;
using System;
using UnityEngine;
namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Divide : Arithmetic
    {
        [Exclude(VariableType.String)]
        public VariableField a;

        [Exclude(VariableType.String)]
        public VariableField b;

        public VariableReference result;

        public override State Execute()
        {
            if (a.Type == VariableType.String || b.Type == VariableType.String)
            {
                return State.Failed;
            }
            try
            {
                // int divide
                if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.Value = a.IntValue / b.IntValue;
                    return State.Success;
                }
                // normal numeric divide
                else if (a.IsNumericLike && b.IsNumericLike)
                {
                    result.Value = a.NumericValue / b.NumericValue;
                    return State.Success;
                }
                // vector divide, v / a
                else if (a.IsVector && b.IsNumericLike)
                {
                    result.Value = a.VectorValue / b.NumericValue;
                    return State.Success;
                }
                // vector divide, a / v
                else if (a.IsNumericLike && b.IsVector)
                {
                    if (b.Type == VariableType.Vector3)
                    {
                        result.Value = new Vector3(a.NumericValue / b.Vector3Value.x,
                            a.NumericValue / b.Vector3Value.y,
                            a.NumericValue / b.Vector3Value.z);
                    }
                    else if (b.Type == VariableType.Vector2)
                    {
                        result.Value = new Vector2(a.NumericValue / b.Vector3Value.x,
                            a.NumericValue / b.Vector3Value.y);
                    }
                    return State.Success;
                }
                return State.Failed;

            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }

}
