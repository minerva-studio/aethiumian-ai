using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Amlos.AI
{
    [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    public sealed class AIVariableAttribute : Attribute
    {
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

            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            List<VariableData> variables = new();

            foreach (FieldInfo field in type.GetFields(bindingFlags))
            {
                var attribute = (AIVariableAttribute)Attribute.GetCustomAttribute(field, typeof(AIVariableAttribute));
                if (attribute != null)
                {
                    variables.Add(VariableData.AttributeVariableOf(attribute, field, field.FieldType, field.IsStatic));
                }
            }

            foreach (PropertyInfo property in type.GetProperties(bindingFlags))
            {
                var attribute = (AIVariableAttribute)Attribute.GetCustomAttribute(property, typeof(AIVariableAttribute));
                if (attribute != null)
                {
                    var isStatic = property.GetGetMethod()?.IsStatic ?? property.GetSetMethod()?.IsStatic ?? false;
                    variables.Add(VariableData.AttributeVariableOf(attribute, property, property.PropertyType, isStatic));
                }
            }

            foreach (MethodInfo method in type.GetMethods(bindingFlags))
            {
                var attribute = (AIVariableAttribute)Attribute.GetCustomAttribute(method, typeof(AIVariableAttribute));
                if (attribute != null)
                {
                    variables.Add(VariableData.AttributeVariableOf(attribute, method, method.ReturnType, method.IsStatic));
                }
            }

            return cache[type] = variables;
        }

    }
}
