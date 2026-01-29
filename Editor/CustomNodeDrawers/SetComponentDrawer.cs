using Amlos.AI.Nodes;
using System;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(SetComponentValue))]
    public class SetComponentDrawer : MethodCallerDrawerBase
    {
        private static readonly GUIContent ComponentLabel = new GUIContent("Component");
        private TypeReferenceDrawer drawer;


        public SetComponentValue Node => (SetComponentValue)node;

        public override void Draw()
        {
            if (!DrawComponent())
                return;

            EditorGUI.indentLevel++;
            //DrawTypeReference("Component", Node.type);
            DrawTypeReference(ComponentLabel, Node.type, ref drawer);
            if (tree.targetScript)
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Use Target Script Type"), false, () => Node.TypeReference.SetReferType(tree.targetScript.GetClass()));
                if (Node.GetComponent)
                    menu.AddItem(new GUIContent("Use Variable Type"), false, () => Node.TypeReference.SetReferType(tree.GetVariableType(Node.Component.UUID)));
                RightClickMenu(menu);
            }
            Type componentType = Node.type;
            Component component = null;
            if (componentType == null || !componentType.IsSubclassOf(typeof(Component)))
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Component is not valid");
                return;
            }
            else if (Node.getComponent && (!tree.prefab || !tree.prefab.TryGetComponent(componentType, out component)))
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Component is not found in the prefab");
                return;
            }
            EditorGUI.indentLevel--;

            GUILayout.Space(10);

            var pointers = property.FindPropertyRelative(nameof(SetComponentValue.fieldData));
            DrawSetFields(pointers, component, componentType);
        }

    }
}
