using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Variable that stores current value of an variable
    /// <br></br>
    /// Used inside an <see cref="BehaviourTree"/> instance
    /// </summary>
    [Serializable]
    public class Variable
    {
        public UUID uuid;
        [SerializeField] private VariableType type;
        [SerializeField] private string name;
        [SerializeField] private object value;

        [SerializeField] private bool isReadOnly;
        [SerializeField] private bool isGlobal;

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
        public UnityEngine.Object unityObjectValue => GetValue<UnityEngine.Object>();


        public Variable(VariableData data, bool isGlobal = false)
        {
            this.isGlobal = isGlobal;
            this.uuid = data.uuid;
            this.name = data.name;
            this.type = data.Type;
            this.value = VariableUtility.Parse(data.Type, data.defaultValue);
        }

        public Variable(AssetReferenceData data)
        {
            this.type = VariableType.UnityObject;
            this.uuid = data.uuid;
            this.name = data.asset.Exist()?.name;
            this.value = data.asset;
        }

        public void SetValue(object value)
        {
            this.value = VariableUtility.ImplicitConversion(type, value);
        }

        protected T GetValue<T>()
        {
            var type = VariableUtility.GetVariableType(typeof(T));
            T t = (T)VariableUtility.ImplicitConversion(type, value);
            return t;
        }
    }
}
