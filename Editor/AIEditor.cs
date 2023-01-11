using Amlos.AI.Visual;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;

namespace Amlos.AI.Editor
{

    public delegate void SelectNodeEvent(TreeNode node);
    public delegate void SelectServiceEvent(Service node);
    /// <summary>
    /// AI editor window
    /// </summary>
    public class AIEditor : EditorWindow
    {
        public enum Window
        {
            nodes,
            graph,
            variables,
            assetReference,
            properties,
            settings
        }
        public enum RightWindow
        {
            None,
            All,
            Determines,
            Actions,
            Calls,
            Services,
            Arithmetic,
            Unity,
        }

        public BehaviourTreeData tree;
        public AIEditorSetting setting;

        public int toolBarIndex;

        public Vector2 middleScrollPos;
        public Vector2 leftScrollPos;
        public Vector2 rightWindowScrollPos;


        public TreeNode selectedNode;
        public TreeNode selectedNodeParent;
        public Service selectedService;

        public NodeDrawHandler nodeDrawer;
        public SerializedProperty nodeRawDrawingProperty;

        public bool overviewWindowOpen = true;
        public Window window;
        public RightWindow rightWindow;
        public bool rawReferenceSelect;
        public SelectNodeEvent selectEvent;

        public List<TreeNode> unreachables;
        public List<TreeNode> allNodes;
        private List<TreeNode> reachables;


        public TreeNode SelectedNode { get => selectedNode; set { SelectNode(value); } }



        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/AI Editor")]
        public static AIEditor ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            var window = GetWindow(typeof(AIEditor), false, "AI Editor");
            window.name = "AI Editor";
            return window as AIEditor;

        }

        public void Load(BehaviourTreeData data)
        {
            tree = data;
            SelectedNode = data.Head;
        }

        private void SelectNode(TreeNode value)
        {
            rightWindow = RightWindow.None;
            selectedNode = value;
            if (value is null)
            {
                return;
            }
            selectedService = tree.IsServiceCall(value) ? tree.GetServiceHead(value) : null;
            selectedNodeParent = selectedNode != null ? tree.GetNode(selectedNode.parent) : null;
        }


        public void Refresh()
        {
            Initialize();
            rawReferenceSelect = false;
            SelectedNode = null;
            GetAllNode();
        }

        private void Initialize()
        {
            setting = AIEditorSetting.GetOrCreateSettings();
            if (tree) EditorUtility.SetDirty(tree);
        }

        void OnGUI()
        {
            Initialize();
            StartWindow();
            GUILayout.Space(5);

            GetAllNode();

            if (tree && window == Window.graph)
            {
                DrawGraph();
            }

            #region Draw Header
            GUILayout.Toolbar(-1, new string[] { "" });
            if (!SelectTree())
            {
                DrawNewBTWindow();
                EndWindow();
                return;
            }

            if (setting.enableGraph)
            {
                window = (Window)GUILayout.Toolbar((int)window, new string[] { "Tree", "Graph", "Variable Table", "Asset References", "Tree Properties", "Editor Settings" }, GUILayout.MinHeight(30));
            }
            else
            {
                window = (Window)GUILayout.Toolbar((int)window - 1, new string[] { "Tree", "Variable Table", "Asset References", "Tree Properties", "Editor Settings" }, GUILayout.MinHeight(30));
                if ((int)window == -1) window = Window.nodes;
                if ((int)window > 0) window++;
            }
            #endregion
            GUILayout.Space(10);

            //Initialize();
            GUI.enabled = !setting.safeMode;
            switch (window)
            {
                case Window.nodes:
                    DrawTree();
                    break;
                case Window.assetReference:
                    DrawAssetReferenceTable();
                    break;
                case Window.variables:
                    DrawVariableTable();
                    break;
                case Window.properties:
                    DrawProperties();
                    break;
                case Window.settings:
                    DrawSettings();
                    break;
                default:
                    break;
            }

            if (window != Window.variables)
            {
                selectedVariableData = null;
                tableDrawDetail = false;
            }

            EndWindow();

            if (GUI.changed) Repaint();
        }

        #region Graph 
        private List<GraphNode> GraphNodes { get => tree ? tree.Graph.graphNodes : null; set => tree.Graph.graphNodes = value; }
        private List<Connection> Connections { get => tree ? tree.Graph.connections : null; set => tree.Graph.connections = value; }


        private ConnectionPoint selectedInPoint;
        private ConnectionPoint selectedOutPoint;

        private Vector2 offset;
        private Vector2 drag;
        private EditorHeadNode editorHeadNode;


        private void DrawGraph()
        {
            DrawGrid(20, 0.2f, Color.gray);
            DrawGrid(100, 0.4f, Color.gray);

            DrawNodes();
            DrawConnections();

            DrawConnectionLine(Event.current);

            ProcessNodeEvents(Event.current);
            ProcessEvents(Event.current);
        }

