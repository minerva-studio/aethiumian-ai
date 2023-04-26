using Amlos.AI.Nodes;
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
            foreach (var item in NodeFactory.UserAssemblies
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
