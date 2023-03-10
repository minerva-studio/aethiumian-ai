using Amlos.AI.Nodes;
using System;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(SetComponentValue))]
    public class SetComponentDrawer : MethodCallerDrawerBase
    {
        public SetComponentValue Node => (SetComponentValue)node;

        public override void Draw()
        {
            if (!DrawComponent(Node)) return;

            EditorGUI.indentLevel++;
            DrawTypeReference("Component", Node.componentReference);
            Type componentType = Node.componentReference;
            Component component = null;
            if (componentType == null || !componentType.IsSubclassOf(typeof(Component)))
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Component is not valid");
                return;
            }
            else if (Node.getComponent && (!TreeData.prefab || !TreeData.prefab.TryGetComponent(componentType, out component)))
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Component is not found in the prefab");
                return;
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(10);
            DrawSetFields(Node, component, componentType);
        }

    }
}
