using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Applies ceiling to a scalar or every component of a vector.
    /// </summary>
    [Serializable]
    [NodeTip("Applies ceiling to a scalar or every component of a vector.")]
    public sealed class Ceil : Arithmetic
    {
        [NumericOrVector]
        [Readable]
        public VariableField a;

        [Writable]
        public VariableReference result;

        /// <summary>
        /// Executes ceiling dispatch for scalar and vector values without boxing the input.
        /// </summary>
        public override State Execute()
        {
            try
            {
                switch (a.Type)
                {
                    case VariableType.Int:
                        result.SetValue(a.IntValue);
                        break;
                    case VariableType.Float:
                        result.SetValue(Mathf.Ceil(a.FloatValue));
                        break;
                    case VariableType.Vector2:
                    {
                        Vector2 value = a.Vector2Value;
                        result.SetValue(new Vector2(Mathf.Ceil(value.x), Mathf.Ceil(value.y)));
                        break;
                    }
                    case VariableType.Vector3:
                    {
                        Vector3 value = a.Vector3Value;
                        result.SetValue(new Vector3(Mathf.Ceil(value.x), Mathf.Ceil(value.y), Mathf.Ceil(value.z)));
                        break;
                    }
                    case VariableType.Vector4:
                    {
                        Vector4 value = a.Vector4Value;
                        result.SetValue(new Vector4(Mathf.Ceil(value.x), Mathf.Ceil(value.y), Mathf.Ceil(value.z), Mathf.Ceil(value.w)));
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
