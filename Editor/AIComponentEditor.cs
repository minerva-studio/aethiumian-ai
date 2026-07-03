using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Minimal AI component inspector with direct field-level drawing rules.
    /// </summary>
    [CustomEditor(typeof(AI))]
    internal sealed class AIComponentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            AI ai = (AI)target;
            serializedObject.Update();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                DrawProperty(iterator);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawProperty(SerializedProperty property)
        {
            if (Application.isPlaying && property.name == nameof(AI.autoRestart))
            {
                return;
            }

            bool readOnly = property.propertyPath == "m_Script";
            using (new EditorGUI.DisabledScope(readOnly))
            {
                EditorGUILayout.PropertyField(property, includeChildren: true);
            }
        }
    }
}
