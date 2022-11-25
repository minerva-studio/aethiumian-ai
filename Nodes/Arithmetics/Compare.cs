using System;
using UnityEngine;

namespace Amlos.AI
{
    public enum CompareSign
    {
        notEquals,
        less,
        lessOrEquals,
        equals,
        greaterOrEquals,
        greater,
    }

    [Flags]
    public enum CompareFlag
    {
        less = 1,
        equals = 2,
        greater = 4,
        lessOrEquals = less | equals,
        greaterOrEquals = greater | equals,
        notEquals = less | greater,
    }

    /// <summary>
    /// Numeric: Normal value comparison <br></br>
    /// Vector, String: Equality Check only <br></br>
    /// Bool: XOR or XNOR
    /// </summary>
    [Serializable]
    public sealed class Compare : Arithmetic
    {
        public VariableField a;
        public VariableField b;

        public VariableReference<bool> result;
        public CompareSign mode;

        public override void Execute()
        {
            if (a.IsNumeric && b.IsNumeric)
            {
                float valA = b.NumericValue;
                float valB = a.NumericValue;
                var result = CompareNumeric(valA, valB, mode);
                this.result.Value = result;
                End(result);
                return;
            }
            if (a.Type == VariableType.Vector2 && b.Type == VariableType.Vector2)
            {
                var valA = b.Vector2Value;
                var valB = a.Vector2Value;
                var result = CompareVector(valA, valB, mode);
                this.result.Value = result;
                End(result);
                return;
            }
            if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
            {
                var valA = b.Vector3Value;
                var valB = a.Vector3Value;
                var result = CompareVector(valA, valB, mode);
                this.result.Value = result;
                End(result);
                return;
            }
            if (a.Type == VariableType.String && b.Type == VariableType.String)
            {
                var result = CompareComparable(a.StringValue, b.StringValue, mode);
                this.result.Value = result;
                End(result);
                return;
            }
            if (a.Type == VariableType.Bool && b.Type == VariableType.Bool)
            {
                var result = CompareComparable(a.BoolValue, b.BoolValue, mode);
                this.result.Value = result;
                End(result);
                return;
            }

            //Not a valid comparison
            End(false);
        }


        public static bool CompareNumeric(float a, float b, CompareSign mode)
        {
            return mode switch
            {
                CompareSign.less => (a < b),
                CompareSign.lessOrEquals => (a <= b),
                CompareSign.notEquals => (a != b),
                CompareSign.equals => (a == b),
                CompareSign.greaterOrEquals => (a >= b),
                CompareSign.greater => (a > b),
                _ => (false),
            };
        }

        public static bool CompareVector(Vector3 a, Vector3 b, CompareSign mode)
        {
            return mode switch
            {
                CompareSign.notEquals => (a != b),
                CompareSign.equals => (a == b),
                _ => (false),
            };
        }
        public static bool CompareVector(Vector2 a, Vector2 b, CompareSign mode)
        {
            return mode switch
            {
                CompareSign.notEquals => (a != b),
                CompareSign.equals => (a == b),
                _ => (false),
            };
        }

        public static bool CompareComparable(IComparable a, IComparable b, CompareSign mode)
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
    }


    public static class CompareSignExtensions
    {
        public static EqualitySign ToEqualityCheck(this CompareSign c)
        {
            return c == CompareSign.equals ? EqualitySign.equals : EqualitySign.notEquals;
        }
    }
}
