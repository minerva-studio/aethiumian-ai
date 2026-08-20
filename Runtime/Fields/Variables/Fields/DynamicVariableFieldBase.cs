using Aethiumian.AI.References;
using System;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableUtility;

namespace Aethiumian.AI.Variables
{
    /// <summary>Base class for dynamically typed fields backed by a tagged serialized payload.</summary>
    [Serializable]
    public abstract class DynamicVariableFieldBase : VariableValueFieldBase, IDynamicVariableField
    {
        [SerializeField] private VariableValue value;

        protected override object ReadConstantValue() => value.GetValue(Type);
        protected override TResult ReadConstant<TResult>() => value.GetValue<TResult>(Type);
        protected void SetConstantValue(object constant) => value.SetValue(Type, constant);
        protected void ResetConstantValue() => value.Reset();

#if UNITY_EDITOR
        protected override void WriteConstantValue(object newValue) => SetConstantValue(newValue);
#endif

        /// <summary>Gets the current value converted for a reflected member type.</summary>
        public object GetValue(Type fieldType) => ImplicitConversion(fieldType, Value);

    }
}
