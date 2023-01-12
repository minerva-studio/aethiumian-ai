using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    internal class AIEditorWindowModule
    {
        protected AIEditorWindow editorWindow;

        protected TreeNode SelectedNode { get => editorWindow.SelectedNode; set => editorWindow.SelectedNode = value; }
        protected TreeNode SelectedNodeParent => editorWindow.SelectedNodeParent;
        protected AIEditorSetting editorSetting => editorWindow.editorSetting;
        protected AISetting settings => editorWindow.setting;
        protected BehaviourTreeData tree => editorWindow.tree;
        protected Rect position => editorWindow.position;


        public List<TreeNode> unreachables => editorWindow.unreachables;
        public List<TreeNode> allNodes => editorWindow.allNodes;
        public List<TreeNode> reachables => editorWindow.reachables;



        internal void Initialize(AIEditorWindow editorWindow)
        {
            this.editorWindow = editorWindow;
        }







        public void DrawNewBTWindow()
        {
            editorWindow.DrawNewBTWindow();
        }
    }
}