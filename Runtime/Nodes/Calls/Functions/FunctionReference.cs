using Aethiumian.AI.Accessors;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Serializable callable selected by the function picker.
    /// </summary>
    [Serializable]
    public sealed class FunctionReference : IDuplicable
    {
        public string declaringTypeFullName = string.Empty;
        public string declaringAssemblyName = string.Empty;
        public string methodName = string.Empty;
        public string customId = string.Empty;
        public List<string> parameterTypeNames = new();

        public bool HasMethod => !string.IsNullOrEmpty(methodName);

        public void SetMethod(MethodInfo method, string customId = null)
        {
            methodName = method?.Name ?? string.Empty;
            this.customId = customId ?? string.Empty;
            SetDeclaringType(method?.DeclaringType);
            SetParameterTypes(method);
        }

        public void SetDeclaringType(Type type)
        {
            declaringTypeFullName = type?.FullName ?? string.Empty;
            declaringAssemblyName = type?.Assembly.GetName().Name ?? string.Empty;
        }

        public Type ResolveDeclaringType()
        {
            if (string.IsNullOrEmpty(declaringTypeFullName) || string.IsNullOrEmpty(declaringAssemblyName))
            {
                return null;
            }

            return Type.GetType($"{declaringTypeFullName}, {declaringAssemblyName}", false);
        }

        private void SetParameterTypes(MethodInfo method)
        {
            parameterTypeNames.Clear();
            if (method == null)
            {
                return;
            }

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                Type type = parameter.ParameterType;
                parameterTypeNames.Add($"{type.FullName}, {type.Assembly.GetName().Name}");
            }
        }

        public object Duplicate()
        {
            return new FunctionReference
            {
                declaringTypeFullName = declaringTypeFullName,
                declaringAssemblyName = declaringAssemblyName,
                methodName = methodName,
                customId = customId,
                parameterTypeNames = new List<string>(parameterTypeNames)
            };
        }
    }
}
