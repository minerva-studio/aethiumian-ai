using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Collections.Generic;
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

        private UUID clipboard;

        private TreeNode selectedNode;
        private TreeNode selectedNodeParent;

        public NodeDrawHandler nodeDrawer;
        public SerializedProperty nodeRawDrawingProperty;

        public bool overviewWindowOpen = true;
        public bool rawReferenceSelect;

        public RightWindow rightWindow;
        public SelectNodeEvent selectEvent;

        public Vector2 middleScrollPos;
        public Vector2 leftScrollPos;
        public Vector2 rightWindowScrollPos;

        Mode mode;
        EditorHeadNode editorHeadNode;


        internal new TreeNode SelectedNode
        {
            get => selectedNode;
            set
            {
                rightWindow = RightWindow.None;
                selectedNode = value;
                if (value is not null) selectedNodeParent = selectedNode != null ? Tree.GetNode(selectedNode.parent) : null;
            }
        }
        internal new TreeNode SelectedNodeParent => selectedNodeParent ??= (selectedNode == null ? null : Tree.GetNode(selectedNode.parent));
        internal EditorHeadNode EditorHeadNode => editorHeadNode ??= new();



        public void DrawTree()
        {
            if (!overviewWindowOpen)
                overviewWindowOpen = GUILayout.Button("Open Overview");
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
                if (overviewWindowOpen)
                    DrawOverview();

                GUILayout.Space(10);

                if (SelectedNode is null)
                {
                    TreeNode head = Tree.Head;
                    if (head != null)
                        SelectNode(head);
                    else
                        CreateHeadNode();
                }
                if (SelectedNode != null)
                {
                    if (SelectedNode is EditorHeadNode)
                        DrawTreeHead();
                    else
                        DrawSelectedNode(SelectedNode);
                }

                GUILayout.Space(10);

                if (rightWindow != RightWindow.None)
                    DrawNodeTypeSelectionWindow();
                else
                    DrawNodeTypeSelectionPlaceHolderWindow();
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Try delete the node
        /// </summary>
        /// <param name="node"></param>
        public void TryDeleteNode(TreeNode node)
        {
            if (EditorUtility.DisplayDialog("Deleting Node", $"Delete the node {node.name} ({node.uuid}) ?", "OK", "Cancel"))
            {
                var parent = Tree.GetNode(node.parent);
                Tree.Remove(node);
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
        }

        public void SelectNode(TreeNode node)
        {
            SelectedNode = node;
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

        #region Left Window  
        /// <summary>
        /// Draw Overview window
        /// </summary>
        private void DrawOverview()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(EditorSetting.overviewWindowSize), GUILayout.MinWidth(EditorSetting.overviewWindowSize - 1));

            EditorGUILayout.LabelField("Tree Overview");
            {
                var global = new GUIContent("Global tree") { tooltip = "Display the entire behaviour tree" };
                var local = new GUIContent("Local tree") { tooltip = "Show only the local tree of selected node" };
                mode = (Mode)GUILayout.Toolbar((int)mode, new GUIContent[] { global, local });
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

            leftScrollPos = EditorGUILayout.BeginScrollView(
                leftScrollPos,
                GUIStyle.none,
                GUI.skin.verticalScrollbar
            );
            GUILayout.BeginVertical(
                GUILayout.MinWidth(EditorSetting.overviewWindowSize - 50),
                GUILayout.MinHeight(400)
            );
            EditorGUILayout.LabelField("Tree");
            List<TreeNode> allNodeFromHead = new();

            if (mode == Mode.Global)
            {
                DrawOverview(Tree.Head, allNodeFromHead, 3);
            }
            else
            {
                var parent = SelectedNode == Tree.Head || SelectedNode == EditorHeadNode ? EditorHeadNode : SelectedNodeParent;
                TryNodeSelection(parent, "PARENT");
                DrawOverview(SelectedNode, allNodeFromHead, 3 * 2);
            }

            GUILayout.Space(10);
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
                    if (TryNodeSelection(node))
                    {
                        GUILayout.EndVertical();
                        GUILayout.EndScrollView();
                        GUILayout.EndVertical();
                        return;
                    }
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.FlexibleSpace();
            overviewWindowOpen = !GUILayout.Button("Close");
            GUILayout.EndVertical();
        }

        private bool TryNodeSelection(TreeNode node) => TryNodeSelection(node, node.name);
        private bool TryNodeSelection(TreeNode node, string name)
        {
            var label = new GUIContent(name) { tooltip = $"{node.name} ({node.GetType().Name})" };

            // NOT CLICKING
            if (!GUILayout.Button(label)) return false;

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
                menu.AddItem(new GUIContent("Open"), false, () => SelectNode(node));
                menu.AddItem(new GUIContent("Delete"), false, () => TryDeleteNode(node));
                menu.AddSeparator("/");
                menu.ShowAsContext();
            }
            return false;
        }

        /// <summary>
        /// helper for drawing overview
        /// </summary>
        /// <param name="node"></param>
        /// <param name="drawn"></param>
        /// <param name="indent"></param>
        private void DrawOverview(TreeNode node, List<TreeNode> drawn, int indent)
        {
            // in case of selecting editor tree node, use actual head instead
            if (node is EditorHeadNode ed)
            {
                node = Tree.Head;
            }
            // ignore null case
            if (node == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            var nodeSelected = TryNodeSelection(node);
            GUILayout.EndHorizontal();
            if (nodeSelected) return;

            drawn.Add(node);

            // find all children's uuid
            var children = node.services.Select(s => s.UUID).Union(node.GetChildrenReference().Select(r => r.UUID));
            if (children is null) return;

            foreach (var item in children)
            {
                TreeNode childNode = Tree.GetNode(item);
                if (childNode is null) continue;
                if (drawn.Contains(childNode)) continue;
                DrawOverview(childNode, drawn, indent + EditorSetting.overviewHierachyIndentLevel);
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
                var script = Resources
                    .FindObjectsOfTypeAll<MonoScript>()
                    .FirstOrDefault(n => n.GetClass() == nodeDrawer.drawer.GetType());
                EditorGUILayout.ObjectField(
                    "Current Node Drawer",
                    script,
                    typeof(MonoScript),
                    false
                );
                GUI.enabled = state;
            }
            if (
                SelectedNodeParent == null
                && SelectedNode.uuid != Tree.headNodeUUID
                && ReachableNodes.Contains(SelectedNode)
            )
            {
                Debug.LogError($"Node {SelectedNode.name} has a missing parent reference!");
            }
            var option = GUILayout.Toolbar(
                -1,
                new string[]
                {
                    SelectedNodeParent == null ? "" : "Open Parent",
                    "Copy",
                    "Delete Node"
                },
                GUILayout.MinHeight(30)
            );
            if (option == 0)
            {
                if (SelectedNodeParent != null)
                    SelectNode(SelectedNodeParent);
            }
            if (option == 1)
            {
                //GUIUtility.systemCopyBuffer = JsonUtility.ToJson(SelectedNode);
                clipboard = SelectedNode.uuid;
            }
            if (option == 2)
            {
                TryDeleteNode(node);
            }
            GUILayout.EndVertical();
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
                        var currentColor = GUI.contentColor;
                        GUI.contentColor = Color.red;
                        GUILayout.Label("Node not found: " + treeNode.services[i]);
                        GUI.contentColor = currentColor;
                        continue;
                    }
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(18);
                    if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
                    {
                        treeNode.services.RemoveAt(i);
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
            GUILayout.Space(50);
            if (GUILayout.Button("Close"))
            {
                rightWindow = RightWindow.None;
                GUILayout.EndVertical();
                return;
            }
            GUILayout.Space(50);
            GUILayout.EndVertical();
        }

        /// <summary>
        /// draw node selection window
        /// </summary>
        private void DrawNodeSelectionWindow()
        {
            if (string.IsNullOrEmpty(rightWindowInputFilter))
                DrawCreateNewNodeWindow();
            else
                DrawAllNodeTypeWithMatchesName(rightWindowNameFilter);
            GUILayout.Space(16);
            DrawExistNodeSelectionWindow(typeof(TreeNode));
        }

        private void DrawAllNodeTypeWithMatchesName(string nameFilter)
        {
            var classes = NodeFactory.GetSubclassesOf(typeof(TreeNode));
            foreach (var type in classes)
            {
                if (type.IsAbstract)
                    continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute)))
                    continue;
                // filter
                if (
                    IsValidRegex(rightWindowInputFilter)
                    && Regex.Matches(type.Name, nameFilter).Count == 0
                )
                    continue;

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
            content.tooltip = NodeTipAttribute.GetEntry(type);
            content.text = AliasAttribute.GetEntry(type);
        }

        private void DrawExistNodeSelectionWindow(Type type)
        {
            var nodes = Tree.AllNodes.Where(n => n.GetType().IsSubclassOf(type) && n != Tree.Head).OrderBy(n => n.name);
            if (nodes.Count() == 0)
                return;
            GUILayout.Label("Exist Nodes...");
            foreach (var node in nodes)
            {
                //not a valid type
                if (!node.GetType().IsSubclassOf(type))
                    continue;
                //head
                if (node == Tree.Head)
                    continue;
                //select for service but the node is not allowed to appear in a service
                //if (selectedService != null && Attribute.GetCustomAttribute(node.GetType(), typeof(AllowServiceCallAttribute)) == null) continue;
                //filter
                if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(node.name, rightWindowNameFilter).Count == 0)
                    continue;
                if (GUILayout.Button(node.name))
                {
                    TreeNode parent = Tree.GetNode(node.parent);
                    if (parent == null || rawReferenceSelect)
                    {
                        SelectNode(node);
                    }
                    else if (EditorUtility.DisplayDialog($"Node has a parent already", $"This Node is connecting to {parent.name}, move {(SelectedNode != null ? "under" + SelectedNode.name : "")} ?", "OK", "Cancel"))
                    {
                        var originParent = Tree.GetNode(node.parent);
                        if (originParent is not null)
                            RemoveFromParent(originParent, node);

                        SelectNode(node);
                    }
                }
            }

            void SelectNode(TreeNode node)
            {
                if (selectEvent == null)
                    Debug.LogWarning("No event exist");
                selectEvent?.Invoke(node);
                rightWindow = RightWindow.None;
                rawReferenceSelect = false;
            }
        }

        private void DrawCreateNewNodeWindow()
        {
            GUILayout.Label("New...");

            var clipnode = Tree.GetNode(clipboard);
            if (clipnode != null)
            {
                if (SelectEvent_TryPaste(clipnode))
                    return;
            }

            GUILayout.Label("Composites");
            if (SelectFlowNodeType(out Type value))
            {
                SelectEvent_CreateAndSelect(value);
                return;
            }

            GUILayout.Label("Logics");
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
        private bool SelectEvent_TryPaste(TreeNode clipboardNode)
        {
            if (GUILayout.Button($"Paste ({clipboardNode.name})"))
            {
                var nodes = NodeFactory.DeepCloneSubTree(clipboardNode, Tree);
                Tree.AddRange(nodes);
                var node = nodes[0];
                SelectEvent_Select(node);
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
            ReachableNodes.Add(node);
            rightWindow = RightWindow.None;
            editorWindow.Refresh();
            SelectNode(node);
        }

        private void DrawTypeSelectionWindow(Type masterType, System.Action typeWindowCloseFunc)
        {
            GUILayout.Label(masterType.Name.ToTitleCase());
            var classes = NodeFactory.GetSubclassesOf(masterType);
            foreach (var type in classes)
            {
                if (type.IsAbstract) continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
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
            rightWindowScrollPos = EditorGUILayout.BeginScrollView(
                rightWindowScrollPos,
                GUIStyle.none,
                GUI.skin.verticalScrollbar
            );
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
            rawReferenceSelect = isRawSelect;
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

        protected bool SelectFlowNodeType(out Type nodeType)
        {
            var types = NodeFactory.GetSubclassesOf(typeof(Flow));
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
    }
}
