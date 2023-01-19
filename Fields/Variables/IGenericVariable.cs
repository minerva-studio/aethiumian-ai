using Minerva.Module;
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
