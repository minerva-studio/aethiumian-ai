using Amlos.AI.References;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Variables
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


        public readonly static VariableData GameObjectVariable = new(GAME_OBJECT_VARIABLE_NAME, VariableType.UnityObject) { uuid = localGameObject, isStandard = true, typeReference = typeof(GameObject) };
        public readonly static VariableData TransformVariable = new(TRANSFORM_VARIABLE_NAME, VariableType.UnityObject) { uuid = localTransform, isStandard = true, typeReference = typeof(Transform) };
        public readonly static VariableData TargetScriptVariable = new(TARGET_SCRIPT_VARIABLE, VariableType.UnityObject) { uuid = targetScript, isStandard = true };




        [SerializeField] public string name;
        [SerializeField] private UUID uuid;
        [SerializeField] private VariableType type;

        public string defaultValue;
        public bool isStatic;
        public bool isGlobal;
        public bool isStandard;

        private TypeReference typeReference = new();

        public VariableType Type => type;
        public UUID UUID => uuid;
        /// <summary> Is standard variable in the behaviour tree </summary>
        public bool IsStandardVariable => isStandard;
        /// <summary> Check is the variable a valid variable that has its <see cref="Minerva.Module.UUID"/> label </summary>
        public bool IsValid => UUID != UUID.Empty;
        /// <summary> The object type of the variable (the <see cref="System.Type"/>) </summary>
        public Type ObjectType => GetReferType();
        /// <summary> THe type reference of data value </summary>
        public TypeReference TypeReference => GetTypeReference();






        private VariableData()
        {
            uuid = UUID.NewUUID();
            typeReference = new TypeReference();
        }

        public VariableData(string name) : this()
        {
            this.name = name;
            defaultValue = string.Empty;
        }

        public VariableData(string name, VariableType variableType) : this(name)
        {
            type = variableType;
            switch (variableType)
            {
                case VariableType.UnityObject:
                    TypeReference.SetBaseType(typeof(UnityEngine.Object));
                    break;
                case VariableType.Generic:
                    TypeReference.SetBaseType(typeof(object));
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
                TypeReference.SetBaseType(VariableUtility.GetType(variableType));
            }
            type = variableType;
        }

        public void UpdateTypeReference(Type type)
        {
            TypeReference.SetBaseType(type);
            if (TypeReference.ReferType == null || !TypeReference.ReferType.IsSubclassOf(type))
            {
                TypeReference.SetReferType(type);
            }
        }

        public bool IsSubClassof(Type type)
        {
            if (TypeReference.ReferType == null)
            {
                return false;
            }
            else return TypeReference.ReferType.IsSubclassOf(type);
        }


        private Type GetReferType()
        {
            switch (type)
            {
                case VariableType.String:
                case VariableType.Int:
                case VariableType.Float:
                case VariableType.Bool:
                case VariableType.Vector2:
                case VariableType.Vector3:
                case VariableType.Vector4:
                    return VariableUtility.GetType(type);
                case VariableType.UnityObject:
                case VariableType.Generic:
                default:
                    return TypeReference.ReferType;
            }
        }


        private TypeReference GetTypeReference()
        {
            typeReference ??= new TypeReference();
            if (typeReference.BaseType is null)
            {
                Type referType = VariableUtility.GetType(type);
                typeReference.SetBaseType(referType);
                typeReference.SetReferType(referType);
            }
            else if (typeReference.ReferType is null)
            {
                Type referType = VariableUtility.GetType(type);
                typeReference.SetReferType(referType);
            }
            return typeReference;
        }
    }
}
