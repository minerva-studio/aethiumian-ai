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
        public string name;
        public UUID uuid;
        [SerializeField] private VariableType type;
        public string defaultValue;
        public TypeReference typeReference = new TypeReference();

        /// <summary> Check is the variable a valid variable that has its <see cref="UUID"/> label </summary>
        public bool isValid => uuid != UUID.Empty;
        /// <summary> The object type of the variable (the <see cref="System.Type"/>) </summary>
        public Type ObjectType => GetObjectType();
        public VariableType Type => type;


        public VariableData()
        {
            uuid = UUID.NewUUID();
            typeReference = new TypeReference();
        }

        public VariableData(string name, string defaultValue) : this()
        {
            this.name = name;
            this.defaultValue = defaultValue;
        }

        public VariableData(string name, VariableType variableType, string defaultValue) : this(name, defaultValue)
        {
            type = variableType;
            switch (variableType)
            {
                case VariableType.UnityObject:
                    typeReference.SetBaseType(typeof(UnityEngine.Object));
                    break;
                case VariableType.Generic:
                    typeReference.SetBaseType(typeof(object));
                    break;
                default:
                    break;
            }
        }

        public void SetType(VariableType variableType)
        {
            if (variableType == type) return;

            if (variableType == VariableType.UnityObject)
            {
                UpdateTypeReference(typeof(UnityEngine.Object));
            }
            else if (variableType == VariableType.Generic)
            {
                UpdateTypeReference(typeof(object));
            }
            else
            {
                typeReference.SetBaseType(VariableUtility.GetType(variableType));
            }
            type = variableType;
        }

        public void UpdateTypeReference(Type type)
        {
            typeReference.SetBaseType(type);
            if (typeReference.ReferType == null || !typeReference.ReferType.IsSubclassOf(type))
            {
                typeReference.SetReferType(type);
            }
        }

        private void UpdateTypeReference()
        {
            typeReference = new TypeReference();
            Type referType = VariableUtility.GetType(type);
            typeReference.SetBaseType(referType);
            typeReference.SetReferType(referType);
        }

        private Type GetObjectType()
        {
            if (typeReference == null || typeReference.ReferType == null)
            {
                UpdateTypeReference();
            }

            return typeReference.ReferType;
        }


        public bool IsSubClassof(Type type)
        {
            if (typeReference == null || typeReference.ReferType == null)
            {
                UpdateTypeReference();
            }

            if (typeReference.ReferType == null)
            {
                return false;
            }
            else return typeReference.ReferType.IsSubclassOf(type);
        }
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
        public UnityEngine.Object unityObjectValue => GetValue<UnityEngine.Object>();


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
