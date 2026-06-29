using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
namespace Aethiumian.AI.Editor
{

    public delegate void SelectNodeEvent(TreeNode node);
    public delegate void SelectServiceEvent(Service node);

    /// <summary>
    /// AI editor window
    /// </summary>
    public class AIEditorWindow : EditorWindow
    {
        public enum Window
        {
            nodes,
            graph,
            variables,
            properties
        }
        public enum RightWindow
        {
            None,
            Composite,
            All,
            MenuPaths,
            Determines,
            Actions,
            Calls,
            Services,
            Arithmetic,
            Unity,
        }



        public BehaviourTreeData tree;
        public AIEditorSetting editorSetting;
        public AISetting setting;

        public HashSet<TreeNode> reachableNodes;
        public Window window;

        /// <summary>
        /// Shared node clipboard used by every AI editor window.
        /// </summary>
        public static Clipboard SharedClipboard { get; } = new();

        /// <summary>
        /// Gets the shared node clipboard.
        /// </summary>
        public Clipboard Clipboard => SharedClipboard;
        TreeNodeModule treeWindow;
        VariableTableModule variableTable;
        GraphModule graph;

        private bool undoEventRegistered;
        [SerializeField]
        private bool selectionLocked;

        public IReadOnlyList<TreeNode> AllNodes => tree.EditorNodes;
        public TreeNode SelectedNode
        {
            get => treeWindow?.SelectedNode;
            set { treeWindow?.SelectNode(value); }
        }
        public TreeNode SelectedNodeParent => treeWindow?.SelectedNodeParent;



        #region Window API

        // Menu entry opens an empty editor window instead of reusing a tree-bound window.
        [MenuItem("Window/Aethiumian AI/AI Editor")]
        public static AIEditorWindow ShowWindow()
        {
            if (!TryGetOpenWindow(null, out AIEditorWindow window))
            {
                window = CreateWindow<AIEditorWindow>();
            }

            window.Initialize();
            window.FollowUnitySelection();
            window.UpdateWindowTitle();
            window.Show();
            window.Focus();
            return window;
        }

        /// <summary>
        /// Opens or focuses the AI editor window for the provided behaviour tree.
        /// </summary>
        /// <param name="data">The behaviour tree to edit.</param>
        /// <returns>The editor window bound to the requested tree.</returns>
        public static AIEditorWindow ShowWindow(BehaviourTreeData data)
        {
            if (!data)
            {
                return ShowWindow();
            }

            if (!TryGetOpenWindow(data, out AIEditorWindow window))
            {
                window = CreateWindow<AIEditorWindow>();
                window.Load(data);
            }
            else
            {
                window.Initialize();
                window.UpdateWindowTitle();
            }

            window.Show();
            window.Focus();
            return window;
        }

        /// <summary>
        /// Opens the editor for a tree and selects the requested node.
        /// </summary>
        /// <param name="data">The behaviour tree that owns the node.</param>
        /// <param name="node">The node to select.</param>
        /// <returns>The editor window used for the request.</returns>
        public static AIEditorWindow OpenNode(BehaviourTreeData data, TreeNode node)
        {
            AIEditorWindow window = ShowWindow(data);
            window.Initialize();
            window.window = Window.nodes;
            if (node != null)
            {
                window.SelectedNode = node;
            }

            window.Focus();
            return window;
        }

        /// <summary>
        /// Opens a node selection pane in the editor window for a tree.
        /// </summary>
        /// <param name="data">The behaviour tree used by the selection request.</param>
        /// <param name="rightWindow">The selection pane type.</param>
        /// <param name="callback">Callback invoked when a node is selected.</param>
        /// <param name="isRawSelect">True when selection should not alter tree parent structure.</param>
        /// <returns>The editor window used for the request.</returns>
        public static AIEditorWindow RequestNodeSelection(BehaviourTreeData data, RightWindow rightWindow, SelectNodeEvent callback, bool isRawSelect = false)
        {
            if (!data || callback == null)
            {
                return null;
            }

            AIEditorWindow window = ShowWindow(data);
            window.Initialize();
            window.window = Window.nodes;
            window.OpenSelectionWindow(rightWindow, callback, isRawSelect);
            window.Focus();
            return window;
        }

