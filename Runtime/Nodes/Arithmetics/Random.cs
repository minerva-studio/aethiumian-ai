using Amlos.AI.Variables;
using Minerva.Module;
using System;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Random : Arithmetic
    {
        public enum Type
        {
            range,
            normalized,
        }

        public Type type;
        [NumericOrVector][DisplayIf(nameof(type), Type.range)] public VariableField min;
        [NumericOrVector][DisplayIf(nameof(type), Type.range)] public VariableField max;
        [NumericOrVector] public VariableReference result;

        public override State Execute()
        {
            if (!result.HasReference)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(result)));
            }

            switch (type)
            {
                case Type.range:
                    if (!min.HasReference)
                    {
                        return HandleException(InvalidNodeException.VariableIsRequired(nameof(min)));
                    }
                    if (!max.HasReference)
                    {
                        return HandleException(InvalidNodeException.VariableIsRequired(nameof(max)));
                    }
                    if (result.Type == VariableType.Int)
                    {
                        result.SetValue(UnityEngine.Random.Range(min.IntValue, max.IntValue));
                    }
                    if (result.Type == VariableType.Float)
                    {
                        result.SetValue(UnityEngine.Random.Range(min.FloatValue, max.FloatValue));
                    }
                    if (result.Type == VariableType.Vector2)
                    {
                        result.SetValue(VectorUtility.Random(min.Vector2Value, max.Vector2Value));
                    }
                    if (result.Type == VariableType.Vector3)
                    {
                        result.SetValue(VectorUtility.Random(min.Vector3Value, max.Vector3Value));
                    }
                    break;
                case Type.normalized:
                    if (result.Type == VariableType.Int)
                    {
                        result.SetValue(UnityEngine.Random.Range(0, 2));
                    }
                    if (result.Type == VariableType.Float)
                    {
                        result.SetValue(UnityEngine.Random.value);
                    }
                    if (result.Type == VariableType.Vector2)
                    {
                        result.SetValue(VectorUtility.Random(1f, 1f));
                    }
                    if (result.Type == VariableType.Vector3)
                    {
                        result.SetValue(VectorUtility.Random(1f, 1f, 1f));
                    }
                    break;
                default:
                    break;
            }
            return State.Success;
        }
    }
}
