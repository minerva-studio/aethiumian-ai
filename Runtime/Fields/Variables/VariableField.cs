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
    public class VariableField<T> : VariableFieldBase
    {
        [SerializeField] private T value;
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


        public VariableField() { }







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

    }


    /// <summary>
    /// a variable field in the node with any type
    /// </summary> 
    [Serializable]
    public class VariableField : DynamicVariableFieldBase, IDynamicVariableField
    {
        [SerializeField] protected VariableType type;

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

    }
}
