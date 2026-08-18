using Aethiumian.AI.Attributes;
using Aethiumian.AI.References;
using System;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableUtility;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// a variable field in the node with given type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class VariableField<T> : VariableFieldBase, ISerializationCallbackReceiver
    {
        [SerializeField] private T value;
        [SerializeField] protected VariableType type;
        [SerializeField] private int payloadVersion;

        [SerializeField][DisplayIf(nameof(type), VariableType.String)] protected string stringValue = "";
        [SerializeField][DisplayIf(nameof(type), VariableType.Int)] protected int intValue;
        [SerializeField][DisplayIf(nameof(type), VariableType.Float)] protected float floatValue;
        [SerializeField][DisplayIf(nameof(type), VariableType.Bool)] protected bool boolValue;
        [SerializeField][DisplayIf(nameof(type), VariableType.Vector2)] protected Vector2 vector2Value;
        [SerializeField][DisplayIf(nameof(type), VariableType.Vector3)] protected Vector3 vector3Value;
        [SerializeField][DisplayIf(nameof(type), VariableType.Vector4)] protected Vector4 vector4Value;
        [SerializeField][DisplayIf(nameof(type), VariableType.UnityObject)] protected UnityEngine.Object unityObjectValue;


        protected VariableType ConstantType => type;
        public override Type FieldObjectType => typeof(T);
        public override string StringValue => IsConstant ? ImplicitConversion<string>(value) : Variable.stringValue;
        public override bool BoolValue => IsConstant ? ImplicitConversion<bool>(value) : Variable.boolValue;
        public override int IntValue => IsConstant ? ImplicitConversion<int>(value) : Variable.intValue;
        public override float FloatValue => IsConstant ? ImplicitConversion<float>(value) : Variable.floatValue;
        public override Vector2 Vector2Value => IsConstant ? ImplicitConversion<Vector2>(value) : Variable.vector2Value;
        public override Vector3 Vector3Value => IsConstant ? ImplicitConversion<Vector3>(value) : Variable.vector3Value;
        public override Vector4 Vector4Value => IsConstant ? ImplicitConversion<Vector4>(value) : Variable.vector4Value;
        public override Color ColorValue => IsConstant ? ImplicitConversion<Color>(value) : Variable.colorValue;
        public override UnityEngine.Object UnityObjectValue => IsConstant ? ImplicitConversion<UnityEngine.Object>(value) : Variable.unityObjectValue;


        public string ConstantStringValue => ImplicitConversion<string>(value);
        public int ConstantIntValue => ImplicitConversion<int>(value);
        public float ConstantFloatValue => ImplicitConversion<float>(value);
        public bool ConstantBoolValue => ImplicitConversion<bool>(value);
        public Vector2 ConstantVector2Value => ImplicitConversion<Vector2>(value);
        public Vector3 ConstantVector3Value => ImplicitConversion<Vector3>(value);
        public Vector4 ConstantVector4Value => ImplicitConversion<Vector4>(value);
        public UnityEngine.Object ConstantUnityObjectValue => ImplicitConversion<UnityEngine.Object>(value);


        /// <summary>
        /// The value variable field holding
        /// </summary>
        public override object Value => IsConstant ? value : Variable.Value;


        /// <summary>
        /// Boxed constant of the field
        /// </summary>
        public override object ConstantBoxed => value;
        /// <summary>
        /// unboxed constant value if possible
        /// </summary>
        public T Constant => value;


        public override VariableType Type
        {
            get => GetVariableType<T>();
        }


        public VariableField()
        {
            type = GetVariableType<T>();
        }







        /// <summary>
        /// The value variable field holding
        /// </summary>
        public override void SetValue<TValue>(TValue value)
        {
            if (IsConstant) throw new InvalidOperationException("Cannot set value to constant.");
            Variable.SetValue(value);
        }


        public override object Clone()
        {
            return Duplicate();
        }


        /// <summary>
        /// Get constant value and try to avoid boxing for primitive
        /// </summary>
        /// <typeparam name="TType"></typeparam>
        /// <returns></returns>
        protected TType GetConstantValue_Generic<TType>()
        {
            var varType = GetVariableType<TType>();
            return varType == Type && this is IConstantType<TType> variable ? variable.Value : ImplicitConversion<TType>(GetLegacyValue());
        }

        private object GetLegacyValue()
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
                case VariableType.Vector4:
                    return vector4Value;
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
            if (IsConstant) this.value = ImplicitConversion<T>(value);
        }
#endif


        public static implicit operator T(VariableField<T> variableField)
        {
            if (variableField == null) return default;
            if (variableField.IsConstant) return variableField.Constant;
#if UNITY_EDITOR
            // before linking, then cannot get a value
            if (!variableField.HasReference)
            {
                return default;
            }
#endif
            return variableField.Variable.GetValue<T>();
        }

        public static implicit operator VariableField<T>(T value)
        {
            VariableField<T> variableField = new VariableField<T>();
            variableField.SetReference(null);
            variableField.value = value;
            return variableField;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            payloadVersion = 1;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (payloadVersion != 0) return;
            value = ImplicitConversion<T>(GetLegacyValue());
        }
    }


    /// <summary>
    /// a variable field in the node with any type
    /// </summary> 
    [Serializable]
    public class VariableField : DynamicVariableFieldBase, IDynamicVariableField, ISerializationCallbackReceiver
    {
        [SerializeField] protected VariableType type;
        [SerializeField] private int payloadVersion;
        [SerializeField] private string stringValue = "";
        [SerializeField] private int intValue;
        [SerializeField] private float floatValue;
        [SerializeField] private bool boolValue;
        [SerializeField] private Vector2 vector2Value;
        [SerializeField] private Vector3 vector3Value;
        [SerializeField] private Vector4 vector4Value;
        [SerializeField] private UnityEngine.Object unityObjectValue;

        public override bool IsDynamicType => true;
        public override Type FieldObjectType => typeof(object);
        public override VariableType Type { get => type; }
        public bool IsString { get; set; }

        public VariableField() { }
        public VariableField(VariableType type) : this()
        {
            this.type = type;
        }
        public VariableField(object value) : this()
        {
            type = GetVariableType(value?.GetType());
            if (value is Enum enumValue) type = VariableType.Int;
            SetConstantValue(value is Enum ? Convert.ToInt32(value) : value);
        }




        public void ForceSetConstantType(VariableType variableType)
        {
            if (type == variableType) return;
            type = variableType;
            ResetConstantValue();
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
            if (variable is not null) type = variable.Type;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            payloadVersion = 1;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (payloadVersion != 0) return;
            ImportLegacyValue(type, stringValue, intValue, floatValue, boolValue, vector2Value, vector3Value,
                vector4Value, unityObjectValue);
        }
    }
}
