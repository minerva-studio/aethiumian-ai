using Amlos.AI.Nodes;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Editor
{
    internal class AIEditorWindowModule
    {
        protected AIEditorWindow editorWindow;

        protected Rect position => editorWindow.position;

        internal TreeNode SelectedNode
        {
            get => editorWindow.SelectedNode;
            set => editorWindow.SelectedNode = value;
        }
        internal TreeNode SelectedNodeParent => editorWindow.SelectedNodeParent;
        internal AIEditorSetting EditorSetting => editorWindow.editorSetting;
        internal AISetting Settings => editorWindow.setting;
        internal BehaviourTreeData tree => editorWindow.tree;
        internal IReadOnlyList<TreeNode> AllNodes => editorWindow.AllNodes;
        internal HashSet<TreeNode> ReachableNodes => editorWindow.reachableNodes;


        internal void Initialize(AIEditorWindow editorWindow)
        {
            this.editorWindow = editorWindow;
        }

        internal void DrawNewBTWindow()
        {
            editorWindow.DrawNewBTWindow();
        }
    }
}
