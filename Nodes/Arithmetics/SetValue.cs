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
                    a.Value = value.IntValue;
                    return State.Success;
                }
                else if (
                    a.Type == VariableType.Float
                    && (value.Type == VariableType.Int || value.Type == VariableType.Float)
                )
                {
                    a.Value = value.FloatValue;
                    return State.Success;
                }
                else if (a.Type == VariableType.Bool && value.Type == VariableType.Bool)
                {
                    a.Value = value.BoolValue;
                    return State.Success;
                }
                else if (a.Type == VariableType.String && value.Type == VariableType.String)
                {
                    a.Value = value.StringValue;
                    return State.Success;
                }
                else if (a.Type == VariableType.Vector2)
                {
                    a.Value = value.Vector2Value;
                    return State.Success;
                }
                else if (a.Type == VariableType.Vector3)
                {
                    a.Value = value.Vector3Value;
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
