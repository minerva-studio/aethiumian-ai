using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(Repeat))]
    public sealed class RepeatDrawer : NodeDrawerBase
    {
        private static readonly GUIContent NodeLabel = new("Node");
        private static readonly GUIContent RepeatCountLabel = new("Repeat Count");

        /// <summary>
        /// Draws the decorated child and the fixed repeat count.
        /// </summary>
        public override void Draw()
        {
            if (node is not Repeat repeat)
            {
                return;
            }

            DrawNodeReference(NodeLabel, property.FindPropertyRelative(nameof(repeat.node)));
            DrawProperty(RepeatCountLabel, property.FindPropertyRelative(nameof(repeat.repeatCount)));
            NodeMustNotBeNull(repeat.node, nameof(repeat.node));
        }
    }
}
