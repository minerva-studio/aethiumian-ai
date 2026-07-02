using Aethiumian.AI.Variables;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
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
            if (NodePropertyDrawerContext.TryGetTree(property, out var tree) && property.GetAIValue() is VariableBase variable)
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

            if (property.GetAIValue() is not VariableBase variable)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            VariableFieldDrawers.DrawVariable(position, label, property);
        }
    }
}
