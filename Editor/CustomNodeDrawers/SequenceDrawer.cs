using Amlos.AI.Nodes;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Sequence))]
    public class SequenceDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Sequence sequence) return;
            DrawNodeList(nameof(Sequence), sequence.events, sequence);

            if (sequence.events.Count == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(Sequence)} \"{node.name}\" has no element.", MessageType.Warning);
            }
        }
    }
}
