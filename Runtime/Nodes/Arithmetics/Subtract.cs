using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{

    [NodeTip("Do node subtraction")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Subtract : Arithmetic
    {
        [Readable]
        public VariableField a;
        [Readable]
        public VariableField b;
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            if (a.Type == VariableType.Bool || b.Type == VariableType.Bool)
            {
                return State.Failed;
            }
            if (a.Type == VariableType.String || b.Type == VariableType.String)
            {
                return State.Failed;
            }
            try
            {
                if (!ArithmeticCompatibility.TryResolveComponentwiseType(a.Type, b.Type, out VariableType resultType))
                {
                    return State.Failed;
                }

                switch (resultType)
                {
                    case VariableType.Int:
                        result.SetValue(a.IntValue - b.IntValue);
                        break;
                    case VariableType.Float:
                        result.SetValue(a.FloatValue - b.FloatValue);
                        break;
                    case VariableType.Vector2:
                        result.SetValue(a.Vector2Value - b.Vector2Value);
                        break;
                    case VariableType.Vector3:
                        result.SetValue(a.Vector3Value - b.Vector3Value);
                        break;
                    case VariableType.Vector4:
                        result.SetValue(a.Vector4Value - b.Vector4Value);
                        break;
                    default:
                        return State.Failed;
                }
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
            return State.Success;
        }
    }

}
