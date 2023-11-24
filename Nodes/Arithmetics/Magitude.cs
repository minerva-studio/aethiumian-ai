using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Author: Wendi Cai
    /// </summary>
    [Serializable]
    [NodeTip("Get the magnitude of the vector")]
    public sealed class Magitude : Arithmetic
    {
        [Vector]
        public VariableField a;
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                switch (a.Type)
                {
                    case VariableType.Node:
                    case VariableType.Invalid:
                    case VariableType.UnityObject:
                    case VariableType.Generic:
                    case VariableType.String:
                    case VariableType.Int:
                    case VariableType.Float:
                    case VariableType.Bool:
                    default:
                        return State.Failed;
                    case VariableType.Vector2:
                        result.Value = a.Vector2Value.magnitude;
                        break;
                    case VariableType.Vector3:
                        result.Value = a.Vector3Value.magnitude;
                        break;
                    case VariableType.Vector4:
                        result.Value = a.Vector4Value.magnitude;
                        break;
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
