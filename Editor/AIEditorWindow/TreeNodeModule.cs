using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    internal class TreeNodeModule : AIEditorWindowModule
    {
        enum Mode
        {
            Global,
            local,
        }

        //private UUID clipboard;
        //private TreeNode clipboardNode;
        private TreeNode selectedNode;
        private TreeNode selectedNodeParent;

        public NodeDrawHandler nodeDrawer;
        public SerializedProperty nodeRawDrawingProperty;

        public bool overviewWindowOpen = true;
        public bool overviewShowService;
        public List<OverviewEntry> overviewCache;
        public bool isRawReferenceSelect;

        public RightWindow rightWindow;
        public SelectNodeEvent selectEvent;

        public Vector2 middleScrollPos;
        public Vector2 leftScrollPos;
        public Vector2 rightWindowScrollPos;

        Mode mode;
        EditorHeadNode editorHeadNode;


        private Clipboard clipboard => editorWindow.clipboard;
        internal new TreeNode SelectedNode { get => selectedNode; }
        internal new TreeNode SelectedNodeParent => selectedNodeParent ??= (selectedNode == null ? null : Tree.GetParent(selectedNode));
        internal EditorHeadNode EditorHeadNode => editorHeadNode ??= new();



        public void DrawTree()
        {
            if (!overviewWindowOpen) overviewWindowOpen = GUILayout.Button("Open Overview");
            if (!Tree)
            {
                DrawNewBTWindow();
                return;
            }
            GUILayout.BeginHorizontal();

            if (Tree.IsInvalid())
            {
                DrawInvalidTreeInfo();
            }
            else
            {
                if (overviewWindowOpen) DrawOverview();

                GUILayout.Space(10);

                if (SelectedNode is EditorHeadNode)
                {
                    DrawTreeHead();
                }
                else if (SelectedNode is null || !Tree.nodes.Contains(SelectedNode))
                {
                    TreeNode head = Tree.Head;
                    if (head != null) SelectNode(head);
                    else CreateHeadNode();
                }
                else if (SelectedNode != null && Tree.nodes.Contains(SelectedNode))
                {
                    DrawSelectedNode(SelectedNode);
                }

                GUILayout.Space(10);

                if (rightWindow != RightWindow.None) DrawNodeTypeSelectionWindow();
                else DrawNodeTypeSelectionPlaceHolderWindow();
            }
            GUILayout.EndHorizontal();
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
                        Tree.RemoveSubTree(node);
                        break;
                    case 1:
                        return false;
                    case 2:
                        Tree.Remove(node);
                        break;
                }
            }
            // has at least one valid child node
            else
            {
                if (!ok && !EditorUtility.DisplayDialog("Deleting Node", $"Delete the node {node.name} ({node.uuid}) ?", "OK", "Cancel"))
                    return false;
                Tree.Remove(node);
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

            Tree.Remove(node);
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

            Tree.RemoveSubTree(node);

            TryDeleteNode_OpenParent(node);
            return true;
        }

        private void TryDeleteNode_OpenParent(TreeNode node)
        {
            var parent = Tree.GetParent(node);
            if (parent != null)
            {
                RemoveFromParent(parent, node);
                SelectNode(parent);
            }
            else
            {
                SelectNode(Tree.Head);
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
            if (node is not null) selectedNodeParent = selectedNode != null ? Tree.GetParent(selectedNode) : null;
        }

        /// <summary>
        /// Select the parent of given node in the window
        /// </summary>
        /// <param name="node"></param>
        public void SelectParentNode(TreeNode node)
        {
            var parent = Tree.GetParent(node) ?? editorHeadNode;
            SelectNode(parent);
        }

        private void DrawTreeHead()
        {
            SelectNodeEvent selectEvent = (n) => Tree.headNodeUUID = n?.uuid ?? UUID.Empty;
            TreeNode head = Tree.Head;
            string nodeName = head?.name ?? string.Empty;
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Head: " + nodeName);
            EditorGUI.indentLevel++;
            if (head is null)
            {
                if (GUILayout.Button("Select.."))
                    OpenSelectionWindow(RightWindow.All, selectEvent);
            }
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.BeginHorizontal(GUILayout.MaxWidth(80));
                GUILayout.Space(EditorGUI.indentLevel * 16);
                GUILayout.BeginVertical(GUILayout.MaxWidth(80));
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
                    Tree.headNodeUUID = UUID.Empty;
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 1;
                GUILayout.BeginVertical();
                var currentStatus = GUI.enabled;
                GUI.enabled = false;
                var script = Resources
                    .FindObjectsOfTypeAll<MonoScript>()
                    .FirstOrDefault(n => n.GetClass() == head.GetType());
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
                GUI.enabled = currentStatus;

                head.name = EditorGUILayout.TextField("Name", head.name);
                EditorGUILayout.LabelField("UUID", head.uuid);

                GUILayout.EndVertical();
                EditorGUI.indentLevel = oldIndent;
            }
            EditorGUI.indentLevel--;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawInvalidTreeInfo()
        {
            GUILayout.Space(10);
            SetMiddleWindowColorAndBeginVerticle();
            EditorGUILayout.LabelField(
                $"Unable to load behaviour tree \"{Tree.name}\", at least 1 null node appears in data."
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
                $"If the problem still occur, you might need to open behaviour tree data file \"{Tree.name}\" data file in Unity Inspector or an text editor to manually fix the issue"
            );
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("==========");
            EditorGUILayout.LabelField($"First Null Index: {Tree.AllNodes.IndexOf(null)}");
            GUILayout.EndVertical();
        }





        /// <summary>
        /// Create the right click menu for a node
        /// </summary>
        /// <param name="node"></param>
        /// <param name="menu"></param>
        private void CreateRightClickMenu(TreeNode node, GenericMenu menu)
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
            node.AddContent(menu, Tree);
        }

        private void AddPasteOptions(TreeNode node, GenericMenu menu)
        {
            menu.AddSeparator("");

            if (CanDuplicate(node)) menu.AddItem(new GUIContent("Duplicate"), false, () => Duplicate(node));
            else menu.AddDisabledItem(new GUIContent("Duplicate"));
            if (clipboard.HasContent && clipboard.TypeMatch(node)) menu.AddItem(new GUIContent($"Paste Value"), false, () => clipboard.PasteValue(node));
            else menu.AddDisabledItem(new GUIContent("Paste Value"));

            foreach (var item in node.GetType().GetFields())
            {
                //is parent info
                if (item.Name == nameof(TreeNode.parent)) continue;
                object v = item.GetValue(node);
                if (v is NodeReference r)
                {
                    string text = $"Paste as {item.Name.ToTitleCase()}";
                    if (clipboard.HasContent) menu.AddItem(new GUIContent(text), false, () => clipboard.PasteTo(Tree, node, r));
                    else menu.AddDisabledItem(new GUIContent(text));
                }
            }
            if (Tree.GetNode(node.parent) is IListFlow flow)
            {
                int index = flow.IndexOf(node);
                if (index != -1)
                {
                    menu.AddItem(new GUIContent("Paste Before"), false, () => clipboard.PasteAt(Tree, flow, index));
                    menu.AddItem(new GUIContent("Paste After"), false, () => clipboard.PasteAt(Tree, flow, index + 1));
                }
            }
            if (node is IListFlow lf)
            {
                if (clipboard.HasContent)
                {
                    menu.AddItem(new GUIContent("Paste Under (at first)"), false, () => clipboard.PasteAsFirst(Tree, lf));
                    menu.AddItem(new GUIContent("Paste Under (at last)"), false, () => clipboard.PasteAsLast(Tree, lf));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Paste Under (at first)"), false);
                    menu.AddDisabledItem(new GUIContent("Paste Under (at last)"), false);
                }
            }
        }


        #region Left Window  
        /// <summary>
        /// Draw Overview window
        /// </summary>
        private void DrawOverview()
        {
            GUILayout.BeginVertical(GUILayout.Width(EditorSetting.overviewWindowSize));

            EditorGUILayout.LabelField("Tree Overview");
            {
                var global = new GUIContent("Global tree") { tooltip = "Display the entire behaviour tree" };
                var local = new GUIContent("Local tree") { tooltip = "Show only the local tree of selected node" };
                var newMode = (Mode)GUILayout.Toolbar((int)mode, new GUIContent[] { global, local });
                if (newMode != mode)
                {
                    overviewCache = null;
                    mode = newMode;
                }
                overviewShowService = overviewShowService ? !GUILayout.Button("Hide Service") : GUILayout.Button("Show Service");
            }

            EditorGUILayout.LabelField("From Head");
            if (Tree.Head != null)
            {
                GUILayout.BeginHorizontal();
                var head = new GUIContent("HEAD") { tooltip = "The entry node" };
                if (GUILayout.Button(head))
                {
                    SelectNode(EditorHeadNode);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }

            leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar);
            leftScrollPos.x = 0;
            GUILayout.BeginVertical(GUILayout.Width(EditorSetting.overviewWindowSize - 20), GUILayout.MinHeight(300));
            EditorGUILayout.LabelField("Tree");
            //List<TreeNode> allNodeFromHead = new();


            // if overview cache is not initialized
            DrawOutline();

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.FlexibleSpace();
            overviewWindowOpen = !GUILayout.Button("Close");
            GUILayout.EndVertical();
        }

        private void DrawUnreachables()
        {
            var unreachables = AllNodes.Except(ReachableNodes);
            if (unreachables.Count() > 0)
            {
                EditorGUILayout.LabelField("Unreachable Nodes");
                foreach (var node in unreachables)
                {
                    // broken node case, which would not happen anymore
                    if (node is null)
                    {
                        GUILayout.Button("BROKEN NODE");
                        continue;
                    }
                    // if the node is selected, end immediately
                    if (TryNodeSelection(node)) break;
                }
            }
        }

        private void DrawOutline()
        {
            if (overviewCache == null)
            {
                overviewCache = new List<OverviewEntry>();
                if (mode == Mode.Global)
                {
                    GetOverviewHierachy(Tree.Head, overviewCache, 3);
                }
                else
                {
                    var parent = SelectedNode == Tree.Head || SelectedNode == EditorHeadNode ? EditorHeadNode : SelectedNodeParent;
                    TryNodeSelection(parent, "PARENT");
                    GetOverviewHierachy(SelectedNode, overviewCache, 3 * 2);
                }
            }

            var originalRect = GUILayoutUtility.GetLastRect();
            originalRect.y += originalRect.height;
            originalRect.x -= 5;
            originalRect.width += 5;
            int skip = 0;

            int? hide = null;
            for (int i = 0; i < overviewCache.Count; i++)
            {
                OverviewEntry item = overviewCache[i];
                if (item.isServiceStack && !overviewShowService)
                {
                    skip++;
                    continue;
                }
                if (hide.HasValue)
                {
                    if (hide.Value < item.indent)
                    {
                        skip++;
                        continue;
                    }
                    else if (item.node is Flow flow && flow.isFolded)
                    {
                        hide = item.indent;
                    }
                    else hide = null;
                }
                else
                {
                    if (item.node is Flow flow && flow.isFolded)
                    {
                        hide = item.indent;
                    }
                }
                using (new GUILayout.HorizontalScope())
                {
                    var rect = originalRect;
                    int size = GetOutlineRectSize(i);
                    // no indentation
                    if (size > 1)
                    {
                        //Debug.Log(size);
                        const float MULTIPLIER = 1.25f;
                        rect.y += (i - skip) * rect.height * MULTIPLIER;
                        rect.x += item.indent;
                        rect.width -= item.indent;
                        rect.height *= size * MULTIPLIER;
                        //rect.width = EditorSetting.overviewWindowSize - item.indent;
                        EditorGUI.DrawRect(rect, EditorSetting.HierachyColor);
                    }


                    GUILayout.Space(item.indent);
                    var nodeSelected = TryNodeSelection(in item);
                    overviewCache[i] = item;
                    if (nodeSelected) return;
                }
            }

            GUILayout.Space(10);
            DrawUnreachables();
        }

        private int GetOutlineRectSize(int index)
        {
            int skip = 0;
            int? folded = null;
            int indent = overviewCache[index].indent;
            if (overviewCache[index].node is Flow flow && flow.isFolded)
            {
                return 1;
            }
            //var rect = GUILayoutUtility.GetLastRect();
            for (int i = index + 1; i < overviewCache.Count; i++)
            {
                if (overviewCache[i].isServiceStack && !overviewShowService)
                {
                    skip++;
                    continue;
                }
                // reach same indent
                if (overviewCache[i].indent <= indent)
                {
                    return i - index - skip;
                }
                if (overviewCache[i].node is Flow f && f.isFolded && !folded.HasValue)
                {
                    folded = overviewCache[i].indent;
                    continue;
                }
                if (overviewCache[i].indent > folded)
                {
                    skip++; continue;
                }
            }
            return overviewCache.Count - index - skip;
        }

        private bool TryNodeSelection(TreeNode node) => TryNodeSelection(node, node.name);

        private bool TryNodeSelection(TreeNode node, string name) => TryNodeSelection(new OverviewEntry() { node = node, }, name);

        private bool TryNodeSelection(in OverviewEntry entry) => TryNodeSelection(in entry, entry.node.name);

        private bool TryNodeSelection(in OverviewEntry entry, string name)
        {
            TreeNode node = entry.node;
            Color color;


            if (entry.node == selectedNode) color = new Color(0.5f, 0.5f, 0.5f);
            else if (entry.isServiceStack) color = new(.8f, .8f, .8f);
            else color = Color.white;

            using (new GUILayout.HorizontalScope())
            {
                if (entry.node is Flow flow && entry.canFold)
                {
                    var c = flow.isFolded ? Color.red : Color.black;
                    using (GUIColor.By(c))
                        if (GUILayout.Button("", GUILayout.Width(10))) flow.isFolded = !flow.isFolded;
                }

                using (GUIColor.By(color))
                {
                    // NOT CLICKING
                    if (!GUILayout.Button(new GUIContent(name) { tooltip = $"{node.name} ({node.GetType().Name})" })) return false;
                }
            }

            // left click
            if (Event.current.button == 0)
            {
                SelectNode(node);
                return true;
            }
            // right click
            else
            {
                GenericMenu menu = new();
                CreateRightClickMenu(node, menu);
                menu.ShowAsContext();
            }
            return false;
        }

        /// <summary>
        /// helper for getting the overview structure
        /// </summary>
        /// <param name="node"></param>
        /// <param name="drawn"></param>
        /// <param name="indent"></param>
        private void GetOverviewHierachy(TreeNode node, List<OverviewEntry> drawn, int indent, bool isServiceStack = false)
        {
            // in case of selecting editor tree node, use actual head instead
            if (node is EditorHeadNode ed) node = Tree.Head;
            // ignore null case
            if (node == null) return;

            if (!isServiceStack)
            {
                isServiceStack = node is Service;
                if (isServiceStack) indent += EditorSetting.overviewHierachyIndentLevel;
            }
            drawn.Add((node, indent, isServiceStack));

            // find all children's uuid
            var children = node.services?.Select(s => s.UUID).Union(node.GetChildrenReference().Select(r => r.UUID));
            if (children is null) return;

            foreach (var item in children)
            {
                TreeNode childNode = Tree.GetNode(item);
                if (childNode is null) continue;
                if (drawn.Any(g => g.node == childNode)) continue;
                GetOverviewHierachy(childNode, drawn, indent + EditorSetting.overviewHierachyIndentLevel, isServiceStack);
            }
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
            var currentGUIStatus = GUI.enabled;
            GUI.enabled = true;
            GUILayout.BeginVertical();
            middleScrollPos = GUILayout.BeginScrollView(middleScrollPos);
            middleScrollPos.x = 0;
            GUI.enabled = currentGUIStatus;

            SetMiddleWindowColorAndBeginVerticle();
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

            if (!Tree.IsServiceCall(node))
                DrawNodeService(node);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (EditorSetting.debugMode)
            {
                var state = GUI.enabled;
                GUI.enabled = false;
                var script = MonoScriptCache.Get(nodeDrawer.GetCurrentDrawerType());
                EditorGUILayout.ObjectField("Current Node Drawer", script, typeof(MonoScript), false);
                GUI.enabled = state;
            }
            if (SelectedNodeParent == null && SelectedNode.uuid != Tree.headNodeUUID && ReachableNodes.Contains(SelectedNode))
            {
                Debug.LogError($"Node {SelectedNode.name} has a missing parent reference!");
            }
            DrawLowerBar(node);
            GUILayout.EndVertical();


            var menu = new GenericMenu();
            CreateRightClickMenu(node, menu);

            EditorFieldDrawers.RightClickMenu(menu);
        }

        private void DrawLowerBar(TreeNode node)
        {
            var option = GUILayout.Toolbar(-1, new string[] { SelectedNodeParent == null ? "HEAD" : "Open Parent", "Copy", "Delete Node" }, GUILayout.MinHeight(30));
            if (option == 0)
            {
                if (SelectedNodeParent != null)
                    SelectNode(SelectedNodeParent);
                else SelectNode(EditorHeadNode);
            }
            if (option == 1)
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
            if (option == 2)
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
            return node.GetChildrenReference().Any(r => Tree.GetNode(r) != null);
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
                    if (Tree.GetNode(treeNode.services[i]) is not Service item)
                    {
                        GUILayout.BeginHorizontal();
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
                            Tree.Remove(item);
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
        string rightWindowNameFilter;

        /// <summary>
        /// draw node selection window (right)
        /// </summary>
        private void DrawNodeTypeSelectionWindow()
        {
            GUILayout.BeginVertical(GUILayout.Width(200));
            GUILayout.Label("Search");
            rightWindowInputFilter = GUILayout.TextField(rightWindowInputFilter);
            rightWindowNameFilter = $"(?i){rightWindowInputFilter}(?-i)";
            rightWindowScrollPos = GUILayout.BeginScrollView(rightWindowScrollPos, false, false);
            if (
                (!string.IsNullOrEmpty(rightWindowInputFilter))
                && !IsValidRegex(rightWindowInputFilter)
            )
            {
                var c = GUI.contentColor;
                GUI.contentColor = Color.red;
                GUILayout.Label("Invalid Regex");
                GUI.contentColor = c;
            }

            switch (rightWindow)
            {
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

            GUILayout.EndScrollView();
            GUILayout.Space(20);
            if (GUILayout.Button("Close"))
            {
                rightWindow = RightWindow.None;
                GUILayout.EndVertical();
                return;
            }
            GUILayout.Space(20);
            GUILayout.EndVertical();
        }

        /// <summary>
        /// draw node selection window
        /// </summary>
        private void DrawNodeSelectionWindow()
        {
            bool noFilter = string.IsNullOrEmpty(rightWindowInputFilter);
            // should not allow create service here
            if (noFilter && clipboard.HasContent && !clipboard.TypeMatch(typeof(Service)))
            {
                GUILayout.Label("Clipboard");
                if (SelectEvent_TryPaste())
                    return;
                GUILayout.Space(16);
            }

            if (!isRawReferenceSelect)
            {
                if (noFilter) DrawCreateNewNodeWindow();
                else DrawAllNodeTypeWithMatchesName(rightWindowNameFilter);

                GUILayout.Space(16);
            }
            DrawExistNodeSelectionWindow(typeof(TreeNode));
        }

        private void DrawAllNodeTypeWithMatchesName(string nameFilter)
        {
            var classes = TypeCache.GetTypesDerivedFrom<TreeNode>();// NodeFactory.GetSubclassesOf(typeof(TreeNode));
            foreach (var type in classes)
            {
                if (type.IsAbstract) continue;
                if (type.IsSubclassOf(typeof(Service))) continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
                // filter
                if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(type.Name, nameFilter).Count == 0) continue;

                // set node tip
                var content = new GUIContent(type.Name.ToTitleCase());
                AddGUIContentAttributes(type, content);
                if (GUILayout.Button(content))
                {
                    var n = CreateNode(type);
                    selectEvent?.Invoke(n);
                    rightWindow = RightWindow.None;
                }
            }
            GUILayout.Space(16);
        }

        private static void AddGUIContentAttributes(Type type, GUIContent content)
        {
            string value = AliasAttribute.GetEntry(type);
            content.tooltip = NodeTipAttribute.GetEntry(type);
            content.text = string.IsNullOrEmpty(value) ? type.Name.ToTitleCase() : value;
        }

        private void DrawExistNodeSelectionWindow(Type type)
        {
            var nodes = Tree.AllNodes.Where(n => n.GetType().IsSubclassOf(type)).OrderBy(n => n.name);
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
                    if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(node.name, rightWindowNameFilter).Count == 0) continue;
                    // do not show service as existing node
                    if (node is Service) continue;

                    if (GUILayout.Button(node.name))
                    {
                        TreeNode parent = Tree.GetParent(node);
                        if (parent != null && !isRawReferenceSelect)
                        {
                            if (EditorUtility.DisplayDialog($"Node has a parent already", $"This Node is connecting to {parent.name}, move {(SelectedNode != null ? "under" + SelectedNode.name : "")} ?", "OK", "Cancel"))
                            {
                                var originParent = Tree.GetParent(node);
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

        private void DrawCreateNewNodeWindow()
        {
            hideNewNodeOptions = !EditorGUILayout.Foldout(!hideNewNodeOptions, "New...");
            if (hideNewNodeOptions) return;


            if (SelectCommonNodeType(out Type value))
            {
                SelectEvent_CreateAndSelect(value);
                return;
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
                    NodeReference nodeReference = (NodeReference)item.GetValue(parent);
                    if (nodeReference.UUID == uuid)
                        nodeReference.UUID = UUID.Empty;
                }
                else if (item.FieldType == typeof(List<Probability.EventWeight>))
                {
                    List<Probability.EventWeight> nodeReferences =
                        (List<Probability.EventWeight>)item.GetValue(parent);
                    nodeReferences.RemoveAll(r => r.reference.UUID == uuid);
                }
                else if (item.FieldType == typeof(List<NodeReference>))
                {
                    List<NodeReference> nodeReferences = (List<NodeReference>)item.GetValue(parent);
                    nodeReferences.RemoveAll(r => r.UUID == uuid);
                }
                else if (item.FieldType == typeof(UUID))
                {
                    if ((UUID)item.GetValue(parent) == uuid)
                        item.SetValue(parent, UUID.Empty);
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
                if (
                    IsValidRegex(rightWindowInputFilter)
                    && Regex.Matches(type.Name, rightWindowNameFilter).Count == 0
                )
                    continue;
                // set node tip
                var content = new GUIContent(type.Name.ToTitleCase());
                AddGUIContentAttributes(type, content);
                if (GUILayout.Button(content))
                    SelectEvent_CreateAndSelect(type);
            }
            GUILayout.Space(16);
            if (GUILayout.Button("Back"))
            {
                rightWindow = RightWindow.All;
                return;
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
            rightWindow = RightWindow.None;
            editorWindow.Refresh();
            SelectNode(node);
        }

        public void SelectEvent_PasteSubTree()
        {
            var nodes = clipboard.Content;
            Tree.AddRange(nodes);
            var root = nodes[0];
            SelectEvent_Select(root);
        }

        private void DrawTypeSelectionWindow(Type parentType, System.Action typeWindowCloseFunc)
        {
            if (clipboard.HasContent && clipboard.TypeMatch(parentType))
            {
                GUILayout.Label("Clipboard");
                if (SelectEvent_TryPaste())
                    return;
            }

            GUILayout.Label(parentType.Name.ToTitleCase());
            var classes = TypeCache.GetTypesDerivedFrom(parentType);//NodeFactory.GetSubclassesOf(parentType);
            foreach (var type in classes)
            {
                if (type.IsAbstract) continue;
                if (parentType != typeof(Service) && type.IsSubclassOf(typeof(Service))) continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
                if (SelectedNode is Service && Attribute.IsDefined(type, typeof(DisableServiceCallAttribute))) continue;
                // filter
                if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(type.Name, rightWindowNameFilter).Count == 0) continue;

                // set node tip
                var content = new GUIContent(type.Name.ToTitleCase());
                AddGUIContentAttributes(type, content);
                if (GUILayout.Button(content))
                {
                    SelectEvent_CreateAndSelect(type);
                    rightWindow = RightWindow.None;
                }
            }
            GUILayout.Space(16);
            if (GUILayout.Button("Back"))
            {
                typeWindowCloseFunc?.Invoke();
                return;
            }
        }

        private void DrawNodeTypeSelectionPlaceHolderWindow()
        {
            //var rect = GUILayoutUtility.GetRect(200 - 20, 1000);
            //EditorGUI.DrawRect(rect, Color.gray);
            GUILayout.BeginVertical(GUILayout.Width(200));
            rightWindowScrollPos = EditorGUILayout.BeginScrollView(rightWindowScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar);
            rightWindowScrollPos.x = 0;
            EditorGUILayout.LabelField("");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
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
        }

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
                Tree.Add(node);
                node.name = Tree.GenerateNewNodeName(node);
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
                    Tree.headNodeUUID = SelectedNode.uuid;
                });
            }
            GUILayout.EndVertical();
        }

        protected bool SelectCompositeNodeType(out Type nodeType)
        {
            var types = TypeCache.GetTypesDerivedFrom<Flow>();// NodeFactory.GetSubclassesOf(typeof(Flow));
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









        private void WriteClipboard(TreeNode selectedNode)
        {
            clipboard.Clear();
            clipboard.Write(selectedNode, Tree);
        }

        private void WriteClipboardSingle(TreeNode selectedNode)
        {
            clipboard.Clear();
            clipboard.WriteSingle(selectedNode, Tree);
        }

        /// <summary>
        /// Duplicate given node is possible
        /// </summary>
        /// <param name="node"></param>
        private bool CanDuplicate(TreeNode node)
        {
            if (node is Service) return true;
            var parent = Tree.GetParent(node);
            return parent is IListFlow;
        }

        /// <summary>
        /// Duplicate given node is possible
        /// </summary>
        /// <param name="node"></param>
        private void Duplicate(TreeNode node)
        {
            Clipboard clipboard = new();
            clipboard.Write(node, Tree);

            var parent = Tree.GetParent(node);
            List<TreeNode> content = clipboard.Content;
            TreeNode root = content[0];

            // duplicate service
            if (node is Service service)
            {
                Tree.AddRange(clipboard.Content);   // must add range first to add undo record
                parent.AddService(service);
                return;
            }
            else if (parent is IListFlow flow)
            {
                int index = flow.IndexOf(node);
                Tree.AddRange(content);             // must add range first to add undo record
                flow.Insert(index + 1, root);
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
                this.canFold = node is Flow;
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
