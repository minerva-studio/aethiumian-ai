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
                                var random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                result.SetValue(random.NextInt(min.IntValue, max.IntValue));
                                break;
                            case VariableType.Float:
                                if (!ArithmeticCompatibility.IsScalar(min.Type) || !ArithmeticCompatibility.IsScalar(max.Type))
                                {
                                    return State.Failed;
                                }
                                if (HasNaN(min) || HasNaN(max))
                                {
                                    return State.Failed;
                                }
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                if (!result.SetValue(random.NextFloat(
                                    min.FloatValue,
                                    max.FloatValue), failOnNaN))
                                {
                                    return State.Failed;
                                }
                                return State.Success;
                                break;
                            case VariableType.Vector2:
                            {
                                if (!IsCompatibleVectorRange(result.Type, min.Type, max.Type))
                                {
                                    return State.Failed;
                                }
                                if (HasNaN(min, 2) || HasNaN(max, 2))
                                {
                                    return State.Failed;
                                }
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                Vector2 lower = min.Vector2Value;
                                Vector2 upper = max.Vector2Value;
                                Vector2 value = new(
                                    random.NextFloat(lower.x, upper.x),
                                    random.NextFloat(lower.y, upper.y));
                                return result.SetValue(value, failOnNaN) ? State.Success : State.Failed;
                            }
                            case VariableType.Vector3:
                            {
                                if (!IsCompatibleVectorRange(result.Type, min.Type, max.Type))
                                {
                                    return State.Failed;
                                }
                                if (HasNaN(min, 3) || HasNaN(max, 3))
                                {
                                    return State.Failed;
                                }
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                Vector3 lower = min.Vector3Value;
                                Vector3 upper = max.Vector3Value;
                                Vector3 value = new(
                                    random.NextFloat(lower.x, upper.x),
                                    random.NextFloat(lower.y, upper.y),
                                    random.NextFloat(lower.z, upper.z));
                                return result.SetValue(value, failOnNaN) ? State.Success : State.Failed;
                            }
                            case VariableType.Vector4:
                            {
                                if (!IsCompatibleVectorRange(result.Type, min.Type, max.Type))
                                {
                                    return State.Failed;
                                }
                                if (HasNaN(min, 4) || HasNaN(max, 4))
                                {
                                    return State.Failed;
                                }
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                Vector4 lower = min.Vector4Value;
                                Vector4 upper = max.Vector4Value;
                                Vector4 value = new(
                                    random.NextFloat(lower.x, upper.x),
                                    random.NextFloat(lower.y, upper.y),
                                    random.NextFloat(lower.z, upper.z),
                                    random.NextFloat(lower.w, upper.w));
                                return result.SetValue(value, failOnNaN) ? State.Success : State.Failed;
                            }
                            default:
                                return State.Failed;
                        }
                        break;
                    case Type.normalized:
                        switch (result.Type)
                        {
                            case VariableType.Int:
                                var random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                result.SetValue(random.NextInt(0, 2));
                                break;
                            case VariableType.Float:
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                float floatValue = random.NextFloat();
                                return result.SetValue(floatValue, failOnNaN) ? State.Success : State.Failed;
                            case VariableType.Vector2:
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                Vector2 normalized2 = new(random.NextFloat(), random.NextFloat());
                                return result.SetValue(normalized2, failOnNaN) ? State.Success : State.Failed;
                            case VariableType.Vector3:
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                Vector3 normalized3 = new(random.NextFloat(), random.NextFloat(), random.NextFloat());
                                return result.SetValue(normalized3, failOnNaN) ? State.Success : State.Failed;
                            case VariableType.Vector4:
                                random = behaviourTree.RandomSources.Resolve(this, randomSourceOverride);
                                Vector4 normalized4 = new(random.NextFloat(), random.NextFloat(), random.NextFloat(), random.NextFloat());
                                return result.SetValue(normalized4, failOnNaN) ? State.Success : State.Failed;
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
