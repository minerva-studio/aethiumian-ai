using Aethiumian.AI.Variables;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Assigns an input value to a writable variable.")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Assign : Arithmetic
    {
        [Writable]
        [FormerlySerializedAs("a")]
        public VariableReference destination;

        [Readable]
        [FormerlySerializedAs("value")]
        public VariableField source;

        public override State Execute()
        {
            try
            {
                switch (destination.Type)
                {
                    case VariableType.Int:
                        if (HasNaN(source, 1)) return State.Failed;
                        destination.SetValue(source.GetValue<int>());
                        return State.Success;
                    case VariableType.Float:
                        if (HasNaN(source, 1)) return State.Failed;
                        destination.SetValue(source.GetValue<float>());
                        return State.Success;
                    case VariableType.Bool:
                        if (HasNaN(source, 1)) return State.Failed;
                        destination.SetValue(source.GetValue<bool>());
                        return State.Success;
                    case VariableType.String:
                        destination.SetValue(source.GetValue<string>());
                        return State.Success;
                    case VariableType.Vector2:
                        if (HasNaN(source, 2)) return State.Failed;
                        destination.SetValue(source.GetValue<Vector2>());
                        return State.Success;
                    case VariableType.Vector3:
                        if (HasNaN(source, 3)) return State.Failed;
                        destination.SetValue(source.GetValue<Vector3>());
                        return State.Success;
                    case VariableType.Vector4:
                        if (HasNaN(source, 4)) return State.Failed;
                        destination.SetValue(source.GetValue<Vector4>());
                        return State.Success;
                    case VariableType.UnityObject:
                        destination.SetValue(source.GetValue<UnityEngine.Object>());
                        return State.Success;
                    case VariableType.Generic:
                        // Generic storage intentionally remains the object boundary.
                        destination.SetValue(source.GetValue<object>());
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
