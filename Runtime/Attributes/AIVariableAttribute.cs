using Aethiumian.AI.Variables;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Aethiumian.AI
{
    [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class AIVariableAttribute : Attribute
    {
        private const BindingFlags DeclaredInstanceMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        static ConcurrentDictionary<Type, IReadOnlyList<VariableData>> cache = new();

        private readonly string name;
        private readonly UUID uuid;

        public string Name => name;
        public UUID UUID => uuid;

        public AIVariableAttribute(string name)
        {
            this.name = name;
            this.uuid = VariableUtility.CreateStableUUID(name);
        }


        public static IReadOnlyList<VariableData> GetAttributeVariablesFromType(Type type)
        {
            if (cache.TryGetValue(type, out var cachedVariables))
            {
                return cachedVariables;
            }

            List<VariableData> variables = new();
            HashSet<UUID> variableIDs = new();

            AddFieldVariables(type, variables, variableIDs);
            AddPropertyVariables(type, variables, variableIDs);
            AddMethodVariables(type, variables, variableIDs);

            return cache[type] = variables;
        }

        private static void AddFieldVariables(Type type, List<VariableData> variables, HashSet<UUID> variableIDs)
        {
            HashSet<string> seenFieldNames = new();

            foreach (Type current in GetInheritanceChain(type))
            {
                foreach (FieldInfo field in current.GetFields(DeclaredInstanceMembers))
                {
                    // Hidden fields use the derived declaration; the base declaration must not create a second script variable.
                    if (!seenFieldNames.Add(field.Name))
                    {
                        continue;
                    }

                    var attribute = field.GetCustomAttribute<AIVariableAttribute>(false);
                    if (attribute != null)
                    {
                        AddVariable(variables, variableIDs, VariableData.AttributeVariableOf(attribute, field, field.FieldType, field.IsStatic));
                    }
                }
            }
        }

        private static void AddPropertyVariables(Type type, List<VariableData> variables, HashSet<UUID> variableIDs)
        {
            HashSet<string> seenPropertyKeys = new();

            foreach (Type current in GetInheritanceChain(type))
            {
                foreach (PropertyInfo property in current.GetProperties(DeclaredInstanceMembers))
                {
                    string key = GetPropertyOverrideKey(property);
                    if (!seenPropertyKeys.Add(key))
                    {
                        continue;
                    }

                    var attribute = property.GetCustomAttribute<AIVariableAttribute>(false);
                    attribute ??= GetBasePropertyAttribute(property);
                    if (attribute != null)
                    {
                        var isStatic = property.GetGetMethod(true)?.IsStatic ?? property.GetSetMethod(true)?.IsStatic ?? false;
                        AddVariable(variables, variableIDs, VariableData.AttributeVariableOf(attribute, property, property.PropertyType, isStatic));
                    }
                }
            }
        }

        private static void AddMethodVariables(Type type, List<VariableData> variables, HashSet<UUID> variableIDs)
        {
            HashSet<string> seenMethodKeys = new();

            foreach (Type current in GetInheritanceChain(type))
            {
                foreach (MethodInfo method in current.GetMethods(DeclaredInstanceMembers))
                {
                    string key = GetMethodOverrideKey(method);
                    if (!seenMethodKeys.Add(key))
                    {
                        continue;
                    }

                    var attribute = method.GetCustomAttribute<AIVariableAttribute>(false);
                    attribute ??= GetBaseMethodAttribute(method);
                    if (attribute != null)
                    {
                        AddVariable(variables, variableIDs, VariableData.AttributeVariableOf(attribute, method, method.ReturnType, method.IsStatic));
                    }
                }
            }
        }

        private static IEnumerable<Type> GetInheritanceChain(Type type)
        {
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                yield return current;
            }
        }

        private static string GetPropertyOverrideKey(PropertyInfo property)
        {
            MethodInfo accessor = property.GetGetMethod(true) ?? property.GetSetMethod(true);
            if (accessor == null)
            {
                return property.DeclaringType.AssemblyQualifiedName + ":" + property.Name;
            }

            return GetMethodOverrideKey(accessor);
        }

        private static AIVariableAttribute GetBasePropertyAttribute(PropertyInfo property)
        {
            MethodInfo accessor = property.GetGetMethod(true) ?? property.GetSetMethod(true);
            MethodInfo baseAccessor = accessor?.GetBaseDefinition();
            if (accessor == null || baseAccessor == null || baseAccessor == accessor)
            {
                return null;
            }

            foreach (PropertyInfo baseProperty in baseAccessor.DeclaringType.GetProperties(DeclaredInstanceMembers))
            {
                MethodInfo basePropertyAccessor = baseProperty.GetGetMethod(true) ?? baseProperty.GetSetMethod(true);
                if (basePropertyAccessor == null)
                {
                    continue;
                }

                if (basePropertyAccessor.GetBaseDefinition() == baseAccessor)
                {
                    return baseProperty.GetCustomAttribute<AIVariableAttribute>(false);
                }
            }

            return null;
        }

        private static string GetMethodOverrideKey(MethodInfo method)
        {
            MethodInfo baseDefinition = method.GetBaseDefinition();
            MethodInfo keyMethod = baseDefinition ?? method;
            return keyMethod.Module.ModuleVersionId + ":" + keyMethod.MetadataToken;
        }

        private static AIVariableAttribute GetBaseMethodAttribute(MethodInfo method)
        {
            MethodInfo baseDefinition = method.GetBaseDefinition();
            if (baseDefinition == null || baseDefinition == method)
            {
                return null;
            }

            return baseDefinition.GetCustomAttribute<AIVariableAttribute>(false);
        }

        private static void AddVariable(List<VariableData> variables, HashSet<UUID> variableIDs, VariableData variable)
        {
            if (variableIDs.Add(variable.UUID))
            {
                variables.Add(variable);
            }
        }
    }
}
