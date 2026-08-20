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
        public override string StringValue => GetValue<string>();
        public override bool BoolValue => GetValue<bool>();
        public override int IntValue => GetValue<int>();
        public override float FloatValue => GetValue<float>();
        public override Vector2 Vector2Value => GetValue<Vector2>();
        public override Vector3 Vector3Value => GetValue<Vector3>();
        public override Vector4 Vector4Value => GetValue<Vector4>();
        public override Color ColorValue => GetValue<Color>();
        public override UnityEngine.Object UnityObjectValue => GetValue<UnityEngine.Object>();

        /// <summary>Reads the dynamic payload through the canonical conversion pipeline.</summary>
        public override TResult GetValue<TResult>()
        {
            return IsConstant
                ? value.GetValue<TResult>(Type)
                : Variable.GetValue<TResult>();
        }
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
