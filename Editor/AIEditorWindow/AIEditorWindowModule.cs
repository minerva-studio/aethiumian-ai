using Aethiumian.AI.Nodes;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    internal class AIEditorWindowModule
    {
        internal const string ConnectionRejectedMessage =
            "Connection rejected: the target is no longer valid for this tree position.";

        protected AIEditorWindow editorWindow;

        internal TreeNode SelectedNode { get => editorWindow ? editorWindow.SelectedNode : null; set { if (editorWindow) editorWindow.SelectedNode = value; } }
        internal AIEditorSetting EditorSetting => editorWindow.editorSetting;
        internal AISetting Settings => editorWindow.setting;
        internal BehaviourTreeData tree => editorWindow.tree;
        internal HashSet<TreeNode> ReachableNodes => editorWindow.reachableNodes;


        internal void Initialize(AIEditorWindow editorWindow)
        {
            this.editorWindow = editorWindow;
        }

        internal void DrawNewBTWindow()
        {
            editorWindow.DrawNewBTWindow();
        }

        /// <summary>
        /// Shows a notification on the owning editor window.
        /// </summary>
        /// <param name="content">Notification content.</param>
        internal void ShowNotification(GUIContent content)
        {
            editorWindow.ShowNotification(content);
        }

        /// <summary>Shows the common feedback for a rejected topology connection.</summary>
        internal void ShowConnectionRejectedNotification()
        {
            ShowNotification(new GUIContent(ConnectionRejectedMessage));
        }
    }
}
