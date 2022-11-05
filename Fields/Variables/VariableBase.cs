using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// The base class of all field type of variable
    /// 
    /// Author: Wendell Cai
    /// </summary>
    [Serializable]
    public abstract class VariableBase : ICloneable
    {
        [SerializeField] private UUID uuid;
        [SerializeField] private Variable variable;

        public abstract VariableType Type { get; set; }
        public abstract object Constant { get; }


        public virtual bool IsConstant { get => uuid == UUID.Empty; }
        public virtual bool IsGeneric => false;
        public bool IsVector => Type == VariableType.Vector2 || Type == VariableType.Vector3;
        public bool IsNumeric => Type == VariableType.Int || Type == VariableType.Float;
        public bool HasReference => uuid != UUID.Empty;
        public Variable Variable => variable;
        public UUID UUID => uuid;


        /// <summary>
        /// Get value from field
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// set value to variable
        /// </summary>
        /// <param name="value"></param>
        public abstract object Value
        {
            get;
            set;
        }


        public abstract string StringValue { get; }
        public abstract bool BoolValue { get; }
        public abstract int IntValue { get; }
        public abstract float FloatValue { get; }
        public abstract Vector2 Vector2Value { get; }
        public abstract Vector3 Vector3Value { get; }


        public float NumericValue
        {
            get
            {
                switch (Type)
                {
                    case VariableType.Int:
                        return IntValue;
                    case VariableType.Float:
                        return FloatValue;
                    default:
                        throw new ArithmeticException($"Variable {UUID} is not a numeric type");
                }
            }
        }

        public Vector3 VectorValue => Type == VariableType.Vector2 ? Vector2Value : Vector3Value;





        public void SetReference(VariableData variable)
        {
            if (variable == null)
            {
                this.uuid = UUID.Empty;
            }
            this.uuid = variable.uuid;
        }

        public void SetRuntimeReference(Variable variable)
        {
            this.variable = variable;
        }


        public virtual object Clone()
        {
            return MemberwiseClone();
        }



        public VariableType[] GetVariableTypes(MemberInfo fieldBaseMemberInfo)
        {
            //non generic case
            if (!(this is VariableField || this is VariableReference))
            {
                return new VariableType[] { Type };
            }

            //generic case 
            var possible = Attribute.GetCustomAttribute(fieldBaseMemberInfo, typeof(TypeLimitAttribute)) is TypeLimitAttribute varLimit
                ? varLimit.VariableTypes
                : (VariableType[])Enum.GetValues(typeof(VariableType));

            possible = Attribute.GetCustomAttribute(fieldBaseMemberInfo, typeof(TypeExcludeAttribute)) is TypeExcludeAttribute varExclude
                ? possible.Except(varExclude.VariableTypes).ToArray()
                : possible;

            return possible;
        }


        protected static VariableType GetGenericVariableType<T>()
        {
            T a = default;
            switch (a)
            {
                case int:
                    return VariableType.Int;
                case string:
                    return VariableType.String;
                case bool:
                    return VariableType.Bool;
                case float:
                    return VariableType.Float;
                case Vector2:
                    return VariableType.Vector2;
                case Vector3:
                    return VariableType.Vector3;
                default:
                    break;
            }
            return default;
        }
    }
}
