using Amlos.AI.Nodes;
using Amlos.AI.References;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Sequence))]
    public class SequenceDrawer : NodeDrawerBase
    {
        private NodeReferenceTreeView list;

        public override void Draw()
        {
            if (node is not Sequence sequence) return;

            SerializedProperty listProperty = property.FindPropertyRelative(nameof(sequence.events));
            list ??= DrawNodeList<NodeReference>(nameof(Sequence), listProperty);
            list.Draw();

            if (listProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(Sequence)} \"{node.name}\" has no element.", MessageType.Warning);
            }
        }
    }
}
