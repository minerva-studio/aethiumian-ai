using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Variables
{
    public abstract class Variable
    {
        [SerializeField] private string name;
        [SerializeField] private UUID uuid;

        /// <summary>
        /// The name of the variable
        /// </summary>
        public string Name => name;
        /// <summary>
        /// UUID of the variable, unique identifying each of variable
        /// </summary>
        public UUID UUID => uuid;




        public string stringValue => GetValue<string>();
        public int intValue => GetValue<int>();
        public float floatValue => GetValue<float>();
        public bool boolValue => GetValue<bool>();
        public Vector2 vector2Value => GetValue<Vector2>();
        public Vector3 vector3Value => GetValue<Vector3>();
        public Vector3 vector4Value => GetValue<Vector4>();
        public Color colorValue => GetValue<Color>();
        public UnityEngine.Object unityObjectValue => GetValue<UnityEngine.Object>();





        /// <summary>
        /// The real value of the variable, boxed
        /// </summary>
        public abstract object Value { get; }
        /// <summary>
        /// Type of the variable, not necessary the object type
        /// </summary>
        public abstract VariableType Type { get; }
        /// <summary>
        /// object type of the variable
        /// </summary>
        public abstract Type ObjectType { get; }
        /// <summary>
        /// Is valid uuid
        /// </summary>
        public bool IsValid => UUID != UUID.Empty;




        public Variable()
        {
        }

        public Variable(UUID uUID, string name)
        {
            uuid = uUID;
            this.name = name;
        }




        public abstract T GetValue<T>();
        public abstract void SetValue<T>(T value);




        public void SetValue(object value) => SetValue<object>(value);




        public bool Equals(Variable variable)
        {
            if (variable is null) return UUID == UUID.Empty;
            return UUID == variable.UUID;
        }

        public override bool Equals(object obj) => Equals(obj as Variable);

        public override int GetHashCode()
        {
            return UUID.GetHashCode();
        }
    }
}