        /// <summary>
        /// Try find an open editor window for the requested tree.
        /// </summary>
        /// <param name="data">The tree to match, or null for an empty editor window.</param>
        /// <param name="window">The matching editor window.</param>
        /// <returns>True when a matching open window exists.</returns>
        public static bool TryGetOpenWindow(BehaviourTreeData data, out AIEditorWindow window)
        {
            AIEditorWindow[] windows = Resources.FindObjectsOfTypeAll<AIEditorWindow>();
            foreach (AIEditorWindow candidate in windows)
            {
                if (!candidate)
                {
                    continue;
                }

                if (candidate.tree == data)
                {
                    window = candidate;
                    return true;
                }
            }

            window = null;
            return false;
        }

        #endregion

        #region Unity Lifecycle

        private void OnGUI()
        {
            Initialize();
            using (new GUILayout.HorizontalScope())
            using (new GUILayout.VerticalScope())
            {
                GetAllNode();

                if (tree && window == Window.graph)
                {
                    graph.DrawGraph();
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    DrawWindowToolbar();
                }

                DrawBehaviourTreeSelection();

                using (new EditorGUI.DisabledScope(editorSetting.safeMode))
                {
                    switch (window)
                    {
                        case Window.nodes:
                            treeWindow.DrawTree();
                            break;
                        case Window.variables:
                            variableTable.DrawVariableTable();
                            break;
                        case Window.properties:
                            DrawProperties();
                            break;
                        default:
                            break;
                    }
                }

                if (window != Window.variables)
                {
                    variableTable.Reset();
                }
            }

            if (GUI.changed) Repaint();
        }

        public override void SaveChanges()
        {
            if (tree) AssetDatabase.SaveAssetIfDirty(tree);
            base.SaveChanges();
        }

        private void OnValidate()
        {
            SaveChanges();
        }

        private void OnLostFocus()
        {
            Undo.undoRedoPerformed -= Refresh;
            undoEventRegistered = false;
            SaveChanges();
        }

        private void OnFocus()
        {
            if (!undoEventRegistered)
            {
                undoEventRegistered = true;
                Undo.undoRedoPerformed += Refresh;
            }
        }

        private void OnSelectionChange()
        {
            FollowUnitySelection();
            Repaint();
        }

        private void Awake()
        {
            UpdateWindowTitle();
            if (!undoEventRegistered)
            {
                undoEventRegistered = true;
                Undo.undoRedoPerformed += Refresh;
            }
        }

        private void OnDestroy()
        {
            Undo.undoRedoPerformed -= Refresh;
            undoEventRegistered = false;
        }

        #endregion

        #region Initialization And Tree State

        public void Load(BehaviourTreeData data)
        {
            Initialize();
            SetSelectedTree(data);
        }

        /// <summary>
        /// Refresh the window (re-init, rebuild table, get all nodes)
        /// </summary>
        public void Refresh()
        {
            Initialize();
            treeWindow.isRawReferenceSelect = false;
            if (tree)
            {
                tree.RegenerateTable();
                GetAllNode();
            }
        }

        private void Initialize()
        {
            editorSetting = AIEditorSetting.GetOrCreateSettings();
            setting = AISetting.GetOrCreateSettings();

            treeWindow ??= new();
            treeWindow.Initialize(this);

            graph ??= new();
            graph.Initialize(this);

            variableTable ??= new();
            variableTable.Initialize(this);


            if (tree) EditorUtility.SetDirty(tree);
        }

        /// <summary>
        /// Updates the window title to identify the edited behaviour tree.
        /// </summary>
        private void UpdateWindowTitle()
        {
            string title = tree ? tree.name : "AI Editor";
            AIEditorTitleContent.ApplyEditorTitle(this, title);
        }

        /// <summary>
        /// Updates the currently selected behaviour tree.
        /// </summary>
        /// <param name="newTree">The newly selected behaviour tree asset.</param>
        /// <returns>No return value.</returns>
        private void SetSelectedTree(BehaviourTreeData newTree)
        {
            if (newTree == tree)
            {
                UpdateWindowTitle();
                return;
            }

            tree = newTree;
            if (newTree)
            {
                EditorUtility.ClearDirty(tree);
                EditorUtility.SetDirty(tree);
                GetAllNode();
                SelectedNode = tree.Head;
            }
            else
            {
                tree = null;
            }

            UpdateWindowTitle();
        }

        /// <summary>
        /// Gets or sets whether this editor follows the active Unity selection.
        /// </summary>
        internal bool SelectionLocked
        {
            get => selectionLocked;
            set => selectionLocked = value;
        }

