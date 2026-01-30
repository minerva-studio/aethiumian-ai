using Amlos.AI.Nodes;
using Amlos.AI.References;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Loop))]
    public class LoopDrawer : NodeDrawerBase
    {
        private NodeReferenceTreeView list;

        public override void Draw()
        {
            if (node is not Loop loop) return;

            SerializedProperty loopTypeProperty = property.FindPropertyRelative(nameof(Loop.loopType));
            SerializedProperty conditionProperty = property.FindPropertyRelative(nameof(Loop.condition));
            SerializedProperty loopCountProperty = property.FindPropertyRelative(nameof(Loop.loopCount));
            SerializedProperty listProperty = property.FindPropertyRelative(nameof(loop.events));

            EditorGUILayout.PropertyField(loopTypeProperty, new GUIContent("Loop Type"));
            Loop.LoopType loopType = (Loop.LoopType)loopTypeProperty.enumValueIndex;

            if (loopType == Loop.LoopType.@while)
            {
                EditorGUILayout.PropertyField(conditionProperty, new GUIContent("Condition"), true);
            }
            if (loopType == Loop.LoopType.@for)
            {
                EditorGUILayout.PropertyField(loopCountProperty, new GUIContent("Loop Count"), true);
            }

            list ??= DrawNodeList<NodeReference>(nameof(Loop), listProperty);
            list.Draw();

            if (loopType == Loop.LoopType.@doWhile)
            {
                EditorGUILayout.PropertyField(conditionProperty, new GUIContent("Condition"), true);
            }

            if (loopType == Loop.LoopType.@while || loopType == Loop.LoopType.doWhile)
            {
                if (conditionProperty?.boxedValue is NodeReference conditionReference)
                {
                    NodeMustNotBeNull(conditionReference, nameof(loop.condition));
                }
            }

            if (listProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(Loop)} \"{node.name}\" has no element.", MessageType.Warning);
            }
        }
    }
}
