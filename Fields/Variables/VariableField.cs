using Minerva.Module;
using System;
using UnityEngine;
using static Amlos.AI.Variables.VariableUtility;

namespace Amlos.AI.Variables
{
    /// <summary>
    /// a variable field in the node with given type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class VariableField<T> : VariableBase
    {
        public VariableType type;

        [SerializeField][DisplayIf(nameof(type), VariableType.String)] protected string stringValue = "";
        [SerializeField][DisplayIf(nameof(type), VariableType.Int)] protected int intValue;
        [SerializeField][DisplayIf(nameof(type), VariableType.Float)] protected float floatValue;
        [SerializeField][DisplayIf(nameof(type), VariableType.Bool)] protected bool boolValue;
        [SerializeField][DisplayIf(nameof(type), VariableType.Vector2)] protected Vector2 vector2Value;
        [SerializeField][DisplayIf(nameof(type), VariableType.Vector3)] protected Vector3 vector3Value;
        [SerializeField][DisplayIf(nameof(type), VariableType.UnityObject)] protected UUID unityObjectUUIDValue;
        [SerializeField][DisplayIf(nameof(type), VariableType.UnityObject)] protected UnityEngine.Object unityObjectValue;


        protected VariableType ConstantType => type;
        public override Type ObjectType => typeof(T);
        public override object Constant { get => GetConstantValue(); }

        public override string StringValue => IsConstant ? ImplicitConversion<string>(Value) : Variable.stringValue;
        public override bool BoolValue => IsConstant ? ImplicitConversion<bool>(Value) : Variable.boolValue;
        public override int IntValue => IsConstant ? ImplicitConversion<int>(Value) : Variable.intValue;
        public override float FloatValue => IsConstant ? ImplicitConversion<float>(Value) : Variable.floatValue;
        public override Vector2 Vector2Value => IsConstant ? ImplicitConversion<Vector2>(Value) : Variable.vector2Value;
        public override Vector3 Vector3Value => IsConstant ? ImplicitConversion<Vector3>(Value) : Variable.vector3Value;
        public override UnityEngine.Object UnityObjectValue => IsConstant ? unityObjectValue : Variable.unityObjectValue;
        public override UUID ConstanUnityObjectUUID => unityObjectUUIDValue;


        public override object Value
        {
            get => IsConstant ? GetConstantValue() : Variable.Value;
            set { if (IsConstant) { throw new InvalidOperationException("Cannot set value to constant."); } else Variable.SetValue(value); }
        }

        public override VariableType Type
        {
            get => type = GetVariableType(typeof(T));
        }


        public VariableField()
        {
            type = GetVariableType(typeof(T));
        }


        public override object Clone()
        {
            return MemberwiseClone();
        }



        protected object GetConstantValue()
        {
            switch (Type)
            {
                case VariableType.String:
                    return stringValue;
                case VariableType.Int:
                    return intValue;
                case VariableType.Float:
                    return floatValue;
                case VariableType.Bool:
                    return boolValue;
                case VariableType.Vector2:
                    return vector2Value;
                case VariableType.Vector3:
                    return vector3Value;
                case VariableType.UnityObject:
                    return unityObjectValue;
                case VariableType.Node:
                    throw new InvalidOperationException("Cannot get a constant value of type node");
                case VariableType.Invalid:
                default:
                    throw new ArithmeticException();
            }
        }

#if UNITY_EDITOR
        public override void ForceSetConstantValue(object value)
        {
            if (IsConstant)
                switch (Type)
                {
                    case VariableType.String:
                        stringValue = (string)value;
                        break;
                    case VariableType.Int:
                        intValue = (int)value;
                        break;
                    case VariableType.Float:
                        floatValue = (float)value;
                        break;
                    case VariableType.Bool:
                        boolValue = (bool)value;
                        break;
                    case VariableType.Vector2:
                        vector2Value = (Vector2)value;
                        break;
                    case VariableType.Vector3:
                        vector3Value = (Vector3)value;
                        break;
                    case VariableType.Invalid:
                    default:
                        throw new ArithmeticException();
                }
        }
#endif


        public static implicit operator T(VariableField<T> variableField)
        {
            return ImplicitConversion<T>(variableField.Value);
        }

        public static implicit operator VariableField<T>(T value)
        {
            VariableField<T> variableField = new VariableField<T>();
            switch (value)
            {
                case int:
                    variableField.intValue = (int)(object)value;
                    break;
                case float:
                    variableField.floatValue = (float)(object)value;
                    break;
                case bool:
                    variableField.boolValue = (bool)(object)value;
                    break;
                case string:
                    variableField.stringValue = (string)(object)value;
                    break;
                case Vector2:
                    variableField.vector2Value = (Vector2)(object)value;
                    break;
                case Vector3:
                    variableField.vector3Value = (Vector3)(object)value;
                    break;
                default:
                    break;
            }
            return variableField;
        }
    }


