using Minerva.Module;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static Amlos.AI.Variables.VariableUtility;

namespace Amlos.AI.Variables
{
    /// <summary>
    /// The base class of all field type of variable
    /// 
    /// Author: Wendell Cai
    /// </summary>
    [Serializable]
    public abstract class VariableBase : ICloneable,
        IStringVariable,
        IIntegerVariable,
        IBoolVariable,
        IFloatVariable,
        IVector2Variable,
        IVector3Variable,
        IVector4Variable,
        IColorVariable,
        IUnityObjectVariable
    {
        [SerializeField] private UUID uuid;
        private Variable variable;
        /// <summary> ObjectType of the field </summary> 
        public abstract Type FieldObjectType { get; }
        /// <summary> Type of the variable field, invariant for non-generic and variant for generics </summary>
        public abstract VariableType Type { get; }

        /// <summary> constant value of the field </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public abstract object ConstantBoxed { get; }


        /// <summary> is field a constant </summary>
        public virtual bool IsConstant => uuid == UUID.Empty;
        /// <summary> is field allowing any type of variable (not same as Generic Variable type) </summary>
        public virtual bool IsGeneric => false;



        /// <summary> is field a field of vector type (ie <see cref="Vector2"/>,<see cref="Vector3"/>) </summary>
        public bool IsVector => Type == VariableType.Vector2 || Type == VariableType.Vector3 || Type == VariableType.Vector4;
        /// <summary> is field a field of numeric type (ie <see cref="int"/>,<see cref="float"/>) </summary>
        public bool IsNumeric => Type == VariableType.Int || Type == VariableType.Float;
        /// <summary> is field a field of numeric-like type (ie <see cref="int"/>,<see cref="float"/>,<see cref="bool"/>,<see cref="UnityEngine.Object"/>) </summary>
        public bool IsNumericLike => Type == VariableType.Int || Type == VariableType.Float || Type == VariableType.Bool || Type == VariableType.UnityObject;
        /// <summary> Determine whether given variable can be a game object </summary>
        public bool IsFromGameObject => Value is GameObject or Component;



        /// <summary> does this field connect to a variable? (in editor, if the field has uuid refer to)</summary>
        public bool HasEditorReference => uuid != UUID.Empty;
        /// <summary> is this field connect to a variable (in runtime, if the field actually have a variable reference to)? </summary>
        public bool HasReference => variable?.IsValid == true;
        /// <summary> is this field a constant or connect to a variable (in runtime, if the field actually have a variable reference to)? </summary>
        public bool HasValue => HasReference || IsConstant;
        /// <summary> get the variable connect to the field, note this property only available in runtime </summary>
        public Variable Variable => variable;
        /// <summary> the uuid of the variable </summary>
        public UUID UUID => uuid;


        /// <summary>
        /// the Actual value of the variable
        /// <br/>
        /// Note that is will always return the value type that matches this variable, (ie <see cref="VariableType.String"/> => <see cref="string"/>, <see cref="VariableType.Int"/> => <see cref="int"/>)
        /// <br/>
        /// use in case of generic variable handling
        /// </summary>
        public abstract object Value { get; }

        /// <summary>
        /// Set the value of the variable base
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        public abstract void SetValue<T>(T value);


        /// <summary> Safe to get <see cref="string"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract string StringValue { get; }

        /// <summary> Safe to get <see cref="bool"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract bool BoolValue { get; }

        /// <summary> Safe to get <see cref="int"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract int IntValue { get; }

        /// <summary> Safe to get <see cref="float"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract float FloatValue { get; }

        /// <summary> Safe to get <see cref="Vector2"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract Vector2 Vector2Value { get; }

        /// <summary> Safe to get <see cref="Vector3"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract Vector3 Vector3Value { get; }

        /// <summary> Safe to get <see cref="Vector4"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract Vector4 Vector4Value { get; }

        /// <summary> Safe to get <see cref="Color"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract Color ColorValue { get; }

        /// <summary> Safe to get <see cref="UnityEngine.Object"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public abstract UnityEngine.Object UnityObjectValue { get; }
        public abstract UUID ConstanUnityObjectUUID { get; }


        /// <summary> Safe to get <see cref="GameObject"/> value of a variable </summary> 
        public GameObject GameObjectValue => ImplicitConversion<GameObject>(Value);

        /// <summary> Safe to get <see cref="Transform"/> value of a variable </summary> 
        public Transform TransformValue => ImplicitConversion<Transform>(Value);

        /// <summary> Save to get <see cref="Vector2Int"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector2Int Vector2IntValue => Vector2Int.RoundToInt(Vector2Value);

        /// <summary> Save to get <see cref="Vector3Int"/> value of a variable </summary>
        /// <exception cref="InvalidCastException"></exception>
        public Vector3Int Vector3IntValue => Vector3Int.RoundToInt(Vector3Value);


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
                    case VariableType.Bool:
                        return BoolValue ? 1 : 0;
                    case VariableType.UnityObject:
                        return UnityObjectValue ? 1 : 0;
                    case VariableType.Generic:
                        if (Value is float f) return f;
                        else if (Value is int i) return i;
                        else if (Value is bool b) return b ? 1 : 0;
                        else if (Value is UnityEngine.Object o) return o ? 1 : 0;
                        throw new InvalidCastException($"Variable {UUID} is not a numeric type");
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
                    case VariableType.Vector4:
                        return Vector4Value;
                    case VariableType.Generic:
                        if (Value is Vector2 v2) return v2;
                        else if (Value is Vector2Int v2i) return (Vector2)v2i;
                        else if (Value is Vector3 v3) return v3;
                        else if (Value is Vector3Int v3i) return v3i;
                        throw new InvalidCastException($"Variable {UUID} is not a numeric type");
                    default:
                        throw new InvalidCastException($"Variable {UUID} is not a vector type");
                }
            }
        }


        /// <summary>
        /// Get component value from the variable
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetComponent<T>() => ImplicitConversion<T>(Value);




        /// <summary>
        /// set the refernce in editor
        /// </summary>
        /// <param name="variable"></param>
        public virtual void SetReference(VariableData variable)
        {
            uuid = variable == null ? UUID.Empty : variable.UUID;
        }

        /// <summary>
        /// set the reference in constructing <see cref="BehaviourTree"/>
        /// </summary>
        /// <param name="variable"></param>
        public virtual void SetRuntimeReference(Variable variable)
        {
            uuid = variable?.UUID ?? UUID.Empty;
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
            var possible = Attribute.GetCustomAttribute(fieldBaseMemberInfo, typeof(ConstraintAttribute)) is ConstraintAttribute varLimit
                ? varLimit.VariableTypes
                : (VariableType[])Enum.GetValues(typeof(VariableType));

            possible = Attribute.GetCustomAttribute(fieldBaseMemberInfo, typeof(ExcludeAttribute)) is ExcludeAttribute varExclude
                ? possible.Except(varExclude.VariableTypes).ToArray()
                : possible;

            return possible;
        }


        public override string ToString()
        {
            return $"Variable {uuid}";
        }
    }
}
