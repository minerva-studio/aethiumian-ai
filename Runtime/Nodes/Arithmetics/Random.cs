using Aethiumian.AI.Randomization;
using Aethiumian.AI.Variables;
using Minerva.Module;
using System;

namespace Aethiumian.AI.Nodes
{
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
        public AIRandomSourceReference randomSourceOverride = new();

        public override State Execute()
        {
            if (!result.HasReference)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(result), this));
            }

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
                    if (result.Type == VariableType.Int)
                    {
                        result.SetValue(random.NextInt(min.IntValue, max.IntValue));
                    }
                    if (result.Type == VariableType.Float)
                    {
                        result.SetValue(random.NextFloat(min.FloatValue, max.FloatValue));
                    }
                    if (result.Type == VariableType.Vector2)
                    {
                        result.SetValue(random.NextVector2(min.Vector2Value, max.Vector2Value));
                    }
                    if (result.Type == VariableType.Vector3)
                    {
                        result.SetValue(random.NextVector3(min.Vector3Value, max.Vector3Value));
                    }
                    break;
                case Type.normalized:
                    if (result.Type == VariableType.Int)
                    {
                        result.SetValue(random.NextInt(0, 2));
                    }
                    if (result.Type == VariableType.Float)
                    {
                        result.SetValue(random.NextFloat());
                    }
                    if (result.Type == VariableType.Vector2)
                    {
                        result.SetValue(random.NextVector2(1f, 1f));
                    }
                    if (result.Type == VariableType.Vector3)
                    {
                        result.SetValue(random.NextVector3(1f, 1f, 1f));
                    }
                    break;
                default:
                    break;
            }
            return State.Success;
        }
    }
}
