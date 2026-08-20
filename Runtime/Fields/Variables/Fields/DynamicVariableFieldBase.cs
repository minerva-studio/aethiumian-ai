using Aethiumian.AI.References;
using System;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableUtility;

namespace Aethiumian.AI.Variables
{
    /// <summary>Base class for dynamically typed fields backed by a tagged serialized payload.</summary>
    [Serializable]
    public abstract class DynamicVariableFieldBase : VariableFieldBase
    {
        [SerializeField] private VariableValue value;

        public override object Value => IsConstant ? GetConstantValue() : RuntimeVariable.Value;

        /// <summary>Reads the dynamic payload through the canonical conversion pipeline.</summary>
        public override TResult GetValue<TResult>()
        {
            return IsConstant
                ? value.GetValue<TResult>(Type)
                : RuntimeVariable.GetValue<TResult>();
        }
        protected object GetConstantValue() => value.GetValue(Type);
        protected void SetConstantValue(object constant) => value.SetValue(Type, constant);
        protected void ResetConstantValue() => value.Reset();

        /// <summary>Gets the current value converted for a reflected member type.</summary>
        public object GetValue(Type fieldType) => ImplicitConversion(fieldType, Value);

#if UNITY_EDITOR
        public override void ForceSetConstantValue(object newValue)
        {
            if (IsConstant) SetConstantValue(newValue);
        }
#endif
    }
}
