using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
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
        private const float RightWindowMinWidth = 180f;
        private const float RightWindowMaxWidth = 600f;

        private TreeNode selectedNode;
        private TreeNode selectedNodeParent;

        public NodeDrawHandler nodeDrawer;
        public SerializedProperty nodeRawDrawingProperty;

        public bool overviewWindowOpen = true;
        public List<OverviewEntry> overviewCache;
        public bool isRawReferenceSelect;

        public RightWindow rightWindow;
        public SelectNodeEvent selectEvent;

        public Vector2 middleScrollPos;
        public Vector2 leftScrollPos;
        public Vector2 rightWindowScrollPos;

        public Mode mode;
        EditorHeadNode editorHeadNode;

        private TreeViewState overviewTreeViewState;
        private BehaviourTreeOverviewTreeView overviewTreeView;

        [SerializeField] private float leftPaneWidth = 260f;
        [SerializeField] private float rightPaneWidth = 220f;
        [NonSerialized] private bool resizingLeftPane;
        [NonSerialized] private bool resizingRightPane;
        [NonSerialized] private float resizeStartMouseX;
        [NonSerialized] private float resizeStartWidth;

        public Clipboard clipboard => editorWindow.clipboard;
        public bool overviewShowService { get => EditorSetting.overviewShowService; set => EditorSetting.overviewShowService = value; }
        internal new TreeNode SelectedNode { get => selectedNode; }
        internal new TreeNode SelectedNodeParent => selectedNodeParent ??= (selectedNode == null ? null : tree.GetParent(selectedNode));
        internal EditorHeadNode EditorHeadNode => editorHeadNode ??= new();



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

                // Left
                if (overviewWindowOpen)
                {
                    using (new GUILayout.VerticalScope(GUILayout.Width(leftPaneWidth)))
                    {
                        DrawOverview();
                    }
                    DrawVerticalSplitter(ref resizingLeftPane, ref leftPaneWidth, LeftWindowMinWidth, LeftWindowMaxWidth, false);
                }

                // Middle 
                using (new GUILayout.VerticalScope(GUILayout.Width(position.width - (overviewWindowOpen ? leftPaneWidth : 0) - rightPaneWidth - 10)))
                {
                    DrawHeader(SelectedNode);

                    if (SelectedNode is EditorHeadNode)
                    {
                        DrawTreeHead();
                    }
                    else if (SelectedNode is null || !tree.nodes.Contains(SelectedNode))
                    {
                        TreeNode head = tree.Head;
                        if (head != null) SelectNode(head);
                        else CreateHeadNode();
                    }
                    else if (SelectedNode != null && tree.nodes.Contains(SelectedNode))
                    {
                        DrawSelectedNode(SelectedNode);
                    }
                }

                // Right
                if (rightWindow != RightWindow.None)
                {
                    DrawVerticalSplitter(ref resizingRightPane, ref rightPaneWidth, RightWindowMinWidth, RightWindowMaxWidth, true);
                    using (new GUILayout.VerticalScope(GUILayout.Width(rightPaneWidth)))
                    {
                        DrawNodeTypeSelectionWindow();
                    }
                }
                else
                {
                    using (new GUILayout.VerticalScope(GUILayout.Width(rightPaneWidth)))
                    {
                        DrawNodeTypeSelectionPlaceHolderWindow();
                    }
                }
            }
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

            TryDeleteNode_OpenParent(node);
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
            TryDeleteNode_OpenParent(node);
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

            TryDeleteNode_OpenParent(node);
            return true;
        }

        private void TryDeleteNode_OpenParent(TreeNode node)
        {
            var parent = tree.GetNode(node.parent);
            if (parent != null)
            {
                RemoveFromParent(parent, node);
                SelectNode(parent);
            }
            else
            {
                SelectNode(tree.Head);
            }
        }

        /// <summary>
        /// Select node in the window
        /// </summary>
        /// <param name="node"></param>
        public void SelectNode(TreeNode node)
        {
            // use this line to magically remove the focus the line
            GUI.FocusControl(null);
            rightWindow = RightWindow.None;
            selectedNode = node;
            if (node is not null) selectedNodeParent = selectedNode != null ? tree.GetParent(selectedNode) : null;
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

        private void DrawTreeHead()
        {
            SelectNodeEvent selectEvent = (n) => tree.headNodeUUID = n?.uuid ?? UUID.Empty;
            TreeNode head = tree.Head;
            string nodeName = head?.name ?? string.Empty;

            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Head: " + nodeName);
                }

                using (EditorGUIIndent.Increase)
                {
                    if (head is null)
                    {
                        if (GUILayout.Button("Select.."))
                            OpenSelectionWindow(RightWindow.All, selectEvent);
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
                                OpenSelectionWindow(RightWindow.All, selectEvent);
                            }
                            else if (GUILayout.Button("Delete"))
                            {
                                tree.headNodeUUID = UUID.Empty;
                            }
                        }

                        using (EditorGUIIndent.Increase)
                        using (new GUILayout.VerticalScope())
                        {
                            var script = MonoScriptCache.Get(head.GetType());
                            using (GUIEnable.By(false))
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

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Delete"), false, () => TryDeleteNode(node));
            menu.AddItem(new GUIContent("Delete Subtree"), false, () => TryDeleteSubTree(node));

            menu.AddSeparator("");
            if (EditorSetting.debugMode) menu.AddItem(new GUIContent("Copy Serialized Data"), false, () => GUIUtility.systemCopyBuffer = JsonUtility.ToJson(node));
            menu.AddItem(new GUIContent("Copy Subtree"), false, () => WriteClipboard(node));
            menu.AddItem(new GUIContent("Copy"), false, () => WriteClipboardSingle(node));
            AddPasteOptions(node, menu);

            menu.AddSeparator("");
            node.AddContent(menu, tree);
        }

        private void AddPasteOptions(TreeNode node, GenericMenu menu)
        {
            menu.AddSeparator("");

            if (CanDuplicate(node)) menu.AddItem(new GUIContent("Duplicate"), false, () => Duplicate(node));
            else menu.AddDisabledItem(new GUIContent("Duplicate"));
            if (clipboard.HasContent && clipboard.TypeMatch(node)) menu.AddItem(new GUIContent($"Paste Value"), false, () => clipboard.PasteValue(node));
            else menu.AddDisabledItem(new GUIContent("Paste Value"));

            var slots = node.ToReferenceSlots();
            // --- Paste to single reference slots
            var singleSlots = slots.OfType<INodeReferenceSingleSlot>().ToList();
            foreach (var slot in singleSlots)
            {
                // is parent info
                if (slot.Name == nameof(TreeNode.parent)) continue;
                string text = $"Paste as {slot.Name.ToTitleCase()}";
                if (clipboard.HasContent) menu.AddItem(new GUIContent(text), false, () => clipboard.PasteTo(tree, node, slot));
                else menu.AddDisabledItem(new GUIContent(text));
            }

            // --- Paste to list slots
            var listSlots = slots.OfType<INodeReferenceListSlot>().ToList();

            if (clipboard.HasContent && clipboard.Root is not Service && listSlots.Count > 0)
            {
                if (listSlots.Count == 1)
                {
                    var slot = listSlots[0];
                    menu.AddItem(new GUIContent($"Paste Under (at first)"), false, () => clipboard.PasteAt(tree, node, slot, 0));
                    menu.AddItem(new GUIContent($"Paste Under (at last)"), false, () => clipboard.PasteAt(tree, node, slot, slot.Count));
                }
                else
                {
                    menu.AddItem(new GUIContent("Paste Under (at first)..."), false, () => ShowPasteUnderMenu(node, listSlots, atFirst: true));
                    menu.AddItem(new GUIContent("Paste Under (at last)..."), false, () => ShowPasteUnderMenu(node, listSlots, atFirst: false));
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste Under (at first)"));
                menu.AddDisabledItem(new GUIContent("Paste Under (at last)"));
            }

            // --- Paste Before/After
            bool hasBeforeAfter = false;
            TreeNode parentNode = tree.GetParent(node);
            if (clipboard.HasContent && clipboard.Root is not Service && parentNode != null)
            {
                var parentSlots = parentNode.ToReferenceSlots();
                for (int i = 0; i < parentSlots.Count; i++)
                {
                    if (parentSlots[i] is not INodeReferenceListSlot parentListSlot)
                    {
                        continue;
                    }

                    int idx = parentListSlot.IndexOf(node);
                    if (idx < 0)
                    {
                        continue;
                    }

                    menu.AddItem(new GUIContent("Paste Before"), false, () => clipboard.PasteAt(tree, parentNode, parentListSlot, idx));
                    menu.AddItem(new GUIContent("Paste After"), false, () => clipboard.PasteAt(tree, parentNode, parentListSlot, idx + 1));
                    hasBeforeAfter = true;
                    break;
                }
            }

            if (!hasBeforeAfter)
            {
                menu.AddDisabledItem(new GUIContent("Paste Before"));
                menu.AddDisabledItem(new GUIContent("Paste After"));
            }

            void ShowPasteUnderMenu(TreeNode owner, List<INodeReferenceListSlot> candidates, bool atFirst)
            {
                GenericMenu slotMenu = new();
                for (int i = 0; i < candidates.Count; i++)
                {
                    var slot = candidates[i];
                    string label = atFirst ? $"First/{slot.Name}" : $"Last/{slot.Name}";
                    slotMenu.AddItem(new GUIContent(label), false, () =>
                    {
                        int index = atFirst ? 0 : slot.Count;
                        clipboard.PasteAt(tree, owner, slot, index);
                    });
                }
                slotMenu.ShowAsContext();
            }
        }


        #region Left Window  
        /// <summary>
        /// Draw Overview window
        /// </summary>
        /// <summary>
        /// Draw Overview window
        /// </summary>
        private void DrawOverview()
        {
            EditorGUILayout.LabelField("Tree Overview", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var global = new GUIContent("Global tree") { tooltip = "Display the entire behaviour tree" };
                var local = new GUIContent("Local tree") { tooltip = "Show only the local tree of selected node" };

                var newMode = (Mode)GUILayout.Toolbar((int)mode, new GUIContent[] { global, local }, EditorStyles.toolbarButton);
                if (newMode != mode)
                {
                    mode = newMode;
                }

                bool newShowService = GUILayout.Toggle(overviewShowService, "Service", EditorStyles.toolbarButton, GUILayout.Width(60));
                if (newShowService != overviewShowService)
                {
                    overviewShowService = newShowService;
                }
            }

            EnsureOverviewTreeView();

            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(200)
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

        #endregion




        private void SetMiddleWindowColorAndBeginVerticle()
        {
            var colorStyle = new GUIStyle();
            colorStyle.normal.background = Texture2D.whiteTexture;
            var baseColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(64 / 255f, 64 / 255f, 64 / 255f);
            GUILayout.BeginVertical(colorStyle, GUILayout.MinHeight(position.height - 150));
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
                    nodeDrawer.Draw();
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
            GUILayout.BeginVertical();
            GUILayout.Space(10);
            GUILayout.Label("Service");
            treeNode.services ??= new List<NodeReference>();
            if (treeNode.services.Count == 0)
            {
                GUILayout.Label("No service");
            }
            else
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < treeNode.services.Count; i++)
                {
                    if (tree.GetNode(treeNode.services[i]) is not Service item)
                    {
                        var currentColor = GUI.contentColor;
                        GUI.contentColor = Color.red;
                        GUILayout.Label("Node not found: " + treeNode.services[i]);
                        GUI.contentColor = currentColor;
                        if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
                        {
                            treeNode.services.RemoveAt(i);
                            i--;
                        }
                        GUILayout.EndHorizontal();
                        continue;
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(18);
                    if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
                    {
                        treeNode.services.RemoveAt(i);
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
                        treeNode.services.RemoveAt(i);
                        treeNode.services.Insert(i - 1, item);
                    }
                    GUI.enabled = formerGUIStatus;
                    if (i == treeNode.services.Count - 1)
                        GUI.enabled = false;
                    if (GUILayout.Button("v", GUILayout.MaxWidth(18)))
                    {
                        treeNode.services.RemoveAt(i);
                        treeNode.services.Insert(i + 1, item);
                    }
                    GUI.enabled = formerGUIStatus;
                    GUILayout.Label(item.GetType().Name);
                    if (GUILayout.Button("Open"))
                    {
                        SelectNode(item);
                    }
                    GUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }
            if (GUILayout.Button("Add"))
            {
                OpenSelectionWindow(
                    RightWindow.Services,
                    (e) =>
                    {
                        treeNode.AddService(e as Service);
                        e.parent = treeNode;
                    }
                );
            }
            GUILayout.EndVertical();
        }




        #region Right window

        [SerializeField] bool hideNewNodeOptions;
        [SerializeField] bool hideExistsNodeOptions;
        [SerializeField] bool hideReachableNodeOptions;
        [SerializeField] bool hideNonreachableNodeOptions;
        string rightWindowInputFilter;
        SearchField rightWindowSearchField;
        string[] rightWindowSearchTokens = Array.Empty<string>();
        private readonly Stack<NodeMenuPathFolder> menuPathFolderStack = new();

        static GUIStyle RightWindowNodeButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };

        /// <summary>
        /// Gets the shared node menu cache.
        /// </summary>
        /// <returns>The shared cache.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private static NodeMenuCache MenuCache => NodeMenuCache.Shared;

        /// <summary>
        /// Gets the current menu path folder.
        /// </summary>
        /// <returns>The current menu path folder.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private NodeMenuPathFolder CurrentMenuPathFolder => menuPathFolderStack.Count == 0 ? MenuCache.MenuPathRoot : menuPathFolderStack.Peek();


        /// <summary>
        /// draw node selection window (right)
        /// </summary>
        private void DrawNodeTypeSelectionWindow()
        {
            DrawRightWindowSearchBar();

            using (var scrollScope = new GUILayout.ScrollViewScope(rightWindowScrollPos, GUILayout.ExpandWidth(true)))
            using (new GUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                rightWindowScrollPos = scrollScope.scrollPosition;

                switch (rightWindow)
                {
                    case RightWindow.MenuPaths:
                        DrawMenuPathSelectionWindow(() => rightWindow = RightWindow.All);
                        break;
                    case RightWindow.All:
                        DrawNodeSelectionWindow();
                        break;
                    case RightWindow.Composite:
                        DrawTypeSelectionWindow(typeof(Flow), () => rightWindow = RightWindow.All);
                        break;
                    case RightWindow.Actions:
                        DrawTypeSelectionWindow(typeof(Nodes.Action), () => rightWindow = RightWindow.All);
                        break;
                    case RightWindow.Determines:
                        DrawTypeSelectionWindow(typeof(DetermineBase), () => rightWindow = RightWindow.All);
                        break;
                    case RightWindow.Calls:
                        DrawTypeSelectionWindow(typeof(Call), () => rightWindow = RightWindow.All);
                        break;
                    case RightWindow.Arithmetic:
                        DrawTypeSelectionWindow(typeof(Arithmetic), () => rightWindow = RightWindow.All);
                        break;
                    case RightWindow.Unity:
                        DrawTypeSelectionUnityWindow();
                        break;
                    case RightWindow.Services:
                        DrawTypeSelectionWindow(typeof(Service), () => rightWindow = RightWindow.None);
                        break;
                }
            }

            GUILayout.Space(20);
            if (GUILayout.Button("Close"))
            {
                rightWindow = RightWindow.None;
            }
            GUILayout.Space(20);
        }

        /// <summary>
        /// Draw the search bar for filtering nodes in the right pane.
        /// </summary>
        /// <param name="rightWindowInputFilter">Unused.</param>
        /// <returns>None.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private void DrawRightWindowSearchBar()
        {
            rightWindowSearchField ??= new SearchField();

            if ((rightWindowSearchTokens == null || rightWindowSearchTokens.Length == 0) && !string.IsNullOrWhiteSpace(rightWindowInputFilter))
            {
                UpdateRightWindowSearchTokens();
            }

            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Search Nodes", EditorStyles.boldLabel);
                using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    string newFilter = rightWindowSearchField.OnToolbarGUI(rightWindowInputFilter ?? string.Empty);
                    if (!string.Equals(newFilter, rightWindowInputFilter, StringComparison.Ordinal))
                    {
                        rightWindowInputFilter = newFilter;
                        UpdateRightWindowSearchTokens();
                    }
                }
            }
        }

        /// <summary>
        /// Update cached search tokens for keyword matching.
        /// </summary>
        /// <param name="rightWindowInputFilter">Unused.</param>
        /// <returns>None.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private void UpdateRightWindowSearchTokens()
        {
            if (string.IsNullOrWhiteSpace(rightWindowInputFilter))
            {
                rightWindowSearchTokens = Array.Empty<string>();
                return;
            }

            rightWindowSearchTokens = rightWindowInputFilter
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Test whether the provided value matches all search tokens.
        /// </summary>
        /// <param name="value">The value to test against the current tokens.</param>
        /// <returns>True if all tokens match; otherwise false.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private bool MatchesSearchTokens(string value)
        {
            if (rightWindowSearchTokens == null || rightWindowSearchTokens.Length == 0)
            {
                return true;
            }

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (var token in rightWindowSearchTokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Draw a boxed section in the right pane.
        /// </summary>
        /// <param name="title">The section title.</param>
        /// <param name="drawContent">The delegate that renders the section body.</param>
        /// <returns>None.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private void DrawRightWindowSection(string title, System.Action drawContent)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                drawContent?.Invoke();
            }
        }

        /// <summary>
        /// draw node selection window
        /// </summary>
        private void DrawNodeSelectionWindow()
        {
            if (!isRawReferenceSelect)
            {
                DrawRightWindowSection("Add New Nodes", DrawNewNodeSelectionSection);
                GUILayout.Space(10);
            }

            DrawRightWindowSection("Select Existing Nodes", DrawExistingNodeSelectionSection);
        }

        /// <summary>
        /// Draw the "Add New Nodes" section in the right pane.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private void DrawNewNodeSelectionSection()
        {
            bool noFilter = rightWindowSearchTokens == null || rightWindowSearchTokens.Length == 0;
            // should not allow create service here
            if (noFilter && clipboard.HasContent && !clipboard.TypeMatch(typeof(Service)))
            {
                GUILayout.Label("Clipboard");
                if (SelectEvent_TryPaste())
                    return;
                GUILayout.Space(16);
            }

            if (noFilter)
            {
                DrawCreateNewNodeWindow();
                return;
            }

            DrawAllNodeTypeWithMatchesName();
            GUILayout.Space(16);
        }

        /// <summary>
        /// Draw the "Select Existing Nodes" section in the right pane.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private void DrawExistingNodeSelectionSection()
        {
            DrawExistNodeSelectionWindow(typeof(TreeNode));
        }

        /// <summary>
        /// Draw all node types matching the current keyword search.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private void DrawAllNodeTypeWithMatchesName()
        {
            var classes = MenuCache.AllNodeTypes;
            foreach (var type in classes)
            {
                if (type.IsAbstract) continue;
                if (type.IsSubclassOf(typeof(Service))) continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;

                string displayName = GetSearchDisplayName(type);
                if (!MatchesSearchTokens(displayName)) continue;

                var content = new GUIContent(displayName);
                AddGUIContentAttributes(type, content);
                if (GUILayout.Button(content))
                {
                    var n = CreateNode(type);
                    selectEvent?.Invoke(n);
                    tree.SerializedObject.Update();

                    rightWindow = RightWindow.None;
                }
            }
            GUILayout.Space(16);
        }

        private static void AddGUIContentAttributes(Type type, GUIContent content)
        {
            content.tooltip = MenuCache.GetTooltip(type);
            content.text = MenuCache.GetDisplayName(type);
        }

        /// <summary>
        /// Get the display name used for search matching.
        /// </summary>
        /// <param name="type">The node type.</param>
        /// <returns>The display name for the given node type.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private static string GetSearchDisplayName(Type type)
        {
            return MenuCache.GetDisplayName(type);
        }

        private void DrawExistNodeSelectionWindow(Type type)
        {
            var nodes = tree.EditorNodes.Where(n => n.GetType().IsSubclassOf(type)).OrderBy(n => n.name);
            if (nodes.Count() == 0) return;

            hideExistsNodeOptions = !EditorGUILayout.Foldout(!hideExistsNodeOptions, "Exist Nodes...");
            if (hideExistsNodeOptions) return;

            hideReachableNodeOptions = !EditorGUILayout.Foldout(!hideReachableNodeOptions, "Reachables...");
            if (!hideReachableNodeOptions)
                DrawList(type, nodes.Where(node => ReachableNodes.Contains(node)));

            hideNonreachableNodeOptions = !EditorGUILayout.Foldout(!hideNonreachableNodeOptions, "Nonreachables...");
            if (!hideNonreachableNodeOptions)
                DrawList(type, nodes.Where(node => !ReachableNodes.Contains(node)));

            void SelectNode(TreeNode node)
            {
                if (selectEvent == null)
                    Debug.LogWarning("No event exist");

                selectEvent?.Invoke(node);
                tree.SerializedObject.Update();

                rightWindow = RightWindow.None;
                isRawReferenceSelect = false;
            }

            void DrawList(Type type, IEnumerable<TreeNode> nodes)
            {
                foreach (var node in nodes)
                {
                    //not a valid type
                    if (!node.GetType().IsSubclassOf(type)) continue;
                    //select for service but the node is not allowed to appear in a service
                    //if (selectedService != null && Attribute.GetCustomAttribute(node.GetType(), typeof(AllowServiceCallAttribute)) == null) continue;
                    //filter
                    if (!MatchesSearchTokens(node.name)) continue;
                    // do not show service as existing node
                    if (node is Service) continue;

                    if (GUILayout.Button(node.name, RightWindowNodeButtonStyle))
                    {
                        TreeNode parent = tree.GetParent(node);
                        if (parent != null && !isRawReferenceSelect)
                        {
                            if (EditorUtility.DisplayDialog($"Node has a parent already", $"This Node is connecting to {parent.name}, move {(SelectedNode != null ? "under" + SelectedNode.name : "")} ?", "OK", "Cancel"))
                            {
                                var originParent = tree.GetParent(node);
                                if (originParent is not null)
                                    RemoveFromParent(originParent, node);
                                SelectNode(node);
                            }
                        }
                        else
                        {
                            SelectNode(node);
                        }
                    }
                }
            }
        }

        private void DrawTypeSelectionUnityWindow()
        {
            var classes = new Type[]
            {
                typeof(ComponentAction),
                typeof(ComponentCall),
                null,
                typeof(CallStatic),
                typeof(CallGameObject),
                null,
                typeof(GetComponentValue),
                typeof(SetComponentValue),
                typeof(GetObjectValue),
                typeof(SetObjectValue),
                null,
                typeof(GetComponent),
            };

            DrawRightWindowSection("Unity", () =>
            {
                foreach (var type in classes)
                {
                    if (type == null)
                    {
                        GUILayout.Space(EditorGUIUtility.singleLineHeight);
                        continue;
                    }
                    if (type.IsAbstract)
                        continue;
                    if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute)))
                        continue;
                    // filter
                    string displayName = GetSearchDisplayName(type);
                    if (!MatchesSearchTokens(displayName))
                        continue;
                    // set node tip
                    var content = new GUIContent(displayName);
                    AddGUIContentAttributes(type, content);
                    if (GUILayout.Button(content, RightWindowNodeButtonStyle))
                        SelectEvent_CreateAndSelect(type);
                }
                GUILayout.Space(16);
                if (GUILayout.Button("Back"))
                {
                    rightWindow = RightWindow.All;
                    return;
                }
            });
        }

        private void DrawTypeSelectionWindow(Type parentType, System.Action typeWindowCloseFunc)
        {
            if (clipboard.HasContent && clipboard.TypeMatch(parentType))
            {
                GUILayout.Label("Clipboard");
                if (SelectEvent_TryPaste())
                    return;
            }

            DrawRightWindowSection(parentType.Name.ToTitleCase(), () =>
            {
                var classes = MenuCache.GetDerivedTypes(parentType);
                foreach (var type in classes)
                {
                    if (type.IsAbstract) continue;
                    if (parentType != typeof(Service) && type.IsSubclassOf(typeof(Service))) continue;
                    if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
                    if (SelectedNode is Service && Attribute.IsDefined(type, typeof(DisableServiceCallAttribute))) continue;

                    string displayName = GetSearchDisplayName(type);
                    if (!MatchesSearchTokens(displayName)) continue;

                    var content = new GUIContent(displayName);
                    AddGUIContentAttributes(type, content);
                    if (GUILayout.Button(content, RightWindowNodeButtonStyle))
                    {
                        SelectEvent_CreateAndSelect(type);
                        rightWindow = RightWindow.None;
                    }
                }
                GUILayout.Space(16);
                if (GUILayout.Button("Back"))
                {
                    typeWindowCloseFunc?.Invoke();
                }
            });
        }

        /// <summary>
        /// Open node selection window
        /// </summary>
        /// <param name="window">window type</param>
        /// <param name="e"></param>
        /// <param name="isRawSelect">true for only selecting node, but changing structure of the Tree </param>
        public void OpenSelectionWindow(RightWindow window, SelectNodeEvent e, bool isRawSelect = false)
        {
            rightWindow = window;
            selectEvent = e;
            isRawReferenceSelect = isRawSelect;
            UpdateRightWindowSearchTokens();
        }

        private void DrawCreateNewNodeWindow()
        {
            hideNewNodeOptions = !EditorGUILayout.Foldout(!hideNewNodeOptions, "New...");
            if (hideNewNodeOptions) return;

            if (SelectCommonNodeType(out Type value))
            {
                SelectEvent_CreateAndSelect(value);
                return;
            }

            if (HasVisibleMenuPathEntries(MenuCache.MenuPathRoot))
            {
                if (GUILayout.Button(new GUIContent("Menu Paths...", "Custom menu paths for nodes")))
                {
                    OpenMenuPathWindow();
                    return;
                }

                GUILayout.Space(EditorGUIUtility.singleLineHeight);
            }

            GUILayout.Label("Logics");
            rightWindow = !GUILayout.Button(new GUIContent("Composites...", "Flow control nodes in AI"))
                ? rightWindow
                : RightWindow.Composite;

            GUILayout.Space(EditorGUIUtility.singleLineHeight);

            rightWindow = !GUILayout.Button(new GUIContent("Determine...", "A type of nodes that return true/false by determine conditions given"))
                ? rightWindow
                : RightWindow.Determines;

            rightWindow = !GUILayout.Button(new GUIContent("Arithmetic...", "A type of nodes that do basic algorithm"))
                ? rightWindow
                : RightWindow.Arithmetic;

            GUILayout.Label("Calls");
            rightWindow = !GUILayout.Button(new GUIContent("Calls...", "A type of nodes that calls certain methods"))
                ? rightWindow
                : RightWindow.Calls;

            GUILayout.Label("Actions");
            rightWindow = !GUILayout.Button(new GUIContent("Actions...", "A type of nodes that perform certain actions"))
                ? rightWindow
                : RightWindow.Actions;

            GUILayout.Label("Unity");
            rightWindow = !GUILayout.Button(new GUIContent("Unity...", "Calls and action related to Unity"))
                ? rightWindow
                : RightWindow.Unity;
        }

        /// <summary>
        /// Open the menu path window and reset navigation.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void OpenMenuPathWindow()
        {
            menuPathFolderStack.Clear();
            rightWindow = RightWindow.MenuPaths;
        }

        /// <summary>
        /// Draw the custom menu path window with nested navigation.
        /// </summary>
        /// <param name="closeWindow">The action used to close the menu path window.</param>
        /// <returns>None.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private void DrawMenuPathSelectionWindow(System.Action closeWindow)
        {
            var rootFolder = MenuCache.MenuPathRoot;
            if (!HasVisibleMenuPathEntries(rootFolder))
            {
                DrawRightWindowSection("Menu", () =>
                {
                    EditorGUILayout.LabelField("No menu path entries.");
                    if (GUILayout.Button("Back"))
                    {
                        closeWindow?.Invoke();
                    }
                });

                return;
            }

            var currentFolder = CurrentMenuPathFolder;
            string title = GetMenuPathTitle();

            DrawRightWindowSection(title, () =>
            {
                if (DrawMenuPathTypes(currentFolder))
                {
                    return;
                }

                if (DrawMenuPathFolders(currentFolder))
                {
                    return;
                }

                GUILayout.Space(16);
                if (GUILayout.Button("Back"))
                {
                    NavigateMenuPathBack(closeWindow);
                }
            });
        }

        /// <summary>
        /// Draw node type buttons for the current menu path folder.
        /// </summary>
        /// <param name="folder">The menu path folder to draw.</param>
        /// <returns>True if a node was selected; otherwise false.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private bool DrawMenuPathTypes(NodeMenuPathFolder folder)
        {
            if (folder == null || folder.Types.Count == 0)
            {
                return false;
            }

            foreach (var type in folder.Types)
            {
                if (!ShouldShowMenuPathType(type))
                {
                    continue;
                }

                string displayName = GetSearchDisplayName(type);
                if (!MatchesSearchTokens(displayName))
                {
                    continue;
                }

                var content = MenuCache.GetContent(type);
                if (GUILayout.Button(content, RightWindowNodeButtonStyle))
                {
                    SelectEvent_CreateAndSelect(type);
                    rightWindow = RightWindow.None;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Draw nested folder buttons for the current menu path folder.
        /// </summary>
        /// <param name="folder">The menu path folder to draw.</param>
        /// <returns>True if a folder was opened; otherwise false.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private bool DrawMenuPathFolders(NodeMenuPathFolder folder)
        {
            if (folder == null)
            {
                return false;
            }

            foreach (var child in folder.Children.Values)
            {
                if (!HasVisibleMenuPathEntries(child))
                {
                    continue;
                }

                string label = $"{child.Name}...";
                if (GUILayout.Button(label, RightWindowNodeButtonStyle))
                {
                    menuPathFolderStack.Push(child);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Navigate back to the previous menu path folder or close the window.
        /// </summary>
        /// <param name="closeWindow">The action used to close the menu path window.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void NavigateMenuPathBack(System.Action closeWindow)
        {
            if (menuPathFolderStack.Count > 0)
            {
                menuPathFolderStack.Pop();
                return;
            }

            closeWindow?.Invoke();
        }

        /// <summary>
        /// Build the current menu path title for display.
        /// </summary>
        /// <returns>The formatted menu path title.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private string GetMenuPathTitle()
        {
            if (menuPathFolderStack.Count == 0)
            {
                return "Menu Paths";
            }

            string path = string.Join("/", menuPathFolderStack.Reverse().Select(folder => folder.Name));
            return $"Menu Paths / {path}";
        }

        /// <summary>
        /// Determine whether a menu path folder has any visible node entries.
        /// </summary>
        /// <param name="folder">The folder to inspect.</param>
        /// <returns>True if the folder has visible entries; otherwise false.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when exiting GUI event processing.</exception>
        private bool HasVisibleMenuPathEntries(NodeMenuPathFolder folder)
        {
            if (folder == null)
            {
                return false;
            }

            foreach (var type in folder.Types)
            {
                if (!ShouldShowMenuPathType(type))
                {
                    continue;
                }

                string displayName = GetSearchDisplayName(type);
                if (MatchesSearchTokens(displayName))
                {
                    return true;
                }
            }

            foreach (var child in folder.Children.Values)
            {
                if (HasVisibleMenuPathEntries(child))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Check whether a node type should be shown in the menu path section.
        /// </summary>
        /// <param name="type">The node type to test.</param>
        /// <returns>True if the node type is eligible; otherwise false.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private bool ShouldShowMenuPathType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsSubclassOf(typeof(Service)))
            {
                return false;
            }

            if (SelectedNode is Service && Attribute.IsDefined(type, typeof(DisableServiceCallAttribute)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// remove the node from parent's reference
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="child"></param>
        private void RemoveFromParent(TreeNode parent, TreeNode child)
        {
            UUID uuid = child.uuid;

            var fields = parent.GetType().GetFields();
            foreach (var item in fields)
            {
                if (item.FieldType == typeof(NodeReference))
                {
                    INodeReference nodeReference = (NodeReference)item.GetValue(parent);
                    if (nodeReference.UUID == uuid)
                    {
                        nodeReference.Set(null);
                        //Debug.Log("Removed");
                    }
                }
                else if (item.FieldType == typeof(List<Probability.EventWeight>))
                {
                    List<Probability.EventWeight> nodeReferences =
                        (List<Probability.EventWeight>)item.GetValue(parent);
                    int count = nodeReferences.RemoveAll(r => r.reference.UUID == uuid);
                    //Debug.Log("Removed " + count);
                }
                else if (item.FieldType == typeof(List<PseudoProbability.EventWeight>))
                {
                    List<PseudoProbability.EventWeight> nodeReferences =
                        (List<PseudoProbability.EventWeight>)item.GetValue(parent);
                    int count = nodeReferences.RemoveAll(r => r.reference.UUID == uuid);
                    //Debug.Log("Removed " + count);
                }
                else if (item.FieldType == typeof(List<NodeReference>))
                {
                    List<NodeReference> nodeReferences = (List<NodeReference>)item.GetValue(parent);
                    int count = nodeReferences.RemoveAll(r => r.UUID == uuid);
                    //Debug.Log("Removed " + count);
                }
                else if (item.FieldType == typeof(NodeReference[]))
                {
                    var nodeReferences = (NodeReference[])item.GetValue(parent);
                    int index = UnityEditor.ArrayUtility.FindIndex(nodeReferences, r => r.UUID == uuid);
                    if (index >= 0)
                    {
                        UnityEditor.ArrayUtility.RemoveAt(ref nodeReferences, index);
                        item.SetValue(parent, nodeReferences);
                        //Debug.Log("Removed at" + index);
                    }
                }
                else if (item.FieldType == typeof(UUID))
                {
                    if ((UUID)item.GetValue(parent) == uuid)
                        item.SetValue(parent, UUID.Empty);
                    //Debug.Log("Removed");
                }
            }
        }










        /// <summary>
        /// Try execute paste command
        /// </summary>
        /// <param name="clipboardNode"></param>
        /// <returns></returns>
        private bool SelectEvent_TryPaste()
        {
            if (!clipboard.HasContent) return false;
            if (GUILayout.Button($"Paste ({clipboard.Root.name})"))
            {
                SelectEvent_PasteSubTree();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Try execute select
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private void SelectEvent_CreateAndSelect(Type type)
        {
            var node = CreateNode(type);
            SelectEvent_Select(node);
        }

        /// <summary>
        /// Try execute select
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private void SelectEvent_Select(TreeNode node)
        {
            selectEvent?.Invoke(node);
            tree.SerializedObject.Update();
            rightWindow = RightWindow.None;
            editorWindow.Refresh();
            SelectNode(node);
        }

        public void SelectEvent_PasteSubTree()
        {
            var nodes = clipboard.Content;
            tree.AddRange(nodes);
            var root = nodes[0];
            SelectEvent_Select(root);
        }

        //private void DrawTypeSelectionWindow(Type parentType, System.Action typeWindowCloseFunc)
        //{
        //    if (clipboard.HasContent && clipboard.TypeMatch(parentType))
        //    {
        //        GUILayout.Label("Clipboard");
        //        if (SelectEvent_TryPaste())
        //            return;
        //    }

        //    GUILayout.Label(parentType.Name.ToTitleCase());
        //    var classes = TypeCache.GetTypesDerivedFrom(parentType);//NodeFactory.GetSubclassesOf(parentType);
        //    foreach (var type in classes)
        //    {
        //        if (type.IsAbstract) continue;
        //        if (parentType != typeof(Service) && type.IsSubclassOf(typeof(Service))) continue;
        //        if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
        //        if (SelectedNode is Service && Attribute.IsDefined(type, typeof(DisableServiceCallAttribute))) continue;
        //        // filter
        //        if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(type.Name, rightWindowNameFilter).Count == 0) continue;

        //        // set node tip
        //        var content = new GUIContent(type.Name.ToTitleCase());
        //        AddGUIContentAttributes(type, content);
        //        if (GUILayout.Button(content))
        //        {
        //            SelectEvent_CreateAndSelect(type);
        //            rightWindow = RightWindow.None;
        //        }
        //    }
        //    GUILayout.Space(16);
        //    if (GUILayout.Button("Back"))
        //    {
        //        typeWindowCloseFunc?.Invoke();
        //        return;
        //    }
        //}

        private void DrawNodeTypeSelectionPlaceHolderWindow()
        {
            using (var scrollScope = new GUILayout.ScrollViewScope(rightWindowScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar))
            {
                rightWindowScrollPos = scrollScope.scrollPosition;
                rightWindowScrollPos.x = 0;
                EditorGUILayout.LabelField("");
            }
        }

        ///// <summary>
        ///// Open node selection window
        ///// </summary>
        ///// <param name="window">window type</param>
        ///// <param name="e"></param>
        ///// <param name="isRawSelect">true for only selecting node, but changing structure of the Tree </param>
        //public void OpenSelectionWindow(RightWindow window, SelectNodeEvent e, bool isRawSelect = false)
        //{
        //    rightWindow = window;
        //    selectEvent = e;
        //    isRawReferenceSelect = isRawSelect;
        //}

        public bool IsValidRegex(string input)
        {
            try
            {
                if (string.IsNullOrEmpty(input))
                    return false;
                Regex.IsMatch("", input);
                return true;
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        private TreeNode CreateNode(Type nodeType)
        {
            if (nodeType.IsSubclassOf(typeof(TreeNode)))
            {
                TreeNode node = NodeFactory.Create(nodeType);
                tree.Add(node);
                node.name = tree.GenerateNewNodeName(node);
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
                OpenSelectionWindow(RightWindow.All,
                (node) =>
                {
                    SelectNode(node);
                    tree.headNodeUUID = SelectedNode.uuid;
                });
            }
            GUILayout.EndVertical();
        }

        protected bool SelectCompositeNodeType(out Type nodeType)
        {
            var types = MenuCache.GetDerivedTypes(typeof(Flow));
            foreach (var type in types)
            {
                if (type.IsSubclassOf(typeof(Service))) continue;

                GUIContent content = new(type.Name.ToTitleCase());
                AddGUIContentAttributes(type, content);
                if (GUILayout.Button(content))
                {
                    nodeType = type;
                    return true;
                }
            }
            nodeType = null;
            return false;
        }

        protected bool SelectCommonNodeType(out Type nodeType)
        {
            var types = EditorSetting.GetCommonNodeTypes();
            if (types.Length == 0)
            {
                nodeType = null;
                return false;
            }
            GUILayout.Label("Common");
            //var types = NodeFactory.GetSubclassesOf(typeof(Flow));
            foreach (var type in types)
            {
                // do not show service as flow node, although they are.
                // service are only available to service selection.
                if (type.IsSubclassOf(typeof(Service))) continue;

                GUIContent content = new(type.Name.ToTitleCase());
                AddGUIContentAttributes(type, content);
                if (GUILayout.Button(content))
                {
                    nodeType = type;
                    return true;
                }
            }
            nodeType = null;
            return false;
        }









        public void WriteClipboard(TreeNode selectedNode)
        {
            clipboard.Clear();
            clipboard.Write(selectedNode, tree);
        }

        private void WriteClipboardSingle(TreeNode selectedNode)
        {
            clipboard.Clear();
            clipboard.WriteSingle(selectedNode, tree);
        }

        /// <summary>
        /// Duplicate given node is possible
        /// </summary>
        /// <param name="node"></param>
        private bool CanDuplicate(TreeNode node)
        {
            if (node is Service) return true;
            var parent = tree.GetParent(node);
            return parent.GetListSlot() != null;
        }

        /// <summary>
        /// Duplicate given node is possible
        /// </summary>
        /// <param name="node"></param>
        private void Duplicate(TreeNode node)
        {
            Clipboard clipboard = new();
            clipboard.Write(node, tree);

            var parent = tree.GetParent(node);
            List<TreeNode> content = clipboard.Content;
            TreeNode root = content[0];

            // duplicate service
            if (root is Service service)
            {
                tree.AddRange(content);   // must add range first to add undo record
                parent.AddService(service);
                return;
            }
            else if (parent.GetListSlot() is INodeReferenceListSlot listSlot)
            {
                int index = listSlot.IndexOf(node);
                tree.AddRange(content);             // must add range first to add undo record
                listSlot.Insert(index + 1, root);
                root.parent = parent;
            }
            else
            {
                Debug.LogError($"Cannot duplicate node {node.name}");
            }
        }



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
                this.canFold = node is Flow and not Wait and not Constant and not Pause;
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
