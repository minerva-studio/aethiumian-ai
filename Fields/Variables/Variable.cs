using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Data of an variable in <see cref="BehaviourTreeData"/>, use for intitalization of variables
    /// </summary>
    [Serializable]
    public class VariableData
    {
        public UUID uuid;
        public VariableType type;
        public string name;
        public string defaultValue;

        public VariableData()
        {
            uuid = UUID.NewUUID();
        }
        public VariableData(string name, string defaultValue) : this()
        {
            this.name = name;
            this.defaultValue = defaultValue;
        }

        /// <summary>
        /// Check is the variable a valid variable that has its uuid label
        /// </summary>
        public bool isValid => uuid != UUID.Empty;
    }

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


        public Variable(string name)
        {
            this.name = name;
            uuid = UUID.NewUUID();
        }
        public Variable(string name, object defaultValue)
        {
            this.name = name;
            this.value = defaultValue;
            uuid = UUID.NewUUID();
        }
        public Variable(VariableData data)
        {
            this.uuid = data.uuid;
            this.name = data.name;
            this.type = data.type;
            this.value = VariableUtility.Parse(data.type, data.defaultValue);
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
