using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI
{
    public enum EqualitySign
    {
        notEquals,
        equals,
    }

    [NodeTip("Check two value's equality")]
    [Serializable]
    public class Equals : Arithmetic
    {
        public VariableField a;
        public VariableField b;

        public override void Execute()
        {
            if (a.Type != b.Type)
            {
                End(false);
            }

            switch (a.Type)
            {
                case VariableType.String:
                    End(a.StringValue == b.StringValue);
                    break;
                case VariableType.Int:
                    End(a.IntValue == b.IntValue);
                    break;
                case VariableType.Float:
                    End(a.FloatValue == b.FloatValue);
                    break;
                case VariableType.Bool:
                    End(a.BoolValue == b.BoolValue);
                    break;
                case VariableType.Vector2:
                    End(a.Vector2Value == b.Vector2Value);
                    break;
                case VariableType.Vector3:
                    End(a.Vector3Value == b.Vector3Value);
                    break;
                default:
                    End(false);
                    break;
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
