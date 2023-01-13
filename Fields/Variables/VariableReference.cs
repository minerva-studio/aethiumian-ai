using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Base class of all Variable Reference, a type of field that can only refer to a variable
    /// </summary>
    public abstract class VariableReferenceBase : VariableBase
    {
        public override bool IsConstant => false;
        public override object Constant => throw new InvalidOperationException("Variable Reference field does not have a constant value.");

        public override object Clone()
        {
            return MemberwiseClone();
        }


        public override object Value { get => Variable?.Value; set => Variable.SetValue(value); }



        public override string StringValue => Variable.stringValue;
        public override bool BoolValue => Variable.boolValue;
        public override int IntValue => Variable.intValue;
        public override float FloatValue => Variable.floatValue;
        public override Vector2 Vector2Value => Variable.vector2Value;
        public override Vector3 Vector3Value => Variable.vector3Value;
        public override UnityEngine.Object UnityObjectValue => Variable.unityObjectValue;
        public override UUID ConstanUnityObjectUUID => UUID.Empty;

    }

    /// <summary>
    /// a reference field to type T variable in the node
    /// </summary>
    [Serializable]
    public class VariableReference<T> : VariableReferenceBase
    {
        public override bool IsGeneric => true;
        public override Type ObjectType => typeof(T);
        public override VariableType Type
        {
            get => VariableUtility.GetVariableType(typeof(T));
        }


        public static implicit operator T(VariableReference<T> variableField)
        {
            return (T)variableField.Value;
        }
    }

    /// <summary>
    /// a reference field to any variable in the node
    /// </summary>
    [Serializable]
    public class VariableReference : VariableReference<object>, IGenericVariable
    {
        public VariableType type;
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
            if (variable != null) type = variable.Type;
        }
    }

}
