using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    public enum EqualitySign
    {
        notEquals,
        equals,
    }

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
            if (a.Type == VariableType.Generic || b.type == VariableType.Generic)
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

        public static bool ValueEquals(object a, object b, EqualitySign sign)
        {
            if (a.GetType() != b.GetType())
            {
                {
                    if ((a is float f) && (b is int i))
                    {
                        return sign == EqualitySign.equals ? f == i : f != i;
                    }
                    else if ((a is int i2) && (b is float f2))
                    {
                        return sign == EqualitySign.equals ? f2 == i2 : f2 != i2;
                    }
                }
                throw new ArithmeticException();
            }
            else
            {
                switch (a)
                {
                    case int i:
                        return sign == EqualitySign.equals ? i == (int)b : i != (int)b;
                    case float f:
                        return sign == EqualitySign.equals ? f == (float)b : f != (float)b;
                    case bool @bool:
                        return sign == EqualitySign.equals ? @bool == (bool)b : @bool != (bool)b;
                    case string i:
                        return sign == EqualitySign.equals ? i == (string)b : i != (string)b;
                    case Vector2 v2:
                        return sign == EqualitySign.equals ? v2 == (Vector2)b : v2 != (Vector2)b;
                    case Vector3 v3:
                        return sign == EqualitySign.equals ? v3 == (Vector3)b : v3 != (Vector3)b;
                    default:
                        throw new ArithmeticException();
                }
            }
        }
    }
}
