using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Specifies what kind of custom field drawer method is being registered.
    /// </summary>
    public enum CustomAIFieldDrawerKind
    {
        /// <summary>
        /// Register a rect-based draw method.
        /// </summary>
        Draw,
        /// <summary>
        /// Register a height provider for a rect-based drawer.
        /// </summary>
        Height
    }

    /// <summary>
    /// Custom field drawer for a field for AI editor.
    /// <br>
    /// </br>
    /// The parameters for such method is either:
    /// <br/>
    /// T Method(Rect position, GUIContent label, T field, BehaviourTreeData treeData)
    /// <br/> or <br/>
    /// T Method(Rect position, GUIContent label, T field)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class CustomAIFieldDrawerAttribute : Attribute
    {
        private sealed class DrawerEntry
        {
            public MethodInfo DrawMethod { get; set; }
            public MethodInfo HeightMethod { get; set; }
        }

        private static Dictionary<Type, DrawerEntry> methods = new Dictionary<Type, DrawerEntry>();

        /// <summary>
        /// The type that this drawer targets.
        /// </summary>
        public Type Type { get; }

        /// <summary>
        /// The drawer method kind.
        /// </summary>
        public CustomAIFieldDrawerKind Kind { get; }

        static CustomAIFieldDrawerAttribute()
        {
            Update();
        }

        public CustomAIFieldDrawerAttribute()
        {
        }

        public CustomAIFieldDrawerAttribute(Type type)
            : this(type, CustomAIFieldDrawerKind.Draw)
        {
        }

        public CustomAIFieldDrawerAttribute(Type type, CustomAIFieldDrawerKind kind)
        {
            Type = type;
            Kind = kind;
        }

        /// <summary>
        /// Refresh the drawer method cache.
        /// </summary>
        public static void Update()
        {
            methods ??= new Dictionary<Type, DrawerEntry>();
            methods.Clear();
            foreach (var item in TypeCache.GetMethodsWithAttribute<CustomAIFieldDrawerAttribute>())
            {
                var attr = GetCustomAttribute(item, typeof(CustomAIFieldDrawerAttribute)) as CustomAIFieldDrawerAttribute;
                if (attr?.Type == null)
                {
                    continue;
                }

                if (!methods.TryGetValue(attr.Type, out var entry))
                {
                    entry = new DrawerEntry();
                    methods.Add(attr.Type, entry);
                }

                if (attr.Kind == CustomAIFieldDrawerKind.Height)
                {
                    if (entry.HeightMethod != null)
                    {
                        Debug.LogError($"Cannot define multiple drawer heights for {attr.Type}");
                        continue;
                    }
                    entry.HeightMethod = item;
                }
                else
                {
                    if (entry.DrawMethod != null)
                    {
                        Debug.LogError($"Cannot define multiple drawers for {attr.Type}");
                        continue;
                    }
                    entry.DrawMethod = item;
                }
            }
        }

        /// <summary>
        /// Returns true when a drawer exists for the provided type.
        /// </summary>
        public static bool IsDrawerDefined(Type type)
        {
            return methods.TryGetValue(type, out var entry) && entry.DrawMethod != null;
        }

        /// <summary>
        /// Get the height needed to draw the value using a custom drawer.
        /// </summary>
        /// <param name="value">The current value.</param>
        /// <param name="behaviourTreeData">The behaviour tree context.</param>
        /// <returns>The height required to draw the value.</returns>
        public static float GetDrawerHeight(object value, BehaviourTreeData behaviourTreeData)
        {
            if (value == null)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (!methods.TryGetValue(value.GetType(), out var entry) || entry.HeightMethod == null)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            var method = entry.HeightMethod;
            var parameters = method.GetParameters();
            if (parameters.Length < 1)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            object[] args = new object[parameters.Length];
            args[0] = value;
            if (parameters.Length == 2)
            {
                args[1] = behaviourTreeData;
            }
            else if (parameters.Length != 1)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            var result = method.Invoke(null, args);
            return result is float height ? height : EditorGUIUtility.singleLineHeight;
        }

        /// <summary>
        /// Try invoke the drawer method for a given value.
        /// </summary>
        /// <param name="result">Output result after drawing.</param>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="label">Field label.</param>
        /// <param name="value">Value to draw.</param>
        /// <param name="behaviourTreeData">Behaviour tree context.</param>
        public static void TryInvoke(out object result, Rect position, GUIContent label, object value, BehaviourTreeData behaviourTreeData)
        {
            result = null;
            if (value == null)
            {
                return;
            }

            if (!methods.TryGetValue(value.GetType(), out var entry) || entry.DrawMethod == null)
            {
                return;
            }

            var method = entry.DrawMethod;
            object[] parameters = new object[method.GetParameters().Length];
            if (parameters.Length < 3)
            {
                return;
            }

            parameters[0] = position;
            parameters[1] = label;
            parameters[2] = value;

            switch (parameters.Length)
            {
                case 3:
                    result = method.Invoke(null, parameters);
                    break;
                case 4:
                    parameters[3] = behaviourTreeData;
                    result = method.Invoke(null, parameters);
                    break;
                default:
                    return;
            }
        }
    }
}

