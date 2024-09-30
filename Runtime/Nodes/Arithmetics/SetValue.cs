using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{

    [Serializable]
    public sealed class SetValue : Arithmetic
    {
        public VariableReference a;
        public VariableField value;

        public override State Execute()
        {
            try
            {
                if (a.Type == VariableType.Int && value.Type == VariableType.Int)
                {
                    a.SetValue(value.IntValue);
                    return State.Success;
                }
                else if (
                    a.Type == VariableType.Float
                    && (value.Type == VariableType.Int || value.Type == VariableType.Float)
                )
                {
                    a.SetValue(value.FloatValue);
                    return State.Success;
                }
                else if (a.Type == VariableType.Bool && value.Type == VariableType.Bool)
                {
                    a.SetValue(value.BoolValue);
                    return State.Success;
                }
                else if (a.Type == VariableType.String && value.Type == VariableType.String)
                {
                    a.SetValue(value.StringValue);
                    return State.Success;
                }
                else if (a.Type == VariableType.Vector2)
                {
                    a.SetValue(value.Vector2Value);
                    return State.Success;
                }
                else if (a.Type == VariableType.Vector3)
                {
                    a.SetValue(value.Vector3Value);
                    return State.Success;
                }
                else if (a.Type == VariableType.Vector4)
                {
                    a.SetValue(value.Vector4Value);
                    return State.Success;
                }
                else if (a.Type == VariableType.UnityObject)
                {
                    a.SetValue(value.UnityObjectValue);
                    return State.Success;
                }
                // boxing when using generic
                else if (a.Type == VariableType.Generic)
                {
                    a.SetValue(value.Value);
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
