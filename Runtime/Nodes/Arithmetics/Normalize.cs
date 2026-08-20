using Aethiumian.AI.Variables;
using System;

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
                        result.SetValue(a.Vector2Value.normalized);
                        return State.Success;
                    case VariableType.Vector3:
                        result.SetValue(a.Vector3Value.normalized);
                        return State.Success;
                    case VariableType.Vector4:
                        result.SetValue(a.Vector4Value.normalized);
                        return State.Success;
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
