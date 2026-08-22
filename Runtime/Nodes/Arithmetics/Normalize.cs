using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// author: Wendi Cai
    /// </summary>
    [Serializable]
    [NodeTip("Get the normalized vector of the input vector")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Normalize : Arithmetic
    {
        [Vector]
        [Readable]
        public VariableField a;

        [Exclude(VariableType.Float, VariableType.Int)]
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            try
            {
                switch (a.Type)
                {
                    case VariableType.Vector2:
                        if (HasNaN(a)) return State.Failed;
                        Vector2 normalized2 = a.Vector2Value.normalized;
                        return result.SetValue(normalized2, failOnNaN) ? State.Success : State.Failed;
                    case VariableType.Vector3:
                        if (HasNaN(a)) return State.Failed;
                        Vector3 normalized3 = a.Vector3Value.normalized;
                        return result.SetValue(normalized3, failOnNaN) ? State.Success : State.Failed;
                    case VariableType.Vector4:
                        if (HasNaN(a)) return State.Failed;
                        Vector4 normalized4 = a.Vector4Value.normalized;
                        return result.SetValue(normalized4, failOnNaN) ? State.Success : State.Failed;
                    default:
                        return State.Failed;
                }
            }
            catch (Exception e)
            {
                return HandleException(e);
            }
        }
    }
}
