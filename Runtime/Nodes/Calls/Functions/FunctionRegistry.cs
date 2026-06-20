using Amlos.AI.References;
using Amlos.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Resolves and describes methods used by FunctionCall and its editor picker.
    /// </summary>
    public static class FunctionRegistry
    {
        public enum ReceiverAssignment
        {
            None,
            Preserve,
            TargetScript,
            GameObject,
            Transform
        }

        public sealed class FunctionCandidate
        {
            public MethodInfo Method { get; set; }
            public string Path { get; set; }
            public string CustomId { get; set; }
            public string DisplayName { get; set; }
            public Type ReceiverType { get; set; }
            public bool RequiresReceiver { get; set; }
            public bool IsRegistered { get; set; }
            public ReceiverAssignment ReceiverAssignment { get; set; }
            public string SearchText => $"{Path} {DisplayName} {FormatSignature(Method, GetDisplayReceiverType())} {ReceiverType?.Name}";

            public Type GetDisplayReceiverType()
            {
                return RequiresReceiver ? ReceiverType : null;
            }
        }

        private static readonly Dictionary<string, MethodInfo> customMethods = new();
        private static bool customMethodsLoaded;

        public static IEnumerable<FunctionCandidate> GetCustomFunctions()
        {
            EnsureCustomMethods();
            foreach (var item in customMethods)
            {
                AIFunctionAttribute attribute = item.Value.GetCustomAttribute<AIFunctionAttribute>();
                string path = string.IsNullOrEmpty(attribute?.Path) ? "Global" : $"Global/{attribute.Path}";
                yield return new FunctionCandidate
                {
                    Method = item.Value,
                    Path = path,
                    CustomId = item.Key,
                    DisplayName = string.IsNullOrEmpty(attribute?.DisplayName) ? item.Value.Name : attribute.DisplayName,
                    IsRegistered = true,
                    ReceiverAssignment = ReceiverAssignment.None,
                };
            }
        }

        public static IEnumerable<FunctionCandidate> GetArithmeticFunctions()
        {
            return typeof(FunctionArithmetic)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(IsValidCallMethod)
                .Select(method => new FunctionCandidate
                {
                    Method = method,
                    Path = "Arithmetic",
                    DisplayName = method.Name,
                    IsRegistered = true,
                    ReceiverAssignment = ReceiverAssignment.None,
                });
        }

        public static IEnumerable<FunctionCandidate> GetMethods(Type type, BindingFlags flags, string path, ReceiverAssignment receiverAssignment)
        {
            return GetMethods(type, flags, path, receiverAssignment, includeUnregisteredFolder: false);
        }

        public static IEnumerable<FunctionCandidate> GetMethods(Type type, BindingFlags flags, string path, ReceiverAssignment receiverAssignment, bool includeUnregisteredFolder)
        {
            if (type == null)
            {
                return Enumerable.Empty<FunctionCandidate>();
            }

            return type.GetMethods(flags)
                .Where(IsValidCallMethod)
                .Select(method =>
                {
                    AIFunctionAttribute attribute = method.GetCustomAttribute<AIFunctionAttribute>();
                    bool isRegistered = attribute != null;
                    string methodPath = BuildContextPath(path, attribute, isRegistered, includeUnregisteredFolder);
                    return new FunctionCandidate
                    {
                        Method = method,
                        Path = methodPath,
                        DisplayName = string.IsNullOrEmpty(attribute?.DisplayName) ? method.Name : attribute.DisplayName,
                        ReceiverType = method.IsStatic ? null : type,
                        RequiresReceiver = !method.IsStatic,
                        IsRegistered = isRegistered,
                        ReceiverAssignment = GetMethodReceiverAssignment(method, receiverAssignment),
                    };
                });
        }

        public static MethodInfo Resolve(FunctionReference reference)
        {
            if (reference == null || !reference.HasMethod)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(reference.customId))
            {
                EnsureCustomMethods();
                if (customMethods.TryGetValue(reference.customId, out MethodInfo customMethod))
                {
                    return customMethod;
                }
            }

            Type declaringType = reference.ResolveDeclaringType();
            if (declaringType == null)
            {
                return null;
            }

            return declaringType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(method => MethodMatches(reference, method));
        }

        public static bool IsAwaitableReturn(Type returnType)
        {
            if (returnType == null)
            {
                return false;
            }

            if (typeof(System.Collections.IEnumerator).IsAssignableFrom(returnType))
            {
                return true;
            }

            if (typeof(Task).IsAssignableFrom(returnType))
            {
                return true;
            }

#if UNITY_2023_1_OR_NEWER
            if (returnType == typeof(Awaitable) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Awaitable<>)))
            {
                return true;
            }
