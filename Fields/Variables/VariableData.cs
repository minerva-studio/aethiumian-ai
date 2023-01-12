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
        [SerializeField] private UUID uuid;
        [SerializeField] private VariableType type;

        public string defaultValue;
        public bool isStatic;

        public TypeReference typeReference = new TypeReference();

        /// <summary> Check is the variable a valid variable that has its <see cref="Minerva.Module.UUID"/> label </summary>
        public bool isValid => UUID != UUID.Empty;
        /// <summary> The object type of the variable (the <see cref="System.Type"/>) </summary>
        public Type ObjectType => GetObjectType();
        public VariableType Type => type;
        public UUID UUID => uuid;

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
}
