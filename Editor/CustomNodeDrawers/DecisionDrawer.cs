using Amlos.AI.Nodes;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Decision))]
    public class DecisionDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Decision decision) return;
            //DrawNodeList(nameof(Decision), decision.eventUUIDs, decision); 
            DrawNodeList(nameof(Decision), decision.events, decision);

            if (decision.events.Count == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(Decision)} \"{node.name}\" has no element.", MessageType.Warning);
            }
        }
    }
}