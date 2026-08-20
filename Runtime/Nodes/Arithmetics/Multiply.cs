using Aethiumian.AI.Variables;
using System;
using System.Text;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Multiplies two numeric values or vectors.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Multiply : Arithmetic
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
            if (a.Type == VariableType.String && b.Type == VariableType.Float)
            {
                return State.Failed;
            }
            else if (a.Type == VariableType.Float && b.Type == VariableType.String)
            {
                return State.Failed;
            }
            // Vector-Vector multiplication should use Dot or Cross
            // However we would allow you to do it for mutiplying the vector components
            //if (a.IsVector && b.IsVector)
            //{
            //    return State.Failed;
            //    return;
            //}
            try
            {
                VariableType resultType;
                if (a.Type == VariableType.String && b.Type == VariableType.Int)
                {
                    var newString = new StringBuilder(a.StringValue.Length * b.IntValue).Insert(0, a.StringValue, b.IntValue).ToString();
                    result.SetValue(newString);
                    return State.Success;
                }
                else if (a.Type == VariableType.Int && b.Type == VariableType.String)
                {
                    var newString = new StringBuilder(b.StringValue.Length * a.IntValue).Insert(0, b.StringValue, a.IntValue).ToString();
                    result.SetValue(newString);
                    return State.Success;
                }
                else if (!ArithmeticCompatibility.TryResolveComponentwiseType(a.Type, b.Type, out resultType))
                {
                    return State.Failed;
                }

                switch (resultType)
                {
                    case VariableType.Int:
                        result.SetValue(a.IntValue * b.IntValue);
                        break;
                    case VariableType.Float:
                        result.SetValue(a.FloatValue * b.FloatValue);
                        break;
                    case VariableType.Vector2:
                        result.SetValue(Vector2.Scale(a.Vector2Value, b.Vector2Value));
                        break;
                    case VariableType.Vector3:
                        result.SetValue(Vector3.Scale(a.Vector3Value, b.Vector3Value));
                        break;
                    case VariableType.Vector4:
                        result.SetValue(Vector4.Scale(a.Vector4Value, b.Vector4Value));
                        break;
                    default:
                        return State.Failed;
                }

                return State.Success;
            }
            catch (System.Exception e)
            {
                return HandleException(e);
            }
        }
    }

}
