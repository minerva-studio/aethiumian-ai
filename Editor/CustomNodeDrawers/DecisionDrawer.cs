using Amlos.AI.Nodes;
using Amlos.AI.References;
using UnityEditor;
using UnityEditorInternal;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Decision))]
    public class DecisionDrawer : NodeDrawerBase
    {
        ReorderableList list;

        public override void Draw()
        {
            if (node is not Decision decision) return;
            SerializedProperty listProperty = nodeProperty.FindPropertyRelative(nameof(decision.events));
            list ??= DrawNodeList<NodeReference>(nameof(Decision), listProperty, decision);
            list.serializedProperty = listProperty;
            list.DoLayoutList();

            if (decision.events.Count == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(Decision)} \"{node.name}\" has no element.", MessageType.Warning);
            }
        }
    }
}