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

        public override string StringValue => IsConstant ? value.StringValue : Variable.stringValue;
        public override bool BoolValue => IsConstant ? value.BoolValue : Variable.boolValue;
        public override int IntValue => IsConstant ? value.IntValue : Variable.intValue;
        public override float FloatValue => IsConstant ? value.FloatValue : Variable.floatValue;
        public override Vector2 Vector2Value => IsConstant ? value.Vector2Value : Variable.vector2Value;
        public override Vector3 Vector3Value => IsConstant ? value.Vector3Value : Variable.vector3Value;
        public override Vector4 Vector4Value => IsConstant ? value.Vector4Value : Variable.vector4Value;
        public override Color ColorValue => IsConstant ? value.ColorValue : Variable.colorValue;
        public override UnityEngine.Object UnityObjectValue => IsConstant ? value.UnityObjectValue : Variable.unityObjectValue;
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
        protected void ImportLegacyValue(VariableType type, string stringValue, int intValue, float floatValue,
            bool boolValue, Vector2 vector2Value, Vector3 vector3Value, Vector4 vector4Value,
            UnityEngine.Object unityObjectValue)
        {
            object legacy = type switch
            {
                VariableType.String => stringValue, VariableType.Int => intValue, VariableType.Float => floatValue,
                VariableType.Bool => boolValue, VariableType.Vector2 => vector2Value, VariableType.Vector3 => vector3Value,
                VariableType.Vector4 => vector4Value, VariableType.UnityObject or VariableType.Generic => unityObjectValue,
                _ => null,
            };
            if (legacy != null || type is VariableType.String or VariableType.Int or VariableType.Float or VariableType.Bool or VariableType.Vector2 or VariableType.Vector3 or VariableType.Vector4)
            {
                value.SetValue(type, legacy);
            }
        }

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