        /// <summary>
        /// Updates the selected tree from Unity's active selection when this window is unlocked.
        /// </summary>
        internal void FollowUnitySelection()
        {
            if (selectionLocked)
            {
                return;
            }

            if (TryGetSelectedTreeFromUnitySelection(out BehaviourTreeData selectedTree))
            {
                SetSelectedTree(selectedTree);
            }
        }

        /// <summary>
        /// Resolves the behaviour tree represented by the current Unity selection.
        /// </summary>
        /// <param name="selectedTree">The resolved behaviour tree, when a valid selection exists.</param>
        /// <returns>True when the active selection maps to a behaviour tree.</returns>
        private static bool TryGetSelectedTreeFromUnitySelection(out BehaviourTreeData selectedTree)
        {
            UnityEngine.Object activeObject = Selection.activeObject;
            if (activeObject is BehaviourTreeData treeData)
            {
                selectedTree = treeData;
                return selectedTree;
            }

            if (activeObject is AI aiComponent)
            {
                selectedTree = aiComponent.Data;
                return selectedTree;
            }

            GameObject selectedGameObject = Selection.activeGameObject;
            if (!selectedGameObject && activeObject is GameObject objectAsset)
            {
                selectedGameObject = objectAsset;
            }

            if (selectedGameObject)
            {
                selectedTree = selectedGameObject.GetComponent<AI>()?.Data;
                return selectedTree;
            }

            selectedTree = null;
            return false;
        }

        #endregion

        #region Drawing

        /// <summary>
        /// Draws the toolbar header with behaviour tree selection and window tabs.
        /// </summary>
        /// <returns>True when a behaviour tree is selected; otherwise false.</returns>
        private bool DrawBehaviourTreeSelection()
        {
            var tree = EditorGUILayout.ObjectField("Behaviour Tree", this.tree, typeof(BehaviourTreeData), false) as BehaviourTreeData;
            if (tree != this.tree)
            {
                SetSelectedTree(tree);
            }
            return tree != null;
        }

