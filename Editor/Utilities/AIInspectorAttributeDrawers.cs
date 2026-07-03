using Aethiumian.AI.Inspector;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Draws AI fields that should remain visible but not editable.
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    internal sealed class ReadOnlyAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    /// <summary>
    /// Hides fields while the editor is in play mode.
    /// </summary>
    [CustomPropertyDrawer(typeof(HideInRuntimeAttribute))]
    internal sealed class HideInRuntimeAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!Application.isPlaying)
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Application.isPlaying ? 0f : EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

    /// <summary>
    /// Draws a property only when the configured field condition is true.
    /// </summary>
    [CustomPropertyDrawer(typeof(DisplayIfAttribute))]
    internal sealed class DisplayIfAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldDraw(property))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return ShouldDraw(property) ? EditorGUI.GetPropertyHeight(property, label, true) : 0f;
        }

        private bool ShouldDraw(SerializedProperty property)
        {
            if (attribute is not DisplayIfAttribute displayIf)
            {
                return true;
            }

            object owner = ResolveOwner(property);
            if (owner == null)
            {
                return true;
            }

            FieldInfo dependentField = owner.GetType().GetField(displayIf.path, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return dependentField == null || displayIf.Matches(dependentField.GetValue(owner));
        }

        private static object ResolveOwner(SerializedProperty property)
        {
            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0)
            {
                return property.serializedObject.targetObject;
            }

            SerializedProperty parentProperty = property.serializedObject.FindProperty(path.Substring(0, lastDot));
            return parentProperty?.GetAIValue() ?? property.serializedObject.targetObject;
        }
    }
}
