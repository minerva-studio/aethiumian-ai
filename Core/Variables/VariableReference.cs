using System;
using UnityEngine;

namespace Amlos.AI
{
    public abstract class VariableReferenceBase : VariableFieldBase
    {
        public override bool IsConstant { get => false; }
        public override object Constant => throw new NotImplementedException();

        public override object Clone()
        {
            return MemberwiseClone();
        }
    }

    /// <summary>
    /// a reference field to type T variable in the node
    /// </summary>
    [Serializable]
    public class VariableReference<T> : VariableReferenceBase
    {
        public override bool IsGeneric => true;
        public T Value { get => (T)Variable.Value; set => Variable.SetValue(value); }

        public override VariableType Type
        {
            get => GetFieldType();
            set => throw new ArithmeticException();
        }



        private VariableType GetFieldType() => GetGenericVariableType<T>();



        public static implicit operator T(VariableReference<T> variableField)
        {
            return variableField.Value;
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
         
        public string StringValue => Variable.stringValue;
        public bool BoolValue => Variable.boolValue;
        public int IntValue => Variable.intValue;
        public float FloatValue => Variable.floatValue;
        public float NumericValue => ((IGenericVariable)this).NumericValue;
        public Vector2 Vector2Value => Variable.vector2Value;
        public Vector3 Vector3Value => Variable.vector3Value;
        public Vector3 VectorValue => ((IGenericVariable)this).VectorValue;

    }

}