        /// <summary>
        /// Draws the window selection tabs inside the header toolbar.
        /// </summary>
        /// <returns>No return value.</returns>
        private void DrawWindowToolbar()
        {
            if (!Enum.IsDefined(typeof(Window), window))
            {
                window = Window.nodes;
            }

            if (editorSetting.enableGraph)
            {
                window = (Window)EditorGUILayout.Popup(
                    (int)window,
                    new[] { "Tree", "Graph", "Variable Table", "Tree Properties" },
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(120f));
            }
            else
            {
                if (window == Window.graph)
                {
                    window = Window.nodes;
                }

                int selectedWindow = window switch
                {
                    Window.nodes => 0,
                    Window.variables => 1,
                    Window.properties => 2,
                    _ => 0,
                };

                selectedWindow = EditorGUILayout.Popup(
                    selectedWindow,
                    new[] { "Tree", "Variable Table", "Tree Properties" },
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(120f));

                window = selectedWindow switch
                {
                    1 => Window.variables,
                    2 => Window.properties,
                    _ => Window.nodes,
                };
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                Refresh();
            }

            if (GUILayout.Button(new GUIContent("Settings", "Open AI Editor Preferences."), EditorStyles.toolbarButton))
            {
                AIEditorPreferenceProvider.OpenPreferences();
            }

            Rect maintenanceRect = GUILayoutUtility.GetRect(new GUIContent(""), EditorStyles.toolbarDropDown);
            if (EditorGUI.DropdownButton(maintenanceRect, new GUIContent(""), FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                ShowMaintenanceMenu(maintenanceRect);
            }

            GUIContent lockContent = new(string.Empty, "Lock the selected behaviour tree.");
            selectionLocked = GUILayout.Toggle(selectionLocked, lockContent, "IN LockButton", GUILayout.Width(20f));
        }

        /// <summary>
        /// Shows the maintenance dropdown menu in the toolbar.
        /// </summary>
        /// <param name="buttonRect">The rect of the dropdown button for anchoring.</param>
        /// <returns>No return value.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        private void ShowMaintenanceMenu(Rect buttonRect)
        {
            GenericMenu menu = new();
            bool hasTree = tree != null;

            menu.AddItem(new GUIContent("Refresh"), false, () =>
            {
                Refresh();
            });

            if (hasTree)
            {
                menu.AddItem(new GUIContent("Upgrade All"), false, () =>
                {
                    UpradeAllNode();
                });

                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Clear All Null Reference"), false, () =>
                {
                    foreach (var node in AllNodes) NodeFactory.FillNull(node);
                });

                menu.AddItem(new GUIContent("Fix Null Parent issue"), false, () =>
                {
                    tree.Relink();
                });

                if (editorSetting.enableGraph)
                {
                    menu.AddItem(new GUIContent("Recreate Graph"), false, () =>
                    {
                        graph.CreateGraph();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Recreate Graph"));
                }

                int unusedNodeCount = GetUnusedNodes().Count;
                if (unusedNodeCount > 0)
                {
                    menu.AddItem(new GUIContent($"Delete All Unused Nodes ({unusedNodeCount})"), false, () =>
                    {
                        DeleteAllUnusedNodes();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Delete All Unused Nodes"));
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Upgrade All"));
                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("Clear All Null Reference"));
                menu.AddDisabledItem(new GUIContent("Fix Null Parent issue"));
                menu.AddDisabledItem(new GUIContent("Recreate Graph"));
                menu.AddDisabledItem(new GUIContent("Delete All Unused Nodes"));
            }

            menu.AddSeparator("");
            string clipboardRoot = Clipboard.HasContent ? Clipboard.treeNodes[0]?.name ?? "None" : "None";
            menu.AddDisabledItem(new GUIContent($"Shared Clipboard: {Clipboard.Count} node(s), root: {clipboardRoot}"));
            if (Clipboard.HasContent)
            {
                menu.AddItem(new GUIContent("Clear Shared Clipboard"), false, Clipboard.Clear);
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Clear Shared Clipboard"));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Open In Folder"), false, () =>
            {
                if (tree)
                {
                    string path = AssetDatabase.GetAssetPath(tree);
                    EditorUtility.RevealInFinder(path);
                }
                else
                    EditorUtility.DisplayDialog("No Tree Selected", "Please select a behaviour tree to open its folder.", "OK");
            });
            menu.AddItem(new GUIContent("Open In External Editor"), false, () =>
            {
                if (tree)
                    AssetDatabase.OpenAsset(tree);
                else
                    EditorUtility.DisplayDialog("No Tree Selected", "Please select a behaviour tree to open it in external editor.", "OK");

            });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Debug"), editorSetting.debugMode, () =>
            {
                editorSetting.debugMode = !editorSetting.debugMode;
                AIEditorSetting.SaveSettings(editorSetting);
            });

            menu.DropDown(buttonRect);
        }

        private void DrawProperties()
        {
            using (new EditorGUI.IndentLevelScope(1))
            using (new GUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);
                if (!tree)
                {
                    DrawNewBTWindow();
                    return;
                }

                GUIContent content;
                content = new GUIContent("Target Prefab", "the prefab that ai controls");
                tree.prefab = EditorGUILayout.ObjectField(content, tree.prefab, typeof(GameObject), false) as GameObject;
                content = new GUIContent("Target Script", "the script that ai controls, usually an enemy script");
                tree.targetScript = EditorGUILayout.ObjectField(content, tree.targetScript, typeof(MonoScript), false) as MonoScript;
                content = new GUIContent("Target Animation Controller", "the animation controller of the AI");
                tree.animatorController = EditorGUILayout.ObjectField(content, tree.animatorController, typeof(UnityEditor.Animations.AnimatorController), false) as UnityEditor.Animations.AnimatorController;
                tree.noActionMaximumDurationLimit = EditorGUILayout.Toggle("Disable Action Time Limit", tree.noActionMaximumDurationLimit);
                if (!tree.noActionMaximumDurationLimit) tree.actionMaximumDuration = EditorGUILayout.FloatField("Maximum Execution Time", tree.actionMaximumDuration);

                Header("Error handle");
                tree.treeErrorHandle = (BehaviourTreeErrorSolution)EditorGUILayout.EnumPopup("Tree Error Handle", tree.treeErrorHandle);
                tree.nodeErrorHandle = (NodeErrorSolution)EditorGUILayout.EnumPopup("Node Error Handle", tree.nodeErrorHandle);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Header(string title, bool space = true)
        {
            if (space) EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        #endregion

        #region Tree Asset Creation

        public void DrawNewBTWindow()
        {
            SelectedNode = null;
            // Open Save panel and save it
            if (!GUILayout.Button("Create New Behaviour Tree", GUILayout.MinHeight(30))) return;

            var path = EditorUtility.SaveFilePanel("New Behaviour Tree", "", "AI_NewBehaviourTree.asset", "asset");
            if (path == "") return;

            var behaviourTree = CreateInstance<BehaviourTreeData>();
            var p = Application.dataPath;
            AssetDatabase.CreateAsset(behaviourTree, "Assets" + path[p.Length..path.Length]);
            AssetDatabase.Refresh();
            tree = behaviourTree;
            window = Window.properties;
            UpdateWindowTitle();


            if (Selection.activeGameObject)
            {
                var aI = Selection.activeGameObject.GetComponent<AI>();
                if (!aI)
                {
                    aI = Selection.activeGameObject.AddComponent<AI>();
                }
                if (!aI.Data)
                {
                    aI.Data = behaviourTree;
                }
            }
        }

        #endregion

        #region Node Cache And Maintenance

        /// <summary>
        /// Initialize node lists
        /// </summary>
        private void GetAllNode()
        {
            if (!tree) return;

            reachableNodes ??= new();
            reachableNodes.Clear();
            if (treeWindow != null) treeWindow.overviewCache = null;
            GetReachableNodes(reachableNodes, tree.Head);
        }

        private void GetReachableNodes(HashSet<TreeNode> list, TreeNode curr)
        {
            if (curr == null) return;
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

        private List<TreeNode> GetUnusedNodes()
        {
            GetAllNode();
            return AllNodes
                .Where(node => node != null && !reachableNodes.Contains(node))
                .ToList();
        }

        private void DeleteAllUnusedNodes()
        {
            List<TreeNode> unusedNodes = GetUnusedNodes();
            if (unusedNodes.Count == 0)
            {
                EditorUtility.DisplayDialog("Delete All Unused Nodes", "No unused nodes found.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete All Unused Nodes",
                    $"Delete {unusedNodes.Count} unused node(s) from {tree.name}?",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            bool shouldResetSelection = SelectedNode != null && unusedNodes.Contains(SelectedNode);
            HashSet<Minerva.Module.UUID> removedNodeUUIDs = new(unusedNodes.Select(node => node.uuid));

            // Record one undo step for the entire cleanup instead of one step per node.
            Undo.RecordObject(tree, "Delete All Unused Nodes");
            foreach (var node in unusedNodes)
            {
                tree.Remove(node, false);
            }

            graph.RemoveNodes(removedNodeUUIDs);
            tree.RegenerateTable();
            EditorUtility.SetDirty(tree);
            Refresh();

            if (shouldResetSelection)
            {
                SelectedNode = tree.Head;
            }
        }

        #endregion

        #region Module Operations

        public void OpenSelectionWindow(RightWindow window, SelectNodeEvent e, bool isRawSelect = false)
        {
            treeWindow?.OpenSelectionWindow(window, e, isRawSelect);
        }

        internal bool TryDeleteNode(TreeNode childNode)
        {
            return treeWindow?.TryDeleteNode(childNode) == true;
        }

        /// <summary>
        /// Attempts to upgrade the provided node via the tree module.
        /// </summary>
        /// <param name="node">The node to upgrade.</param>
        /// <param name="prompt">Whether to show a confirmation dialog.</param>
        /// <returns><c>true</c> if the upgrade succeeded; otherwise, <c>false</c>.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        internal bool TryUpgradeNode(TreeNode node, bool prompt = true)
        {
            if (node == null)
            {
                return false;
            }

            return treeWindow?.TryUpgradeNode(node, prompt) == true;
        }

        /// <summary>
        /// Upgrades all nodes in the tree to the latest version if they are eligible for an upgrade.
        /// </summary>
        /// <remarks>This method records an undo operation for the upgrade process. It iterates through
        /// all nodes and attempts to upgrade each one that meets the upgrade criteria. A dialog is displayed upon
        /// completion, indicating the number of nodes upgraded or that all nodes are already up to date.</remarks>
        internal void UpradeAllNode()
        {
            Undo.RecordObject(tree, "Upgrade All Nodes");
            int upgradedCount = 0;
            foreach (var node in AllNodes.ToArray())
            {
                if (node.CanUpgrade())
                {
                    TryUpgradeNode(node, false);
                    upgradedCount++;
                }
            }
            if (upgradedCount > 0)
            {
                EditorUtility.DisplayDialog("Upgrade Completed", $"Upgraded {upgradedCount} nodes to the latest version.", "OK");
                Refresh();
            }
            else
            {
                EditorUtility.DisplayDialog("Upgrade Completed", "All nodes are already up to date.", "OK");
            }
        }

        #endregion
    }
}
