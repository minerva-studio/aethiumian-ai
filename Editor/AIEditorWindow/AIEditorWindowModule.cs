using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Editor
{
    internal class AIEditorWindowModule
    {
        protected AIEditorWindow editorWindow;

        protected Rect position => editorWindow.position;


        protected TreeNode SelectedNode { get => editorWindow.SelectedNode; set => editorWindow.SelectedNode = value; }
        protected TreeNode SelectedNodeParent => editorWindow.SelectedNodeParent;
        protected AIEditorSetting EditorSetting => editorWindow.editorSetting;
        protected AISetting Settings => editorWindow.setting;
        protected BehaviourTreeData Tree => editorWindow.tree;


        internal List<TreeNode> AllNodes => editorWindow.allNodes;
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