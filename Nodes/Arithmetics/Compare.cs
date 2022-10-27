using System;

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

    [Serializable]
    public class Compare : Arithmetic
    {

        public VariableField a;
        public VariableField b;
        public CompareSign mode;

        public override void Execute()
        {
            if (a.Type == VariableType.String || a.Type == VariableType.Bool)
            {
                End(false);
                return;
            }
            else if (b.Type == VariableType.String || b.Type == VariableType.Bool)
            {
                End(false);
                return;
            }
            else
            {
                float valA = b.NumericValue;
                float valB = a.NumericValue;
                End(CompareValue(valA, valB, mode));
                return;
            }

        }


        public static bool CompareValue(float a, float b, CompareSign mode)
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

        public static bool ValueCompare(IComparable a, IComparable b, CompareSign mode)
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
