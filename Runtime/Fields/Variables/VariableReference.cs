using System;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Base class of all Variable Reference, a type of field that can only refer to a variable
    /// </summary>
    public abstract class VariableReferenceBase : VariableBase
    {
        public sealed override bool IsConstant => false;
        /// <summary>
        /// Variable reference field does not have a constant value, this will throw exception if called
        /// </summary>
        public sealed override object ConstantBoxed => throw new InvalidOperationException("Variable Reference field does not have a constant value.");


        public override object Value => Variable?.Value;



        /// <summary>Reads the referenced variable through the canonical conversion pipeline.</summary>
        public override TResult GetValue<TResult>()
        {
            return Variable.GetValue<TResult>();
        }

        /// <summary>
        /// Generic set value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        public override void SetValue<T>(T value)
        {
            Variable.SetValue(value);
        }

        public override object Clone() => Duplicate();
    }

    /// <summary>
    /// a reference field to type T variable in the node
    /// </summary>
    [Serializable]
    public class VariableReference<T> : VariableReferenceBase
    {
        public override Type FieldObjectType => typeof(T);
        public override VariableType Type => VariableUtility.GetVariableType<T>();

        public static implicit operator T(VariableReference<T> variableField)
        {
            return variableField.Variable.GetValue<T>();
        }
    }

    /// <summary>
    /// a reference field to any variable in the node
    /// </summary>
    [Serializable]
    public class VariableReference : VariableReferenceBase, IDynamicVariableField
    {
        public VariableType type;
        public override bool IsDynamicType => true;
        public override Type FieldObjectType => typeof(object);
        public override VariableType Type { get => type; }


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
