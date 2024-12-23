using Amlos.AI.References;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Amlos.AI.Variables
{
    /// <summary>
    /// Variable that stores current value of an variable
    /// <br/>
    /// Used inside an <see cref="BehaviourTree"/> instance
    /// </summary>
    [Serializable]
    public class TreeVariable : Variable,
        IVariableData<int>,
        IVariableData<float>,
        IVariableData<bool>,
        IVariableData<string>,
        IVariableData<Vector2>,
        IVariableData<Vector3>,
        IVariableData<Vector4>
    {
        [SerializeField] private VariableType type;
        [SerializeField] private bool isGlobal;

        [Header("Value type holder")]
        private ValueUnion self;
        [Header("Reference Type and expected type")]
        private object _genericValue;
        private Type objectType;

        private string _stringValue
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return _genericValue as string ?? _genericValue?.ToString() ?? string.Empty;
            }
        }




        /// <summary> the real value stored inside </summary>
        public override object Value
        {
            get
            {
                switch (type)
                {
                    case VariableType.String:
                        return _stringValue;
                    case VariableType.Int:
                        return self._intValue;
                    case VariableType.Float:
                        return self._floatValue;
                    case VariableType.Bool:
                        return self._boolValue;
                    case VariableType.Vector2:
                        return self._vector2Value;
                    case VariableType.Vector3:
                        return self._vector3Value;
                    case VariableType.Vector4:
                        return self._vector4Value;
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
        public override VariableType Type => type;
        public override Type ObjectType => objectType;

        /// <summary> is field a field game object or component </summary>
        public bool IsGameObjectOrComponent => Value is Component or GameObject;




        int IVariableData<int>.Value { get => self._intValue; set => self._intValue = value; }
        float IVariableData<float>.Value { get => self._floatValue; set => self._floatValue = value; }
        bool IVariableData<bool>.Value { get => self._boolValue; set => self._boolValue = value; }
        string IVariableData<string>.Value { get => _stringValue; set => _genericValue = value; }
        Vector2 IVariableData<Vector2>.Value { get => self._vector2Value; set => self._vector2Value = value; }
        Vector3 IVariableData<Vector3>.Value { get => self._vector3Value; set => self._vector3Value = value; }
        Vector4 IVariableData<Vector4>.Value { get => self._vector4Value; set => self._vector4Value = value; }




        public TreeVariable(VariableData data, bool isGlobal = false) : base(data.UUID, data.name)
        {
            this.isGlobal = isGlobal;
            this.type = data.Type;
            SetValue(VariableUtility.Parse(data.Type, data.DefaultValue));
            this.objectType = data.ObjectType;
        }

        public TreeVariable(AssetReferenceData data) : base(data.UUID, data.Name)
        {
            this.isGlobal = true;
            this.type = VariableType.UnityObject;
            SetValue(data.Asset);
            this.objectType = data.GetType();
        }

        public override void SetValue<T>(T value)
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
                    ((IVariableData<string>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Int:
                    ((IVariableData<int>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Float:
                    ((IVariableData<float>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Bool:
                    ((IVariableData<bool>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector2:
                    ((IVariableData<Vector2>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector3:
                    ((IVariableData<Vector3>)this).SetValueWithConversion(value);
                    return;
                case VariableType.Vector4:
                    ((IVariableData<Vector4>)this).SetValueWithConversion(value);
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

        public override T GetValue<T>()
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
                    return VariableUtility.ImplicitConversion<T, int>(self._intValue);
                case VariableType.Float:
                    return VariableUtility.ImplicitConversion<T, float>(self._floatValue);
                case VariableType.Bool:
                    return VariableUtility.ImplicitConversion<T, bool>(self._boolValue);
                case VariableType.Vector2:
                    return VariableUtility.ImplicitConversion<T, Vector2>(self._vector2Value);
                case VariableType.Vector3:
                    return VariableUtility.ImplicitConversion<T, Vector3>(self._vector3Value);
                case VariableType.Vector4:
                    return VariableUtility.ImplicitConversion<T, Vector4>(self._vector4Value);
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
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct ValueUnion
    {
        [FieldOffset(0)] public int _intValue;
        [FieldOffset(0)] public float _floatValue;
        [FieldOffset(0)] public bool _boolValue;
        [FieldOffset(0)] public Vector2 _vector2Value;
        [FieldOffset(0)] public Vector3 _vector3Value;
        [FieldOffset(0)] public Vector4 _vector4Value;

        public void Reset()
        {
            this = default;
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
}
