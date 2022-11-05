using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Base class of all Variable Reference, a type of field that can only refer to a variable
    /// </summary>
    public abstract class VariableReferenceBase : VariableBase
    {
        public override bool IsConstant { get => false; }
        public override object Constant => throw new NotImplementedException();

        public override object Clone()
        {
            return MemberwiseClone();
        }


        public override object Value { get => Variable.Value; set => Variable.SetValue(value); }



        public override string StringValue => Variable.stringValue;
        public override bool BoolValue => Variable.boolValue;
        public override int IntValue => Variable.intValue;
        public override float FloatValue => Variable.floatValue;
        public override Vector2 Vector2Value => Variable.vector2Value;
        public override Vector3 Vector3Value => Variable.vector3Value; 
    }

    /// <summary>
    /// a reference field to type T variable in the node
    /// </summary>
    [Serializable]
    public class VariableReference<T> : VariableReferenceBase
    {
        public override bool IsGeneric => true;

        public override VariableType Type
        {
            get => GetFieldType();
            set => throw new ArithmeticException();
        }



        private VariableType GetFieldType() => GetGenericVariableType<T>();



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
        public override VariableType Type { get => type; set { if (IsConstant) throw new ArithmeticException(); type = value; } }


    }

}
