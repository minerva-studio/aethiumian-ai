using System;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Compact serialized value payload for AI variable values. 
    /// </summary>
    [Serializable]
    internal struct VariableValue
    {
        [SerializeField] private string stringValue;
        [SerializeField] private int intValue;
        [SerializeField] private bool boolValue;
        [SerializeField] private Vector4 vectorValue;
        [SerializeField] private UnityEngine.Object unityObjectValue;

        public string StringValue
        {
            readonly get => stringValue ?? string.Empty;
            set => stringValue = value ?? string.Empty;
        }

        public int IntValue
        {
            readonly get => intValue;
            set => intValue = value;
        }

        public float FloatValue
        {
            readonly get => vectorValue.x;
            set => vectorValue.x = value;
        }

        public bool BoolValue
        {
            readonly get => boolValue;
            set => boolValue = value;
        }

        public Vector2 Vector2Value
        {
            readonly get => vectorValue;
            set
            {
                vectorValue.x = value.x;
                vectorValue.y = value.y;
            }
        }

        public Vector3 Vector3Value
        {
            readonly get => vectorValue;
            set
            {
                vectorValue.x = value.x;
                vectorValue.y = value.y;
                vectorValue.z = value.z;
            }
        }

        public Vector4 Vector4Value
        {
            readonly get => vectorValue;
            set => vectorValue = value;
        }

        public Color ColorValue
        {
            readonly get => Vector4Value;
            set => Vector4Value = value;
        }

        public UnityEngine.Object UnityObjectValue
        {
            readonly get => unityObjectValue;
            set => unityObjectValue = value;
        }

        /// <summary>
        /// Reset the payload for a new variable type so old values are not reinterpreted.
        /// </summary>
        public void Reset(VariableType type)
        {
            stringValue = string.Empty;
            intValue = default;
            boolValue = default;
            vectorValue = default;
            if (type != VariableType.UnityObject)
            {
                unityObjectValue = null;
            }
        }
    }
}
