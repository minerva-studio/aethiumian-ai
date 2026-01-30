using Amlos.AI.Nodes;
using Amlos.AI.References;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Decision))]
    public class DecisionDrawer : NodeDrawerBase
    {
        NodeReferenceTreeView list;

        public override void Draw()
        {
            if (node is not Decision decision) return;
            SerializedProperty listProperty = property.FindPropertyRelative(nameof(decision.events));
            list ??= DrawNodeList<NodeReference>(nameof(Decision), listProperty);
            list.Draw();

            if (decision.events.Length == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(Decision)} \"{node.name}\" has no element.", MessageType.Warning);
            }
        }
    }
}
