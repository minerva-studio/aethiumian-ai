using System;
using Aethiumian.AI.References;
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


        public readonly object GetValue(VariableType type)
        {
            return type switch
            {
                VariableType.String => StringValue,
                VariableType.Int => IntValue,
                VariableType.Float => FloatValue,
                VariableType.Bool => BoolValue,
                VariableType.Vector2 => Vector2Value,
                VariableType.Vector3 => Vector3Value,
                VariableType.Vector4 => Vector4Value,
                VariableType.UnityObject or VariableType.Generic => UnityObjectValue,
                _ => throw new InvalidCastException(),
            };
        }

        /// <summary>Reads the tagged payload without first boxing its built-in source value.</summary>
        internal readonly TTarget GetValue<TTarget>(VariableType type)
        {
            return type switch
            {
                VariableType.String => ImplicitConverter<TTarget>.From(StringValue),
                VariableType.Int => ImplicitConverter<TTarget>.From(IntValue),
                VariableType.Float => ImplicitConverter<TTarget>.From(FloatValue),
                VariableType.Bool => ImplicitConverter<TTarget>.From(BoolValue),
                VariableType.Vector2 => ImplicitConverter<TTarget>.From(Vector2Value),
                VariableType.Vector3 => ImplicitConverter<TTarget>.From(Vector3Value),
                VariableType.Vector4 => ImplicitConverter<TTarget>.From(Vector4Value),
                VariableType.UnityObject or VariableType.Generic => ImplicitConverter<TTarget>.From(unityObjectValue),
                _ => throw new InvalidCastException(),
            };
        }

        public void SetValue<TSource>(TSource source)
        {
            switch (source)
            {
                case string s:
                    StringValue = s;
                    break;
                case int i:
                    IntValue = i;
                    break;
                case float f:
                    FloatValue = f;
                    break;
                case bool b:
                    BoolValue = b;
                    break;
                case Vector2 v2:
                    Vector2Value = v2;
                    break;
                case Vector3 v3:
                    Vector3Value = v3;
                    break;
                case Vector4 v4:
                    Vector4Value = v4;
                    break;
                case Color c:
                    ColorValue = c;
                    break;
                case UnityEngine.Object uo:
                    UnityObjectValue = uo;
                    break;
                default:
                    throw new InvalidCastException();
            }
        }

        public void SetValue(VariableType type, object value)
        {
            switch (type)
            {
                case VariableType.String:
                    StringValue = VariableUtility.ImplicitConversion<string>(value);
                    return;
                case VariableType.Int:
                    IntValue = VariableUtility.ImplicitConversion<int>(value);
                    return;
                case VariableType.Float:
                    FloatValue = VariableUtility.ImplicitConversion<float>(value);
                    return;
                case VariableType.Bool:
                    BoolValue = VariableUtility.ImplicitConversion<bool>(value);
                    return;
                case VariableType.Vector2:
                    Vector2Value = VariableUtility.ImplicitConversion<Vector2>(value);
                    return;
                case VariableType.Vector3:
                    Vector3Value = VariableUtility.ImplicitConversion<Vector3>(value);
                    return;
                case VariableType.Vector4:
                    Vector4Value = VariableUtility.ImplicitConversion<Vector4>(value);
                    return;
                case VariableType.UnityObject:
                case VariableType.Generic:
                    UnityObjectValue = VariableUtility.ImplicitConversion<UnityEngine.Object>(value);
                    return;
                default:
                    throw new InvalidCastException();
            }
        }

        /// <summary>
        /// Reset the payload for a new variable type so old values are not reinterpreted.
        /// </summary>
        public void Reset()
        {
            stringValue = string.Empty;
            intValue = default;
            boolValue = default;
            vectorValue = default;
            unityObjectValue = null;
        }
    }
}