#endif

            return false;
        }

        public static string FormatSignature(MethodInfo method)
        {
            return FormatSignature(method, null);
        }

        public static string FormatSignature(MethodInfo method, Type receiverType)
        {
            if (method == null)
            {
                return "No function selected";
            }

            IEnumerable<string> parameterLabels = method.GetParameters().Select(parameter => $"{parameter.Name}: {GetTypeName(parameter.ParameterType)}");
            if (receiverType != null)
            {
                parameterLabels = parameterLabels.Prepend($"receiver: {GetTypeName(receiverType)}");
            }

            string parameters = string.Join(", ", parameterLabels);
            return $"{GetTypeName(method.DeclaringType)}.{method.Name}({parameters}) -> {GetTypeName(method.ReturnType)}";
        }

        public static string GetTypeName(Type type)
        {
            if (type == typeof(void))
            {
                return "void";
            }

            if (!type.IsGenericType)
            {
                return type.Name;
            }

            string name = type.Name;
            int tickIndex = name.IndexOf('`');
            if (tickIndex >= 0)
            {
                name = name.Substring(0, tickIndex);
            }

            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(GetTypeName))}>";
        }

        public static string BuildCustomId(MethodInfo method)
        {
            string parameters = string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.AssemblyQualifiedName));
            return $"{method.DeclaringType.Assembly.GetName().Name}:{method.DeclaringType.FullName}.{method.Name}({parameters})";
        }

        public static bool IsValidCallMethod(MethodInfo method)
        {
            if (method == null) return false;
            if (method.IsGenericMethod || method.IsGenericMethodDefinition || method.ContainsGenericParameters) return false;
            if (method.IsSpecialName) return false;
            if (Attribute.IsDefined(method, typeof(ObsoleteAttribute))) return false;

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (parameter.ParameterType == typeof(NodeProgress))
                {
                    return false;
                }

                VariableType variableType = VariableUtility.GetVariableType(parameter.ParameterType);
                if (variableType == VariableType.Invalid)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsBuiltInReceiverReference(VariableReference reference)
        {
            if (reference == null || !reference.HasEditorReference)
            {
                return false;
            }

            return reference.UUID == VariableData.localGameObject
                || reference.UUID == VariableData.localTransform
                || reference.UUID == VariableData.targetScript;
        }

        public static void AssignReceiverResource(FunctionReference function, ReceiverAssignment receiverAssignment, Type targetScriptType = null)
        {
            if (function?.targetObject == null || receiverAssignment == ReceiverAssignment.Preserve)
            {
                return;
            }

            VariableData receiver = receiverAssignment switch
            {
                ReceiverAssignment.TargetScript when targetScriptType != null => VariableData.GetTargetScriptVariable(targetScriptType),
                ReceiverAssignment.GameObject => VariableData.GetGameObjectVariable(),
                ReceiverAssignment.Transform => VariableData.GetTransformVariable(),
                _ => null,
            };

            function.targetObject.SetReference(receiver);
        }

        private static bool MethodMatches(FunctionReference reference, MethodInfo method)
        {
            if (method.Name != reference.methodName)
            {
                return false;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != reference.parameterTypeNames.Count)
            {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                string expected = $"{parameters[i].ParameterType.FullName}, {parameters[i].ParameterType.Assembly.GetName().Name}";
                if (!string.Equals(expected, reference.parameterTypeNames[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static ReceiverAssignment GetMethodReceiverAssignment(MethodInfo method, ReceiverAssignment contextReceiverAssignment)
        {
            return method.IsStatic ? ReceiverAssignment.None : contextReceiverAssignment;
        }

        private static void EnsureCustomMethods()
        {
            if (customMethodsLoaded)
            {
                return;
            }

            customMethodsLoaded = true;
            customMethods.Clear();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                foreach (Type type in GetTypesSafely(assembly))
                {
                    foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                    {
                        if (!Attribute.IsDefined(method, typeof(AIFunctionAttribute)) || !IsValidCallMethod(method))
                        {
                            continue;
                        }

                        customMethods[BuildCustomId(method)] = method;
                    }
                }
            }
        }

        private static string BuildContextPath(string basePath, AIFunctionAttribute attribute, bool isRegistered, bool includeUnregisteredFolder)
        {
            basePath = string.IsNullOrEmpty(basePath) ? "Other" : basePath;
            if (isRegistered)
            {
                return string.IsNullOrEmpty(attribute?.Path) ? basePath : $"{basePath}/{attribute.Path}";
            }

            return includeUnregisteredFolder ? $"{basePath}/Unregistered" : basePath;
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(type => type != null);
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }
    }

    public static class FunctionArithmetic
    {
        public static float Add(float a, float b) => a + b;

        public static float Subtract(float a, float b) => a - b;

        public static float Multiply(float a, float b) => a * b;

        public static float Divide(float a, float b) => b == 0f ? 0f : a / b;

        public static bool Greater(float a, float b) => a > b;

        public static bool Less(float a, float b) => a < b;

        public static bool Equal(float a, float b) => Mathf.Approximately(a, b);
    }
}
