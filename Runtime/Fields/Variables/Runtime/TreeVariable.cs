using Aethiumian.AI.References;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    /// <summary>
    /// Variable that stores current value of an variable
    /// <br/>
    /// Used inside an <see cref="BehaviourTree"/> instance
    /// </summary>
    [Serializable]
    public class TreeVariable : RuntimeVariable,
        IVariableData<int>,
        IVariableData<float>,
        IVariableData<bool>,
        IVariableData<string>,
        IVariableData<Vector2>,
        IVariableData<Vector3>,
        IVariableData<Vector4>
    {
        [SerializeField] private VariableType type;

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




        public TreeVariable(VariableData data) : base(data.UUID, data.name)
        {
            this.type = data.Type;
            SetValue(data.GetDefaultValue());
            this.objectType = data.ObjectType;
        }

        public override void SetValue<T>(T value)
        {
            if (VariableTypeCatalog.Of<T>() == type)
            {
                if (typeof(T) == typeof(int)) { self._intValue = UnsafeUtility.As<T, int>(ref value); return; }
                if (typeof(T) == typeof(float)) { self._floatValue = UnsafeUtility.As<T, float>(ref value); return; }
                if (typeof(T) == typeof(bool)) { self._boolValue = UnsafeUtility.As<T, bool>(ref value); return; }
                if (typeof(T) == typeof(Vector2)) { self._vector2Value = UnsafeUtility.As<T, Vector2>(ref value); return; }
                if (typeof(T) == typeof(Vector3)) { self._vector3Value = UnsafeUtility.As<T, Vector3>(ref value); return; }
                if (typeof(T) == typeof(Vector4)) { self._vector4Value = UnsafeUtility.As<T, Vector4>(ref value); return; }
                if (typeof(T) == typeof(string)) { _genericValue = value; return; }
            }
            switch (type)
            {
                case VariableType.String:
                    _genericValue = VariableUtility.ImplicitConversion<string, T>(value);
                    return;
                case VariableType.Int:
                    self._intValue = VariableUtility.ImplicitConversion<int, T>(value);
                    return;
                case VariableType.Float:
                    self._floatValue = VariableUtility.ImplicitConversion<float, T>(value);
                    return;
                case VariableType.Bool:
                    self._boolValue = VariableUtility.ImplicitConversion<bool, T>(value);
                    return;
                case VariableType.Vector2:
                    self._vector2Value = VariableUtility.ImplicitConversion<Vector2, T>(value);
                    return;
                case VariableType.Vector3:
                    self._vector3Value = VariableUtility.ImplicitConversion<Vector3, T>(value);
                    return;
                case VariableType.Vector4:
                    self._vector4Value = VariableUtility.ImplicitConversion<Vector4, T>(value);
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
            if (type == VariableType.Int && typeof(T) == typeof(int))
            {
                return UnsafeUtility.As<int, T>(ref self._intValue);
            }
            if (type == VariableType.Float && typeof(T) == typeof(float))
                return UnsafeUtility.As<float, T>(ref self._floatValue);
            if (type == VariableType.Bool && typeof(T) == typeof(bool))
            {
                bool value = self._boolValue;
                return UnsafeUtility.As<bool, T>(ref value);
            }
            if (type == VariableType.Vector2 && typeof(T) == typeof(Vector2))
                return UnsafeUtility.As<Vector2, T>(ref self._vector2Value);
            if (type == VariableType.Vector3 && typeof(T) == typeof(Vector3))
                return UnsafeUtility.As<Vector3, T>(ref self._vector3Value);
            if (type == VariableType.Vector4 && typeof(T) == typeof(Vector4))
                return UnsafeUtility.As<Vector4, T>(ref self._vector4Value);
            if (type == VariableType.String && typeof(T) == typeof(string))
                return (T)_genericValue;
            if (type == VariableType.UnityObject && typeof(T) == typeof(UnityEngine.Object))
                return (T)_genericValue;

            switch (type)
            {
                case VariableType.String:
                    return ImplicitConverter<T>.From(_stringValue);
                case VariableType.Int:
                    return ImplicitConverter<T>.From(self._intValue);
                case VariableType.Float:
                    return ImplicitConverter<T>.From(self._floatValue);
                case VariableType.Bool:
                    return ImplicitConverter<T>.From(self._boolValue);
                case VariableType.Vector2:
                    return ImplicitConverter<T>.From(self._vector2Value);
                case VariableType.Vector3:
                    return ImplicitConverter<T>.From(self._vector3Value);
                case VariableType.Vector4:
                    return ImplicitConverter<T>.From(self._vector4Value);
                case VariableType.UnityObject:
                case VariableType.Generic:
                    return ImplicitConverter<T>.From(_genericValue);
                default:
                case VariableType.Node:
                case VariableType.Invalid:
                    break;
            }
            throw new InvalidCastException();
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Explicit)]
    public struct ValueUnion
    {
        [NonSerialized]
        [FieldOffset(0)] public int _intValue;
        [NonSerialized]
        [FieldOffset(0)] public float _floatValue;
        [NonSerialized]
        [FieldOffset(0)] public Vector2 _vector2Value;
        [NonSerialized]
        [FieldOffset(0)] public Vector3 _vector3Value;

        [FieldOffset(0)] public Vector4 _vector4Value;

        public bool _boolValue { get => _intValue != 0; set => _intValue = value ? 1 : 0; }


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