        private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor)
        {
            int widthDivs = Mathf.CeilToInt(position.width / gridSpacing);
            int heightDivs = Mathf.CeilToInt(position.height / gridSpacing);

            Handles.BeginGUI();
            Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

            offset += drag * 0.5f;
            Vector3 newOffset = new(offset.x % gridSpacing, offset.y % gridSpacing, 0);

            for (int i = 0; i < widthDivs; i++)
            {
                Handles.DrawLine(new Vector3(gridSpacing * i, -gridSpacing, 0) + newOffset, new Vector3(gridSpacing * i, position.height, 0f) + newOffset);
            }

            for (int j = 0; j < heightDivs; j++)
            {
                Handles.DrawLine(new Vector3(-gridSpacing, gridSpacing * j, 0) + newOffset, new Vector3(position.width, gridSpacing * j, 0f) + newOffset);
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private void DrawNodes()
        {
            if (GraphNodes != null)
            {
                for (int i = GraphNodes.Count - 1; i >= 0; i--)
                {
                    GraphNode graphNode = GraphNodes[i];
                    if (graphNode == null)
                    {
                        GraphNodes.Remove(graphNode);
                        continue;
                    }
                    TreeNode child = tree.GetNode(graphNode.uuid);
                    if (child == null)
                    {
                        GraphNodes.Remove(graphNode);
                        continue;
                    }
                    int index = 0;
                    string orderInfo;
                    TreeNodeType type;
                    if (child == tree.Head)
                    {
                        type = TreeNodeType.head;
                        orderInfo = "Head";
                        index = 0;
                    }
                    else
                    {
                        TreeNode parentNode = tree.GetNode(child.parent.UUID);
                        if (parentNode != null)
                        {
                            index = parentNode.GetIndexInfo(child);
                            orderInfo = parentNode.GetOrderInfo(child);
                        }
                        else
                        {
                            index = 0;
                            orderInfo = "";
                        }
                        type = unreachables.Contains(child) ? TreeNodeType.unused : TreeNodeType.@default;
                    }


                    graphNode.OnRemoveNode = OnClickRemoveNode;
                    graphNode.OnSelectNode = OnClickSelectNode;
                    graphNode.inPoint.OnClickConnectionPoint = OnClickInPoint;
                    graphNode.outPoint.OnClickConnectionPoint = OnClickOutPoint;
                    graphNode.Refresh(child, orderInfo, index, type);
                    graphNode.Draw();
                }
            }
        }

        private void DrawConnections()
        {
            if (Connections != null)
            {
                for (int i = Connections.Count - 1; i >= 0; i--)
                {
                    if (Connections[i] == null)
                    {
                        Connections.RemoveAt(i);
                        continue;
                    }
                    Connections[i].OnClickRemoveConnection = OnClickRemoveConnection;
                    Connections[i].Draw();
                }
            }
        }

        private void ProcessEvents(Event e)
        {
            drag = Vector2.zero;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        ClearConnectionSelection();
                    }

                    if (e.button == 1)
                    {
                        ProcessContextMenu(e.mousePosition);
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        OnDrag(e.delta);
                    }
                    break;
            }
        }

        private void ProcessNodeEvents(Event e)
        {
            if (GraphNodes != null)
            {
                for (int i = GraphNodes.Count - 1; i >= 0; i--)
                {
                    bool guiChanged = GraphNodes[i].ProcessEvents(e);

                    if (guiChanged)
                    {
                        GUI.changed = true;
                    }
                }
            }
        }

        private void DrawConnectionLine(Event e)
        {
            if (selectedInPoint != null && selectedOutPoint == null)
            {
                Handles.DrawBezier(
                    selectedInPoint.rect.center,
                    e.mousePosition,
                    selectedInPoint.rect.center + Vector2.left * 50f,
                    e.mousePosition - Vector2.left * 50f,
                    Color.white,
                    null,
                    2f
                );

                GUI.changed = true;
            }

            if (selectedOutPoint != null && selectedInPoint == null)
            {
                Handles.DrawBezier(
                    selectedOutPoint.rect.center,
                    e.mousePosition,
                    selectedOutPoint.rect.center - Vector2.left * 50f,
                    e.mousePosition + Vector2.left * 50f,
                    Color.white,
                    null,
                    2f
                );

                GUI.changed = true;
            }
        }

        private void ProcessContextMenu(Vector2 mousePosition)
        {
            GenericMenu genericMenu = new();
            genericMenu.AddItem(new GUIContent("Add node"), false, () => OnClickAddNode(mousePosition));
            genericMenu.ShowAsContext();
        }

        private void OnDrag(Vector2 delta)
        {
            drag = delta;

            if (GraphNodes != null)
            {
                for (int i = 0; i < GraphNodes.Count; i++)
                {
                    GraphNodes[i].Drag(delta);
                }
            }

            GUI.changed = true;
        }

        private void OnClickAddNode(Vector2 mousePosition)
        {
            GraphNodes ??= new List<GraphNode>();

            GraphNodes.Add(new GraphNode(mousePosition, 200, 80));
        }

        private void OnClickInPoint(ConnectionPoint inPoint)
        {
            selectedInPoint = inPoint;

            if (selectedOutPoint != null)
            {
                if (selectedOutPoint.node != selectedInPoint.node)
                {
                    CreateConnection();
                    ClearConnectionSelection();
                }
                else
                {
                    ClearConnectionSelection();
                }
            }
        }

        private void OnClickOutPoint(ConnectionPoint outPoint)
        {
            selectedOutPoint = outPoint;

            if (selectedInPoint != null)
            {
                if (selectedOutPoint.node != selectedInPoint.node)
                {
                    CreateConnection();
                    ClearConnectionSelection();
                }
                else
                {
                    ClearConnectionSelection();
                }
            }
        }

        private void OnClickRemoveNode(GraphNode node)
        {
            if (Connections != null)
            {
                List<Connection> connectionsToRemove = new();

                for (int i = 0; i < Connections.Count; i++)
                {
                    if (Connections[i].inPoint == node.inPoint || Connections[i].outPoint == node.outPoint)
                    {
                        connectionsToRemove.Add(Connections[i]);
                    }
                }

                for (int i = 0; i < connectionsToRemove.Count; i++)
                {
                    Connections.Remove(connectionsToRemove[i]);
                }

                connectionsToRemove = null;
            }

            GraphNodes.Remove(node);
        }

        private void OnClickSelectNode(GraphNode gnode)
        {
            TreeNode treeNode = tree.GetNode(gnode.uuid);
            SelectedNode = treeNode;
            //Debug.Log(treeNode);
            window = Window.nodes;
        }

        private void OnClickRemoveConnection(Connection connection)
        {
            Connections.Remove(connection);
        }

        private void CreateConnection()
        {
            Connections ??= new List<Connection>();
            Connections.Add(new Connection(selectedInPoint, selectedOutPoint, OnClickRemoveConnection));
        }

        private void ClearConnectionSelection()
        {
            selectedInPoint = null;
            selectedOutPoint = null;
        }


        /// <summary>
        /// Create the graph of this behaviour tree
        /// </summary>
        private void CreateGraph()
        {
            GraphNodes ??= new List<GraphNode>();
            Connections ??= new List<Connection>();
            GraphNodes.Clear();
            Connections.Clear();

            List<TreeNode> created = new();

            CreateGraph(tree.Head, Vector2.one * 200, created);
        }

        /// <summary>
        /// recursion of creating graph
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="position"></param>
        /// <param name="created"></param>
        /// <param name="lvl"></param>
        /// <returns></returns>
        private GraphNode CreateGraph(TreeNode treeNode, Vector2 position, List<TreeNode> created, int lvl = 1)
        {
            GraphNode graphNode = new(position, 200, 80)
            {
                uuid = treeNode.uuid
            };
            GraphNodes.Add(graphNode);
            created.Add(treeNode);
            List<NodeReference> list = treeNode.GetChildrenReference();
            Debug.Log(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                NodeReference item = list[i];

                TreeNode child = allNodes.FirstOrDefault(n => n.uuid == item.UUID);

                if (child == null) continue;
                if (created.Contains(child)) continue;

                var childPos = position + ((float)i / list.Count - 0.5f) * (2000f / lvl) * Vector2.right;
                childPos.y += 100;
                var node = CreateGraph(child, childPos, created, ++lvl);
                if (node != null) Connections.Add(new Connection(node.inPoint, graphNode.outPoint, OnClickRemoveConnection));
            }
            return graphNode;
        }
        #endregion

        private void DrawNewBTWindow()
        {
            SelectedNode = null;
            // Open Save panel and save it
            if (GUILayout.Button("Create New Behaviour Tree"))
            {
                var path = EditorUtility.SaveFilePanel("New Behaviour Tree", "", "AI_NewBehaviourTree.asset", "asset");
                if (path != "")
                {
                    var behaviourTree = CreateInstance<BehaviourTreeData>();
                    var p = Application.dataPath;
                    AssetDatabase.CreateAsset(behaviourTree, "Assets" + path[p.Length..path.Length]);
                    AssetDatabase.Refresh();
                    tree = behaviourTree;
                    window = Window.properties;


                    if (Selection.activeGameObject)
                    {
                        var aI = Selection.activeGameObject.GetComponent<AI>();
                        if (!aI)
                        {
                            aI = Selection.activeGameObject.AddComponent<AI>();
                        }
                        if (!aI.data)
                        {
                            aI.data = behaviourTree;
                        }
                    }
                }
            }
        }

        #region Tree

        private void DrawTree()
        {
            if (!overviewWindowOpen) overviewWindowOpen = GUILayout.Button("Open Overview");
            GUILayout.BeginHorizontal();

            if (tree.IsInvalid())
            {
                DrawInvalidTree();
            }
            else
            {
                if (overviewWindowOpen) DrawOverview();

                GUILayout.Space(10);


                if (SelectedNode is null)
                {
                    TreeNode head = tree.Head;
                    if (head != null) SelectedNode = head;
                    else CreateHeadNode();
                }
                if (SelectedNode != null)
                {
                    if (SelectedNode is EditorHeadNode)
                    {
                        DrawTreeHead();
                    }
                    else DrawSelectedNode(SelectedNode);
                }

                GUILayout.Space(10);

                if (rightWindow != RightWindow.None) DrawNodeTypeSelectionWindow();
                else DrawNodeTypeSelectionPlaceHolderWindow();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawInvalidTree()
        {
            GUILayout.Space(10);
            SetMiddleWindowColorAndBeginVerticle();
            EditorGUILayout.LabelField($"Unable to load behaviour tree \"{tree.name}\", at least 1 null node appears in data.");
            EditorGUILayout.LabelField($"Force loading this behaviour tree might result data corruption.");
            EditorGUILayout.LabelField($"Several reasons might cause this problem:");
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField($"1. Node class have been renamed recently.");
            EditorGUILayout.LabelField($"2. Node class have been transferred to another namespace recently.");
            EditorGUILayout.LabelField($"3. Node class have been transferred to another assembly recently.");
            EditorGUILayout.LabelField($"4. Asset corrupted during merging");
            EditorGUI.indentLevel--;
            EditorGUILayout.LabelField($"You can try using MovedFrom Attribute to migrate the node to a new name/namespace/assembly.");
            EditorGUILayout.LabelField($"If the problem still occur, you might need to open behaviour tree data file \"{tree.name}\" data file in Unity Inspector or an text editor to manually fix the issue");
            EditorGUILayout.LabelField("");
            EditorGUILayout.LabelField("==========");
            EditorGUILayout.LabelField($"First Null Index: {tree.AllNodes.IndexOf(null)}");
            GUILayout.EndVertical();
        }

        private bool SelectTree()
        {
            //if (!tree)
            //{
            //    allNodes = new List<TreeNode>();
            //    if (Selection.activeObject is BehaviourTreeData data) tree = data;
            //    else return false;
            //}
            var newTree = (BehaviourTreeData)EditorGUILayout.ObjectField("Behaviour Tree", tree, typeof(BehaviourTreeData), false);
            if (newTree != tree)
            {
                tree = newTree;
                if (newTree)
                {
                    EditorUtility.ClearDirty(tree);
                    EditorUtility.SetDirty(tree);
                    NewTreeSelectUpdate();
                    SelectedNode = tree.Head;
                }
                else
                {
                    tree = null;
                    return false;
                }
            }
            if (!tree)
            {
                tree = null;
                return false;
            }
            return true;
        }

        private void NewTreeSelectUpdate()
        {
            nodeRawDrawingProperty = null;
            GetAllNode();
        }

        #region Left Window 
        private bool isSearchOpened;
        private string overviewInputFilter;
        private string overviewNameFilter;

        /// <summary>
        /// Draw Overview window
        /// </summary>
        private void DrawOverview()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(setting.overviewWindowSize), GUILayout.MinWidth(setting.overviewWindowSize - 1));

            EditorGUILayout.LabelField("Tree Overview");
            //if (isSearchOpened)
            //{
            //    GUILayout.Label("Search");
            //    overviewInputFilter = GUILayout.TextField(overviewInputFilter);
            //    overviewNameFilter = $"(?i){overviewNameFilter}(?-i)";
            //    isSearchOpened = !GUILayout.Button("Close");
            //}
            //else
            //{
            //    isSearchOpened = GUILayout.Button("Search");
            //}
            GUILayout.Space(10);
            leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos, GUIStyle.none, GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.MaxWidth(setting.overviewWindowSize - 50), GUILayout.MinWidth(setting.overviewWindowSize - 50), GUILayout.MinHeight(400));

            EditorGUILayout.LabelField("From Head");
            List<TreeNode> allNodeFromHead = new();

            if (tree.Head != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("HEAD"))
                {
                    editorHeadNode ??= new EditorHeadNode();
                    SelectedNode = editorHeadNode;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            DrawOverview(tree.Head, allNodeFromHead, 3);

            GUILayout.Space(10);
            if (unreachables.Count() > 0)
            {
                EditorGUILayout.LabelField("Unreachable Nodes");
                foreach (var node in unreachables)
                {
                    if (node is null)
                    {
                        GUILayout.Button("BROKEN NODE");
                        continue;
                    }
                    //if (isSearchOpened)
                    //{
                    //    // filter 
                    //    if (IsValidRegex(overviewNameFilter) && Regex.Matches(node.name, overviewNameFilter).Count == 0) continue;
                    //}
                    if (GUILayout.Button(node.name))
                    {
                        SelectedNode = node;
                        GUILayout.EndVertical();
                        GUILayout.EndScrollView();
                        GUILayout.EndVertical();
                        return;
                    }
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            GUILayout.Space(10);
            overviewWindowOpen = !GUILayout.Button("Close");
            GUILayout.EndVertical();
        }

        /// <summary>
        /// helper for drawing overview
        /// </summary>
        /// <param name="node"></param>
        /// <param name="drawn"></param>
        /// <param name="indent"></param>
        private void DrawOverview(TreeNode node, List<TreeNode> drawn, int indent)
        {
            if (node == null) return;

            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            if (GUILayout.Button(node.name))
            {
                SelectedNode = node;
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.EndHorizontal();

            drawn.Add(node);
            var children = node.services.Select(s => s.UUID).Union(node.GetChildrenReference().Select(r => r.UUID));
            if (children is null) return;

            foreach (var item in children)
            {
                TreeNode childNode = tree.GetNode(item);
                if (childNode is null) continue;
                if (drawn.Contains(childNode)) continue;
                //if (childNode is Service) continue;
                //childNode.parent = node.uuid;
                DrawOverview(childNode, drawn, indent + setting.overviewHierachyIndentLevel);
                drawn.Add(childNode);
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
            if (unreachables != null && unreachables.Contains(node))
            {
                var textColor = GUI.contentColor;
                GUI.contentColor = Color.red;
                GUILayout.Label("Warning: this node is unreachable");
                GUI.contentColor = textColor;
            }
            else if (selectedNodeParent == null) GUILayout.Label("Tree Head");
            if (nodeDrawer == null || nodeDrawer.Node != node)
                nodeDrawer = new(this, node);

            if (setting.debugMode && selectedNodeParent != null) EditorGUILayout.LabelField("Parent UUID", selectedNodeParent.uuid);
            nodeDrawer.Draw();




            if (!tree.IsServiceCall(node)) DrawNodeService(node);
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            if (setting.debugMode)
            {
                var state = GUI.enabled;
                GUI.enabled = false;
                var script = Resources.FindObjectsOfTypeAll<MonoScript>().FirstOrDefault(n => n.GetClass() == nodeDrawer.drawer.GetType());
                EditorGUILayout.ObjectField("Current Node Drawer", script, typeof(MonoScript), false);
                GUI.enabled = state;
            }
            if (selectedNodeParent == null && SelectedNode.uuid != tree.headNodeUUID && !unreachables.Contains(SelectedNode))
            {
                Debug.LogError($"Node {SelectedNode.name} has a missing parent reference!");
            }
            var option = GUILayout.Toolbar(-1, new string[] { selectedNodeParent == null ? "" : "Open Parent", "Copy Serialized Data", "Delete Node" }, GUILayout.MinHeight(30));
            if (option == 0)
            {
                SelectedNode = selectedNodeParent;
            }
            if (option == 1)
            {
                GUIUtility.systemCopyBuffer = JsonUtility.ToJson(SelectedNode);
            }
            if (option == 2)
            {
                if (EditorUtility.DisplayDialog("Deleting Node", $"Delete the node {node.name} ({node.uuid}) ?", "OK", "Cancel"))
                {
                    var parent = tree.GetNode(node.parent);
                    tree.RemoveNode(node);
                    if (parent != null)
                    {
                        RemoveFromParent(parent, node);
                        SelectedNode = parent;
                    }
                    SelectedNode = tree.Head;
                }
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
                    if (tree.GetNode(treeNode.services[i]) is not Service item)
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
                        if (EditorUtility.DisplayDialog("Delete Service", "Do you want to delete the service from the tree too?", "OK", "Cancel"))
                        {
                            tree.RemoveNode(item);
                        }
                    }
                    var formerGUIStatus = GUI.enabled;
                    if (i == 0) GUI.enabled = false;
                    if (GUILayout.Button("^", GUILayout.MaxWidth(18)))
                    {
                        treeNode.services.RemoveAt(i);
                        treeNode.services.Insert(i - 1, item);
                    }
                    GUI.enabled = formerGUIStatus;
                    if (i == treeNode.services.Count - 1) GUI.enabled = false;
                    if (GUILayout.Button("v", GUILayout.MaxWidth(18)))
                    {
                        treeNode.services.RemoveAt(i);
                        treeNode.services.Insert(i + 1, item);
                    }
                    GUI.enabled = formerGUIStatus;
                    GUILayout.Label(item.GetType().Name);
                    if (GUILayout.Button("Open"))
                    {
                        SelectedNode = item;
                    }
                    GUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }
            if (GUILayout.Button("Add"))
            {
                OpenSelectionWindow(RightWindow.Services, (e) =>
                {
                    treeNode.AddService(e as Service);
                    e.parent = treeNode;
                });
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
            if ((!string.IsNullOrEmpty(rightWindowInputFilter)) && !IsValidRegex(rightWindowInputFilter))
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
                    DrawTypeSelectionWindow(typeof(Amlos.AI.Action), () => rightWindow = RightWindow.All);
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
                    DrawTypeSelectionUnityWindow(() => rightWindow = RightWindow.All);
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
            if (string.IsNullOrEmpty(rightWindowInputFilter)) DrawCreateNewNodeWindow();
            else DrawAllNodeTypeWithMatchesName(rightWindowNameFilter);
            GUILayout.Space(16);
            DrawExistNodeSelectionWindow(typeof(TreeNode));
        }

        private void DrawAllNodeTypeWithMatchesName(string nameFilter)
        {
            var classes = NodeFactory.GetSubclassesOf(typeof(TreeNode));
            foreach (var type in classes.OrderBy(t => t.Name))
            {
                if (type.IsAbstract) continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
                // filter 
                if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(type.Name, nameFilter).Count == 0) continue;

                // set node tip
                var content = new GUIContent(type.Name.ToTitleCase());
                if (Attribute.IsDefined(type, typeof(NodeTipAttribute)))
                {
                    content.tooltip = (Attribute.GetCustomAttribute(type, typeof(NodeTipAttribute)) as NodeTipAttribute).Tip;
                }
                if (GUILayout.Button(content))
                {
                    var n = CreateNode(type);
                    selectEvent?.Invoke(n);
                    rightWindow = RightWindow.None;
                }
            }
            GUILayout.Space(16);
        }

        private void DrawExistNodeSelectionWindow(Type type)
        {
            var nodes = tree.AllNodes.Where(n => n.GetType().IsSubclassOf(type) && n != tree.Head).OrderBy(n => n.name);
            if (nodes.Count() == 0) return;
            GUILayout.Label("Exist Nodes...");
            foreach (var node in nodes)
            {
                //not a valid type
                if (!node.GetType().IsSubclassOf(type)) continue;
                //head
                if (node == tree.Head) continue;
                //select for service but the node is not allowed to appear in a service
                //if (selectedService != null && Attribute.GetCustomAttribute(node.GetType(), typeof(AllowServiceCallAttribute)) == null) continue;
                //filter
                if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(node.name, rightWindowNameFilter).Count == 0) continue;
                if (GUILayout.Button(node.name))
                {
                    TreeNode parent = tree.GetNode(node.parent);
                    if (parent == null || rawReferenceSelect)
                    {
                        if (selectEvent == null)
                        {
                            Debug.LogWarning("No event exist");
                        }
                        selectEvent?.Invoke(node);
                        rightWindow = RightWindow.None;
                        rawReferenceSelect = false;
                    }
                    else if (EditorUtility.DisplayDialog($"Node has a parent already", $"This Node is connecting to {parent.name}, move {(SelectedNode != null ? "under" + SelectedNode.name : "")} ?", "OK", "Cancel"))
                    {
                        var originParent = tree.GetNode(node.parent);
                        if (originParent is not null)
                        {
                            RemoveFromParent(originParent, node);
                        }

                        //if (selectEvent == null)Debug.LogWarning("No event exist");
                        //else Debug.LogWarning("event exist");

                        selectEvent?.Invoke(node);
                        Debug.LogWarning(selectEvent);
                        rightWindow = RightWindow.None;
                        rawReferenceSelect = false;
                    }
                }
            }
        }

        private void DrawCreateNewNodeWindow()
        {
            GUILayout.Label("New...");
            GUILayout.Label("Composites");
            if (SelectFlowNodeType(out Type value))
            {
                TreeNode node = CreateNode(value);
                selectEvent?.Invoke(node);
                rightWindow = RightWindow.None;
            }
            GUILayout.Label("Logics");
            rightWindow = !GUILayout.Button(new GUIContent("Determine...", "A type of nodes that return true/false by determine conditions given")) ? rightWindow : RightWindow.Determines;
            rightWindow = !GUILayout.Button(new GUIContent("Arithmetic...", "A type of nodes that do basic algorithm")) ? rightWindow : RightWindow.Arithmetic;
            GUILayout.Label("Calls");
            rightWindow = !GUILayout.Button(new GUIContent("Calls...", "A type of nodes that calls certain methods")) ? rightWindow : RightWindow.Calls;
            //if (selectedService == null)
            //{
            GUILayout.Label("Actions");
            rightWindow = !GUILayout.Button(new GUIContent("Actions...", "A type of nodes that perform certain actions")) ? rightWindow : RightWindow.Actions;
            GUILayout.Label("Unity");
            rightWindow = !GUILayout.Button(new GUIContent("Unity...", "Calls and action related to Unity")) ? rightWindow : RightWindow.Unity;
            //}
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
                    List<Probability.EventWeight> nodeReferences = (List<Probability.EventWeight>)item.GetValue(parent);
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

        private void DrawTypeSelectionUnityWindow(System.Action typeWindowCloseFunc)
        {
            var classes = new Type[] {
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
                if (type == null) { GUILayout.Space(EditorGUIUtility.singleLineHeight); continue; }
                if (type.IsAbstract) continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
                // filter 
                if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(type.Name, rightWindowNameFilter).Count == 0) continue;
                // set node tip
                var content = new GUIContent(type.Name.ToTitleCase());
                if (Attribute.IsDefined(type, typeof(NodeTipAttribute)))
                {
                    content.tooltip = (Attribute.GetCustomAttribute(type, typeof(NodeTipAttribute)) as NodeTipAttribute).Tip;
                }
                if (GUILayout.Button(content))
                {
                    var n = CreateNode(type);
                    selectEvent?.Invoke(n);
                    typeWindowCloseFunc?.Invoke();
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
        private void DrawTypeSelectionWindow(Type masterType, System.Action typeWindowCloseFunc)
        {
            var classes = NodeFactory.GetSubclassesOf(masterType);
            foreach (var type in classes.OrderBy(t => t.Name))
            {
                if (type.IsAbstract) continue;
                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute))) continue;
                // filter 
                if (IsValidRegex(rightWindowInputFilter) && Regex.Matches(type.Name, rightWindowNameFilter).Count == 0) continue;

                // set node tip
                var content = new GUIContent(type.Name.ToTitleCase());
                if (Attribute.IsDefined(type, typeof(NodeTipAttribute)))
                {
                    content.tooltip = (Attribute.GetCustomAttribute(type, typeof(NodeTipAttribute)) as NodeTipAttribute).Tip;
                }
                if (GUILayout.Button(content))
                {
                    var n = CreateNode(type);
                    selectEvent?.Invoke(n);
                    typeWindowCloseFunc?.Invoke();
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
            EditorGUILayout.LabelField("");
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        public void OpenSelectionWindow(RightWindow window, SelectNodeEvent e)
        {
            rightWindow = window;
            selectEvent = e;
            //Debug.Log("Set event");
        }

        public bool IsValidRegex(string input)
        {
            try
            {
                if (string.IsNullOrEmpty(input)) return false;
                Regex.IsMatch("", input);
                return true;
            }
            catch (ExitGUIException) { throw; }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion



        #endregion

        private void DrawSettings()
        {
            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Settings");
            var currentStatus = GUI.enabled;
            GUI.enabled = true;

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.LabelField("Tree");
            setting.overviewHierachyIndentLevel = EditorGUILayout.IntField("Overview Hierachy Indent", setting.overviewHierachyIndentLevel);
            setting.overviewWindowSize = EditorGUILayout.FloatField("Overview Window Size", setting.overviewWindowSize);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.LabelField("Variable Table");
            setting.variableTableEntryWidth = EditorGUILayout.IntField("Variable Entry Width", setting.variableTableEntryWidth);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.LabelField("Other");
            //bool v = false;// EditorGUILayout.Toggle("Use Raw Drawer", setting.useRawDrawer);
            //if (v != setting.useRawDrawer)
            //{
            //    setting.useRawDrawer = v;
            //    NewTreeSelectUpdate();
            //}
            setting.safeMode = EditorGUILayout.Toggle("Enable Safe Mode", setting.safeMode);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Debug");
            setting.debugMode = GUILayout.Toggle(setting.debugMode, "Debug Mode");

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            GUILayout.Label("Tree");
            if (GUILayout.Button("Clear All Null Reference", GUILayout.Height(30), GUILayout.Width(200)))
                foreach (var node in allNodes) NodeFactory.FillNullField(node);

            if (GUILayout.Button("Refresh Tree Window", GUILayout.Height(30), GUILayout.Width(200)))
            {
                tree.RegenerateTable();
                SelectedNode = tree.Head;
            }
            if (GUILayout.Button("Fix Null Parent issue", GUILayout.Height(30), GUILayout.Width(200)))
            {
                tree.ReLink();
            }

            GUILayout.Label("Graph");
            if (!setting.enableGraph && GUILayout.Button("Enable Graph View", GUILayout.Height(30), GUILayout.Width(200)))
            {
                setting.enableGraph = true;
            }
            if (setting.enableGraph && GUILayout.Button("Disable Graph View", GUILayout.Height(30), GUILayout.Width(200)))
            {
                setting.enableGraph = false;
            }
            if (setting.enableGraph)
            {
                if (GUILayout.Button("Recreate Graph", GUILayout.Height(30), GUILayout.Width(200))) CreateGraph();
            }

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            if (GUILayout.Button("Reset Settings", GUILayout.Height(30), GUILayout.Width(200))) setting = AIEditorSetting.ResetSettings();
            //if (GUILayout.Button("Reshadow"))
            //{
            //    Reshadow();
            //}
            GUI.enabled = currentStatus;
            GUILayout.FlexibleSpace();
            GUIStyle style = new GUIStyle() { richText = true };
            EditorGUILayout.TextField("2022 Minerva Game Studio, Documentation see: <a href=\"https://github.com/Minerva-Studio/Library-of-Meialia-AI/blob/main/DOC_EN.md\">Documentation link</a>", style);
            GUILayout.EndVertical();
        }

        private void DrawAssetReferenceTable()
        {
            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Asset References");
            for (int i = 0; i < tree.assetReferences.Count; i++)
            {
                AssetReferenceData item = tree.assetReferences[i];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("x", GUILayout.Width(EditorGUIUtility.singleLineHeight)))
                {
                    tree.assetReferences.Remove(item);
                    i--;
                    continue;
                }
                EditorGUILayout.LabelField(item.asset.Exist()?.name ?? string.Empty, GUILayout.Width(200));
                EditorGUILayout.ObjectField(tree.GetAsset(item.uuid), typeof(UnityEngine.Object), false);
                EditorGUILayout.LabelField(item.uuid);
                item.uuid = item.asset.Exist() ? AssetReferenceBase.GetUUID(item.asset) : UUID.Empty;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(50);
            if (GUILayout.Button("Clear all unused asset"))
            {
                if (EditorUtility.DisplayDialog("Clear All Unused Asset", "Clear all unused asset?", "OK", "Cancel"))
                    tree.ClearUnusedAssetReference();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void DrawProperties()
        {
            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Properties");

            GUIContent content;
            content = new GUIContent("Target Prefab", "the prefab that ai controls");
            tree.prefab = EditorGUILayout.ObjectField(content, tree.prefab, typeof(GameObject), false) as GameObject;
            content = new GUIContent("Target Script", "the script that ai controls, usually an enemy script");
            tree.targetScript = EditorGUILayout.ObjectField(content, tree.targetScript, typeof(MonoScript), false) as MonoScript;
            content = new GUIContent("Target Animation Controller", "the animation controller of the AI");
            tree.animatorController = EditorGUILayout.ObjectField(content, tree.animatorController, typeof(UnityEditor.Animations.AnimatorController), false) as UnityEditor.Animations.AnimatorController;


            tree.errorHandle = (BehaviourTreeErrorSolution)EditorGUILayout.EnumPopup("Error Handle", tree.errorHandle);
            tree.noActionMaximumDurationLimit = EditorGUILayout.Toggle("Disable Action Time Limit", tree.noActionMaximumDurationLimit);
            if (!tree.noActionMaximumDurationLimit) tree.actionMaximumDuration = EditorGUILayout.FloatField("Maximum Execution Time", tree.actionMaximumDuration);
            GUILayout.EndVertical();
        }


        #region Var Table

        private TypeReferenceDrawer typeDrawer;
        private VariableData selectedVariableData;
        private bool tableDrawDetail;

        private void DrawVariableTable()
        {
            if (tableDrawDetail)
            {
                DrawVariableDetail(selectedVariableData);
                return;
            }

            GUILayout.BeginVertical();
            GUILayoutOption width = GUILayout.MaxWidth(setting.variableTableEntryWidth);
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(setting.variableTableEntryWidth * 3);
            GUILayout.Label("Variable Table");
            GUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.LabelField("Info", width);
            //EditorGUILayout.LabelField("", width);
            GUILayout.Label("Name", width);
            GUILayout.Label("Type", width);
            GUILayout.Label("Default", doubleWidth);
            GUILayout.EndHorizontal();
            //tree.variables.Sort((a, b) => a.name.CompareTo(b.name));
            for (int index = 0; index < tree.variables.Count; index++)
            {
                VariableData item = tree.variables[index];
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("x", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight)))
                {
                    tree.variables.RemoveAt(index);
                    index--;
                    GUILayout.EndHorizontal();
                    continue;
                }
                if (GUILayout.Button(item.Type + ": " + item.name, width))
                {
                    tableDrawDetail = true;
                    selectedVariableData = item;
                }
                item.name = GUILayout.TextField(item.name, width);
                item.SetType((VariableType)EditorGUILayout.EnumPopup(item.Type, width));
                DrawDefaultValue(item);

                //GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            if (tree.variables.Count == 0)
            {
                EditorGUILayout.LabelField("No variable exist");
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add")) tree.variables.Add(new VariableData(tree.GenerateNewVariableName("newVar"), defaultValue: "default"));
            if (tree.variables.Count > 0 && GUILayout.Button("Remove")) tree.variables.RemoveAt(tree.variables.Count - 1);
            GUILayout.Space(50);
            GUILayout.EndVertical();
        }

        private void DrawDefaultValue(VariableData item)
        {
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(setting.variableTableEntryWidth * 3);
            bool i;
            switch (item.Type)
            {
                case VariableType.String:
                    item.defaultValue = GUILayout.TextField(item.defaultValue, doubleWidth);
                    break;
                case VariableType.Int:
                    {
                        i = int.TryParse(item.defaultValue, out int val);
                        if (!i) { val = 0; }
                        item.defaultValue = EditorGUILayout.IntField(val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Float:
                    {
                        i = float.TryParse(item.defaultValue, out float val);
                        if (!i) { val = 0; }
                        item.defaultValue = EditorGUILayout.FloatField(val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Bool:
                    {
                        i = bool.TryParse(item.defaultValue, out bool val);
                        if (!i) { val = false; }
                        item.defaultValue = EditorGUILayout.Toggle(val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Vector2:
                    {
                        i = VectorUtilities.TryParseVector2(item.defaultValue, out Vector2 val);
                        if (!i) { val = default; }
                        item.defaultValue = EditorGUILayout.Vector2Field("", val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Vector3:
                    {
                        i = VectorUtilities.TryParseVector3(item.defaultValue, out Vector3 val);
                        if (!i) { val = default; }
                        item.defaultValue = EditorGUILayout.Vector3Field("", val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Invalid:
                    GUILayout.Label("Invalid Variable Type");
                    break;
                case VariableType.UnityObject:
                    item.typeReference ??= new TypeReference();
                    if (item.typeReference.BaseType is null) item.typeReference.SetBaseType(typeof(UnityEngine.Object));
                    GUILayout.Label(item.typeReference.classFullName, doubleWidth);
                    break;
                case VariableType.Generic:
                    item.typeReference ??= new TypeReference();
                    if (item.typeReference.BaseType is null) item.typeReference.SetBaseType(typeof(object));
                    GUILayout.Label(item.typeReference.classFullName, doubleWidth);
                    break;
                default:
                    GUILayout.Label($" ");
                    break;
            }
        }

        private void DrawVariableDetail(VariableData vd)
        {
            EditorGUILayout.LabelField(vd.Type + ": " + vd.name);
            vd.name = EditorGUILayout.TextField("Name", vd.name);
            vd.SetType((VariableType)EditorGUILayout.EnumPopup("Type", vd.Type));

            if (vd.Type == VariableType.Generic)
            {
                vd.typeReference ??= new();
                vd.typeReference.SetBaseType(typeof(object));
                typeDrawer ??= new TypeReferenceDrawer(vd.typeReference, "Type Reference");
                typeDrawer.Reset(vd.typeReference, "Type Reference");
                typeDrawer.Draw();
            }
            else if (vd.Type == VariableType.UnityObject)
            {
                vd.typeReference ??= new();
                vd.typeReference.SetBaseType(typeof(UnityEngine.Object));
                typeDrawer ??= new TypeReferenceDrawer(vd.typeReference, "Type Reference");
                typeDrawer.Reset(vd.typeReference, "Type Reference");
                typeDrawer.Draw();
            }
            else
            {
                EditorGUILayout.LabelField("Default Value:"); DrawDefaultValue(vd);
            }
            GUILayout.Space(50);
            if (GUILayout.Button("Return", GUILayout.MaxHeight(30), GUILayout.MaxWidth(100)))
            {
                tableDrawDetail = false;
            }
        }
        #endregion






        private void DrawTreeHead()
        {
            SelectNodeEvent selectEvent = (n) => tree.headNodeUUID = n?.uuid ?? UUID.Empty;
            TreeNode head = tree.Head;
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
                    SelectedNode = head;
                }
                else if (GUILayout.Button("Replace"))
                {
                    OpenSelectionWindow(RightWindow.All, selectEvent);
                }
                else if (GUILayout.Button("Delete"))
                {
                    tree.headNodeUUID = UUID.Empty;
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 1;
                GUILayout.BeginVertical();
                var currentStatus = GUI.enabled;
                GUI.enabled = false;
                var script = Resources.FindObjectsOfTypeAll<MonoScript>().FirstOrDefault(n => n.GetClass() == head.GetType());
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





        /// <summary>
        /// Initialize node lists
        /// </summary>
        private void GetAllNode()
        {
            if (!tree)
            {
                return;
            }
            allNodes = tree.AllNodes;
            reachables = GetReachableNodes();
            unreachables = allNodes.Except(reachables).ToList();
        }

        private List<TreeNode> GetReachableNodes()
        {
            List<TreeNode> nodes = new();
            if (tree.Head != null) GetReachableNodes(nodes, tree.Head);
            return nodes;
        }

        private void GetReachableNodes(List<TreeNode> list, TreeNode curr)
        {
            list.Add(curr);
            foreach (var item in curr.GetChildrenReference())
            {
                var node = tree.GetNode(item);
                if (node is not null && !list.Contains(node))
                {
                    GetReachableNodes(list, node);
                }
            }
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
                OpenSelectionWindow(RightWindow.All, (node) =>
                 {
                     SelectedNode = node;
                     tree.headNodeUUID = SelectedNode.uuid;
                 });
            }
            GUILayout.EndVertical();
        }





        public bool SelectFlowNodeType(out Type nodeType)
        {
            var types = NodeFactory.GetSubclassesOf(typeof(Flow));
            foreach (var item in types.OrderBy(t => t.Name))
            {
                if (GUILayout.Button(new GUIContent(item.Name.ToTitleCase(), NodeTypeExtension.GetTip(item.Name))))
                {
                    nodeType = item;
                    return true;
                }
            }
            nodeType = null;
            return false;
        }


        public TreeNode CreateNode(Type nodeType)
        {
            if (nodeType.IsSubclassOf(typeof(TreeNode)))
            {
                TreeNode node = NodeFactory.CreateNode(nodeType);
                tree.AddNode(node);
                node.name = tree.GenerateNewNodeName(node);
                return node;
            }
            throw new ArgumentException($"Type {nodeType} is not a valid type of node");
        }






        private void StartWindow()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
        }

        private void EndWindow()
        {
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUI.enabled = true;
        }





        public override void SaveChanges()
        {
            AssetDatabase.SaveAssetIfDirty(tree);
            base.SaveChanges();
        }

        /// <summary>
        /// A node that only use as a placeholder for AIE
        /// </summary>
        internal class EditorHeadNode : TreeNode
        {
            public NodeReference head = new();

            public override void Execute()
            {
                throw new NotImplementedException();
            }

            public override void Initialize()
            {
                throw new NotImplementedException();
            }
        }
    }
}