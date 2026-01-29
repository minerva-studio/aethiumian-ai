using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Property drawer for variable fields used by AI nodes.
    /// </summary>
    [CustomPropertyDrawer(typeof(VariableBase), true)]
    public sealed class VariableBasePropertyDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (NodePropertyDrawerContext.TryGetTree(property, out var tree) && property.GetValue() is VariableBase variable)
            {
                return VariableFieldDrawers.GetVariableHeight(variable, tree);
            }

            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!NodePropertyDrawerContext.TryGetTree(property, out var tree))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            if (property.GetValue() is not VariableBase variable)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            VariableFieldDrawers.DrawVariable(position, label, variable, tree);
            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.Update();
                property.boxedValue = variable;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }
            EditorGUI.EndProperty();
        }
    }
}
