using Amlos.AI.Variables;
using Minerva.Module.Editor;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    //[CustomPropertyDrawer(typeof(VariableBase), true)]
    public class VariableBaseDrawer : PropertyDrawer
    {
        public const float CONST_HEIGHT = 1;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return CONST_HEIGHT * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            BehaviourTreeData tree = property.serializedObject.targetObject as BehaviourTreeData;
            if (tree != null)
            {
                Debug.Log("Found instrance");
                VariableFieldDrawers.DrawVariable(label.text, property.GetValue() as VariableBase, tree, null);
            }
            else
            {
                Debug.Log("No instrance");
                EditorGUI.PropertyField(position, property, label);
            }
            EditorGUI.EndProperty();
        }
    }
}

