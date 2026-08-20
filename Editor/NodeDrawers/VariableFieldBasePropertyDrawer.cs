using Aethiumian.AI.Variables;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Property drawer for variable fields used by AI nodes.
    /// </summary>
    [CustomPropertyDrawer(typeof(VariableFieldBase), true)]
    public sealed class VariableFieldBasePropertyDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (NodePropertyDrawerUtility.TryGetTree(property, out var tree) && property.GetAIValue() is VariableFieldBase variable)
            {
                return VariableFieldDrawers.GetVariableHeight(variable, tree);
            }

            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!NodePropertyDrawerUtility.TryGetTree(property, out var tree))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            if (property.GetAIValue() is not VariableFieldBase variable)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            VariableFieldDrawers.DrawVariable(position, label, property);
        }
    }
}
