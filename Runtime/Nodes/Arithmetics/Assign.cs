using Amlos.AI.Variables;
using System;
using UnityEngine.Serialization;

namespace Amlos.AI.Nodes
{
    [Serializable]
    public sealed class Assign : Arithmetic
    {
        [Writable]
        [FormerlySerializedAs("a")]
        public VariableReference destination;

        [Readable]
        [FormerlySerializedAs("value")]
        public VariableField source;

        public override State Execute()
        {
            try
            {
                if (destination.Type == VariableType.Int && source.Type == VariableType.Int)
                {
                    destination.SetValue(source.IntValue);
                    return State.Success;
                }
                else if (
                    destination.Type == VariableType.Float
                    && (source.Type == VariableType.Int || source.Type == VariableType.Float)
                )
                {
                    destination.SetValue(source.FloatValue);
                    return State.Success;
                }
                else if (destination.Type == VariableType.Bool && source.Type == VariableType.Bool)
                {
                    destination.SetValue(source.BoolValue);
                    return State.Success;
                }
                else if (destination.Type == VariableType.String && source.Type == VariableType.String)
                {
                    destination.SetValue(source.StringValue);
                    return State.Success;
                }
                else if (destination.Type == VariableType.Vector2)
                {
                    destination.SetValue(source.Vector2Value);
                    return State.Success;
                }
                else if (destination.Type == VariableType.Vector3)
                {
                    destination.SetValue(source.Vector3Value);
                    return State.Success;
                }
                else if (destination.Type == VariableType.Vector4)
                {
                    destination.SetValue(source.Vector4Value);
                    return State.Success;
                }
                else if (destination.Type == VariableType.UnityObject)
                {
                    destination.SetValue(source.UnityObjectValue);
                    return State.Success;
                }
                // boxing when using generic
                else if (destination.Type == VariableType.Generic)
                {
                    destination.SetValue(source.Value);
                    return State.Success;
                }
                else
                    return State.Failed;
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
