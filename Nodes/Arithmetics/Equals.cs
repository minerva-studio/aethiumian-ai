using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Check two value's equality")]
    [Serializable]
    public sealed class Equals : Arithmetic
    {
        public VariableField a;
        public VariableField b;

        public override State Execute()
        {
            // unity object comare: if is game object/component, only compare whether it is on the same object
            if (a.Value is GameObject or Component && b.Value is GameObject or Component)
            {
                return StateOf(a.GameObjectValue == b.GameObjectValue);
            }
            // generic compare: directly compare generic value
            if (a.Type == VariableType.Generic || b.Type == VariableType.Generic)
            {
                return StateOf(a.Value == b.Value);
            }

            if (a.Type != b.Type)
            {
                return State.Failed;
            }

            switch (a.Type)
            {
                case VariableType.String:
                    return StateOf(a.StringValue == b.StringValue);
                case VariableType.Int:
                    return StateOf(a.IntValue == b.IntValue);
                case VariableType.Float:
                    return StateOf(a.FloatValue == b.FloatValue);
                case VariableType.Bool:
                    return StateOf(a.BoolValue == b.BoolValue);
                case VariableType.Vector2:
                    return StateOf(a.Vector2Value == b.Vector2Value);
                case VariableType.Vector3:
                    return StateOf(a.Vector3Value == b.Vector3Value);
                case VariableType.UnityObject:
                    return StateOf(a.UnityObjectValue == b.UnityObjectValue);
                default:
                    return State.Failed;
            }

        }
    }
}
