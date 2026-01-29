using Amlos.AI.Nodes;
using System;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(GetComponentValue))]
    public class GetComponentDrawer : MethodCallerDrawerBase
    {
        private static readonly GUIContent componentLabel = new GUIContent("Component");
        private TypeReferenceDrawer drawer;

        public GetComponentValue Node => (GetComponentValue)node;

        public override void Draw()
        {
            if (!DrawComponent())
                return;

            EditorGUI.indentLevel++;
            DrawTypeReference(componentLabel, Node.type, ref drawer);

            GenericMenu menu = new();
            if (tree.targetScript)
                menu.AddItem(new GUIContent("Use Target Script Type"), false, () => Node.TypeReference.SetReferType(tree.targetScript.GetClass()));
            if (!Node.GetComponent)
                menu.AddItem(new GUIContent("Use Variable Type"), false, () => Node.TypeReference.SetReferType(tree.GetVariableType(Node.Component.UUID)));
            RightClickMenu(menu);

            Type componentType = Node.type;
            Component component = null;
            if (componentType == null || !componentType.IsSubclassOf(typeof(Component)))
            {
                EditorGUILayout.HelpBox("Component is not valid", MessageType.Error);
                return;
            }
            if (tree.prefab)
                tree.prefab.TryGetComponent(componentType, out component);
            if (Node.getComponent && tree.prefab && !component)
            {
                EditorGUILayout.HelpBox("Component is not found in the prefab", MessageType.Info);
                //return;
            }
            EditorGUI.indentLevel--;
            GUILayout.Space(10);

            SerializedProperty entryListProperty = property.FindPropertyRelative(nameof(GetComponentValue.fieldPointers));
            DrawGetFields(entryListProperty, component, componentType);
        }
    }
}
