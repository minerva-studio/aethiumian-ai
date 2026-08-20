using System;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Base class of all Variable Reference, a type of field that can only refer to a variable
    /// </summary>
    public abstract class VariableReferenceBase : VariableFieldBase
    {
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
            return variableField.GetValue<T>();
        }
    }

    /// <summary>
    /// a reference field to any variable in the node
    /// </summary>
    [Serializable]
    public class VariableReference : VariableReferenceBase, IDynamicVariableField
    {
        public VariableType type;
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
        public override void SetRuntimeReference(RuntimeVariable variable)
        {
            base.SetRuntimeReference(variable);
            if (variable is not null) type = variable.Type;
        }
    }
}
