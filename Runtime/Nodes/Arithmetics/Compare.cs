using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Compares two values using the configured comparison operator.")]
    /// <summary>
    /// Numeric: Normal value comparison <br/>
    /// Vector, String: Equality Check only <br/>
    /// Bool: XOR or XNOR
    /// </summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Compare : Arithmetic
    {
        [Readable]
        public VariableField a;
        public CompareSign mode;
        [Readable]
        public VariableField b;

        [Writable]
        public VariableReference<bool> result;

        public override State Execute()
        {
            if (a.Type == VariableType.Int && b.Type == VariableType.Int)
            {
                int valA = a.IntValue;
                int valB = b.IntValue;
                var result = CompareNumeric(valA, mode, valB);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }
            if (ArithmeticCompatibility.IsScalar(a.Type) && ArithmeticCompatibility.IsScalar(b.Type))
            {
                float valA = a.FloatValue;
                float valB = b.FloatValue;
                var result = CompareNumeric(valA, mode, valB);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }
            if (a.Type == VariableType.Vector2 && b.Type == VariableType.Vector2)
            {
                var valA = a.Vector2Value;
                var valB = b.Vector2Value;
                var result = CompareVector(valA, mode, valB);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }
            if (a.Type == VariableType.Vector3 && b.Type == VariableType.Vector3)
            {
                var valA = a.Vector3Value;
                var valB = b.Vector3Value;
                var result = CompareVector(valA, mode, valB);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }
            if (a.Type == VariableType.Vector4 && b.Type == VariableType.Vector4)
            {
                var valA = a.Vector4Value;
                var valB = b.Vector4Value;
                var result = CompareVector(valA, mode, valB);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }
            if (a.Type == VariableType.String && b.Type == VariableType.String)
            {
                var result = ValueUtility.Compare(a.StringValue, b.StringValue, mode);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }
            if (a.Type == VariableType.Bool && b.Type == VariableType.Bool)
            {
                var result = ValueUtility.Compare(a.BoolValue, b.BoolValue, mode);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }
            // generic compare
            if (a.Value is IComparable c1 && b.Value is IComparable c2)
            {
                var result = ValueUtility.Compare(c1, c2, mode);
                if (this.result.HasReference) this.result.SetValue(result);
                return StateOf(result);
            }

            //Not a valid comparison
            return State.Failed;
        }


        public static bool CompareNumeric(float a, CompareSign mode, float b)
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

        public static bool CompareNumeric(int a, CompareSign mode, int b)
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

        public static bool CompareVector(Vector3 a, CompareSign mode, Vector3 b)
        {
            return mode switch
            {
                CompareSign.notEquals => (a != b),
                CompareSign.equals => (a == b),
                _ => (false),
            };
        }
        public static bool CompareVector(Vector2 a, CompareSign mode, Vector2 b)
        {
            return mode switch
            {
                CompareSign.notEquals => (a != b),
                CompareSign.equals => (a == b),
                _ => (false),
            };
        }

        public static bool CompareVector(Vector4 a, CompareSign mode, Vector4 b)
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
