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
            public string DisplaySignature { get; set; }
            public string SortKey { get; set; }
            public string SearchText { get; set; }

            public Type GetDisplayReceiverType()
            {
                return RequiresReceiver ? ReceiverType : null;
            }
        }

        private readonly struct MethodCandidateCacheKey : IEquatable<MethodCandidateCacheKey>
        {
            private readonly Type type;
            private readonly BindingFlags flags;
            private readonly string path;
            private readonly ReceiverAssignment receiverAssignment;
            private readonly bool includeUnregisteredFolder;

            public MethodCandidateCacheKey(Type type, BindingFlags flags, string path, ReceiverAssignment receiverAssignment, bool includeUnregisteredFolder)
            {
                this.type = type;
                this.flags = flags;
                this.path = path ?? string.Empty;
                this.receiverAssignment = receiverAssignment;
                this.includeUnregisteredFolder = includeUnregisteredFolder;
            }

            public bool Equals(MethodCandidateCacheKey other)
            {
                return type == other.type
                    && flags == other.flags
                    && path == other.path
                    && receiverAssignment == other.receiverAssignment
                    && includeUnregisteredFolder == other.includeUnregisteredFolder;
            }

            public override bool Equals(object obj)
            {
                return obj is MethodCandidateCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(type, flags, path, receiverAssignment, includeUnregisteredFolder);
            }
        }

        private static readonly Dictionary<string, MethodInfo> customMethods = new();
        private static readonly Dictionary<MethodCandidateCacheKey, List<FunctionCandidate>> methodCandidateCache = new();
        private static readonly object cacheLock = new();
        private static List<FunctionCandidate> customFunctionCandidateCache;
        private static List<FunctionCandidate> arithmeticFunctionCandidateCache;
        private static bool customMethodsLoaded;

        static FunctionRegistry()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (_, _) => ClearCache();
        }

        public static void ClearCache()
        {
            // Domain reload clears static fields; this covers runtime assembly loads and future explicit hot-load invalidation.
            lock (cacheLock)
            {
                customMethods.Clear();
                methodCandidateCache.Clear();
                customFunctionCandidateCache = null;
                arithmeticFunctionCandidateCache = null;
                customMethodsLoaded = false;
            }
        }

        public static IEnumerable<FunctionCandidate> GetCustomFunctions()
        {
            lock (cacheLock)
            {
                customFunctionCandidateCache ??= BuildCustomFunctionCandidates().ToList();
                return customFunctionCandidateCache;
            }
        }

        public static IEnumerable<FunctionCandidate> GetArithmeticFunctions()
        {
            lock (cacheLock)
            {
                arithmeticFunctionCandidateCache ??= BuildArithmeticFunctionCandidates().ToList();
                return arithmeticFunctionCandidateCache;
            }
        }

        private static IEnumerable<FunctionCandidate> BuildCustomFunctionCandidates()
        {
            EnsureCustomMethods();
            foreach (var item in customMethods)
            {
                AIFunctionAttribute attribute = item.Value.GetCustomAttribute<AIFunctionAttribute>();
                string path = string.IsNullOrEmpty(attribute?.Path) ? "Global" : $"Global/{attribute.Path}";
                yield return CreateCandidate(
                    item.Value,
                    path,
                    item.Key,
                    string.IsNullOrEmpty(attribute?.DisplayName) ? item.Value.Name : attribute.DisplayName,
                    receiverType: null,
                    requiresReceiver: false,
                    isRegistered: true,
                    receiverAssignment: ReceiverAssignment.None);
            }
        }

        private static IEnumerable<FunctionCandidate> BuildArithmeticFunctionCandidates()
        {
            HashSet<string> methodKeys = new();

            foreach (MethodInfo method in typeof(ArithmeticFunctions).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(IsValidCallMethod))
            {
                methodKeys.Add(BuildMethodSignatureKey(method));
                yield return CreateCandidate(
                    method,
                    GetArithmeticPath(method),
                    customId: null,
                    displayName: method.Name,
                    receiverType: null,
                    requiresReceiver: false,
                    isRegistered: true,
                    receiverAssignment: ReceiverAssignment.None);
            }

            foreach (MethodInfo method in typeof(Mathf).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(IsValidCallMethod))
            {
                if (!methodKeys.Add(BuildMethodSignatureKey(method)))
                {
                    continue;
                }

                yield return CreateCandidate(
                    method,
                    "Arithmetic",
                    customId: null,
                    displayName: method.Name,
                    receiverType: null,
                    requiresReceiver: false,
                    isRegistered: true,
                    receiverAssignment: ReceiverAssignment.None);
            }
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

            MethodCandidateCacheKey cacheKey = new(type, flags, path, receiverAssignment, includeUnregisteredFolder);
            lock (cacheLock)
            {
                if (!methodCandidateCache.TryGetValue(cacheKey, out List<FunctionCandidate> candidates))
                {
                    candidates = BuildMethodCandidates(type, flags, path, receiverAssignment, includeUnregisteredFolder).ToList();
                    methodCandidateCache[cacheKey] = candidates;
                }

                return candidates;
            }
        }

        private static IEnumerable<FunctionCandidate> BuildMethodCandidates(Type type, BindingFlags flags, string path, ReceiverAssignment receiverAssignment, bool includeUnregisteredFolder)
        {
            return type.GetMethods(flags)
                .Where(IsValidCallMethod)
                .Select(method =>
                {
                    AIFunctionAttribute attribute = method.GetCustomAttribute<AIFunctionAttribute>();
                    bool isRegistered = attribute != null;
                    string methodPath = BuildContextPath(path, attribute, isRegistered, includeUnregisteredFolder);
                    return CreateCandidate(
                        method,
                        methodPath,
                        customId: null,
                        displayName: string.IsNullOrEmpty(attribute?.DisplayName) ? method.Name : attribute.DisplayName,
                        receiverType: method.IsStatic ? null : type,
                        requiresReceiver: !method.IsStatic,
                        isRegistered: isRegistered,
                        receiverAssignment: GetMethodReceiverAssignment(method, receiverAssignment));
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
                lock (cacheLock)
                {
                    EnsureCustomMethods();
                    if (customMethods.TryGetValue(reference.customId, out MethodInfo customMethod))
                    {
                        return customMethod;
                    }
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
            return FormatSignature(method, receiverType, includeDeclaringType: true);
        }

        public static string FormatSignature(MethodInfo method, Type receiverType, bool includeDeclaringType)
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
            string callableName = includeDeclaringType ? $"{GetTypeName(method.DeclaringType)}.{method.Name}" : method.Name;
            return $"{callableName}({parameters}) -> {GetTypeName(method.ReturnType)}";
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
                if (parameter.IsOut || parameter.ParameterType.IsByRef || parameter.ParameterType.IsArray || parameter.ParameterType.IsPointer)
                {
                    return false;
                }

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

        private static string BuildMethodSignatureKey(MethodInfo method)
        {
            string parameters = string.Join("|", method.GetParameters().Select(parameter => parameter.ParameterType.FullName));
            return $"{method.Name}({parameters})";
        }

        private static bool IsArithmeticConstant(MethodInfo method)
        {
            if (method.DeclaringType != typeof(ArithmeticFunctions) || method.GetParameters().Length > 0)
            {
                return false;
            }

            return method.Name is nameof(ArithmeticFunctions.PI)
                or nameof(ArithmeticFunctions.Infinity)
                or nameof(ArithmeticFunctions.NegativeInfinity)
                or nameof(ArithmeticFunctions.Deg2Rad)
                or nameof(ArithmeticFunctions.Rad2Deg)
                or nameof(ArithmeticFunctions.Epsilon);
        }

        private static string GetArithmeticPath(MethodInfo method)
        {
            if (IsArithmeticConstant(method))
            {
                return "Arithmetic/Constants";
            }

            if (method.DeclaringType != typeof(ArithmeticFunctions))
            {
                return "Arithmetic";
            }

            return method.Name switch
            {
                nameof(ArithmeticFunctions.Phase01)
                    or nameof(ArithmeticFunctions.SineWave)
                    or nameof(ArithmeticFunctions.CosineWave)
                    or nameof(ArithmeticFunctions.SineWave01)
                    or nameof(ArithmeticFunctions.CosineWave01)
                    or nameof(ArithmeticFunctions.TriangleWave01)
                    or nameof(ArithmeticFunctions.Pulse) => "Arithmetic/Sampling",

                nameof(ArithmeticFunctions.EaseInQuad)
                    or nameof(ArithmeticFunctions.EaseOutQuad)
                    or nameof(ArithmeticFunctions.EaseInOutQuad)
                    or nameof(ArithmeticFunctions.EaseInCubic)
                    or nameof(ArithmeticFunctions.EaseOutCubic)
                    or nameof(ArithmeticFunctions.EaseInOutCubic)
                    or nameof(ArithmeticFunctions.EaseInSine)
                    or nameof(ArithmeticFunctions.EaseOutSine)
                    or nameof(ArithmeticFunctions.EaseInOutSine) => "Arithmetic/Easing",

                nameof(ArithmeticFunctions.Saturate)
                    or nameof(ArithmeticFunctions.Remap)
                    or nameof(ArithmeticFunctions.Remap01)
                    or nameof(ArithmeticFunctions.InverseLerpUnclamped) => "Arithmetic/Mapping",

                _ => "Arithmetic",
            };
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

        private static FunctionCandidate CreateCandidate(
            MethodInfo method,
            string path,
            string customId,
            string displayName,
            Type receiverType,
            bool requiresReceiver,
            bool isRegistered,
            ReceiverAssignment receiverAssignment)
        {
            Type displayReceiverType = requiresReceiver ? receiverType : null;
            bool includeDeclaringType = ShouldShowDeclaringType(path);
            string displaySignature = FormatSignature(method, displayReceiverType, includeDeclaringType);
            string sortKey = BuildSortKey(method, path);

            return new FunctionCandidate
            {
                Method = method,
                Path = path,
                CustomId = customId,
                DisplayName = displayName,
                ReceiverType = receiverType,
                RequiresReceiver = requiresReceiver,
                IsRegistered = isRegistered,
                ReceiverAssignment = receiverAssignment,
                DisplaySignature = displaySignature,
                SortKey = sortKey,
                SearchText = BuildSearchText(method, path, displayName, displaySignature, receiverType),
            };
        }

        private static string BuildSortKey(MethodInfo method, string path)
        {
            if (IsArithmeticPath(path))
            {
                return method.Name;
            }

            return $"{GetTypeName(method.DeclaringType)}.{method.Name}";
        }

        private static string BuildSearchText(MethodInfo method, string path, string displayName, string displaySignature, Type receiverType)
        {
            string parameterTypes = string.Join(" ", method.GetParameters().Select(parameter => GetTypeName(parameter.ParameterType)));
            return $"{path} {displayName} {displaySignature} {method.Name} {method.DeclaringType?.FullName} {receiverType?.FullName} {parameterTypes} {GetTypeName(method.ReturnType)}";
        }

        private static bool ShouldShowDeclaringType(string path)
        {
            return !IsArithmeticPath(path);
        }

        private static bool IsArithmeticPath(string path)
        {
            return string.Equals(GetTopLevelPath(path), "Arithmetic", StringComparison.Ordinal);
        }

        private static string GetTopLevelPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            int slashIndex = path.IndexOf('/');
            return slashIndex < 0 ? path : path.Substring(0, slashIndex);
        }

        private static void EnsureCustomMethods()
        {
            lock (cacheLock)
            {
                if (customMethodsLoaded)
                {
                    return;
                }

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

                customMethodsLoaded = true;
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
}
