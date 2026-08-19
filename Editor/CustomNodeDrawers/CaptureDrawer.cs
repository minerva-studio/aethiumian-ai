using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(Capture))]
    public sealed class CaptureDrawer : NodeDrawerBase
    {
        private static readonly GUIContent NodeLabel = new("Node");
        private static readonly GUIContent ResultLabel = new("Result");

        /// <summary>
        /// Draws the decorated child and writable boolean result variable.
        /// </summary>
        public override void Draw()
        {
            if (node is not Capture capture)
            {
                return;
            }

            DrawNodeReference(NodeLabel, property.FindPropertyRelative(nameof(capture.node)));
            DrawProperty(ResultLabel, property.FindPropertyRelative(nameof(capture.result)));
            NodeMustNotBeNull(capture.node, nameof(capture.node));
            if (capture.result == null || !capture.result.HasEditorReference)
            {
                EditorGUILayout.HelpBox("Capture has no result variable. The child result will pass through without being stored.", MessageType.Warning);
            }
        }
    }
}
