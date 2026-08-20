using Aethiumian.AI.Attributes;
using Aethiumian.AI.Randomization;
using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Generates a random value within the configured range.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Random : Arithmetic
    {
        public enum Type
        {
            range,
            normalized,
        }

        public Type type;
        [NumericOrVector]
        [DisplayIf(nameof(type), Type.range)]
        [Readable]
        public VariableField min;

        [NumericOrVector]
        [DisplayIf(nameof(type), Type.range)]
        [Readable]
        public VariableField max;

        [NumericOrVector]
        [Writable]
        public VariableReference result;
        public RandomSourceBinding randomSourceOverride = RandomSourceBinding.WithScope(RandomSourceScope.Local);

        public override State Execute()
        {
            if (!result.HasReference)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(result), this));
            }

            try
            {
                var random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                switch (type)
                {
                    case Type.range:
                        if (!min.HasValue)
                        {
                            return HandleException(InvalidNodeException.VariableIsRequired(nameof(min), this));
                        }
                        if (!max.HasValue)
                        {
                            return HandleException(InvalidNodeException.VariableIsRequired(nameof(max), this));
                        }

                        switch (result.Type)
                        {
                            case VariableType.Int:
                                if (min.Type != VariableType.Int || max.Type != VariableType.Int)
                                {
                                    return State.Failed;
                                }
                                result.SetValue(random.NextInt(min.IntValue, max.IntValue));
                                break;
                            case VariableType.Float:
                                if (!ArithmeticCompatibility.IsScalar(min.Type) || !ArithmeticCompatibility.IsScalar(max.Type))
                                {
                                    return State.Failed;
                                }
                                result.SetValue(random.NextFloat(
                                    min.FloatValue,
                                    max.FloatValue));
                                break;
                            case VariableType.Vector2:
                            {
                                if (!IsCompatibleVectorRange(result.Type, min.Type, max.Type))
                                {
                                    return State.Failed;
                                }
                                Vector2 lower = min.Vector2Value;
                                Vector2 upper = max.Vector2Value;
                                result.SetValue(new Vector2(
                                    random.NextFloat(lower.x, upper.x),
                                    random.NextFloat(lower.y, upper.y)));
                                break;
                            }
                            case VariableType.Vector3:
                            {
                                if (!IsCompatibleVectorRange(result.Type, min.Type, max.Type))
                                {
                                    return State.Failed;
                                }
                                Vector3 lower = min.Vector3Value;
                                Vector3 upper = max.Vector3Value;
                                result.SetValue(new Vector3(
                                    random.NextFloat(lower.x, upper.x),
                                    random.NextFloat(lower.y, upper.y),
                                    random.NextFloat(lower.z, upper.z)));
                                break;
                            }
                            case VariableType.Vector4:
                            {
                                if (!IsCompatibleVectorRange(result.Type, min.Type, max.Type))
                                {
                                    return State.Failed;
                                }
                                Vector4 lower = min.Vector4Value;
                                Vector4 upper = max.Vector4Value;
                                result.SetValue(new Vector4(
                                    random.NextFloat(lower.x, upper.x),
                                    random.NextFloat(lower.y, upper.y),
                                    random.NextFloat(lower.z, upper.z),
                                    random.NextFloat(lower.w, upper.w)));
                                break;
                            }
                            default:
                                return State.Failed;
                        }
                        break;
                    case Type.normalized:
                        switch (result.Type)
                        {
                            case VariableType.Int:
                                result.SetValue(random.NextInt(0, 2));
                                break;
                            case VariableType.Float:
                                result.SetValue(random.NextFloat());
                                break;
                            case VariableType.Vector2:
                                result.SetValue(new Vector2(random.NextFloat(), random.NextFloat()));
                                break;
                            case VariableType.Vector3:
                                result.SetValue(new Vector3(random.NextFloat(), random.NextFloat(), random.NextFloat()));
                                break;
                            case VariableType.Vector4:
                                result.SetValue(new Vector4(random.NextFloat(), random.NextFloat(), random.NextFloat(), random.NextFloat()));
                                break;
                            default:
                                return State.Failed;
                        }
                        break;
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

        /// <summary>
        /// Checks whether a random vector range uses scalars or the requested vector width.
        /// </summary>
        private static bool IsCompatibleVectorRange(VariableType resultType, VariableType minType, VariableType maxType)
        {
            return (ArithmeticCompatibility.IsScalar(minType) || minType == resultType)
                && (ArithmeticCompatibility.IsScalar(maxType) || maxType == resultType);
        }
    }
}
