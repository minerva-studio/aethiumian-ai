using Amlos.Module;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// a variable field in the node with given type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class VariableField<T> : VariableFieldBase
    {
        public VariableType type;

        [DisplayIf(nameof(type), VariableType.String)] public string stringValue = "";
        [DisplayIf(nameof(type), VariableType.Int)] public int intValue;
        [DisplayIf(nameof(type), VariableType.Float)] public float floatValue;
        [DisplayIf(nameof(type), VariableType.Bool)] public bool boolValue;
        [DisplayIf(nameof(type), VariableType.Vector2)] public Vector2 vector2Value;
        [DisplayIf(nameof(type), VariableType.Vector3)] public Vector3 vector3Value;

        public override object Constant { get => GetConstantValue(); }
        protected VariableType ConstantType => type;

        public virtual T Value
        {
            get { return IsConstant ? (T)GetConstantValue() : (T)Variable.Value; }
            set { if (IsConstant) { throw new ArithmeticException(); } else Variable.SetValue(value); }
        }

        public override VariableType Type
        {
            get => GetDataType();
            set => throw new InvalidOperationException("cannot set type to a non-generic variable field");
        }

        protected VariableType GetDataType() => type = GetGenericVariableType<T>();


        public override object Clone()
        {
            return MemberwiseClone();
        }



        protected object GetConstantValue()
        {
            switch (Type)
            {
                case VariableType.String:
                    return stringValue;
                case VariableType.Int:
                    return intValue;
                case VariableType.Float:
                    return floatValue;
                case VariableType.Bool:
                    return boolValue;
                case VariableType.Vector2:
                    return vector2Value;
                case VariableType.Vector3:
                    return vector3Value;
                case VariableType.Invalid:
                default:
                    throw new ArithmeticException();
            }
        }


        public static implicit operator T(VariableField<T> variableField)
        {
            return variableField.Value;
        }

        public static implicit operator VariableField<T>(T value)
        {
            VariableField<T> variableField = new VariableField<T>();
            switch (value)
            {
                case int:
                    variableField.intValue = (int)(object)value;
                    break;
                case float:
                    variableField.floatValue = (float)(object)value;
                    break;
                case bool:
                    variableField.boolValue = (bool)(object)value;
                    break;
                case string:
                    variableField.stringValue = (string)(object)value;
                    break;
                default:
                    break;
            }
            return variableField;
        }
    }

    /// <summary>
    /// a variable field in the node with any type
    /// </summary> 
    [Serializable]
    public class VariableField : VariableField<object>, IGenericVariable
    {
        public override bool IsGeneric => true;
        public override object Constant { get => GetConstantValue(); }
        public override VariableType Type { get => IsConstant ? ConstantType : Variable.type; set { if (IsConstant) type = value; } }


        public override object Value
        {
            get => IsConstant ? GetConstantValue() : Variable.Value;
            set { if (IsConstant) throw new ArithmeticException(); else variable.SetValue(value); }
        }

        public string StringValue => IsConstant ? stringValue : Variable.stringValue;
        public bool BoolValue => IsConstant ? boolValue : Variable.boolValue;
        public int IntValue => IsConstant ? intValue : Variable.intValue;
        public float FloatValue => IsConstant ? floatValue : Variable.floatValue;
        public float NumericValue => ((IGenericVariable)this).GetNumericValue();
        public Vector2 Vector2Value => IsConstant ? vector2Value : Variable.vector2Value;
        public Vector3 Vector3Value => IsConstant ? vector3Value : Variable.vector3Value;
        public Vector3 VectorValue => Type == VariableType.Vector2 ? Vector2Value : Vector3Value;





        public override object Clone()
        {
            return MemberwiseClone();
        }
    }

    /// <summary>
    /// a dynamic variable field in the node that has type controlled by the script
    /// </summary> 
    [Serializable]
    public class Parameter : VariableField
    {
        public Parameter()
        {
            uuid = Core.UUID.Empty;
        }

        public static object[] ToValueArray(TreeNode node, List<Parameter> parameters)
        {
            var arr = new object[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                Parameter item = parameters[i];
                if (item.type == VariableType.Invalid)
                {
                    arr[i] = new NodeProgress(node);
                }
                else
                {
                    arr[i] = item.Value;
                }
            }
            return arr;
        }
    }

}
