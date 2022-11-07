using Minerva.Module;
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

        /// <summary> Type of the field </summary> 
        public abstract VariableType Type { get; }

        /// <summary> constant value of the field </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public abstract object Constant { get; }


        /// <summary> is field a constant </summary>
        public virtual bool IsConstant => uuid == UUID.Empty;
        /// <summary> is field allowing any type of variable </summary>
        public virtual bool IsGeneric => false;
        /// <summary> is field a field of vector type (ie <see cref="Vector2"/>,<see cref="Vector3"/>) </summary>
        public bool IsVector => Type == VariableType.Vector2 || Type == VariableType.Vector3;
        /// <summary> is field a field of numeric type (ie <see cref="int"/>,<see cref="float"/>) </summary>
        public bool IsNumeric => Type == VariableType.Int || Type == VariableType.Float;
        /// <summary> does this field connect to a variable? </summary>
        public bool HasReference => uuid != UUID.Empty;
        /// <summary> get the variable connect to the field, note this property only available in runtime </summary>
        public Variable Variable => variable;
        /// <summary> the uuid of the variable </summary>
        public UUID UUID => uuid;


        /// <summary>
        /// the Actual value of the variable
        /// <br></br>
        /// Note that is will always return the value type that matches this variable, (ie <see cref="VariableType.String"/> => <see cref="string"/>, <see cref="VariableType.Int"/> => <see cref="int"/>)
        /// <br></br>
        /// Do not expect this value is save, only use in case of generic variable handling
        /// </summary> 
        /// <exception cref="InvalidOperationException"></exception>
        public abstract object Value { get; set; }


        /// <summary> Save to get <see cref="string"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract string StringValue { get; }

        /// <summary> Save to get <see cref="bool"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract bool BoolValue { get; }

        /// <summary> Save to get <see cref="int"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract int IntValue { get; }

        /// <summary> Save to get <see cref="float"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract float FloatValue { get; }

        /// <summary> Save to get <see cref=" Vector2"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract Vector2 Vector2Value { get; }

        /// <summary> Save to get <see cref=" Vector3"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract Vector3 Vector3Value { get; }

        /// <summary> Numeric value of the field </summary>
        /// <exception cref="InvalidCastException"></exception>
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
                        throw new InvalidCastException($"Variable {UUID} is not a numeric type");
                }
            }
        }

        /// <summary> Vector value of the field </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector3 VectorValue
        {
            get
            {
                switch (Type)
                {
                    case VariableType.Vector2:
                        return Vector2Value;
                    case VariableType.Vector3:
                        return Vector3Value;
                    default:
                        throw new InvalidCastException($"Variable {UUID} is not a vector type");
                }
            }
        }




        /// <summary>
        /// set the refernce in editor
        /// </summary>
        /// <param name="variable"></param>
        public virtual void SetReference(VariableData variable)
        {
            this.uuid = variable == null ? UUID.Empty : variable.uuid;
        }

        /// <summary>
        /// set the reference in constructing <see cref="BehaviourTree"/>
        /// </summary>
        /// <param name="variable"></param>
        public virtual void SetRuntimeReference(Variable variable)
        {
            this.variable = variable;
        }

#if UNITY_EDITOR
        public virtual void ForceSetConstantValue(object value)
        {
            throw new NotImplementedException();
        }
#endif

        /// <summary>
        /// Clone the variable
        /// </summary>
        /// <returns></returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }


        /// <summary>
        /// get restricted variable type allowed in this variable
        /// </summary>
        /// <param name="fieldBaseMemberInfo"></param>
        /// <returns></returns>
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
    }
}
