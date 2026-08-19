using Aethiumian.AI.References;
using System;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableUtility;

namespace Aethiumian.AI.Variables
{
    /// <summary>Base class for dynamically typed fields backed by a tagged serialized payload.</summary>
    [Serializable]
    public abstract class DynamicVariableFieldBase : VariableFieldBase,
        IIntegerConstant, IStringConstant, IFloatConstant, IBoolConstant,
        IVector2Constant, IVector3Constant, IVector4Constant, IUnityObjectConstant
    {
        [SerializeField] private VariableValue value;

        // Dynamic fields retain their authored storage type, but callers may read the value
        // through another compatible type (for example an integer range used for a float result).
        public override string StringValue => IsConstant ? GetConstantStringValue() : Variable.stringValue;
        public override bool BoolValue => IsConstant ? GetConstantBoolValue() : Variable.boolValue;
        public override int IntValue => IsConstant ? GetConstantIntValue() : Variable.intValue;
        public override float FloatValue => IsConstant ? GetConstantFloatValue() : Variable.floatValue;
        public override Vector2 Vector2Value => IsConstant ? GetConstantVector2Value() : Variable.vector2Value;
        public override Vector3 Vector3Value => IsConstant ? GetConstantVector3Value() : Variable.vector3Value;
        public override Vector4 Vector4Value => IsConstant ? GetConstantVector4Value() : Variable.vector4Value;
        public override Color ColorValue => IsConstant ? GetConstantColorValue() : Variable.colorValue;
        public override UnityEngine.Object UnityObjectValue => IsConstant ? GetConstantUnityObjectValue() : Variable.unityObjectValue;
        public string ConstantStringValue => value.StringValue;
        public int ConstantIntValue => value.IntValue;
        public float ConstantFloatValue => value.FloatValue;
        public bool ConstantBoolValue => value.BoolValue;
        public Vector2 ConstantVector2Value => value.Vector2Value;
        public Vector3 ConstantVector3Value => value.Vector3Value;
        public Vector4 ConstantVector4Value => value.Vector4Value;
        public UnityEngine.Object ConstantUnityObjectValue => value.UnityObjectValue;
        public override object Value => IsConstant ? GetConstantValue() : Variable.Value;
        public override object ConstantBoxed => GetConstantValue();

        protected object GetConstantValue() => value.GetValue(Type);
        protected void SetConstantValue(object constant)
        {
            value.SetValue(Type, constant);
        }
        protected void ResetConstantValue() => value.Reset();

        private string GetConstantStringValue() => Type == VariableType.String ? value.StringValue : ImplicitConversion<string>(GetConstantValue());
        private int GetConstantIntValue() => Type == VariableType.Int ? value.IntValue : Type == VariableType.Float ? (int)value.FloatValue : ImplicitConversion<int>(GetConstantValue());
        private float GetConstantFloatValue() => Type == VariableType.Float ? value.FloatValue : Type == VariableType.Int ? value.IntValue : ImplicitConversion<float>(GetConstantValue());
        private bool GetConstantBoolValue() => Type switch
        {
            VariableType.Bool => value.BoolValue,
            VariableType.Int => value.IntValue != 0,
            VariableType.Float => value.FloatValue != 0,
            _ => ImplicitConversion<bool>(GetConstantValue()),
        };
        private Vector2 GetConstantVector2Value() => Type == VariableType.Vector2 ? value.Vector2Value : ImplicitConversion<Vector2>(GetConstantValue());
        private Vector3 GetConstantVector3Value() => Type == VariableType.Vector3 ? value.Vector3Value : ImplicitConversion<Vector3>(GetConstantValue());
        private Vector4 GetConstantVector4Value() => Type == VariableType.Vector4 ? value.Vector4Value : ImplicitConversion<Vector4>(GetConstantValue());
        private Color GetConstantColorValue() => Type == VariableType.Vector4 ? value.ColorValue : ImplicitConversion<Color>(GetConstantValue());
        private UnityEngine.Object GetConstantUnityObjectValue() => Type is VariableType.UnityObject or VariableType.Generic
            ? value.UnityObjectValue
            : ImplicitConversion<UnityEngine.Object>(GetConstantValue());
        /// <summary>Gets the current value converted for a reflected member type.</summary>
        public object GetValue(Type fieldType)
        {
            return ImplicitConversion(fieldType, Value);
        }

#if UNITY_EDITOR
        public override void ForceSetConstantValue(object newValue)
        {
            if (IsConstant) SetConstantValue(newValue);
        }
#endif
    }
}
