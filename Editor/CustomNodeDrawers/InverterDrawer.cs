using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(Inverter))]
    public class InverterDrawer : NodeDrawerBase
    {
        private static readonly GUIContent NodeLabel = new("Node");

        public override void Draw()
        {
            if (node is not Inverter inverter) return;

            DrawNodeReference(NodeLabel, property.FindPropertyRelative(nameof(inverter.node)));
            NodeMustNotBeNull(inverter.node, nameof(inverter.node));
        }
    }
}
