using System;
using UnityEngine;

namespace Amlos.AI.Variables
{
    public enum CompareSign
    {
        [InspectorName("!=")]
        notEquals,
        [InspectorName("<")]
        less,
        [InspectorName("<=")]
        lessOrEquals,
        [InspectorName("==")]
        equals,
        [InspectorName(">=")]
        greaterOrEquals,
        [InspectorName(">")]
        greater,
    }

    public enum EqualitySign
    {
        [InspectorName("!=")]
        notEquals,
        [InspectorName("==")]
        equals,
    }


    public static class ValueUtility
    {
        public static bool Compare(IComparable a, IComparable b, CompareSign mode)
        {
            return mode switch
            {
                CompareSign.less => (a.CompareTo(b) < 0),
                CompareSign.lessOrEquals => (a.CompareTo(b) <= 0),
                CompareSign.equals => (a.CompareTo(b) == 0),
                CompareSign.notEquals => (a.CompareTo(b) != 0),
                CompareSign.greaterOrEquals => (a.CompareTo(b) >= 0),
                CompareSign.greater => (a.CompareTo(b) > 0),
                _ => (false),
            };
        }

        public static bool Equals(object a, object b, CompareSign sign) => Equals(a, b, sign == CompareSign.equals ? EqualitySign.equals : EqualitySign.notEquals);
        public static bool Equals(object a, object b, EqualitySign sign)
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
