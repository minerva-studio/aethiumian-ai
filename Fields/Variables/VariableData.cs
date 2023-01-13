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
        public const string GAME_OBJECT_VARIABLE_NAME = "GameObject";
        public const string TARGET_SCRIPT_VARIABLE = "Target Script";
        public const string TRANSFORM_VARIABLE_NAME = "Transform";

        public readonly static UUID localGameObject = new Guid("ffffffff-ffff-ffff-ffff-000000000000");
        public readonly static UUID targetScript = new Guid("ffffffff-ffff-ffff-ffff-000000000001");
        public readonly static UUID localTransform = new Guid("ffffffff-ffff-ffff-ffff-000000000002");


        public readonly static VariableData GameObjectVariable = new(GAME_OBJECT_VARIABLE_NAME, VariableType.UnityObject) { uuid = localGameObject, isStandard = true };
        public readonly static VariableData TransformVariable = new(TRANSFORM_VARIABLE_NAME, VariableType.UnityObject) { uuid = localTransform, isStandard = true };
        public readonly static VariableData TargetScriptVariable = new(TARGET_SCRIPT_VARIABLE, VariableType.UnityObject) { uuid = targetScript, isStandard = true };




        [SerializeField] public string name;
        [SerializeField] private UUID uuid;
        [SerializeField] private VariableType type;

        public string defaultValue;
        public bool isStatic;
        public bool isGlobal;
        public bool isStandard;

        public TypeReference typeReference = new TypeReference();

        /// <summary>
        /// Is standard variable in the behaviour tree
        /// </summary>
        public bool IsStandardVariable => isStandard;
        /// <summary> Check is the variable a valid variable that has its <see cref="Minerva.Module.UUID"/> label </summary>
        public bool isValid => UUID != UUID.Empty;
        /// <summary> The object type of the variable (the <see cref="System.Type"/>) </summary>
        public Type ObjectType => GetObjectType();
        public VariableType Type => type;
        public UUID UUID => uuid;

        private VariableData()
        {
            uuid = UUID.NewUUID();
            typeReference = new TypeReference();
        }

        public VariableData(string name) : this()
        {
            this.name = name;
            this.defaultValue = string.Empty;
        }

        public VariableData(string name, VariableType variableType) : this(name)
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
    }
}
