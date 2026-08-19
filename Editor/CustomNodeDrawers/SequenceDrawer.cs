using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using UnityEditor;

namespace Aethiumian.AI.Editor
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
                EditorGUILayout.HelpBox("Empty Sequence returns Success.", MessageType.Info);
            }
        }
    }
}
