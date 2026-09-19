using System;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>Stores one immutable, typed observation of a runtime variable for change detection.</summary>
    public readonly struct VariableValueSnapshot : IEquatable<VariableValueSnapshot>
    {
        private readonly VariableType type;
        private readonly VariableValue value;
        private readonly bool usesGameObjectIdentity;
        private readonly GameObject gameObject;
        private readonly object genericValue;

        private VariableValueSnapshot(
            VariableType type,
            VariableValue value,
            bool usesGameObjectIdentity,
            GameObject gameObject,
            object genericValue)
        {
            this.type = type;
            this.value = value;
            this.usesGameObjectIdentity = usesGameObjectIdentity;
            this.gameObject = gameObject;
            this.genericValue = genericValue;
        }

        /// <summary>Captures the current value of a resolved variable field.</summary>
        /// <param name="field">The variable field to observe.</param>
        /// <returns>A typed snapshot that is independent of later writes to the variable.</returns>
        public static VariableValueSnapshot Capture(VariableFieldBase field)
        {
            if (field == null)
            {
                return default;
            }

            VariableType type = field.Type;
            if (type == VariableType.Generic)
            {
                object genericValue = field.Value;
                if (TryGetGameObject(genericValue, out GameObject gameObject))
                {
                    return new VariableValueSnapshot(type, default, true, gameObject, null);
                }

                return new VariableValueSnapshot(type, default, false, null, genericValue);
            }

            VariableValue value = default;
            switch (type)
            {
                case VariableType.String:
                    value.StringValue = field.StringValue;
                    break;
                case VariableType.Int:
                    value.IntValue = field.IntValue;
                    break;
                case VariableType.Float:
                    value.FloatValue = field.FloatValue;
                    break;
                case VariableType.Bool:
                    value.BoolValue = field.BoolValue;
                    break;
                case VariableType.Vector2:
                    value.Vector2Value = field.Vector2Value;
                    break;
                case VariableType.Vector3:
                    value.Vector3Value = field.Vector3Value;
                    break;
                case VariableType.Vector4:
                    value.Vector4Value = field.Vector4Value;
                    break;
                case VariableType.UnityObject:
                    UnityEngine.Object unityObject = field.UnityObjectValue;
                    if (TryGetGameObject(unityObject, out GameObject gameObject))
                    {
                        return new VariableValueSnapshot(type, default, true, gameObject, null);
                    }

                    value.UnityObjectValue = unityObject;
                    break;
                default:
                    return new VariableValueSnapshot(type, default, false, null, field.Value);
            }

            return new VariableValueSnapshot(type, value, false, null, null);
        }

        /// <summary>Compares two snapshots using variable types and Unity object identity semantics.</summary>
        public bool Equals(VariableValueSnapshot other)
        {
            if (type != other.type || usesGameObjectIdentity != other.usesGameObjectIdentity)
            {
                return false;
            }

            if (usesGameObjectIdentity)
            {
                return gameObject == other.gameObject;
            }

            return type switch
            {
                VariableType.String => value.StringValue == other.value.StringValue,
                VariableType.Int => value.IntValue == other.value.IntValue,
                VariableType.Float => value.FloatValue == other.value.FloatValue,
                VariableType.Bool => value.BoolValue == other.value.BoolValue,
                VariableType.Vector2 => value.Vector2Value == other.value.Vector2Value,
                VariableType.Vector3 => value.Vector3Value == other.value.Vector3Value,
                VariableType.Vector4 => value.Vector4Value == other.value.Vector4Value,
                VariableType.UnityObject => value.UnityObjectValue == other.value.UnityObjectValue,
                VariableType.Generic => object.Equals(genericValue, other.genericValue),
                _ => object.Equals(genericValue, other.genericValue),
            };
        }

        public override bool Equals(object obj) => obj is VariableValueSnapshot other && Equals(other);

        /// <summary>Returns a hash code consistent with <see cref="Equals(VariableValueSnapshot)"/>.</summary>
        public override int GetHashCode()
        {
            if (usesGameObjectIdentity)
            {
                return HashCode.Combine(type, true, gameObject);
            }

            return type switch
            {
                VariableType.String => HashCode.Combine(type, value.StringValue),
                VariableType.Int => HashCode.Combine(type, value.IntValue),
                VariableType.Float => HashCode.Combine(type, value.FloatValue),
                VariableType.Bool => HashCode.Combine(type, value.BoolValue),
                VariableType.Vector2 => HashCode.Combine(type, value.Vector2Value),
                VariableType.Vector3 => HashCode.Combine(type, value.Vector3Value),
                VariableType.Vector4 => HashCode.Combine(type, value.Vector4Value),
                VariableType.UnityObject => HashCode.Combine(type, value.UnityObjectValue),
                _ => HashCode.Combine(type, genericValue),
            };
        }

        private static bool TryGetGameObject(object value, out GameObject gameObject)
        {
            switch (value)
            {
                case GameObject source:
                    gameObject = source;
                    return true;
                case Component source:
                    gameObject = source ? source.gameObject : null;
                    return true;
                default:
                    gameObject = null;
                    return false;
            }
        }
    }
}
