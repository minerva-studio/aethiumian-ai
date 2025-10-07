using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using static Amlos.AI.Variables.VariableUtility;

namespace Amlos.AI.Variables
{
    /// <summary>
    /// Data of an variable in <see cref="BehaviourTreeData"/>, use for intitalization of variables
    /// </summary>
    [Serializable]
    public class VariableData : IEquatable<VariableData>
    {
        public const string MISSING_VARIABLE_NAME = "MISSING";
        public const string NONE_VARIABLE_NAME = "NONE";
        public const string GAME_OBJECT_VARIABLE_NAME = "GameObject";
        public const string TARGET_SCRIPT_VARIABLE_NAME = "Target Script";
        public const string TRANSFORM_VARIABLE_NAME = "Transform";

        public readonly static UUID localGameObject = new Guid("ffffffff-ffff-ffff-ffff-000000000000");
        public readonly static UUID targetScript = new Guid("ffffffff-ffff-ffff-ffff-000000000001");
        public readonly static UUID localTransform = new Guid("ffffffff-ffff-ffff-ffff-000000000002");


        public readonly static VariableData GameObjectVariable = GetGameObjectVariable();
        public readonly static VariableData TransformVariable = GetTransformVariable();
        public readonly static VariableData TargetScriptVariable = StandardOf(TARGET_SCRIPT_VARIABLE_NAME, targetScript, VariableType.UnityObject);




        [SerializeField] public string name;
        [SerializeField] private UUID uuid;
        [SerializeField] private VariableType type;
        [SerializeField] private VariableFlag flags;
        [SerializeField] private string defaultValue;
        [SerializeField] private GenericTypeReference typeReference = new();
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
        public GenericTypeReference TypeReference => GetTypeReference();
        /// <summary> Variable flag </summary>
        public VariableFlag Flags { get => flags; set => flags = value; }
        /// <summary> Is standard variable in the behaviour tree, ie local game object, local transforms etc. </summary>
        public bool IsStandardVariable => (flags & VariableFlag.Standard) != 0;
        public bool IsGlobal { get => (flags & VariableFlag.Global) != 0; set => SetMask(ref flags, VariableFlag.Global, value); }
        public bool IsStatic { get => (flags & VariableFlag.Static) != 0; set => SetMask(ref flags, VariableFlag.Static, value); }
        public bool IsScript => (flags & VariableFlag.FromScript) != 0 || IsFromAttribute;
        public bool IsFromAttribute => (flags & VariableFlag.FromAttribute) != 0;

        public string DefaultValue { get => defaultValue; set => defaultValue = value; }
        public string Path { get => path; set => path = value; }

        private VariableData()
        {
            uuid = UUID.NewUUID();
            typeReference = new GenericTypeReference();
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

        private GenericTypeReference GetTypeReference()
        {
            typeReference ??= new GenericTypeReference();
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
            if (value && !IsScript)
            {
                // for a placeholder
                this.path = name;
            }
            SetMask(ref flags, VariableFlag.FromScript, value);
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




        public bool? IsReadable(Type type)
        {
            MemberInfo[] memberInfos = type.GetMember(Path);
            if (memberInfos.Length > 0)
            {
                MemberInfo memberInfo = memberInfos[0];
                var memberResultType = GetResultType(memberInfo);
                VariableType selected = GetVariableType(memberResultType);
                return CanRead(memberInfo);
            }
            return null;
        }

        public bool? IsWritable(Type type)
        {
            MemberInfo[] memberInfos = type.GetMember(Path);
            if (memberInfos.Length > 0)
            {
                MemberInfo memberInfo = memberInfos[0];
                var memberResultType = GetResultType(memberInfo);
                VariableType selected = GetVariableType(memberResultType);
                return CanWrite(memberInfo);
            }
            return null;
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
            else if (IsStandardVariable)
            {
                return $"{name} [Standard]";
            }
            else
            {
                return name;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableData GetGameObjectVariable()
            => StandardOf(GAME_OBJECT_VARIABLE_NAME, localGameObject, VariableType.UnityObject, typeof(GameObject));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableData GetTransformVariable()
            => StandardOf(TRANSFORM_VARIABLE_NAME, localTransform, VariableType.UnityObject, typeof(Transform));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VariableData GetTargetScriptVariable(GenericTypeReference type)
            => StandardOf(TARGET_SCRIPT_VARIABLE_NAME, targetScript, VariableType.UnityObject, type);

        public static List<VariableData> GetAttributeVariablesFromType(Type type)
        {
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            List<VariableData> variables = new();

            foreach (FieldInfo field in type.GetFields(bindingFlags))
            {
                var attribute = (AIVariableAttribute)Attribute.GetCustomAttribute(field, typeof(AIVariableAttribute));
                if (attribute != null)
                {
                    // Debug.Log($"Field '{field.Name}' of type '{field.FieldType}' has AIVariableAttribute.");
                    variables.Add(AttributeVariableOf(attribute, field, field.FieldType, field.IsStatic));
                }
            }

            foreach (PropertyInfo property in type.GetProperties(bindingFlags))
            {
                var attribute = (AIVariableAttribute)Attribute.GetCustomAttribute(property, typeof(AIVariableAttribute));
                if (attribute != null)
                {
                    // .Log($"Field '{property.Name}' of type '{property.PropertyType}' has AIVariableAttribute.");
                    var isStatic = property.GetGetMethod()?.IsStatic ?? property.GetSetMethod()?.IsStatic ?? false;
                    variables.Add(AttributeVariableOf(attribute, property, property.PropertyType, isStatic));
                }
            }

            foreach (MethodInfo method in type.GetMethods(bindingFlags))
            {
                var attribute = (AIVariableAttribute)Attribute.GetCustomAttribute(method, typeof(AIVariableAttribute));
                if (attribute != null)
                {
                    // Debug.Log($"Field '{method.Name}' of type '{method.ReturnType}' has AIVariableAttribute.");
                    variables.Add(AttributeVariableOf(attribute, method, method.ReturnType, method.IsStatic));
                }
            }

            return variables;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VariableData StandardOf(string str, UUID id, VariableType variableType, Type referenceType = null)
            => new() { name = str, uuid = id, flags = VariableFlag.Standard, type = variableType, typeReference = referenceType };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VariableData AttributeVariableOf(AIVariableAttribute attribute, MemberInfo member, Type type, bool isStatic)
        {
            return new(attribute.Name, VariableUtility.GetVariableType(type))
            {
                uuid = attribute.UUID,
                typeReference = type,
                Path = member.Name,
                flags = isStatic ? VariableFlag.FromScriptAttributeStaticVariable : VariableFlag.FromScriptAttribute,
            };
        }

        private static VariableFlag SetMask(ref VariableFlag baseValue, VariableFlag flag, bool set)
        {
            if (set) return baseValue |= flag;
            return baseValue &= ~flag;
        }

        public bool Equals(VariableData other)
        {
            return uuid == other.UUID;
        }
    }
}
