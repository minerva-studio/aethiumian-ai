using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#endif

namespace Aethiumian.AI.Editor
{
    /// <summary>Owns the Overview TreeView lifecycle and Overview-only interactions.</summary>
    internal sealed class TreeNodeOverviewController
    {
        private readonly TreeNodeModule owner;
        private readonly AIEditorWindow editor;
        private TreeViewState treeViewState;
        private BehaviourTreeOverviewTreeView treeView;

        /// <summary>Initializes the Overview controller for one Nodes page.</summary>
        /// <param name="owner">The Nodes page that supplies selection and tree data.</param>
        internal TreeNodeOverviewController(TreeNodeModule owner, AIEditorWindow editor)
        {
            this.owner = owner;
            this.editor = editor;
        }

        /// <summary>Draws the Overview toolbar and TreeView.</summary>
        /// <param name="leftPaneWidth">The current Overview pane width.</param>
        internal void Draw(float leftPaneWidth)
        {
            EnsureTreeView();

            bool compactHeader = leftPaneWidth < 230f;
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Overview", EditorStyles.boldLabel, GUILayout.Width(68f));
                GUILayout.FlexibleSpace();

                bool showLocal = owner.mode == TreeNodeModule.Mode.local;
                GUIContent local = new(compactHeader ? "L" : "Local")
                {
                    tooltip = "Show only the local tree of selected node"
                };
                bool newShowLocal = GUILayout.Toggle(
                    showLocal,
                    local,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compactHeader ? 28f : 60f));
                if (newShowLocal != showLocal)
                {
                    owner.mode = newShowLocal ? TreeNodeModule.Mode.local : TreeNodeModule.Mode.Global;
                }

                GUIContent service = new(compactHeader ? "S" : "Service")
                {
                    tooltip = "Show service nodes in the overview"
                };
                bool newShowService = GUILayout.Toggle(
                    owner.overviewShowService,
                    service,
                    EditorStyles.toolbarButton,
                    GUILayout.Width(compactHeader ? 28f : 60f));
                if (newShowService != owner.overviewShowService)
                {
                    owner.overviewShowService = newShowService;
                }

                if (GUILayout.Button(
                    new GUIContent(string.Empty, "Expand all overview entries"),
                    EditorStyles.toolbarDropDown,
                    GUILayout.Width(20f)))
                {
                    treeView.ExpandAll();
                }
            }

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            treeView.SetData(owner);
            treeView.OnGUI(rect);
            treeView.HandleKeyboardShortcuts(Event.current);

            GUILayout.Space(10f);
            owner.overviewWindowOpen = !GUILayout.Button("Close");
        }

        /// <summary>Expands the Overview subtree rooted at the specified node.</summary>
        /// <param name="node">The subtree root.</param>
        internal void ExpandSubtree(TreeNode node)
        {
            if (node == null || owner.tree == null)
            {
                return;
            }

            owner.overviewWindowOpen = true;
            EnsureTreeView();
            treeView.SetData(owner);
            treeView.ExpandSubtree(node);
            editor.Repaint();
        }

        /// <summary>Discards cached TreeView state after the active tree changes.</summary>
        internal void Invalidate()
        {
            treeViewState = null;
            treeView = null;
        }

        /// <summary>Creates the TreeView lazily to preserve Nodes page startup cost.</summary>
        private void EnsureTreeView()
        {
            treeViewState ??= new TreeViewState();
            treeView ??= new BehaviourTreeOverviewTreeView(treeViewState);
        }
    }
}
