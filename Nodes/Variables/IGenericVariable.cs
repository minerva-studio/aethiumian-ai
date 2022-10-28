using System;
using UnityEngine;

///
namespace Amlos.AI
{
    /// <summary>
    /// interface for two generic variables
    /// </summary>
    public interface IGenericVariable
    {
        public UUID UUID { get; }
        public VariableType Type { get; set; }
        public string StringValue { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public bool BoolValue { get; }
        public Vector2 Vector2Value { get; }
        public Vector3 Vector3Value { get; }
        public float NumericValue { get => GetNumericValue(); }
        public Vector3 VectorValue => Type == VariableType.Vector2 ? Vector2Value : Vector3Value;


        public float GetNumericValue()
        {
            switch (Type)
            {
                case VariableType.Int:
                    return IntValue;
                case VariableType.Float:
                    return FloatValue;
                case VariableType.String:
                case VariableType.Bool:
                default:
                    throw new ArithmeticException($"Variable {UUID} is not a numeric type");
            }
        }
    }
}
