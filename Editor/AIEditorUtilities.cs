using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using InspectorSerializedPropertyExtensions = Minerva.Module.Editor.SerializedPropertyExtensions;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// AI editor local string helpers.
    /// </summary>
    internal static class AIEditorStringExtensions
    {
        public static string ToTitleCase(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            if (text.Length < 2)
            {
                return text.ToUpper(CultureInfo.InvariantCulture);
            }

            StringBuilder builder = new();
            builder.Append(char.ToUpper(text[0], CultureInfo.InvariantCulture));
            bool wasCapitalized = true;

            for (int i = 1; i < text.Length; i++)
            {
                bool isCapitalized = char.IsUpper(text, i);
                if (isCapitalized && !wasCapitalized)
                {
                    builder.Append(' ');
                    builder.Append(char.ToUpper(text[i], CultureInfo.InvariantCulture));
                }
                else
                {
                    builder.Append(text[i]);
                }

                wasCapitalized = isCapitalized;
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Local bridge for serialized property reflection supplied by Minerva Inspector.
    /// </summary>
    internal static class AIEditorSerializedPropertyExtensions
    {
        public static object GetAIValue(this SerializedProperty property)
            => InspectorSerializedPropertyExtensions.GetValue(property);

        public static MemberInfo GetAIMemberInfo(this SerializedProperty property)
            => InspectorSerializedPropertyExtensions.GetMemberInfo(property);
    }

    /// <summary>
    /// Local MonoScript cache for AI editor script previews.
    /// </summary>
    internal static class AIEditorMonoScriptCache
    {
        private static Dictionary<Type, MonoScript> scripts;

        public static MonoScript Get<T>() => Get(typeof(T));

        public static MonoScript Get(Type type)
        {
            if (type == null)
            {
                return null;
            }

            scripts ??= BuildCache();
            return scripts.TryGetValue(type, out MonoScript script) ? script : null;
        }

        private static Dictionary<Type, MonoScript> BuildCache()
        {
            Dictionary<Type, MonoScript> cache = new();
            foreach (MonoScript script in Resources.FindObjectsOfTypeAll<MonoScript>())
            {
                Type type = script.GetClass();
                if (type != null)
                {
                    cache[type] = script;
                }
            }

            return cache;
        }
    }

    /// <summary>
    /// Restores the editor GUI indent level when disposed.
    /// </summary>
    internal sealed class IndentScope : IDisposable
    {
        private readonly int previousIndent;
        private bool disposed;

        public static IndentScope Increase => new();

        public IndentScope(int indentation = 1)
        {
            previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel += indentation;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            EditorGUI.indentLevel = previousIndent;
        }
    }

    /// <summary>
    /// AI editor local drawing helpers used by legacy IMGUI panels.
    /// </summary>
    internal static class AIEditorFieldDrawers
    {
        public static object DrawField(string labelName, object value, Type type)
            => DrawField(new GUIContent(labelName), value, type);

        public static object DrawField(GUIContent label, object value, Type type)
        {
            if (type == typeof(string))
            {
                return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            }

            if (type == typeof(int))
            {
                return EditorGUILayout.IntField(label, value is int i ? i : default);
            }

            if (type == typeof(long))
            {
                return EditorGUILayout.LongField(label, value is long l ? l : default);
            }

            if (type == typeof(float))
            {
                return EditorGUILayout.FloatField(label, value is float f ? f : default);
            }

            if (type == typeof(double))
            {
                return EditorGUILayout.DoubleField(label, value is double d ? d : default);
            }

            if (type == typeof(bool))
            {
                return EditorGUILayout.Toggle(label, value is bool b && b);
            }

            if (type == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(label, value is Vector2 v ? v : default);
            }

            if (type == typeof(Vector2Int))
            {
                return EditorGUILayout.Vector2IntField(label, value is Vector2Int v ? v : default);
            }

            if (type == typeof(Vector3))
            {
                return EditorGUILayout.Vector3Field(label, value is Vector3 v ? v : default);
            }

            if (type == typeof(Vector3Int))
            {
                return EditorGUILayout.Vector3IntField(label, value is Vector3Int v ? v : default);
            }

            if (type == typeof(Vector4))
            {
                return EditorGUILayout.Vector4Field(label, value is Vector4 v ? v : default);
            }

            if (type == typeof(Quaternion))
            {
                Quaternion quaternion = value is Quaternion q ? q : Quaternion.identity;
                Vector4 raw = new(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                raw = EditorGUILayout.Vector4Field(label, raw);
                return new Quaternion(raw.x, raw.y, raw.z, raw.w);
            }

            if (type == typeof(Color))
            {
                return EditorGUILayout.ColorField(label, value is Color c ? c : default);
            }

            if (type == typeof(Rect))
            {
                return EditorGUILayout.RectField(label, value is Rect r ? r : default);
            }

            if (type == typeof(RectInt))
            {
                return EditorGUILayout.RectIntField(label, value is RectInt r ? r : default);
            }

            if (type == typeof(Bounds))
            {
                return EditorGUILayout.BoundsField(label, value is Bounds b ? b : default);
            }

            if (type == typeof(BoundsInt))
            {
                return EditorGUILayout.BoundsIntField(label, value is BoundsInt b ? b : default);
            }

            if (type == typeof(LayerMask))
            {
                LayerMask mask = value is LayerMask layerMask ? layerMask : default;
                return DrawLayerMask(label, mask);
            }

            if (type != null && type.IsEnum)
            {
                Enum current = value as Enum ?? (Enum)Enum.GetValues(type).GetValue(0);
                return Attribute.GetCustomAttribute(type, typeof(FlagsAttribute)) != null
                    ? EditorGUILayout.EnumFlagsField(label, current)
                    : EditorGUILayout.EnumPopup(label, current);
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, type, true);
            }

            EditorGUILayout.LabelField(label, new GUIContent(type?.Name ?? "Unsupported"));
            return value;
        }

        public static void PropertyField(Rect position, SerializedProperty property, GUIContent label, bool includeChildren = false)
        {
            EditorGUI.PropertyField(position, property, label, includeChildren);
        }

        public static bool RightClickMenu(GenericMenu menu)
        {
            return RightClickMenu(menu, GUILayoutUtility.GetLastRect());
        }

        public static bool RightClickMenu(GenericMenu menu, Rect rect)
        {
            if (Event.current.type != EventType.MouseDown
                || Event.current.button != 1
                || !rect.Contains(Event.current.mousePosition))
            {
                return false;
            }

            menu.ShowAsContext();
            return true;
        }

        private static LayerMask DrawLayerMask(GUIContent label, LayerMask value)
        {
            string[] layers = new string[32];
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = LayerMask.LayerToName(i);
            }

            return new LayerMask { value = EditorGUILayout.MaskField(label, value.value, layers) };
        }
    }
}
