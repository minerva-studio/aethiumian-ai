using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Do Variable addition")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Add : Arithmetic
    {
        [Readable]
        public VariableField a;
        [Readable]
        public VariableField b;
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                VariableType resultType;
                if (a.Type == VariableType.String || b.Type == VariableType.String)
                {
                    result.SetValue(a.StringValue + b.StringValue);
                    return State.Success;
                }
                else if (!ArithmeticCompatibility.TryResolveComponentwiseType(a.Type, b.Type, out resultType))
                {
                    return State.Failed;
                }

                switch (resultType)
                {
                    case VariableType.Int:
                        result.SetValue(a.IntValue + b.IntValue);
                        break;
                    case VariableType.Float:
                        result.SetValue(a.FloatValue + b.FloatValue);
                        break;
                    case VariableType.Vector2:
                        result.SetValue(a.Vector2Value + b.Vector2Value);
                        break;
                    case VariableType.Vector3:
                        result.SetValue(a.Vector3Value + b.Vector3Value);
                        break;
                    case VariableType.Vector4:
                        result.SetValue(a.Vector4Value + b.Vector4Value);
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
    }
}
