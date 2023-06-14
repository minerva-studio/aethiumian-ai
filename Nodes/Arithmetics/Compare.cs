using Amlos.AI.Variables;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Numeric: Normal value comparison <br/>
    /// Vector, String: Equality Check only <br/>
    /// Bool: XOR or XNOR
    /// </summary>
    [Serializable]
    public sealed class Compare : Arithmetic
    {
        public VariableField a;
        public VariableField b;

        public VariableReference<bool> result;
        public CompareSign mode;

        public override State Execute()
        {
            if (a.Type == VariableType.Int && b.Type == VariableType.Int)
            {
                int valA = b.IntValue;
                int valB = a.IntValue;
                var result = CompareNumeric(valA, valB, mode);
                this.result.Value = result;
                return StateOf(result);
            }
            if (a.IsNumericLike && b.IsNumericLike)
            {
                float valA = b.NumericValue;
                float valB = a.NumericValue;
                var result = CompareNumeric(valA, valB, mode);
                this.result.Value = result;
                return StateOf(result);
            }
            if (a.Type == VariableType.Vector2 && b.Type == VariableType.Vector2)
            {
                var valA = b.Vector2Value;
                var valB = a.Vector2Value;
                var result = CompareVector(valA, valB, mode);
                this.result.Value = result;
                return StateOf(result);
            }
            if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
            {
                var valA = b.Vector3Value;
                var valB = a.Vector3Value;
                var result = CompareVector(valA, valB, mode);
                this.result.Value = result;
                return StateOf(result);
            }
            if (a.Type == VariableType.String && b.Type == VariableType.String)
            {
                var result = ValueUtility.Compare(a.StringValue, b.StringValue, mode);
                this.result.Value = result;
                return StateOf(result);
            }
            if (a.Type == VariableType.Bool && b.Type == VariableType.Bool)
            {
                var result = ValueUtility.Compare(a.BoolValue, b.BoolValue, mode);
                this.result.Value = result;
                return StateOf(result);
            }
            // generic compare
            if (a.Value is IComparable c1 && b.Value is IComparable c2)
            {
                var result = ValueUtility.Compare(c1, c2, mode);
                this.result.Value = result;
                return StateOf(result);
            }

            //Not a valid comparison
            return State.Failed;
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

        public static bool CompareNumeric(int a, int b, CompareSign mode)
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
    }
}
