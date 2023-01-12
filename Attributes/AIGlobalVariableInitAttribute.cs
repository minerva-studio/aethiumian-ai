using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class AIGlobalVariableInitAttribute : Attribute
    {
        private static Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();
        private static readonly HashSet<string> internalAssemblyNames = new()
        {
            "Bee.BeeDriver",
            "ExCSS.Unity",
            "Mono.Security",
            "mscorlib",
            "netstandard",
            "Newtonsoft.Json",
            "nunit.framework",
            "ReportGeneratorMerged",
            "Unrelated",
            "SyntaxTree.VisualStudio.Unity.Bridge",
            "SyntaxTree.VisualStudio.Unity.Messaging",
        };

        public string variableName;

        static AIGlobalVariableInitAttribute()
        {
            Update();
        }

        public AIGlobalVariableInitAttribute(string variableName)
        {
            this.variableName = variableName;
        }

        public static void Update()
        {
            methods ??= new Dictionary<string, MethodInfo>();
            methods.Clear();
            foreach (var item in GetUserCreatedAssemblies()
                .SelectMany(a => a.GetTypes()
                .SelectMany(t => t.GetMethods()
                .Where(m => IsDefined(m, typeof(AIGlobalVariableInitAttribute))))))
            {
                var attr = GetCustomAttribute(item, typeof(AIGlobalVariableInitAttribute)) as AIGlobalVariableInitAttribute;
                if (methods.ContainsKey(attr.variableName))
                {
                    Debug.LogError($"Cannot define multiple drawers for {attr.variableName}");
                }
                else methods.Add(attr.variableName, item);
            }
        }

        public static IEnumerable<Assembly> GetUserCreatedAssemblies()
        {
            var appDomain = AppDomain.CurrentDomain;
            foreach (var assembly in appDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                var assemblyName = assembly.GetName().Name;
                if (assemblyName.StartsWith("System") ||
                   assemblyName.StartsWith("Unity") ||
                   assemblyName.StartsWith("UnityEditor") ||
                   assemblyName.StartsWith("UnityEngine") ||
                   internalAssemblyNames.Contains(assemblyName))
                {
                    continue;
                }

                yield return assembly;
            }
        }

        internal static bool GetInitValue(string name, out object value)
        {
            if (!methods.TryGetValue(name, out var method))
            {
                value = null;
                return false;
            }

            try
            {
                value = method.Invoke(null, null);
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }
    }
}
