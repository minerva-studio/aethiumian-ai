using System;
using Aethiumian.AI.Accessors;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// The base class of all field type of variable 
    /// Author: Wendell Cai
    /// </summary>
    [Serializable]
    public abstract class VariableFieldBase : ICloneable,
        IDuplicable,
        IVariableBinding
    {
        [SerializeField] private UUID uuid;
        private RuntimeVariable variable;
        /// <summary> ObjectType of the field </summary>
        public abstract Type FieldObjectType { get; }
        /// <summary> Type of the variable field, invariant for non-generic and variant for generics </summary>
        public abstract VariableType Type { get; }

        /// <summary> is field a field of vector type (ie <see cref="Vector2"/>,<see cref="Vector3"/>) </summary>
        public bool IsVector => Type == VariableType.Vector2 || Type == VariableType.Vector3 || Type == VariableType.Vector4;
        /// <summary> is field a field of numeric type (ie <see cref="int"/>,<see cref="float"/>) </summary>
        public bool IsNumeric => Type == VariableType.Int || Type == VariableType.Float;
        /// <summary> is field a field of numeric-like type (ie <see cref="int"/>,<see cref="float"/>,<see cref="bool"/>,<see cref="UnityEngine.Object"/>) </summary>
        public bool IsNumericLike => Type == VariableType.Int || Type == VariableType.Float || Type == VariableType.Bool || Type == VariableType.UnityObject;
        /// <summary> Determine whether given variable can be a game object </summary>
        public bool IsFromGameObject => Type switch
        {
            VariableType.UnityObject => UnityObjectValue is GameObject or Component,
            VariableType.Generic => Value is GameObject or Component,
            _ => false,
        };

        /// <summary> Whether the actual value of the variable is null </summary>/// <summary>
        /// is the variable null? only meaningful when <see cref="HasValue"/> is true
        /// </summary>
        public bool IsNull
        {
            get
            {
                if (!HasValue)
                {
                    if (!HasEditorReference)
                    {
                        return true;
                    }

                    _ = GetRequiredRuntimeVariable();
                }

                switch (Type)
                {
                    case VariableType.Invalid:
                        return true;
                    case VariableType.Int:
                    case VariableType.Float:
                    case VariableType.Bool:
                    case VariableType.Vector2:
                    case VariableType.Vector3:
                    case VariableType.Vector4:
                        return false;
                    case VariableType.String:
                    case VariableType.UnityObject:
                    case VariableType.Generic:
                    case VariableType.Node:
                    default:
                        return Value == null;
                }
            }
        }



        /// <summary>Gets whether this field has a valid runtime value.</summary>
        public virtual bool HasValue => HasReference;
        /// <summary> does this field connect to a variable? (in editor, if the field has uuid refer to)</summary>
        public bool HasEditorReference => uuid != UUID.Empty;
        /// <summary> is this field connect to a variable (in runtime, if the field actually have a variable reference to)? </summary>
        public bool HasReference => variable?.IsValid == true;
        /// <summary> get the variable connect to the field, note this property only available in runtime </summary>
        public RuntimeVariable RuntimeVariable => variable;
        /// <summary> the uuid of the variable </summary>
        public UUID UUID => uuid;


        /// <summary>
        /// Gets the actual value of the referenced variable.
        /// </summary>
        /// <remarks>An empty reference returns <see langword="null"/>. An authored reference that has not been resolved throws.</remarks>
        public virtual object Value
        {
            get
            {
                if (HasReference)
                {
                    return variable.Value;
                }

                return !HasEditorReference ? null : GetRequiredRuntimeVariable().Value;
            }
        }


        /// <summary> Safe to get <see cref="string"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public string StringValue => GetValue<string>();

        /// <summary> Safe to get <see cref="bool"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public bool BoolValue => GetValue<bool>();

        /// <summary> Safe to get <see cref="int"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public int IntValue => GetValue<int>();

        /// <summary> Safe to get <see cref="float"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public float FloatValue => GetValue<float>();

        /// <summary> Safe to get <see cref="Vector2"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector2 Vector2Value => GetValue<Vector2>();

        /// <summary> Safe to get <see cref="Vector3"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector3 Vector3Value => GetValue<Vector3>();

        /// <summary> Safe to get <see cref="Vector4"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector4 Vector4Value => GetValue<Vector4>();

        /// <summary> Safe to get <see cref="Color"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Color ColorValue => GetValue<Color>();

        /// <summary> Safe to get <see cref="UnityEngine.Object"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public UnityEngine.Object UnityObjectValue => GetValue<UnityEngine.Object>();


        /// <summary> Safe to get <see cref="GameObject"/> value of a variable </summary>
        public GameObject GameObjectValue => UnityObjectValue switch
        {
            GameObject gameObject => gameObject,
            Component component => component.gameObject,
            null => null,
            _ => throw new InvalidCastException(),
        };

        /// <summary> Safe to get <see cref="Transform"/> value of a variable </summary>
        public Transform TransformValue => GetComponent<Transform>();

        /// <summary> Save to get <see cref="Vector2Int"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector2Int Vector2IntValue => Vector2Int.RoundToInt(Vector2Value);

        /// <summary> Save to get <see cref="Vector3Int"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector3Int Vector3IntValue => Vector3Int.RoundToInt(Vector3Value);

        /// <summary>
        /// Positional value of the field, if the field is vector, return the vector value, if the field is from game object, return the game object's position
        /// </summary>
        public Vector3 PositionValue
        {
            get
            {
                Vector3 position;
                if (IsVector)
                {
                    position = Vector3Value;
                }
                else if (IsFromGameObject && TransformValue is Transform transform && transform)
                {
                    position = transform.position;
                }
                else throw new InvalidOperationException(
                $"Variable Type \"{Type}\" has invalid value: {this.Value}");
                return position;
            }
        }



        /// <summary> Numeric value of the field </summary>
        /// <exception cref="InvalidCastException"></exception>
        public float NumericValue
        {
            get
            {
                switch (Type)
                {
                    case VariableType.Int:
                        return IntValue;
                    case VariableType.Float:
                        return FloatValue;
                    case VariableType.Bool:
                        return BoolValue ? 1 : 0;
                    case VariableType.UnityObject:
                        return UnityObjectValue ? 1 : 0;
                    case VariableType.Generic:
                        if (Value is float f) return f;
                        else if (Value is int i) return i;
                        else if (Value is bool b) return b ? 1 : 0;
                        else if (Value is UnityEngine.Object o) return o ? 1 : 0;
                        throw new InvalidCastException($"Variable {UUID} is not a numeric type");
                    default:
                        throw new InvalidCastException($"Variable {UUID} is not a numeric type");
                }
            }
        }

        /// <summary>
        /// Scalar value of the field, if the field is vector, return the first component of the vector, if the field is from game object, return 1 if the game object is not null, otherwise return 0
        /// </summary>
        public float ScalarValue
        {
            get
            {
                return Type switch
                {
                    VariableType.Int => IntValue,
                    VariableType.Float => FloatValue,
                    VariableType.Vector2 => Vector2Value.x,
                    VariableType.Vector3 => Vector3Value.x,
                    VariableType.Vector4 => Vector4Value.x,
                    VariableType.Bool => BoolValue ? 1 : 0,
                    VariableType.UnityObject => UnityObjectValue ? 1 : 0,
                    VariableType.Generic => Value switch
                    {
                        float f => f,
                        int i => i,
                        Vector2 v2 => v2.x,
                        Vector2Int v2i => v2i.x,
                        Vector3 v3 => v3.x,
                        Vector3Int v3i => v3i.x,
                        Vector4 v4 => v4.x,
                        Color color => color.r,
                        bool b => b ? 1 : 0,
                        UnityEngine.Object o => o ? 1 : 0,
                        _ => throw new InvalidCastException($"Variable {UUID} is not a scalar type"),
                    },
                    _ => throw new InvalidCastException($"Variable {UUID} is not a scalar type."),
                };
            }
        }

        /// <summary>Gets the scalar projection converted to an integer using truncation.</summary>
        public int IntScalarValue => (int)ScalarValue;


        /// <summary>Gets the complete vector value, preserving all four lanes.</summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector4 VectorValue
        {
            get
            {
                switch (Type)
                {
                    case VariableType.Vector2:
                        {
                            Vector2 value = Vector2Value;
                            return new Vector4(value.x, value.y, 0f, 0f);
                        }
                    case VariableType.Vector3:
                        {
                            Vector3 value = Vector3Value;
                            return new Vector4(value.x, value.y, value.z, 0f);
                        }
                    case VariableType.Vector4:
                        return Vector4Value;
                    case VariableType.Generic:
                        if (Value is Vector2 v2) return new Vector4(v2.x, v2.y, 0f, 0f);
                        else if (Value is Vector2Int v2i) return new Vector4(v2i.x, v2i.y, 0f, 0f);
                        else if (Value is Vector3 v3) return new Vector4(v3.x, v3.y, v3.z, 0f);
                        else if (Value is Vector3Int v3i) return new Vector4(v3i.x, v3i.y, v3i.z, 0f);
                        else if (Value is Vector4 v4) return v4;
                        else if (Value is Color color) return new Vector4(color.r, color.g, color.b, color.a);
                        throw new InvalidCastException($"Variable {UUID} is not a numeric type");
                    default:
                        throw new InvalidCastException($"Variable {UUID} is not a vector type");
                }
            }
        }

        /// <summary>Gets the value normalized to four component-wise floating-point lanes.</summary>
        public Vector4 ComponentwiseValue
        {
            get
            {
                if (IsVector)
                {
                    return VectorValue;
                }
                else if (Type.ComponentCount() == 1)
                {
                    float scalar = ScalarValue;
                    return new Vector4(scalar, scalar, scalar, scalar);
                }
                else
                {
                    throw new InvalidCastException($"Variable {UUID} is not a componentwise type");
                }
            }
        }

        /// <summary>Gets the value normalized to integer component-wise lanes.</summary>
        /// <remarks>Scalar values broadcast, while lanes beyond a vector's declared shape are zero-filled.</remarks>
        internal ComponentwiseInt4 IntComponentwiseValue
        {
            get
            {
                switch (Type)
                {
                    case VariableType.Int:
                        {
                            int value = IntValue;
                            return new ComponentwiseInt4(value, value, value, value);
                        }
                    case VariableType.Float:
                        {
                            int value = (int)FloatValue;
                            return new ComponentwiseInt4(value, value, value, value);
                        }
                    case VariableType.Bool:
                        {
                            int value = BoolValue ? 1 : 0;
                            return new ComponentwiseInt4(value, value, value, value);
                        }
                    case VariableType.Vector2:
                        {
                            Vector2 value = Vector2Value;
                            return new ComponentwiseInt4((int)value.x, (int)value.y, 0, 0);
                        }
                    case VariableType.Vector3:
                        {
                            Vector3 value = Vector3Value;
                            return new ComponentwiseInt4((int)value.x, (int)value.y, (int)value.z, 0);
                        }
                    case VariableType.Vector4:
                        {
                            Vector4 value = Vector4Value;
                            return new ComponentwiseInt4((int)value.x, (int)value.y, (int)value.z, (int)value.w);
                        }
                    default:
                        throw new InvalidCastException($"Variable {UUID} is not a componentwise type");
                }
            }
        }




        /// <summary>
        /// Gets the current value converted to the requested target type.
        /// </summary>
        public virtual TTarget GetValue<TTarget>()
        {
            if (HasReference)
            {
                return variable.GetValue<TTarget>();
            }

            return !HasEditorReference ? default : GetRequiredRuntimeVariable().GetValue<TTarget>();
        }

        /// <summary>
        /// Set the value of the variable base
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="newValue">The value to assign.</param>
        public virtual void SetValue<T>(T newValue) => GetRequiredRuntimeVariable().SetValue(newValue);

        /// <summary>
        /// Get component value from the variable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetComponent<T>()
        {
            UnityEngine.Object value = UnityObjectValue;
            if (value == null) return default;
            if (value is T direct) return direct;

            GameObject gameObject = value switch
            {
                GameObject source => source,
                Component source => source.gameObject,
                _ => null,
            };

            if (gameObject != null &&
                gameObject.TryGetComponent(out T result))
            {
                return result;
            }

            throw new InvalidCastException($"Cannot get component of type {typeof(T)} from {value}.");
        }



        /// <summary>
        /// set the refernce in editor
        /// </summary>
        /// <param name="variable"></param>
        public virtual void SetReference(VariableData variable)
        {
            uuid = variable == null ? UUID.Empty : variable.UUID;
            this.variable = null;
        }

        /// <summary>
        /// set the reference in constructing <see cref="BehaviourTree"/>
        /// </summary>
        /// <param name="variable"></param>
        public virtual void SetRuntimeReference(RuntimeVariable variable)
        {
            if (variable != null)
            {
                uuid = variable.UUID;
            }

            this.variable = variable;
        }

        /// <summary>Gets the resolved runtime variable or throws for an invalid binding state.</summary>
        /// <returns>The valid runtime variable.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the authored reference is unresolved or absent.</exception>
        protected RuntimeVariable GetRequiredRuntimeVariable()
        {
            if (HasReference)
            {
                return variable;
            }

            if (HasEditorReference)
            {
                throw new InvalidOperationException($"Variable field {UUID} has not been resolved to a runtime variable.");
            }

            throw new InvalidOperationException("Variable field has no runtime binding.");
        }

        /// <summary>
        /// Clone the variable
        /// </summary>
        /// <returns></returns>
        public virtual object Clone()
        {
            return Duplicate();
        }

        public virtual object Duplicate()
        {
            return MemberwiseClone();
        }
        public override string ToString()
        {
            return $"Variable {uuid}";
        }
    }


    public static class VariableFieldBaseExtensions
    {
        /// <summary>Writes component-wise data using the destination variable's numeric or vector shape.</summary>
        /// <param name="value">The four-lane value whose used lanes are selected by the destination type.</param>
        public static void SetComponentwiseValue([Writable] this VariableFieldBase field, Vector4 value)
        {
            switch (field.Type)
            {
                case VariableType.Int:
                case VariableType.Float:
                case VariableType.Bool:
                    field.SetValue(value.x);
                    break;
                case VariableType.Vector2:
                    field.SetValue(new Vector2(value.x, value.y));
                    break;
                case VariableType.Vector3:
                    field.SetValue(new Vector3(value.x, value.y, value.z));
                    break;
                case VariableType.Vector4:
                    field.SetValue(value);
                    break;
                default:
                    throw new InvalidCastException($"Variable {field.UUID} is not a componentwise target type.");
            }
        }

        /// <summary>Writes integer component-wise data using the destination variable's shape.</summary>
        internal static void SetComponentwiseValue([Writable] this VariableFieldBase field, ComponentwiseInt4 value)
        {
            switch (field.Type)
            {
                case VariableType.Int:
                case VariableType.Float:
                case VariableType.Bool:
                    field.SetValue(value.x);
                    break;
                case VariableType.Vector2:
                    field.SetValue(new Vector2(value.x, value.y));
                    break;
                case VariableType.Vector3:
                    field.SetValue(new Vector3(value.x, value.y, value.z));
                    break;
                case VariableType.Vector4:
                    field.SetValue(new Vector4(value.x, value.y, value.z, value.w));
                    break;
                default:
                    throw new InvalidCastException($"Variable {field.UUID} is not a componentwise target type.");
            }
        }
    }
}
