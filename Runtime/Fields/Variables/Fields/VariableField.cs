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
    public class VariableField<T> : VariableValueFieldBase
    {
        [SerializeField] private T value;

        public override Type FieldObjectType => typeof(T);

        /// <summary>
        /// Gets the authored constant value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the field is bound.</exception>
        public T Constant => IsConstant ? value : throw new InvalidOperationException("Cannot read a constant from a bound variable field.");


        public override VariableType Type => GetVariableType<T>();


        public VariableField() { }






        protected override object ReadConstantValue() => value;

        protected override TResult ReadConstant<TResult>() => ImplicitConverter<TResult>.From(value);

        public override object Clone() => Duplicate();


#if UNITY_EDITOR
        protected override void WriteConstantValue(object newValue) => value = ImplicitConversion<T>(newValue);
#endif


        public static implicit operator T(VariableField<T> variableField)
        {
            if (variableField == null) return default;
            return variableField.GetValue<T>();
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
    public class VariableField : DynamicVariableFieldBase
    {
        [SerializeField] protected VariableType type;

        public override bool IsDynamicType => true;
        public override Type FieldObjectType => typeof(object);
        public override VariableType Type { get => type; }

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
        public override void SetRuntimeReference(RuntimeVariable variable)
        {
            base.SetRuntimeReference(variable);
            if (variable is not null) type = variable.Type;
        }

    }
}
