using Aethiumian.AI.Variables;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Author: Wendi Cai
    /// </summary>
    [Serializable]
    [NodeTip("Get the magnitude of the vector")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Magnitude : Arithmetic
    {
        [Vector]
        [Readable]
        public VariableField a;

        [Writable]
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
                        if (HasNaN(a)) return State.Failed;
                        float magnitude2 = a.Vector2Value.magnitude;
                        return result.SetValue(magnitude2, failOnNaN) ? State.Success : State.Failed;
                    case VariableType.Vector3:
                        if (HasNaN(a)) return State.Failed;
                        float magnitude3 = a.Vector3Value.magnitude;
                        return result.SetValue(magnitude3, failOnNaN) ? State.Success : State.Failed;
                    case VariableType.Vector4:
                        if (HasNaN(a)) return State.Failed;
                        float magnitude4 = a.Vector4Value.magnitude;
                        return result.SetValue(magnitude4, failOnNaN) ? State.Success : State.Failed;
                }
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
