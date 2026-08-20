using System;
using UnityEngine;

namespace Aethiumian.AI.Variables
{
    public abstract class RuntimeVariable
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
        /// Shortcut to get the value of the variable as <see cref="string"/> (Same as <see cref="GetValue&lt;string&gt;()"/>)
        /// </summary>
        public string StringValue => GetValue<string>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="int"/> (Same as <see cref="GetValue&lt;int&gt;()"/>)
        /// </summary>
        public int IntValue => GetValue<int>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="float"/> (Same as <see cref="GetValue&lt;float&gt;()"/>)
        /// </summary>
        public float FloatValue => GetValue<float>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="bool"/> (Same as <see cref="GetValue&lt;bool&gt;()"/>)
        /// </summary>
        public bool BoolValue => GetValue<bool>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="Vector2"/> (Same as <see cref="GetValue&lt;Vector2&gt;()"/>)
        /// </summary>
        public Vector2 Vector2Value => GetValue<Vector2>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="Vector3"/> (Same as <see cref="GetValue&lt;Vector3&gt;()"/>)
        /// </summary>
        public Vector3 Vector3Value => GetValue<Vector3>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="Vector4"/> (Same as <see cref="GetValue&lt;Vector4&gt;()"/>)
        /// </summary>
        public Vector4 Vector4Value => GetValue<Vector4>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="Color"/> (Same as <see cref="GetValue&lt;Color&gt;()"/>)
        /// </summary>
        public Color ColorValue => GetValue<Color>();
        /// <summary>
        /// Shortcut to get the value of the variable as <see cref="UnityEngine.Object"/> (Same as <see cref="GetValue&lt;UnityEngine.Object&gt;()"/>)
        /// </summary>
        public UnityEngine.Object UnityObjectValue => GetValue<UnityEngine.Object>();

        /// <summary>
        /// Is valid variable, a variable is valid if it has a non-empty UUID
        /// </summary>
        public bool IsValid => UUID != UUID.Empty;




        public RuntimeVariable()
        {
        }

        public RuntimeVariable(UUID uUID, string name)
        {
            uuid = uUID;
            this.name = name;
        }




        public abstract T GetValue<T>();
        public abstract void SetValue<T>(T value);
        /// <summary>
        /// Set the value of the variable (boxed) through the canonical conversion pipeline.
        /// </summary>
        /// <param name="value"></param>
        public void SetValue(object value) => SetValue<object>(value);




        public bool Equals(RuntimeVariable variable)
        {
            if (variable is null) return UUID == UUID.Empty;
            return UUID == variable.UUID;
        }

        public override bool Equals(object obj) => Equals(obj as RuntimeVariable);

        public override int GetHashCode() => UUID.GetHashCode();
    }
}
