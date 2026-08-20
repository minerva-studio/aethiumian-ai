using System;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Base class for fields that can own an authored constant value or bind to a runtime variable.
    /// </summary>
    [Serializable]
    public abstract class VariableValueFieldBase : VariableFieldBase
    {
        /// <summary>Gets whether this field currently owns its authored constant value.</summary>
        public bool IsConstant => !HasEditorReference;

        /// <summary>Gets whether this field has either a constant value or a valid runtime binding.</summary>
        public sealed override bool HasValue => IsConstant || HasReference;

        /// <summary>Reads the authored value through the object compatibility boundary.</summary>
        protected abstract object ReadConstantValue();

        /// <summary>Reads the authored value without first boxing it.</summary>
        protected abstract TTarget ReadConstant<TTarget>();

#if UNITY_EDITOR
        /// <summary>Writes the authored value through the editor object boundary.</summary>
        protected abstract void WriteConstantValue(object value);
#endif

        /// <summary>Gets either the authored constant or the resolved runtime value.</summary>
        public sealed override object Value => IsConstant ? ReadConstantValue() : base.Value;

        /// <summary>Gets either the authored constant or the resolved runtime value in the requested type.</summary>
        public sealed override TTarget GetValue<TTarget>()
        {
            return IsConstant
                ? ReadConstant<TTarget>()
                : base.GetValue<TTarget>();
        }

        /// <summary>Writes a runtime-bound value; authored constants are immutable through this API.</summary>
        public sealed override void SetValue<TValue>(TValue value)
        {
            if (IsConstant)
            {
                throw new InvalidOperationException("Cannot set value to constant.");
            }

            base.SetValue(value);
        }

#if UNITY_EDITOR
        /// <summary>Replaces the authored constant value while the field is not bound.</summary>
        /// <param name="value">The new authored value.</param>
        /// <exception cref="InvalidOperationException">Thrown when the field has an authored reference.</exception>
        public void ForceSetConstantValue(object value)
        {
            if (!IsConstant)
            {
                throw new InvalidOperationException("Cannot set a constant value on a bound variable field.");
            }

            WriteConstantValue(value);
        }
#endif
    }
}
