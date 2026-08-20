using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Calculates the component-wise minimum of two numeric values or vectors.
    /// </summary>
    [Serializable]
    [NodeTip("Calculates the component-wise minimum of two numeric values or vectors.")]
    public sealed class Min : Arithmetic
    {
        [NumericOrVector]
        [Readable]
        public VariableField a;

        [NumericOrVector]
        [Readable]
        public VariableField b;

        [Writable]
        public VariableReference result;

        /// <summary>
        /// Executes component-wise minimum dispatch without boxing the input values.
        /// </summary>
        public override State Execute()
        {
            if (!ArithmeticCompatibility.TryResolveComponentwiseType(a.Type, b.Type, out VariableType resultType))
            {
                return State.Failed;
            }

            try
            {
                switch (resultType)
                {
                    case VariableType.Int:
                        result.SetValue(Mathf.Min(a.IntValue, b.IntValue));
                        break;
                    case VariableType.Float:
                        result.SetValue(Mathf.Min(a.FloatValue, b.FloatValue));
                        break;
                    case VariableType.Vector2:
                    {
                        Vector2 left = a.Vector2Value;
                        Vector2 right = b.Vector2Value;
                        result.SetValue(new Vector2(Mathf.Min(left.x, right.x), Mathf.Min(left.y, right.y)));
                        break;
                    }
                    case VariableType.Vector3:
                    {
                        Vector3 left = a.Vector3Value;
                        Vector3 right = b.Vector3Value;
                        result.SetValue(new Vector3(Mathf.Min(left.x, right.x), Mathf.Min(left.y, right.y), Mathf.Min(left.z, right.z)));
                        break;
                    }
                    case VariableType.Vector4:
                    {
                        Vector4 left = a.Vector4Value;
                        Vector4 right = b.Vector4Value;
                        result.SetValue(new Vector4(
                            Mathf.Min(left.x, right.x),
                            Mathf.Min(left.y, right.y),
                            Mathf.Min(left.z, right.z),
                            Mathf.Min(left.w, right.w)));
                        break;
                    }
                    default:
                        return State.Failed;
                }

                return State.Success;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
