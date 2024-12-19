using System;
using System.Reflection;
using UnityEngine;
using static Amlos.AI.Variables.VariableUtility;

namespace Amlos.AI.Variables
{
    [Serializable]
    public class ScriptVariable :
        Variable,
        IScriptVariableData<int>,
        IScriptVariableData<bool>,
        IScriptVariableData<string>,
        IScriptVariableData<float>,
        IScriptVariableData<Vector2>,
        IScriptVariableData<Vector3>,
        IScriptVariableData<Vector4>
    {
        [Header("Field Reference to target script")]
        private MemberInfo member;
        private object targetInstance;

        public Type objectType;
        public VariableType type;


        public ScriptVariable(VariableData data, object target, bool isGlobal = false) : base(data.UUID, data.name)
        {
            member = target.GetType().GetMember(data.Path)[0];
            targetInstance = target;
            Init();
        }

        public void Init()
        {
            objectType = GetResultType(member);
            type = GetVariableType(objectType);
        }


        public override object Value
        {
            get
            {
                switch (member)
                {
                    case FieldInfo fieldInfo:
                        return fieldInfo.GetValue(targetInstance);
                    case PropertyInfo propertyInfo:
                        return propertyInfo.GetValue(targetInstance);
                    case MethodInfo methodInfo:
                        return methodInfo.Invoke(targetInstance, Array.Empty<object>());
                    default:
                        break;
                }
                return null;
            }
        }
        public override VariableType Type => type;
        public override Type ObjectType => objectType;
        public object Target => targetInstance;
        public MemberInfo Member => member;


        Func<int> IScriptVariableData<int>.Getter { get; set; }
        Action<int> IScriptVariableData<int>.Setter { get; set; }
        Func<bool> IScriptVariableData<bool>.Getter { get; set; }
        Action<bool> IScriptVariableData<bool>.Setter { get; set; }
        Func<string> IScriptVariableData<string>.Getter { get; set; }
        Action<string> IScriptVariableData<string>.Setter { get; set; }
        Func<float> IScriptVariableData<float>.Getter { get; set; }
        Action<float> IScriptVariableData<float>.Setter { get; set; }
        Func<Vector2> IScriptVariableData<Vector2>.Getter { get; set; }
        Action<Vector2> IScriptVariableData<Vector2>.Setter { get; set; }
        Func<Vector3> IScriptVariableData<Vector3>.Getter { get; set; }
        Action<Vector3> IScriptVariableData<Vector3>.Setter { get; set; }
        Func<Vector4> IScriptVariableData<Vector4>.Getter { get; set; }
        Action<Vector4> IScriptVariableData<Vector4>.Setter { get; set; }




        public override T GetValue<T>()
        {
            if (this is IScriptVariableData<T> d)
            {
                return d.Value;
            }

            return VariableUtility.ImplicitConversion<T>(Value);
        }

        public override void SetValue<T>(T value)
        {
            if (this is IScriptVariableData<T> d)
            {
                d.Value = value;
                return;
            }
            switch (type)
            {
                case VariableType.String:
                    ((IScriptVariableData<string>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Int:
                    ((IScriptVariableData<int>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Float:
                    ((IScriptVariableData<float>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Bool:
                    ((IScriptVariableData<bool>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector2:
                    ((IScriptVariableData<Vector2>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector3:
                    ((IScriptVariableData<Vector3>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector4:
                    ((IScriptVariableData<Vector4>)this).SetValueWithConversion(value);
                    return;
                case VariableType.UnityObject:
                case VariableType.Generic:
                    SetValueDirect(value);
                    return;
                default:
                case VariableType.Node:
                case VariableType.Invalid:
                    break;
            }
            throw new InvalidCastException($"{value} to {type}");
        }

        void SetValueDirect<T>(T value)
        {
            switch (member)
            {
                case FieldInfo fieldInfo:
                    fieldInfo.SetValue(targetInstance, value);
                    break;
                case PropertyInfo propertyInfo:
                    propertyInfo.SetValue(targetInstance, value);
                    break;
                case MethodInfo methodInfo:
                    methodInfo.Invoke(targetInstance, new object[] { value });
                    break;
                default:
                    break;
            }
        }
    }

    internal interface IScriptVariableData<TValue> : IVariableData<TValue>
    {
        object Target { get; }
        MemberInfo Member { get; }
        Func<TValue> Getter { get; set; }
        Action<TValue> Setter { get; set; }

        TValue IVariableData<TValue>.Value
        {
            get
            {
                return (Getter ??= CreateGetter())();
            }
            set
            {
                (Setter ??= CreateSetter())(value);
            }
        }

        Func<TValue> CreateGetter()
        {
            if (Member is PropertyInfo p)
            {
                return (Func<TValue>)p.GetMethod.CreateDelegate(typeof(Func<TValue>), Target);
            }
            if (Member is MethodInfo m)
            {
                return (Func<TValue>)m.CreateDelegate(typeof(Func<TValue>), Target);
            }
            if (Member is FieldInfo field)
            {
                return () => (TValue)field.GetValue(Target);
            }
            throw new NotImplementedException("Cannot create a getter for member " + Member.Name);
        }

        Action<TValue> CreateSetter()
        {
            if (Member is PropertyInfo p)
            {
                return (Action<TValue>)p.SetMethod.CreateDelegate(typeof(Action<TValue>), Target);
            }
            if (Member is MethodInfo m)
            {
                return (Action<TValue>)m.CreateDelegate(typeof(Action<TValue>), Target);
            }
            if (Member is FieldInfo field)
            {
                return (a) => field.SetValue(Target, a);
            }
            throw new NotImplementedException("Cannot create a getter for member " + Member.Name);
        }
    }
}