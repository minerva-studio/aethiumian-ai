using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Check two value's equality")]
    [Serializable]
    public sealed class Equals : Determine
    {
        [Readable]
        public VariableField a;
        [Readable]
        public VariableField b;

        public override bool GetValue()
        {
            // unity object comare: if is game object/component, only compare whether it is on the same object
            if (a.Value is GameObject or Component && b.Value is GameObject or Component)
            {
                return a.GameObjectValue == b.GameObjectValue;
            }
            // generic compare: directly compare generic value
            if (a.Type == VariableType.Generic || b.Type == VariableType.Generic)
            {
                return a.Value == b.Value;
            }

            if (a.Type != b.Type)
            {
                return false;
            }

            switch (a.Type)
            {
                case VariableType.String:
                    return a.StringValue == b.StringValue;
                case VariableType.Int:
                    return a.IntValue == b.IntValue;
                case VariableType.Float:
                    return a.FloatValue == b.FloatValue;
                case VariableType.Bool:
                    return a.BoolValue == b.BoolValue;
                case VariableType.Vector2:
                    return a.Vector2Value == b.Vector2Value;
                case VariableType.Vector3:
                    return a.Vector3Value == b.Vector3Value;
                case VariableType.UnityObject:
                    return a.UnityObjectValue == b.UnityObjectValue;
                default:
                    return false;
            }

        }
    }
}
