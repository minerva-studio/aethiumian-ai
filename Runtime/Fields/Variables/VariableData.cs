﻿using Amlos.AI.References;
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
        public const string MISSING_VARIABLE_NAME = "MISSING";
        public const string NONE_VARIABLE_NAME = "NONE";
        public const string GAME_OBJECT_VARIABLE_NAME = "GameObject";
        public const string TARGET_SCRIPT_VARIABLE_NAME = "Target Script";
        public const string TRANSFORM_VARIABLE_NAME = "Transform";

        public readonly static UUID localGameObject = new Guid("ffffffff-ffff-ffff-ffff-000000000000");
        public readonly static UUID targetScript = new Guid("ffffffff-ffff-ffff-ffff-000000000001");
        public readonly static UUID localTransform = new Guid("ffffffff-ffff-ffff-ffff-000000000002");


        public readonly static VariableData GameObjectVariable = new(GAME_OBJECT_VARIABLE_NAME, VariableType.UnityObject) { uuid = localGameObject, isStandard = true, typeReference = typeof(GameObject) };
        public readonly static VariableData TransformVariable = new(TRANSFORM_VARIABLE_NAME, VariableType.UnityObject) { uuid = localTransform, isStandard = true, typeReference = typeof(Transform) };
        public readonly static VariableData TargetScriptVariable = new(TARGET_SCRIPT_VARIABLE_NAME, VariableType.UnityObject) { uuid = targetScript, isStandard = true };




        [SerializeField] public string name;
        [SerializeField] private UUID uuid;
        [SerializeField] private VariableType type;

        [SerializeField] private string defaultValue;
        [SerializeField] private bool isStatic;
        [SerializeField] private bool isGlobal;
        [SerializeField] private bool isStandard;
        [SerializeField] private bool isScript;

        [SerializeField] private TypeReference typeReference = new();

        [SerializeField] private string path;

        /// <summary>
        /// Type of the variable data, if this is a tree variable
        /// </summary>
        public VariableType Type => type;
        /// <summary>
        /// UUID of the variable
        /// </summary>
        public UUID UUID => uuid;
        /// <summary> Check is the variable a valid variable that has its <see cref="Minerva.Module.UUID"/> label </summary>
        public bool IsValid => UUID != UUID.Empty;
        /// <summary> The object type of the variable (the <see cref="System.Type"/>) </summary>
        public Type ObjectType => GetReferType();
        /// <summary> THe type reference of data value </summary>
        public TypeReference TypeReference => GetTypeReference();
        /// <summary> Is standard variable in the behaviour tree, ie local game object, local transforms etc. </summary>
        public bool IsStandardVariable => isStandard;
        public bool IsGlobal { get => isGlobal; set => isGlobal = value; }
        public bool IsStatic { get => isStatic; set => isStatic = value; }
        public string DefaultValue { get => defaultValue; set => defaultValue = value; }
        public bool IsScript => isScript;
        public string Path { get => path; set => path = value; }




        private VariableData()
        {
            uuid = UUID.NewUUID();
            typeReference = new TypeReference();
        }

        public VariableData(string name) : this()
        {
            this.name = name;
            DefaultValue = string.Empty;
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
                    Type referType = TypeReference.ReferType;
                    referType ??= TypeReference.BaseType;
                    return referType;
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

            return typeReference;
        }


        public void SetType(VariableType variableType)
        {
            if (variableType == type) return;

            if (variableType == VariableType.UnityObject)
            {
                SetBaseType(typeof(UnityEngine.Object));
            }
            else if (variableType == VariableType.Generic)
            {
                SetBaseType(typeof(object));
            }
            else
            {
                TypeReference.SetBaseType(VariableUtility.GetType(variableType));
            }
            type = variableType;
        }

        public void SetScript(bool value)
        {
            if (value)
            {
                // for a placeholder
                if (!this.isScript) this.path = name;
                this.isScript = true;
            }
            else
            {
                this.isScript = false;
            }
        }

        /// <summary>
        /// Set base type of the variable
        /// </summary>
        /// <param name="type"></param>
        public void SetBaseType(Type type)
        {
            TypeReference.SetBaseType(type);
            // update the refer type
            if (ObjectType == null || !IsSubclassof(type))
            {
                TypeReference.SetReferType(type);
            }
        }

        public bool IsSubclassof(Type type)
        {
            if (ObjectType == null)
            {
                return false;
            }
            else return ObjectType == type || ObjectType.IsSubclassOf(type);
        }


        public bool IsGameObjectOrComponent()
        {
            if (ObjectType == null)
            {
                return false;
            }
            return IsSubclassof(typeof(GameObject)) || IsSubclassof(typeof(Component));
        }

        /// <summary>
        /// Get the name show for the variable selection dropdown
        /// </summary>
        /// <returns></returns>
        public string GetDescriptiveName()
        {
            if (IsStatic)
            {
                return $"{name} [Static]";
            }
            else if (IsGlobal)
            {
                return $"{name} [Global]";
            }
            else if (isStandard)
            {
                return $"{name} [Standard]";
            }
            else
            {
                return name;
            }
        }


        public static VariableData GetGameObjectVariable()
        {
            return new(GAME_OBJECT_VARIABLE_NAME, VariableType.UnityObject) { uuid = localGameObject, isStandard = true, typeReference = typeof(GameObject) };
        }

        public static VariableData GetTransformVariable()
        {
            return new(TRANSFORM_VARIABLE_NAME, VariableType.UnityObject) { uuid = localTransform, isStandard = true, typeReference = typeof(Transform) };
        }

        public static VariableData GetTargetScriptVariable(TypeReference type)
        {
            return new(TARGET_SCRIPT_VARIABLE_NAME, VariableType.UnityObject) { uuid = targetScript, isStandard = true, typeReference = type };
        }
    }
}
