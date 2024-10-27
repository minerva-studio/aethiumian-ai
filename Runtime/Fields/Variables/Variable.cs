using Amlos.AI.References;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Variables
{
    /// <summary>
    /// Variable that stores current value of an variable
    /// <br/>
    /// Used inside an <see cref="BehaviourTree"/> instance
    /// </summary>
    [Serializable]
    public class Variable :
        IIntegerVariableData,
        IFloatVariableData,
        IBoolVariableData,
        IStringVariableData,
        IVector2VariableData,
        IVector3VariableData,
        IVector4VariableData
    {
        public UUID uuid;
        [SerializeField] private VariableType type;
        [SerializeField] private bool isGlobal;
        [SerializeField] private string name;

        private int _intValue;
        private float _floatValue;
        private bool _boolValue;
        private string _stringValue;
        private Vector2 _vector2Value;
        private Vector3 _vector3Value;
        private Vector4 _vector4Value;
        private object _genericValue;


        Type objectType;

        /// <summary> the real value stored inside </summary>
        public object Value
        {
            get
            {
                switch (type)
                {
                    case VariableType.String:
                        return _stringValue;
                    case VariableType.Int:
                        return _intValue;
                    case VariableType.Float:
                        return _floatValue;
                    case VariableType.Bool:
                        return _boolValue;
                    case VariableType.Vector2:
                        return _vector2Value;
                    case VariableType.Vector3:
                        return _vector3Value;
                    case VariableType.Vector4:
                        return _vector4Value;
                    case VariableType.UnityObject:
                    case VariableType.Generic:
                        return _genericValue;
                    case VariableType.Node:
                    case VariableType.Invalid:
                    default:
                        throw new NotSupportedException();
                }
            }
        }
        public string Name => name;
        public VariableType Type => type;



        public string stringValue => GetValue<string>();
        public int intValue => GetValue<int>();
        public float floatValue => GetValue<float>();
        public bool boolValue => GetValue<bool>();
        public Vector2 vector2Value => GetValue<Vector2>();
        public Vector3 vector3Value => GetValue<Vector3>();
        public Vector3 vector4Value => GetValue<Vector4>();
        public Color colorValue => GetValue<Color>();
        public UnityEngine.Object unityObjectValue => GetValue<UnityEngine.Object>();
        public Type ObjectType => objectType;



        public bool IsValid => uuid != UUID.Empty;

        /// <summary> is field a field game object or component </summary>
        public bool IsGameObjectOrComponent => Value is Component or GameObject;




        int IVariableData<int>.Value { get => _intValue; set => _intValue = value; }
        float IVariableData<float>.Value { get => _floatValue; set => _floatValue = value; }
        bool IVariableData<bool>.Value { get => _boolValue; set => _boolValue = value; }
        string IVariableData<string>.Value { get => _stringValue; set => _stringValue = value; }
        Vector2 IVariableData<Vector2>.Value { get => _vector2Value; set => _vector2Value = value; }
        Vector3 IVariableData<Vector3>.Value { get => _vector3Value; set => _vector3Value = value; }
        Vector4 IVariableData<Vector4>.Value { get => _vector4Value; set => _vector4Value = value; }





        public Variable(VariableData data, bool isGlobal = false)
        {
            this.isGlobal = isGlobal;
            uuid = data.UUID;
            name = data.name;
            type = data.Type;
            SetValue(VariableUtility.Parse(data.Type, data.defaultValue));
            objectType = data.ObjectType;
        }

        public Variable(AssetReferenceData data)
        {
            isGlobal = true;
            type = VariableType.UnityObject;
            uuid = data.UUID;
            name = data.Asset ? data.Asset.name : string.Empty;
            SetValue(data.Asset);
            objectType = data.GetType();
        }





        public void SetValue(object value) => SetValue<object>(value);

        public void SetValue<T>(T value)
        {
            //this.value = VariableUtility.ImplicitConversion(type, value);
            // same type
            if (VariableTypeProvider<T>.Type == type && this is IVariableData<T> variableData)
            {
                variableData.Value = value;
                return;
            }
            switch (type)
            {
                case VariableType.String:
                    ((IStringVariableData)this).SetValueWithConversion(value);
                    return;
                case VariableType.Int:
                    ((IIntegerVariableData)this).SetValueWithConversion(value);
                    return;
                case VariableType.Float:
                    ((IFloatVariableData)this).SetValueWithConversion(value);
                    return;
                case VariableType.Bool:
                    ((IBoolVariableData)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector2:
                    ((IVector2VariableData)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector3:
                    ((IVector3VariableData)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector4:
                    ((IVector4VariableData)this).SetValueWithConversion(value);
                    return;
                case VariableType.UnityObject:
                    _genericValue = VariableUtility.ImplicitConversion<UnityEngine.Object, T>(value);
                    return;
                case VariableType.Generic:
                    _genericValue = value;
                    return;
                default:
                case VariableType.Node:
                case VariableType.Invalid:
                    break;
            }
            throw new InvalidCastException($"{value} to {type}");
        }

        protected T GetValue<T>()
        {
            //T t = (T)VariableUtility.ImplicitConversion(typeof(T), value); 
            // same type
            if (VariableTypeProvider<T>.Type == type && this is IVariableData<T> variableData)
            {
                return variableData.Value;
            }
            switch (type)
            {
                case VariableType.String:
                    return VariableUtility.ImplicitConversion<T, string>(_stringValue);
                case VariableType.Int:
                    return VariableUtility.ImplicitConversion<T, int>(_intValue);
                case VariableType.Float:
                    return VariableUtility.ImplicitConversion<T, float>(_floatValue);
                case VariableType.Bool:
                    return VariableUtility.ImplicitConversion<T, bool>(_boolValue);
                case VariableType.Vector2:
                    return VariableUtility.ImplicitConversion<T, Vector2>(_vector2Value);
                case VariableType.Vector3:
                    return VariableUtility.ImplicitConversion<T, Vector3>(_vector3Value);
                case VariableType.Vector4:
                    return VariableUtility.ImplicitConversion<T, Vector4>(_vector4Value);
                case VariableType.UnityObject:
                case VariableType.Generic:
                    return VariableUtility.ImplicitConversion<T, object>(_genericValue);
                default:
                case VariableType.Node:
                case VariableType.Invalid:
                    break;
            }
            throw new InvalidCastException();
        }





        public bool Equals(Variable variable)
        {
            if (variable is null) return uuid == UUID.Empty;
            return uuid == variable.uuid;
        }

        public override bool Equals(object obj) => Equals(obj as Variable);

        public override int GetHashCode()
        {
            return uuid.GetHashCode();
        }
    }

    internal interface IVariableData<T>
    {
        T Value { get; set; }

        void SetValueWithConversion<TOther>(TOther value)
        {
            Value = VariableUtility.ImplicitConversion<T, TOther>(value);
        }
    }

    internal interface IIntegerVariableData : IVariableData<int> { }
    internal interface IFloatVariableData : IVariableData<float> { }
    internal interface IStringVariableData : IVariableData<string> { }
    internal interface IBoolVariableData : IVariableData<bool> { }
    internal interface IVector2VariableData : IVariableData<Vector2> { }
    internal interface IVector3VariableData : IVariableData<Vector3> { }
    internal interface IVector4VariableData : IVariableData<Vector4> { }
}
