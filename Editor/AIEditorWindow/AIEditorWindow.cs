using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
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
            Nodes,
            Graph,
            Variables,
            Properties
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
        private static readonly Vector2 EditorWindowMinSize = new(760f, 420f);
        private const float CompactToolbarWidth = 900f;

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

            window.minSize = EditorWindowMinSize;
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
                window.minSize = EditorWindowMinSize;
                window.Load(data);
            }
            else
            {
                window.minSize = EditorWindowMinSize;
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
            window.window = Window.Nodes;
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
            window.window = Window.Nodes;
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

                if (tree && window == Window.Graph)
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
                        case Window.Nodes:
                            treeWindow.DrawTree();
                            break;
                        case Window.Variables:
                            variableTable.DrawVariableTable();
                            break;
                        case Window.Properties:
                            DrawProperties();
                            break;
                        default:
                            break;
                    }
                }

                if (window != Window.Variables)
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

            if (selectedGameObject && selectedGameObject.TryGetComponent(out AI ai))
            {
                selectedTree = ai.Data;
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
                window = Window.Nodes;
            }

            if (!editorSetting.enableGraph && window == Window.Graph)
            {
                window = Window.Nodes;
            }

            bool compact = UseCompactToolbar(EditorGUIUtility.currentViewWidth);
            DrawWindowButton(Window.Nodes, new GUIContent("Nodes", "Show behaviour tree and nodes"), compact);
            if (editorSetting.enableGraph)
            {
                DrawWindowButton(Window.Graph, new GUIContent("Graph", "Show behaviour tree graph"), compact);
            }

            DrawWindowButton(Window.Variables, new GUIContent(compact ? "Vars" : "Variables", "Show variables table"), compact);
            DrawWindowButton(Window.Properties, new GUIContent(compact ? "Props" : "Properties", "Show behaviour tree properties"), compact);

            GUILayout.FlexibleSpace();

            DrawUpgradeToolbarButton(compact);
            DrawClipboardToolbarButton(compact);

            if (GUILayout.Button(GetRefreshButtonContent(compact), EditorStyles.toolbarButton))
            {
                Refresh();
            }

            if (GUILayout.Button(GetSettingsButtonContent(compact), EditorStyles.toolbarButton))
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
        /// Draws one window selector button while keeping the original four-button toolbar shape.
        /// </summary>
        /// <param name="targetWindow">The editor window mode selected by the button.</param>
        /// <param name="content">The button text and tooltip.</param>
        /// <param name="compact">Whether the button should use a tighter width.</param>
        private void DrawWindowButton(Window targetWindow, GUIContent content, bool compact)
        {
            float width = compact ? 56f : 80f;
            if (GUILayout.Toggle(window == targetWindow, content, EditorStyles.toolbarButton, GUILayout.Width(width)))
            {
                window = targetWindow;
            }
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

            menu.AddSeparator("");
            AddTreeOpenMenuItems(menu);

            menu.AddSeparator("");
            if (hasTree)
            {
                int upgradableNodeCount = CountUpgradableNodes();
                if (upgradableNodeCount > 0)
                {
                    menu.AddItem(new GUIContent($"Upgrade All ({upgradableNodeCount})"), false, () =>
                    {
                        UpradeAllNode();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Upgrade All"));
                }

                menu.AddItem(new GUIContent("Clear All Null Reference"), false, () =>
                {
                    foreach (var node in AllNodes) NodeFactory.FillNull(node);
                });

                menu.AddItem(new GUIContent("Fix Null Parent Issue"), false, () =>
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
                menu.AddDisabledItem(new GUIContent("Clear All Null Reference"));
                menu.AddDisabledItem(new GUIContent("Fix Null Parent Issue"));
                menu.AddDisabledItem(new GUIContent("Recreate Graph"));
                menu.AddDisabledItem(new GUIContent("Delete All Unused Nodes"));
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Debug"), editorSetting.debugMode, () =>
            {
                editorSetting.debugMode = !editorSetting.debugMode;
                AIEditorSetting.SaveSettings(editorSetting);
            });

            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// Draws a quick upgrade button when at least one node supports upgrading.
        /// </summary>
        private void DrawUpgradeToolbarButton(bool compact)
        {
            int upgradableNodeCount = CountUpgradableNodes();
            if (upgradableNodeCount <= 0)
            {
                return;
            }

            GUIContent content = GetUpgradeButtonContent(upgradableNodeCount, compact);
            if (GUILayout.Button(content, EditorStyles.toolbarButton))
            {
                UpradeAllNode();
            }
        }

        /// <summary>
        /// Draws the shared clipboard toolbar button and opens its small menu when it has content.
        /// </summary>
        private void DrawClipboardToolbarButton(bool compact)
        {
            bool hasClipboard = Clipboard.HasContent;
            string tooltip = Clipboard.GetStatusText();
            GUIContent clipboardContent = GetClipboardButtonContent(Clipboard.Count, hasClipboard, compact, tooltip);

            using (new EditorGUI.DisabledScope(!hasClipboard))
            {
                if (GUILayout.Button(clipboardContent, EditorStyles.toolbarButton))
                {
                    ShowClipboardMenu(GUILayoutUtility.GetLastRect());
                }
            }
        }

        internal static bool UseCompactToolbar(float viewWidth)
        {
            return viewWidth < CompactToolbarWidth;
        }

        internal static GUIContent GetUpgradeButtonContent(int upgradableNodeCount, bool compact)
        {
            string prefix = compact ? "Up" : "Upgrade";
            return new GUIContent($"{prefix} ({upgradableNodeCount})", $"Upgrade {upgradableNodeCount} node(s) to the latest version.");
        }

        internal static GUIContent GetClipboardButtonContent(int count, bool hasContent, bool compact, string statusText)
        {
            string label = compact ? "Clip" : "Clipboard";
            return new GUIContent(hasContent ? $"{label} ({count})" : label, statusText);
        }

        internal static GUIContent GetRefreshButtonContent(bool compact)
        {
            return new GUIContent(compact ? "Ref" : "Refresh", "Refresh the AI editor.");
        }

        internal static GUIContent GetSettingsButtonContent(bool compact)
        {
            return new GUIContent(compact ? "Prefs" : "Settings", "Open AI Editor Preferences.");
        }

        /// <summary>
        /// Shows clipboard status and maintenance actions.
        /// </summary>
        /// <param name="buttonRect">The rect of the toolbar button for anchoring.</param>
        private void ShowClipboardMenu(Rect buttonRect)
        {
            GenericMenu menu = new();
            menu.AddDisabledItem(new GUIContent(Clipboard.GetStatusText()));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Clear Clipboard"), false, Clipboard.Clear);
            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// Counts upgradable nodes in the current tree.
        /// </summary>
        /// <returns>The number of nodes that support upgrade.</returns>
        private int CountUpgradableNodes()
        {
            return tree ? AllNodes.Count(node => node != null && node.CanUpgrade()) : 0;
        }

        /// <summary>
        /// Adds tree asset open and locate actions to the dropdown menu.
        /// </summary>
        /// <param name="menu">The dropdown menu to append to.</param>
        private void AddTreeOpenMenuItems(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Open Containing Folder"), false, OpenTreeContainingFolder);
            menu.AddItem(new GUIContent("Reveal Asset in Explorer"), false, RevealTreeAssetInExplorer);
            menu.AddItem(new GUIContent("Open In External Editor"), false, OpenTreeInExternalEditor);
            menu.AddItem(new GUIContent("Open In Unity Inspector"), false, OpenTreeInUnityInspector);
        }

        /// <summary>
        /// Opens the folder that contains the selected tree asset.
        /// </summary>
        private void OpenTreeContainingFolder()
        {
            if (!TryGetTreeAssetPaths(out _, out _, out string folderPath))
            {
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                EditorUtility.DisplayDialog("Folder Not Found", $"The behaviour tree folder does not exist:\n{folderPath}", "OK");
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
            });
        }

        /// <summary>
        /// Reveals the selected tree asset in the system file browser.
        /// </summary>
        private void RevealTreeAssetInExplorer()
        {
            if (!TryGetTreeAssetPaths(out string assetPath, out _, out _))
            {
                return;
            }

            EditorUtility.RevealInFinder(assetPath);
        }

        /// <summary>
        /// Opens the selected tree asset file through Unity's configured external editor.
        /// </summary>
        private void OpenTreeInExternalEditor()
        {
            if (!TryGetTreeAssetPaths(out _, out string fullPath, out _))
            {
                return;
            }

            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("File Not Found", $"The behaviour tree asset file does not exist:\n{fullPath}", "OK");
                return;
            }

            // Use Unity's external script editor bridge so the user's configured editor handles the asset file.
            UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, 1);
        }

        /// <summary>
        /// Opens the selected tree through Unity's asset opening path.
        /// </summary>
        private void OpenTreeInUnityInspector()
        {
            if (!TryGetTreeAssetPaths(out _, out _, out _))
            {
                return;
            }

            AssetDatabase.OpenAsset(tree);
        }

        /// <summary>
        /// Resolves the selected tree asset paths used by open and locate menu commands.
        /// </summary>
        /// <param name="assetPath">The Unity project-relative asset path.</param>
        /// <param name="fullPath">The full disk path to the asset file.</param>
        /// <param name="folderPath">The full disk path to the asset's containing folder.</param>
        /// <returns>True when the tree has a valid asset path.</returns>
        private bool TryGetTreeAssetPaths(out string assetPath, out string fullPath, out string folderPath)
        {
            if (!tree)
            {
                assetPath = null;
                fullPath = null;
                folderPath = null;
                EditorUtility.DisplayDialog("No Tree Selected", "Please select a behaviour tree asset first.", "OK");
                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(tree);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                fullPath = null;
                folderPath = null;
                EditorUtility.DisplayDialog("Asset Path Not Found", "The selected behaviour tree is not saved as a project asset.", "OK");
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                fullPath = null;
                folderPath = null;
                EditorUtility.DisplayDialog("Project Path Not Found", "Unity project root path could not be resolved.", "OK");
                return false;
            }

            // Unity asset paths always use '/', while System.IO expects the platform separator.
            string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            folderPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folderPath))
            {
                return true;
            }

            EditorUtility.DisplayDialog("Folder Path Not Found", "The behaviour tree folder path could not be resolved.", "OK");
            return false;
        }

        private void DrawProperties()
        {
            if (!tree)
            {
                DrawNewBTWindow();
                return;
            }
            // using (new EditorGUI.IndentLevelScope(1))
            using (new GUILayout.VerticalScope())
            {
                Header("Properties");

                GUIContent content;
                content = new GUIContent("Target Prefab", "the prefab that ai controls");
                tree.prefab = EditorGUILayout.ObjectField(content, tree.prefab, typeof(GameObject), false) as GameObject;
                content = new GUIContent("Target Script", "the script that ai controls, usually an enemy script");
                tree.targetScript = EditorGUILayout.ObjectField(content, tree.targetScript, typeof(MonoScript), false) as MonoScript;
                content = new GUIContent("Target Animation Controller", "the animation controller of the AI");
                tree.animatorController = EditorGUILayout.ObjectField(content, tree.animatorController, typeof(UnityEditor.Animations.AnimatorController), false) as UnityEditor.Animations.AnimatorController;
                tree.noActionMaximumDurationLimit = EditorGUILayout.Toggle("Disable Action Time Limit", tree.noActionMaximumDurationLimit);
                if (!tree.noActionMaximumDurationLimit) tree.actionMaximumDuration = EditorGUILayout.FloatField("Maximum Execution Time", tree.actionMaximumDuration);

                Header("Error Handle");
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
            window = Window.Properties;
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
