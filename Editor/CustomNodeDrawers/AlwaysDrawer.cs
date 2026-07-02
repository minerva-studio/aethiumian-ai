using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(Always))]
    public class AlwaysDrawer : NodeDrawerBase
    {
        private static readonly GUIContent NodeLabel = new("Node");
        private static readonly GUIContent ReturnValueLabel = new("Return Value");

        public override void Draw()
        {
            if (node is not Always always) return;

            DrawNodeReference(NodeLabel, property.FindPropertyRelative(nameof(always.node)));
            DrawProperty(ReturnValueLabel, property.FindPropertyRelative(nameof(always.returnValue)));
            NodeMustNotBeNull(always.node, nameof(always.node));
        }
    }
}