    /// <summary>
    /// a variable field in the node with any type
    /// </summary> 
    [Serializable]
    public class VariableField : VariableField<object>, IGenericVariable
    {
        public override bool IsGeneric => true;
        public override object Constant { get => GetConstantValue(); }
        public override VariableType Type { get => type; }
        public bool IsString { get; set; }

        public VariableField() { }
        public VariableField(VariableType type) : this()
        {
            this.type = type;
        }
        public VariableField(object value) : this()
        {
            switch (value)
            {
                case Enum:
                case int:
                    Debug.Log(value);
                    type = VariableType.Int;
                    intValue = (int)value;
                    break;
                case float:
                    type = VariableType.Float;
                    floatValue = (float)value;
                    break;
                case bool:
                    type = VariableType.Bool;
                    boolValue = (bool)value;
                    break;
                case string:
                    type = VariableType.String;
                    stringValue = (string)value;
                    break;
                case Vector2Int:
                    type = VariableType.Vector2;
                    vector2Value = (Vector2)(Vector2Int)value;
                    break;
                case Vector2:
                    type = VariableType.Vector2;
                    vector2Value = (Vector2)value;
                    break;
                case Vector3Int:
                    type = VariableType.Vector3;
                    vector3Value = (Vector3)(Vector3Int)value;
                    break;
                case Vector3:
                    type = VariableType.Vector3;
                    vector3Value = (Vector3)value;
                    break;
                case UnityEngine.Object:
                    type = VariableType.UnityObject;
                    break;
                default:
                    type = VariableType.Generic;
                    Debug.Log(value);
                    Debug.Log("No value");
                    break;
            }
        }




        public override object Clone()
        {
            return MemberwiseClone();
        }

        /// <summary>
        /// set the refernce in editor
        /// </summary>
        /// <param name="variable"></param>
        public override void SetReference(VariableData variable)
        {
            base.SetReference(variable);
            if (variable != null) type = variable.Type;
        }

        /// <summary>
        /// set the reference in constructing <see cref="BehaviourTree"/>
        /// </summary>
        /// <param name="variable"></param>
        public override void SetRuntimeReference(Variable variable)
        {
            base.SetRuntimeReference(variable);
            if (variable != null) type = variable.Type;
        }

        public object GetValue(Type fieldType)
        {
            if (fieldType == typeof(string))
            {
                return StringValue;
            }
            else if (fieldType == typeof(int))
            {
                return IntValue;
            }
            else if (fieldType == typeof(float))
            {
                return FloatValue;
            }
            else if (fieldType == typeof(bool))
            {
                return BoolValue;
            }
            else if (fieldType == typeof(Vector2))
            {
                return Vector2Value;
            }
            else if (fieldType == typeof(Vector2Int))
            {
                return Vector2IntValue;
            }
            else if (fieldType == typeof(Vector3))
            {
                return Vector3Value;
            }
            else if (fieldType == typeof(Vector3Int))
            {
                return Vector3IntValue;
            }

            throw new ArgumentException();
        }
    }
}
