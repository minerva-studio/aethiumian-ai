using System;
using UnityEngine;

namespace Aethiumian.AI.Variables
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




        public abstract string stringValue { get; }
        public abstract int intValue { get; }
        public abstract float floatValue { get; }
        public abstract bool boolValue { get; }
        public abstract Vector2 vector2Value { get; }
        public abstract Vector3 vector3Value { get; }
        public abstract Vector4 vector4Value { get; }
        public abstract Color colorValue { get; }
        public abstract UnityEngine.Object unityObjectValue { get; }





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
