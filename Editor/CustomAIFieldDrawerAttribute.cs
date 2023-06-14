using Amlos.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace Amlos.AI.Editor
{
    /// <summary>
    /// Custom field drawer for a field for AI editor
    /// <br>
    /// </br>
    /// The parameters for such method is either:
    /// <br/>
    /// T Method(GUIContent label, T field, BehaviourTreeData treeData)
    /// <br/> or <br/>
    /// T Method(GUIContent label, T field)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class CustomAIFieldDrawerAttribute : Attribute
    {
        private static Dictionary<Type, MethodInfo> methods = new Dictionary<Type, MethodInfo>();

        public Type Type { get; set; }

        static CustomAIFieldDrawerAttribute()
        {
            Update();
        }

        public CustomAIFieldDrawerAttribute()
        {

        }

        public CustomAIFieldDrawerAttribute(Type type)
        {
            this.Type = type;
        }

        public static void Update()
        {
            methods ??= new Dictionary<Type, MethodInfo>();
            methods.Clear();
            foreach (var item in NodeFactory.UserAssemblies.SelectMany(a => a.GetTypes().SelectMany(t => t.GetMethods().Where(m => IsDefined(m, typeof(CustomAIFieldDrawerAttribute))))))
            {
                var attr = GetCustomAttribute(item, typeof(CustomAIFieldDrawerAttribute)) as CustomAIFieldDrawerAttribute;
                if (methods.ContainsKey(attr.Type))
                {
                    Debug.LogError($"Cannot define multiple drawers for {attr.Type}");
                }
                else methods.Add(attr.Type, item);
            }
        }

        public static bool IsDrawerDefined(Type type)
        {
            return methods.ContainsKey(type) == true;
        }

        public static void TryInvoke(out object result, GUIContent label, object value, BehaviourTreeData behaviourTreeData)
        {
            result = null;
            if (!methods.TryGetValue(value.GetType(), out var method))
            {
                return;
            }

            object[] parameters = new object[method.GetParameters().Length];
            if (parameters.Length <= 1)
            {
                return;
            }

            parameters[0] = label;
            parameters[1] = value;
            switch (parameters.Length)
            {
                case 2:
                    result = method.Invoke(null, parameters);
                    break;
                case 3:
                    parameters[2] = behaviourTreeData;
                    result = method.Invoke(null, parameters);
                    break;
                default:
                    return;
            }
        }
    }
}

