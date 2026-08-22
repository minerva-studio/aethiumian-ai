using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    internal class TreeNodeModule : AIEditorWindowModule
    {
        public enum Mode
        {
            Global,
            local,
        }

        private const float SplitterWidth = 4f;
        private const float LeftWindowMinWidth = 160f;
        private const float LeftWindowMaxWidth = 600f;
        internal const float MiddleWindowMinWidth = 260f;

        private TreeNode selectedNode;
        private TreeNode selectedNodeParent;

        public NodeDrawHandler nodeDrawer;
        public SerializedProperty nodeRawDrawingProperty;

        public bool overviewWindowOpen = true;

        public Vector2 middleScrollPos;
        public Vector2 leftScrollPos;

        public Mode mode;
        EditorHeadNode editorHeadNode;

        private TreeNodeOverviewController overviewController;

        [SerializeField] private float leftPaneWidth = 300f;
        [NonSerialized] private bool resizingLeftPane;
        [NonSerialized] private float resizeStartMouseX;
        [NonSerialized] private float resizeStartWidth;

        internal NodeEditorCommandService NodeCommands => editorWindow.NodeCommands;
        internal TreeNodeOverviewController OverviewController => overviewController ??= new(this, editorWindow);
        public bool overviewShowService { get => EditorSetting.overviewShowService; set => EditorSetting.overviewShowService = value; }
        internal TreeNode SelectedNode { get => selectedNode; }
        internal TreeNode SelectedNodeParent => selectedNodeParent ??= (selectedNode == null ? null : tree.GetParent(selectedNode));
        internal EditorHeadNode EditorHeadNode => editorHeadNode ??= new();

        #region Tree Rendering And Pane Layout

        public void DrawTree()
        {
            if (!overviewWindowOpen) overviewWindowOpen = GUILayout.Button("Open Overview");
            if (!tree)
            {
                DrawNewBTWindow();
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                if (tree.IsInvalid())
                {
                    DrawInvalidTreeInfo();
                    return;
                }

                float leftWidth = overviewWindowOpen ? ClampSidePaneWidth(leftPaneWidth, LeftWindowMinWidth, LeftWindowMaxWidth) : 0f;
                float splitterWidth = overviewWindowOpen ? SplitterWidth : 0f;
                float contentWidth = Mathf.Max(0f, EditorGUIUtility.currentViewWidth - splitterWidth);
                float middleWidth = Mathf.Max(0f, contentWidth - leftWidth);

                if (middleWidth < MiddleWindowMinWidth)
                {
                    // Prefer the node editor when the window gets tight, then shrink the overview pane.
                    leftWidth = Mathf.Max(0f, contentWidth - Mathf.Min(MiddleWindowMinWidth, contentWidth));
                    middleWidth = Mathf.Max(0f, contentWidth - leftWidth);
                }

                leftPaneWidth = leftWidth;

                // Left
                if (overviewWindowOpen)
                {
                    using (new GUILayout.VerticalScope(GUILayout.Width(leftWidth)))
                    {
                        DrawOverview();
                    }
                    DrawVerticalSplitter(ref resizingLeftPane, ref leftPaneWidth, LeftWindowMinWidth, LeftWindowMaxWidth, false);
                }

                // Middle 
                using (new GUILayout.VerticalScope(GUILayout.Width(middleWidth)))
                {
                    DrawHeader(SelectedNode);

                    if (SelectedNode is EditorHeadNode)
                    {
                        DrawTreeHead();
                    }
                    else if (SelectedNode is null || !tree.nodes.Contains(SelectedNode))
                    {
                        TreeNode head = tree.Head;
                        if (head != null)
                        {
                            // Keep Layout and Repaint drawing the same controls when auto-selecting the head node.
                            SelectNode(head);
                            DrawSelectedNode(head);
                        }
                        else
                        {
                            CreateHeadNode();
                        }
                    }
                    else if (SelectedNode != null && tree.nodes.Contains(SelectedNode))
                    {
                        DrawSelectedNode(SelectedNode);
                    }
                }

            }
        }

        internal static float ClampSidePaneWidth(float width, float minWidth, float maxWidth)
        {
            return Mathf.Clamp(width, minWidth, maxWidth);
        }

        private void DrawHeader(TreeNode node)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (tree.IsServiceCall(node))
                {
                    GUILayout.Label($"Service {NodeDrawerUtility.GetEditorName(tree.GetServiceHead(node))}, ", EditorStyles.boldLabel);
                }
                GUILayout.Label(NodeDrawerUtility.GetEditorName(node), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                DrawLowerBar(node);
            }
        }

        private void DrawVerticalSplitter(ref bool resizing, ref float width, float minWidth, float maxWidth, bool invertDelta)
        {
            Rect splitterRect = GUILayoutUtility.GetRect(SplitterWidth, SplitterWidth, GUILayout.ExpandHeight(true));
            splitterRect.width = SplitterWidth;

            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                resizing = true;
                resizeStartMouseX = Event.current.mousePosition.x;
                resizeStartWidth = width;
                Event.current.Use();
            }

            if (resizing && Event.current.type == EventType.MouseDrag)
            {
                float delta = Event.current.mousePosition.x - resizeStartMouseX;
                if (invertDelta)
                {
                    delta = -delta;
                }

                width = Mathf.Clamp(resizeStartWidth + delta, minWidth, maxWidth);
                editorWindow.Repaint();
                Event.current.Use();
            }

            if (resizing && (Event.current.type == EventType.MouseUp || Event.current.rawType == EventType.MouseUp))
            {
                resizing = false;
                Event.current.Use();
            }

            var line1 = new Rect(splitterRect.x, splitterRect.y, 1f, splitterRect.height);
            var line2 = new Rect(splitterRect.x + 1f, splitterRect.y, 1f, splitterRect.height);
            EditorGUI.DrawRect(line1, new Color(0f, 0f, 0f, 0.35f));
            EditorGUI.DrawRect(line2, new Color(1f, 1f, 1f, 0.08f));
        }

        #endregion

        #region Selection And Deletion

        /// <summary>
        /// Try delete the node
        /// </summary>
        /// <param name="node"></param>
        public bool TryDeleteNode(TreeNode node, bool ok = false)
        {
            if (HasValidChildren(node))
            {
                int option = ok ? 0 : EditorUtility.DisplayDialogComplex("Deleting Node", $"Delete entire subtree under the node {node.name} ({node.uuid}) ?",
                                "Delete entire subtree", "Cancel", "Only selected node");
                switch (option)
                {
                    case 0:
                        tree.RemoveSubTree(node);
                        break;
                    case 1:
                        return false;
                    case 2:
                        tree.Remove(node);
                        break;
                }
            }
            // has at least one valid child node
            else
            {
                if (!ok && !EditorUtility.DisplayDialog("Deleting Node", $"Delete the node {node.name} ({node.uuid}) ?", "OK", "Cancel"))
                    return false;
                tree.Remove(node);
            }

            FinalizeDelete(node);
            return true;
        }

        /// <summary>
        /// Try delete the node
        /// </summary>
        /// <param name="node"></param>
        public bool TryDeleteNodeOnly(TreeNode node, bool ok = false)
        {
            if (!ok && !EditorUtility.DisplayDialog("Deleting Node", $"Delete the node {node.name} ({node.uuid}) ?", "OK", "Cancel"))
                return false;

            tree.Remove(node);
            FinalizeDelete(node);
            return true;
        }

        /// <summary>
        /// Remove the subtree
        /// </summary>
        /// <param name="node"></param>
        public bool TryDeleteSubTree(TreeNode node, bool ok = false)
        {
            if (!ok && !EditorUtility.DisplayDialog("Deleting Node", $"Delete the node {node.name} ({node.uuid}) ?", "OK", "Cancel"))
            {
                return false;
            }

            tree.RemoveSubTree(node);

            FinalizeDelete(node);
            return true;
        }

        /// <summary>
        /// Refreshes the legacy editor after the data owner commits a deletion.
        /// </summary>
        /// <param name="node">The deleted node whose previous parent should be selected.</param>
        private void FinalizeDelete(TreeNode node)
        {
            editorWindow.Refresh();
            TryDeleteNode_OpenParent(node);
        }

        private void TryDeleteNode_OpenParent(TreeNode node)
        {
            var parent = tree.GetNode(node.parent);
            if (parent != null)
            {
                SelectNode(parent);
            }
            else
            {
                SelectNode(tree.Head);
            }
        }

        /// <summary>
        /// Select a node in the editor.
        /// </summary>
        /// <param name="node"></param>
        public void SelectNode(TreeNode node)
        {
            // use this line to magically remove the focus the line
            GUI.FocusControl(null);
            selectedNode = node;
            selectedNodeParent = node == null || tree == null ? null : tree.GetParent(node);
            editorWindow.NotifySelectionChanged(node);
        }

        /// <summary>
        /// Select the parent of given node in the window
        /// </summary>
        /// <param name="node"></param>
        public void SelectParentNode(TreeNode node)
        {
            var parent = tree.GetParent(node) ?? editorHeadNode;
            SelectNode(parent);
        }

        #endregion

        #region Tree State

        internal void DrawTreeHead()
        {
            TreeNode head = tree.Head;
            string nodeName = head?.name ?? string.Empty;

            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Head: " + nodeName);
                }

                using (IndentScope.Increase)
                {
                    if (head is null)
                    {
                        if (GUILayout.Button("Select.."))
                            editorWindow.NodeSelection.Open(
                                NodeSelectionContext.Nodes,
                                choice =>
                                {
                                    if (!CommitChoiceToHead(choice))
                                        ShowConnectionRejectedNotification();
                                },
                                GUILayoutUtility.GetLastRect(),
                                candidate => candidate != null && tree.CanSetHead(candidate.uuid, allowMoveExisting: true));
                        return;
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        using (new GUILayout.VerticalScope(GUILayout.MaxWidth(80)))
                        {
                            if (GUILayout.Button("Open"))
                            {
                                Debug.Log("Open");
                                SelectNode(head);
                            }
                            else if (GUILayout.Button("Replace"))
                            {
                                editorWindow.NodeSelection.Open(
                                    NodeSelectionContext.Nodes,
                                    choice =>
                                    {
                                        if (!CommitChoiceToHead(choice))
                                            ShowConnectionRejectedNotification();
                                    },
                                    GUILayoutUtility.GetLastRect(),
                                    candidate => candidate != null && tree.CanSetHead(candidate.uuid, allowMoveExisting: true));
                            }
                            else if (GUILayout.Button("Delete"))
                            {
                                TrySetHeadNode(null);
                            }
                        }

                        using (IndentScope.Increase)
                        using (new GUILayout.VerticalScope())
                        {
                            var script = MonoScriptCache.Get(head.GetType());
                            using (new EditorGUI.DisabledScope(true))
                                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);

                            head.name = EditorGUILayout.TextField("Name", head.name);
                            EditorGUILayout.LabelField("UUID", head.uuid);
                        }
                    }
                }
            }
        }

        private void DrawInvalidTreeInfo()
        {
            GUILayout.Space(10);
            SetMiddleWindowColorAndBeginVerticle();
            EditorGUILayout.LabelField(
                $"Unable to load behaviour tree \"{tree.name}\", at least 1 null node appears in data."
            );
            EditorGUILayout.LabelField(
                $"Force loading this behaviour tree might result data corruption."
            );
            EditorGUILayout.LabelField($"Several reasons might cause this problem:");
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"1. Node class have been renamed recently.");
            EditorGUILayout.LabelField(
                $"2. Node class have been transferred to another namespace recently."
            );
            EditorGUILayout.LabelField(
                $"3. Node class have been transferred to another assembly recently."
            );
            EditorGUILayout.LabelField($"4. Asset corrupted during merging");
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField(
                $"You can try using MovedFrom Attribute to migrate the node to a new name/namespace/assembly."
            );
            EditorGUILayout.LabelField(
                $"If the problem still occur, you might need to open behaviour tree data file \"{tree.name}\" data file in Unity Inspector or an text editor to manually fix the issue"
            );
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("==========");
            EditorGUILayout.LabelField($"First Null Index: {tree.nodes.IndexOf(null)}");
            GUILayout.EndVertical();
        }





        #endregion

        #region Context Menu And Command Dispatch

        /// <summary>
        /// Create the right click menu for a node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="menu"></param>
        public void CreateRightClickMenu(TreeNode node, GenericMenu menu, INodeCommandHandler commandHandler = null)
        {
            menu.AddItem(new GUIContent("Open"), false, () => SelectNode(node));
            if (ReachableNodes != null && ReachableNodes.Contains(node)) menu.AddItem(new GUIContent($"Open Parent"), false, () => { if (node != null) SelectParentNode(node); });
            else menu.AddDisabledItem(new GUIContent($"Open Parent"));

            if (node != null)
            {
                menu.AddItem(new GUIContent("Expand All"), false, () => ExpandOverviewSubtree(node));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Expand All"));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete Subtree"), false, () => TryDeleteSubTree(node));

            menu.AddSeparator("");
            if (EditorSetting.debugMode) menu.AddItem(new GUIContent("Copy Serialized Data"), false, () => GUIUtility.systemCopyBuffer = JsonUtility.ToJson(node));
            PopulateNodeCommandMenu(menu, node, commandHandler);

            menu.AddSeparator("");
            node.AddContent(menu, tree);
        }

        /// <summary>Fills a legacy Nodes menu through the shared command registrar.</summary>
        internal void PopulateNodeCommandMenu(GenericMenu menu, TreeNode node, INodeCommandHandler commandHandler = null)
        {
            NodeCommandMenuRegistrar.Register(
                new GenericNodeCommandMenu(menu),
                editorWindow.NodeCommands,
                node,
                commandHandler ?? new TreeNodeCommandHandler(this, editorWindow.NodeCommands));
        }

        /// <summary>Refreshes the legacy view after a shared command mutates tree data.</summary>
        internal void RefreshAfterCommand() => editorWindow.Refresh();

        #endregion

        #region Left Window  
        /// <summary>
        /// Draw Overview window
        /// </summary>
        private void DrawOverview()
        {
            OverviewController.Draw(leftPaneWidth);
        }

        /// <summary>
        /// Expand all overview foldouts under the specified node.
        /// </summary>
        /// <param name="node">The root node of the subtree to expand.</param>
        private void ExpandOverviewSubtree(TreeNode node)
        {
            if (node == null || tree == null)
            {
                return;
            }

            OverviewController.ExpandSubtree(node);
        }

        #endregion




        private void SetMiddleWindowColorAndBeginVerticle()
        {
            var colorStyle = new GUIStyle();
            colorStyle.normal.background = Texture2D.whiteTexture;
            var baseColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(64 / 255f, 64 / 255f, 64 / 255f);
            GUILayout.BeginVertical(colorStyle, GUILayout.ExpandWidth(true));
            GUI.backgroundColor = baseColor;
        }

        private void DrawSelectedNode(TreeNode node)
        {
            using (new GUILayout.VerticalScope())
            {
                using (new EditorGUI.DisabledScope(false))
                {
                    middleScrollPos = GUILayout.BeginScrollView(middleScrollPos);
                    middleScrollPos.x = 0;
                }

                SetMiddleWindowColorAndBeginVerticle();
                {
                    if (!ReachableNodes.Contains(node))
                    {
                        var textColor = GUI.contentColor;
                        GUI.contentColor = Color.red;
                        GUILayout.Label("Warning: this node is unreachable");
                        GUI.contentColor = textColor;
                    }
                    else if (SelectedNodeParent == null)
                        GUILayout.Label("Tree Head");
                    if (nodeDrawer == null || nodeDrawer.Node != node)
                        nodeDrawer = new(editorWindow, node);

                    if (EditorSetting.debugMode && SelectedNodeParent != null)
                        EditorGUILayout.LabelField("Parent UUID", SelectedNodeParent.uuid);
                    DrawNodeInspectorContent(node);
                }
                GUILayout.EndVertical();
                GUILayout.EndScrollView();

                if (EditorSetting.debugMode)
                {
                    var script = MonoScriptCache.Get(nodeDrawer.GetCurrentDrawerType());
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Current Node Drawer", script, typeof(MonoScript), false);
                }
                if (SelectedNodeParent == null && SelectedNode.uuid != tree.headNodeUUID && ReachableNodes.Contains(SelectedNode))
                {
                    Debug.LogError($"Node {SelectedNode.name} has a missing parent reference!");
                }
            }

            Rect rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                CreateRightClickMenu(node, menu);
            }
            //EditorFieldDrawers.RightClickMenu(menu);
        }

        #region Inspector

        /// <summary>
        /// Draws the node drawer after the owning module has selected or reused it.
        /// </summary>
        /// <param name="node">The node whose serialized properties are drawn.</param>
        private void DrawNodeInspectorContent(TreeNode node)
        {
            if (nodeDrawer == null || nodeDrawer.Node != node)
            {
                nodeDrawer = new NodeDrawHandler(editorWindow, node);
            }

            nodeDrawer.Draw();
        }

        private void DrawLowerBar(TreeNode node)
        {
            string last = SelectedNodeParent == null ? "HEAD" : "Parent";
            //var option = GUILayout.Toolbar(-1, new string[] { last, "Copy", "Delete" }, EditorStyles.toolbarButton, GUILayout.MinHeight(30));
            if (GUILayout.Button(last, EditorStyles.toolbarButton))
            {
                if (SelectedNodeParent != null)
                    SelectNode(SelectedNodeParent);
                else SelectNode(EditorHeadNode);
            }
            if (GUILayout.Button("Copy", EditorStyles.toolbarButton))
            {
                if (Event.current.button != 0)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Copy Serialized Data"), false, () => GUIUtility.systemCopyBuffer = JsonUtility.ToJson(node));
                    menu.AddItem(new GUIContent("Copy to clipboard"), false, () => NodeCommands.Copy(node, true));
                    menu.ShowAsContext();
                }
                else
                {
                    NodeCommands.Copy(SelectedNode, true);
                }
                //clipboard = SelectedNode.uuid;
            }
            if (GUILayout.Button("Delete", EditorStyles.toolbarButton))
            {
                if (Event.current.button != 0)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Delete node"), false, () => TryDeleteNodeOnly(node));
                    // has subtree
                    if (HasValidChildren(node)) menu.AddItem(new GUIContent("Delete subtree"), false, () => TryDeleteSubTree(node));
                    menu.ShowAsContext();
                }
                else
                {
                    TryDeleteNode(node);
                }
            }
        }

        private bool HasValidChildren(TreeNode node)
        {
            return node.GetChildrenReference().Any(r => tree.GetNode(r) != null);
        }

        #endregion

        #region Node Creation
        /// <summary>Commits one dropdown choice to the tree Head.</summary>
        private bool CommitChoiceToHead(NodeSelectionChoice choice)
        {
            if (!NodeCommands.TryResolveChoice(choice, NodeSelectionContext.Nodes, out TreeNode root, out IReadOnlyList<TreeNode> addedNodes))
            {
                return false;
            }

            bool committed = addedNodes != null
                ? tree.TryAddAndSetHead(addedNodes, root.uuid, "Set tree Head")
                : TrySetHeadNode(root);
            if (committed && addedNodes != null)
            {
                editorWindow.Refresh();
                SelectNode(root);
            }

            return committed;
        }

        /// <summary>
        /// helper for createing new head when the Ai file just created
        /// </summary>
        private void CreateHeadNode()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("No Head Node", EditorStyles.boldLabel);
            if (GUILayout.Button("Create", GUILayout.Height(30), GUILayout.Width(200)))
            {
                editorWindow.NodeSelection.Open(
                    NodeSelectionContext.Nodes,
                    choice =>
                    {
                        if (!CommitChoiceToHead(choice))
                            ShowConnectionRejectedNotification();
                    },
                    GUILayoutUtility.GetLastRect(),
                    candidate => candidate != null && tree.CanSetHead(candidate.uuid, allowMoveExisting: true));
            }
            GUILayout.EndVertical();
        }

        #endregion

        #region Upgrade

        /// <summary>
        /// Attempts to upgrade a node while preserving identity references.
        /// </summary>
        /// <param name="node">The node to upgrade.</param>
        /// <param name="prompt">Whether to ask for confirmation before upgrading.</param>
        /// <returns><c>true</c> if the upgrade completed; otherwise, <c>false</c>.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        internal bool TryUpgradeNode(TreeNode node, bool prompt = true)
        {
            if (tree == null || node == null)
            {
                return false;
            }

            if (!node.CanUpgrade())
            {
                return false;
            }

            if (prompt && !EditorUtility.DisplayDialog("Upgrade Node", $"Upgrade node {node.name} ({node.uuid})?", "Upgrade", "Cancel"))
            {
                return false;
            }

            if (!tree.TryUpgradeNode(node, out TreeNode upgradedNode))
            {
                EditorUtility.DisplayDialog("Upgrade Failed", $"Upgrade returned no result for node {node.name}.", "OK");
                return false;
            }

            editorWindow.Refresh();
            SelectNode(upgradedNode);
            return true;
        }

        /// <summary>
        /// Changes the authored Head node through the legacy editor transaction boundary.
        /// </summary>
        /// <param name="node">The node to make Head, or <c>null</c> to clear Head.</param>
        /// <returns><c>true</c> when the Head value changed; otherwise, <c>false</c>.</returns>
        internal bool TrySetHeadNode(TreeNode node)
        {
            UUID nextUUID = node?.uuid ?? UUID.Empty;
            if (node != null && tree?.GetNode(nextUUID) != node)
            {
                return false;
            }

            if (tree == null || tree.headNodeUUID == nextUUID)
            {
                return false;
            }

            if (node == null)
            {
                bool cleared = tree.TrySetHead(UUID.Empty, "Set tree Head");
                if (cleared)
                {
                    editorWindow.Refresh();
                }

                return cleared;
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(tree.EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(node);
            if (!tree.CanSetHead(node.uuid, allowMoveExisting: true))
            {
                return false;
            }

            TreeNode parent = incoming.Count == 1 ? incoming[0].Owner : null;

            if (parent != null
                && !EditorUtility.DisplayDialog(
                    "Node has a parent already",
                    $"This Node is connecting to {parent.name}, move to Head?",
                    "OK",
                    "Cancel"))
            {
                return false;
            }

            bool committed = tree.TryMoveToHead(nextUUID, "Set tree Head");
            if (committed)
            {
                editorWindow.Refresh();
                SelectNode(node);
            }

            return committed;
        }

        #endregion

        internal struct OverviewEntry
        {
            public TreeNode node;
            public int indent;
            public bool isServiceStack;
            public readonly bool canFold;

            public OverviewEntry(TreeNode node, int indent, bool isServiceStack)
            {
                this.node = node;
                this.indent = indent;
                this.isServiceStack = isServiceStack;
                this.canFold = node is Flow and not Wait and not Pause;
            }

            public override readonly bool Equals(object obj)
            {
                return obj is OverviewEntry other &&
                       EqualityComparer<TreeNode>.Default.Equals(node, other.node) &&
                       indent == other.indent;
            }

            public override readonly int GetHashCode()
            {
                return HashCode.Combine(node, indent);
            }

            public readonly void Deconstruct(out TreeNode item1, out int item2)
            {
                item1 = node;
                item2 = indent;
            }

            public static implicit operator (TreeNode, int, bool)(OverviewEntry value)
            {
                return (value.node, value.indent, value.isServiceStack);
            }

            public static implicit operator OverviewEntry((TreeNode, int, bool) value)
            {
                return new OverviewEntry(value.Item1, value.Item2, value.Item3);
            }
        }
    }

}
