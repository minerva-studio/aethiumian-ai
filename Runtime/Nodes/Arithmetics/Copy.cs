using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("Copy value of one variable to another")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Copy : Arithmetic
    {
        [Readable]
        public VariableField from;
        [Writable]
        public VariableReference to;

        public override State Execute()
        {
            if (!from.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(from), this));
            }

            if (!to.HasValue)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(to), this));
            }

            try
            {
                switch (from.Type)
                {
                    case VariableType.String:
                        to.SetValue(from.StringValue);
                        break;
                    case VariableType.Int:
                        to.SetValue(from.IntValue);
                        break;
                    case VariableType.Float:
                        to.SetValue(from.FloatValue);
                        break;
                    case VariableType.Bool:
                        to.SetValue(from.BoolValue);
                        break;
                    case VariableType.Vector2:
                        to.SetValue(from.Vector2Value);
                        break;
                    case VariableType.Vector3:
                        to.SetValue(from.Vector3Value);
                        break;
                    case VariableType.Vector4:
                        to.SetValue(from.Vector4Value);
                        break;
                    case VariableType.UnityObject:
                        to.SetValue(from.UnityObjectValue);
                        break;
                    case VariableType.Generic:
                        to.SetValue(from.Value);
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
