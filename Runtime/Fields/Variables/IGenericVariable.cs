using UnityEngine;

///
namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Marks a field whose variable type is selected at authoring time rather than fixed by a C# type argument.
    /// </summary>
    public interface IDynamicVariableField
    {
        public UUID UUID { get; }
        public VariableType Type { get; }
        public string StringValue { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public bool BoolValue { get; }
        public Vector2 Vector2Value { get; }
        public Vector3 Vector3Value { get; }


        public float NumericValue { get; }
        public Vector3 VectorValue { get; }
    }
}
