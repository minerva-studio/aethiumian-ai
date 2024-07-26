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
    public class Variable
    {
        public UUID uuid;
        [SerializeField] private VariableType type;
        [SerializeField] private string name;
        [SerializeField] private object value;
        [SerializeField] private bool isGlobal;
        Type objectType;

        /// <summary> the real value stored inside </summary>
        public object Value => value;
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




        /// <summary> is field a field game object or component </summary>
        public bool IsGameObjectOrComponent => value is Component or GameObject;




        public Variable(VariableData data, bool isGlobal = false)
        {
            this.isGlobal = isGlobal;
            uuid = data.UUID;
            name = data.name;
            type = data.Type;
            value = VariableUtility.Parse(data.Type, data.defaultValue);
            objectType = data.ObjectType;
        }

        public Variable(AssetReferenceData data)
        {
            isGlobal = true;
            type = VariableType.UnityObject;
            uuid = data.UUID;
            name = data.Asset ? data.Asset.name : string.Empty;
            value = data.Asset;
            objectType = data.GetType();
        }

        public void SetValue(object value)
        {
            this.value = VariableUtility.ImplicitConversion(type, value);
        }

        public void SetValue<T>(T value)
        {
            this.value = VariableUtility.ImplicitConversion(type, value);
        }

        protected T GetValue<T>()
        {
            T t = (T)VariableUtility.ImplicitConversion(typeof(T), value);
            return t;
        }
    }
}
