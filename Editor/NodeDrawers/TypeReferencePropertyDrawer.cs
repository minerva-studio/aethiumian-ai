using Aethiumian.AI.References;
using System;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>Draws a serialized type reference as a compact AdvancedDropdown field.</summary>
    [CustomPropertyDrawer(typeof(TypeReference), true)]
    public sealed class TypeReferencePropertyDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            TypeReference current = property.boxedValue as TypeReference;
            Rect fieldRect = EditorGUI.PrefixLabel(position, label);

            if (EditorGUI.DropdownButton(fieldRect, BuildContent(current), FocusType.Passive))
            {
                ShowDropdown(fieldRect, property);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>Builds the compact label and tooltip for the current serialized value.</summary>
        private static GUIContent BuildContent(TypeReference reference)
        {
            if (reference == null || string.IsNullOrEmpty(reference.fullName))
            {
                string expected = reference?.BaseType?.Name ?? typeof(object).Name;
                return new GUIContent("None", $"Expected {expected}");
            }

            Type resolved = reference.ReferType;
            string tooltip = $"{reference.fullName}, {reference.assemblyName}";
            if (resolved != null)
            {
                return new GUIContent(resolved.Name, tooltip);
            }

            string missingName = reference.fullName.Split('.')[^1];
            GUIContent warning = EditorGUIUtility.IconContent("console.warnicon.sml");
            return new GUIContent($"Missing · {missingName}", warning.image, tooltip);
        }

        /// <summary>Shows a dropdown whose callback owns no stale SerializedProperty state.</summary>
        private static void ShowDropdown(Rect anchor, SerializedProperty property)
        {
            UnityEngine.Object target = property?.serializedObject?.targetObject;
            string propertyPath = property?.propertyPath;
            if (target == null || string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            TypeReference current = property.boxedValue as TypeReference;
            Type baseType = current?.BaseType;
            if (baseType == null)
            {
                return;
            }

            TypeReferenceDropdown dropdown = new(baseType, selectedType =>
                TryApplySelection(target, propertyPath, selectedType));
            dropdown.Show(anchor);
        }

        /// <summary>Re-resolves the property and writes the selected type through Unity serialization.</summary>
        internal static bool TryApplySelection(UnityEngine.Object target, string propertyPath, Type selectedType)
        {
            if (target == null || string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            try
            {
                SerializedObject serializedObject = new(target);
                serializedObject.Update();
                SerializedProperty property = serializedObject.FindProperty(propertyPath);
                if (property?.boxedValue is not TypeReference current)
                {
                    return false;
                }

                if (!current.SetReferType(selectedType))
                {
                    return false;
                }

                property.boxedValue = current;
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                return true;
            }
            catch (ArgumentException)
            {
                // The target can be destroyed while the dropdown is open.
                return false;
            }
            catch (InvalidOperationException)
            {
                // The serialized property can become invalid while the dropdown is open.
                return false;
            }
        }
    }
}
