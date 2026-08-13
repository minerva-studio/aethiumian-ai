using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#endif

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
        public List<OverviewEntry> overviewCache;

        public Vector2 middleScrollPos;
        public Vector2 leftScrollPos;

        public Mode mode;
        EditorHeadNode editorHeadNode;

        private TreeViewState overviewTreeViewState;
        private BehaviourTreeOverviewTreeView overviewTreeView;

        [SerializeField] private float leftPaneWidth = 300f;
        [NonSerialized] private bool resizingLeftPane;
        [NonSerialized] private float resizeStartMouseX;
        [NonSerialized] private float resizeStartWidth;

        public Clipboard clipboard => editorWindow.Clipboard;
        public bool overviewShowService { get => EditorSetting.overviewShowService; set => EditorSetting.overviewShowService = value; }
        internal new TreeNode SelectedNode { get => selectedNode; }
        internal new TreeNode SelectedNodeParent => selectedNodeParent ??= (selectedNode == null ? null : tree.GetParent(selectedNode));
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
        /// Removes one node UUID from a specific legacy owner while moving an existing node.
        /// </summary>
        /// <param name="owner">The current reference owner.</param>
        /// <param name="node">The node being moved.</param>
        private static void RemoveFromParent(TreeNode owner, TreeNode node)
        {
            UUID targetUUID = node.uuid;
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
            {
                if (field.Name != nameof(TreeNode.parent) && field.Get(owner)?.UUID == targetUUID)
                {
                    field.Get(owner).Set(null);
                }
            }

            foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
            {
                System.Collections.IList entries = field.Get(owner);
                if (entries == null) continue;
                for (int index = entries.Count - 1; index >= 0; index--)
                {
                    if (entries[index] is INodeReference reference && reference.UUID == targetUUID)
                    {
                        entries.RemoveAt(index);
                    }
                }
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

        private void DrawTreeHead()
        {
            SelectNodeEvent selectCallback = (node) => TrySetHeadNode(node);
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
                            OpenNodeSelectionDropdown(NodeSelectionContext.Nodes, selectCallback);
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
                                OpenNodeSelectionDropdown(NodeSelectionContext.Nodes, selectCallback);
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
        public void CreateRightClickMenu(TreeNode node, GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Open"), false, () => SelectNode(node));
            if (ReachableNodes.Contains(node)) menu.AddItem(new GUIContent($"Open Parent"), false, () => { if (node != null) SelectParentNode(node); });
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
            PopulateNodeCommandMenu(menu, node);

            menu.AddSeparator("");
            node.AddContent(menu, tree);
        }

        /// <summary>Fills a legacy Nodes menu through the shared command registrar.</summary>
        internal void PopulateNodeCommandMenu(GenericMenu menu, TreeNode node)
        {
            NodeCommandMenuRegistrar.Register(
                new GenericNodeCommandMenu(menu),
                this,
                node,
                new TreeNodeCommandHandler(this));
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
            EnsureOverviewTreeView();

            bool compactHeader = leftPaneWidth < 230f;
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Overview", EditorStyles.boldLabel, GUILayout.Width(68f));
                GUILayout.FlexibleSpace();

                bool showLocal = mode == Mode.local;
                // var global = new GUIContent("Global") { tooltip = "Display the entire behaviour tree" };
                var local = new GUIContent(compactHeader ? "L" : "Local") { tooltip = "Show only the local tree of selected node" };
                bool newShowLocal = GUILayout.Toggle(showLocal, local, EditorStyles.toolbarButton, GUILayout.Width(compactHeader ? 28f : 60f));
                if (newShowLocal != showLocal)
                {
                    mode = newShowLocal ? Mode.local : Mode.Global;
                }
                var service = new GUIContent(compactHeader ? "S" : "Service") { tooltip = "Show service nodes in the overview" };
                bool newShowService = GUILayout.Toggle(overviewShowService, service, EditorStyles.toolbarButton, GUILayout.Width(compactHeader ? 28f : 60f));
                if (newShowService != overviewShowService)
                {
                    overviewShowService = newShowService;
                }
                if (GUILayout.Button(new GUIContent("", "Expand all overview entries"), EditorStyles.toolbarDropDown, GUILayout.Width(20)))
                {
                    overviewTreeView.ExpandAll();
                }
            }

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true)
            );

            overviewTreeView.SetData(treeNodeModule: this);
            overviewTreeView.OnGUI(rect);
            overviewTreeView.HandleKeyboardShortcuts(Event.current);

            GUILayout.Space(10);
            overviewWindowOpen = !GUILayout.Button("Close");
        }

        private void EnsureOverviewTreeView()
        {
            overviewTreeViewState ??= new TreeViewState();
            overviewTreeView ??= new BehaviourTreeOverviewTreeView(overviewTreeViewState);
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

            overviewWindowOpen = true;
            EnsureOverviewTreeView();
            overviewTreeView.SetData(this);
            overviewTreeView.ExpandSubtree(node);
            editorWindow.Repaint();
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

        #region Clipboard And Node Commands

        /// <summary>Copies an authored node into the editor clipboard without modifying the tree.</summary>
        internal void CopyNode(TreeNode node, bool includeSubtree)
        {
            if (node == null || tree?.GetNode(node.uuid) != node) return;
            clipboard.Clear();
            if (includeSubtree) clipboard.Write(node, tree);
            else clipboard.WriteSingle(node, tree);
        }

        public void WriteClipboard(TreeNode selectedNode)
        {
            clipboard.Clear();
            clipboard.Write(selectedNode, tree);
        }

        /// <summary>Gets whether the clipboard can replace the target's editable value fields.</summary>
        internal bool CanPasteValue(TreeNode node) => node != null
            && tree?.GetNode(node.uuid) == node
            && clipboard.HasSingleRootContent
            && clipboard.TypeMatch(node);

        #endregion

        /// <summary>Gets whether the clipboard root is valid for a structural insertion.</summary>
        internal bool CanPasteStructure => clipboard.HasSingleRootContent && clipboard.Root is not Service;

        #region Structural Clipboard Commands

        /// <summary>Gets authored single-reference slots that may receive a structural paste.</summary>
        internal IReadOnlyList<INodeReferenceSingleSlot> GetPasteSingleTargets(TreeNode node) => node == null
            ? Array.Empty<INodeReferenceSingleSlot>()
            : node.ToReferenceSlots().OfType<INodeReferenceSingleSlot>().ToArray();

        /// <summary>Gets authored list-reference slots that may receive a structural paste.</summary>
        internal IReadOnlyList<INodeReferenceListSlot> GetPasteListTargets(TreeNode node) => node == null
            ? Array.Empty<INodeReferenceListSlot>()
            : node.ToReferenceSlots().OfType<INodeReferenceListSlot>().ToArray();

        /// <summary>Finds the exact list occurrence used to insert beside an existing node.</summary>
        internal bool TryGetSiblingPasteTarget(TreeNode node, out TreeNode parent, out INodeReferenceListSlot slot, out int index)
        {
            if (!CanPasteStructure) { parent = null; slot = null; index = -1; return false; }
            return TryGetSiblingOccurrence(node, out parent, out slot, out index);
        }

        /// <summary>Finds a node's actual list owner without consulting clipboard state.</summary>
        private bool TryGetSiblingOccurrence(TreeNode node, out TreeNode parent, out INodeReferenceListSlot slot, out int index)
        {
            parent = node == null ? null : tree?.GetParent(node);
            slot = null;
            index = -1;
            if (parent == null) return false;
            foreach (INodeReferenceListSlot candidate in parent.ToReferenceSlots().OfType<INodeReferenceListSlot>())
            {
                int candidateIndex = candidate.IndexOf(node);
                if (candidateIndex < 0) continue;
                slot = candidate;
                index = candidateIndex;
                return true;
            }
            return false;
        }

        /// <summary>Gets whether a node can be duplicated at its actual owned occurrence.</summary>
        internal bool CanDuplicateNode(TreeNode node)
        {
            if (node == null || tree?.GetNode(node.uuid) != node) return false;
            TreeNode parent = tree.GetParent(node);
            return node is Service
                ? parent.CanEditServices()
                : TryGetSiblingOccurrence(node, out _, out _, out _);
        }

        /// <summary>Duplicates a node using the existing clipboard clone mechanism.</summary>
        internal TreeNode DuplicateNode(TreeNode node)
        {
            if (!CanDuplicateNode(node)) return null;
            Clipboard source = new();
            source.Write(node, tree);
            List<TreeNode> content = source.Content;
            foreach (TreeNode item in content) item.name = tree.GenerateNewNodeName(item.name);
            TreeNode root = content[0];
            TreeNode parent = tree.GetParent(node);
            tree.AddRange(content, false);
            if (root is Service service && parent.CanEditServices()
                && ServiceHostNodeUtility.TryAsServiceHost(parent, out IServiceHostNode host))
                host.AddService(service);
            else if (TryGetSiblingOccurrence(node, out _, out INodeReferenceListSlot slot, out int index))
            {
                slot.Insert(index + 1, root);
                root.parent = parent;
            }
            else return null;
            return root;
        }

        internal bool DuplicateNodeWithUndo(TreeNode node)
        {
            if (!CanDuplicateNode(node)) return false;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Duplicate {node?.name}");
            Undo.RegisterCompleteObjectUndo(tree, $"Duplicate {node?.name}");
            try
            {
                if (DuplicateNode(node) == null)
                {
                    Undo.RevertAllDownToGroup(group);
                    Undo.CollapseUndoOperations(group);
                    tree.RegenerateTable();
                    tree.SerializedObject.Update();
                    editorWindow.Refresh();
                    return false;
                }

                tree.SerializedObject.ApplyModifiedProperties();
                tree.SerializedObject.Update();
                tree.RegenerateTable();
                EditorUtility.SetDirty(tree);
                Undo.CollapseUndoOperations(group);
                editorWindow.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                Undo.RevertAllDownToGroup(group);
                Undo.CollapseUndoOperations(group);
                tree.SerializedObject.Update();
                tree.RegenerateTable();
                editorWindow.Refresh();
                return false;
            }
        }

        /// <summary>Pastes clipboard value fields while retaining the target node identity.</summary>
        internal bool PasteValue(TreeNode node)
        {
            if (!CanPasteValue(node)) return false;
            clipboard.PasteValue(tree, node);
            return true;
        }

        /// <summary>Pastes the clipboard subtree into one single-reference slot.</summary>
        internal TreeNode PasteTo(TreeNode owner, INodeReferenceSingleSlot slot)
        {
            if (!CanPasteStructure || owner == null || slot == null) return null;
            HashSet<UUID> existing = tree.EditorNodes.Select(item => item.uuid).ToHashSet();
            clipboard.PasteTo(tree, owner, slot);
            return tree.EditorNodes.FirstOrDefault(item => !existing.Contains(item.uuid));
        }

        /// <summary>Pastes the clipboard subtree into one list-reference position.</summary>
        internal TreeNode PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index)
        {
            if (!CanPasteStructure || owner == null || slot == null) return null;
            HashSet<UUID> existing = tree.EditorNodes.Select(item => item.uuid).ToHashSet();
            clipboard.PasteAt(tree, owner, slot, index);
            return tree.EditorNodes.FirstOrDefault(item => !existing.Contains(item.uuid));
        }

        #endregion

        #region Inspector

        /// <summary>
        /// Draws the selected node drawer in an independently owned scroll view.
        /// The same <see cref="NodeDrawHandler"/> instance is shared by Nodes and Graph.
        /// </summary>
        /// <param name="node">The node to draw.</param>
        /// <param name="scrollPosition">The scroll position owned by the caller.</param>
        internal void DrawGraphInspector(TreeNode node, ref Vector2 scrollPosition)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            scrollPosition.x = 0f;

            if (node is EditorHeadNode)
            {
                DrawTreeHead();
            }
            else if (node == null || tree == null || tree.nodes == null || !tree.nodes.Contains(node))
            {
                EditorGUILayout.HelpBox("Select a node to inspect its properties.", MessageType.Info);
            }
            else
            {
                if (node != tree.Head && ReachableNodes != null && !ReachableNodes.Contains(node))
                {
                    EditorGUILayout.HelpBox("This node is unreachable from the tree head.", MessageType.Warning);
                }

                DrawNodeInspectorContent(node);
            }

            GUILayout.EndScrollView();
        }

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
                    menu.AddItem(new GUIContent("Copy to clipboard"), false, () => WriteClipboard(node));
                    menu.ShowAsContext();
                }
                else
                {
                    WriteClipboard(SelectedNode);
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

        private void DrawNodeService(TreeNode treeNode)
        {
            if (!treeNode.CanEditServices() || !ServiceHostNodeUtility.TryAsServiceHost(treeNode, out var serviceHost))
            {
                return;
            }

            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label("Service");

            var services = serviceHost.EnsureServices();
            if (services.Count == 0)
            {
                GUILayout.Label("No service");
            }
            else
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < services.Count; i++)
                {
                    if (tree.GetNode(services[i]) is not Service item)
                    {
                        // Keep the invalid service row balanced with the normal service row below.
                        using (new GUILayout.HorizontalScope())
                        {
                            var currentColor = GUI.contentColor;
                            GUI.contentColor = Color.red;
                            GUILayout.Label("Node not found: " + services[i]);
                            GUI.contentColor = currentColor;
                            if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
                            {
                                services.RemoveAt(i);
                                i--;
                            }
                        }
                        continue;
                    }

                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Space(18);
                        if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
                        {
                            services.RemoveAt(i);
                            i--;
                            item.parent = NodeReference.Empty;
                            if (
                                EditorUtility.DisplayDialog(
                                    "Delete Service",
                                    "Do you want to delete the service from the tree too?",
                                    "OK",
                                    "Cancel"
                                )
                            )
                            {
                                tree.Remove(item);
                            }
                        }
                        var formerGUIStatus = GUI.enabled;
                        if (i == 0)
                            GUI.enabled = false;
                        if (GUILayout.Button("^", GUILayout.MaxWidth(18)))
                        {
                            services.RemoveAt(i);
                            services.Insert(i - 1, item);
                        }
                        GUI.enabled = formerGUIStatus;
                        if (i == services.Count - 1)
                            GUI.enabled = false;
                        if (GUILayout.Button("v", GUILayout.MaxWidth(18)))
                        {
                            services.RemoveAt(i);
                            services.Insert(i + 1, item);
                        }
                        GUI.enabled = formerGUIStatus;
                        GUILayout.Label(item.GetType().Name);
                        if (GUILayout.Button("Open"))
                        {
                            SelectNode(item);
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }
            Rect addRect = GUILayoutUtility.GetRect(new GUIContent("Add"), GUI.skin.button);
            if (GUI.Button(addRect, "Add"))
            {
                OpenNodeSelectionDropdown(
                    NodeSelectionContext.Services,
                    (e) =>
                    {
                        if (e is not Service service)
                        {
                            return;
                        }

                        serviceHost.AddService(service);
                    },
                    false,
                    addRect);
            }
            GUILayout.EndVertical();
        }




        #endregion

        /// <summary>
        /// Gets the shared node menu cache.
        /// </summary>
        /// <returns>The shared cache.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private static NodeMenuCache MenuCache => NodeMenuCache.Shared;

        /// <summary>
        /// Opens the node selection dropdown for one node assignment or creation flow.
        /// </summary>
        /// <param name="context">The node catalogue to display.</param>
        /// <param name="e">The callback that receives the chosen node.</param>
        /// <param name="isRawSelect">Whether the selection is for a raw reference.</param>
        public void OpenNodeSelectionDropdown(NodeSelectionContext context, SelectNodeEvent e, bool isRawSelect = false)
        {
            Rect anchor = GUILayoutUtility.GetLastRect();
            OpenNodeSelectionDropdown(context, e, isRawSelect, anchor);
        }

        /// <summary>
        /// Opens the node selection dropdown at an explicit IMGUI anchor rectangle.
        /// </summary>
        /// <param name="context">The node catalogue to display.</param>
        /// <param name="e">The callback that receives the chosen node.</param>
        /// <param name="isRawSelect">Whether the selection is for a raw reference.</param>
        /// <param name="anchor">The IMGUI rectangle that opened the dropdown.</param>
        public void OpenNodeSelectionDropdown(NodeSelectionContext context, SelectNodeEvent e, bool isRawSelect, Rect anchor)
        {
            if (anchor.width <= 0f || anchor.height <= 0f)
            {
                anchor = new Rect(0f, 0f, 1f, EditorGUIUtility.singleLineHeight);
            }

            NodeSelectionDropdown dropdown = new(this, context, e, isRawSelect);
            dropdown.Show(anchor);
        }

        #region Node Creation


        /// <summary>
        /// Creates a node selected from the node selection dropdown.
        /// </summary>
        /// <param name="type">The concrete node type to create.</param>
        /// <param name="callback">The callback for the selected node.</param>
        internal void CreateAndSelectNode(Type type, SelectNodeEvent callback)
        {
            var node = CreateNode(type);
            CompleteNodeSelection(node, callback);
        }

        /// <summary>
        /// Commits a selected node to the pending selection callback.
        /// </summary>
        /// <param name="node">The selected node.</param>
        /// <param name="callback">The callback for the selected node.</param>
        private void CompleteNodeSelection(TreeNode node, SelectNodeEvent callback)
        {
            callback?.Invoke(node);
            tree.SerializedObject.Update();
            editorWindow.Refresh();
            SelectNode(node);
        }

        /// <summary>
        /// Commits the clipboard subtree to the pending selection callback.
        /// </summary>
        /// <param name="callback">The callback for the pasted root node.</param>
        internal void PasteSubTree(SelectNodeEvent callback)
        {
            var nodes = clipboard.Content;
            tree.AddRange(nodes);
            var root = nodes[0];
            CompleteNodeSelection(root, callback);
        }

        /// <summary>
        /// Tests whether the current clipboard can be offered by a selection dropdown.
        /// </summary>
        /// <param name="context">The node catalogue to display.</param>
        /// <returns>True when a compatible clipboard item exists.</returns>
        internal bool CanPasteForSelection(NodeSelectionContext context)
        {
            return context != NodeSelectionContext.Services
                && clipboard.HasContent
                && !clipboard.TypeMatch(typeof(Service));
        }

        /// <summary>
        /// Selects an existing node while preserving the legacy re-parent confirmation behavior.
        /// </summary>
        /// <param name="node">The existing node selected by the dropdown.</param>
        /// <param name="callback">The callback for the selected node.</param>
        /// <param name="rawReference">Whether to skip re-parenting.</param>
        /// <returns>True when the selection was committed.</returns>
        internal bool TrySelectExistingNode(TreeNode node, SelectNodeEvent callback, bool rawReference)
        {
            if (node == null || tree == null)
            {
                return false;
            }

            TreeNode parent = tree.GetParent(node);
            if (parent != null && !rawReference)
            {
                if (!EditorUtility.DisplayDialog(
                    "Node has a parent already",
                    $"This Node is connecting to {parent.name}, move {(SelectedNode != null ? "under " + SelectedNode.name : "")}?",
                    "OK",
                    "Cancel"))
                {
                    return false;
                }

                RemoveFromParent(parent, node);
            }

            CompleteNodeSelection(node, callback);
            return true;
        }

        private TreeNode CreateNode(Type nodeType)
        {
            if (nodeType.IsSubclassOf(typeof(TreeNode)))
            {
                TreeNode node = NodeFactory.Create(nodeType);
                tree.Add(node);
                node.name = tree.GenerateNewNodeName(MenuCache.GetDisplayName(nodeType));
                editorWindow.Refresh();
                SelectNode(node);
                return node;
            }
            throw new ArgumentException($"Type {nodeType} is not a valid type of node");
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
                OpenNodeSelectionDropdown(NodeSelectionContext.Nodes,
                (node) =>
                {
                    SelectNode(node);
                    TrySetHeadNode(SelectedNode);
                });
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

            TreeNode upgradedNode;
            try
            {
                upgradedNode = node.Upgrade();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }

            if (upgradedNode == null)
            {
                EditorUtility.DisplayDialog("Upgrade Failed", $"Upgrade returned no result for node {node.name}.", "OK");
                return false;
            }

            int index = tree.nodes.IndexOf(node);
            if (index < 0)
            {
                return false;
            }

            Undo.RecordObject(tree, $"Upgrade node {node.name}");

            upgradedNode.UUID = node.UUID;
            upgradedNode.name = node.name;
            upgradedNode.parent = node.parent;
            // Preserve hosted services only when both old and upgraded node can host them.
            if (ServiceHostNodeUtility.TryAsServiceHost(node, out var oldHost)
                && ServiceHostNodeUtility.TryAsServiceHost(upgradedNode, out var upgradedHost)
                && oldHost.Services != null
                && oldHost.Services.Count > 0)
            {
                var upgradedServices = upgradedHost.EnsureServices();
                if (upgradedServices.Count == 0)
                {
                    upgradedServices.AddRange(oldHost.Services);
                }
            }

            tree.nodes[index] = upgradedNode;
            tree.RegenerateTable();
            EditorUtility.SetDirty(tree);

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

            Undo.RecordObject(tree, "Set tree Head");
            tree.headNodeUUID = nextUUID;
            EditorUtility.SetDirty(tree);
            tree.SerializedObject.Update();
            editorWindow.Refresh();
            return true;
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
