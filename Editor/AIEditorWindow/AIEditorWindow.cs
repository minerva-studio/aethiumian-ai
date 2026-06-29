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


            if (GUILayout.Button(new GUIContent("Nodes", "Show behaviour tree and nodes"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                window = Window.Nodes;
            }
            if (editorSetting.enableGraph && GUILayout.Button(new GUIContent("Graph", "Show behaviour tree graph"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                window = Window.Graph;
            }
            if (GUILayout.Button(new GUIContent("Variables", "Show variables table"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                window = Window.Variables;
            }
            if (GUILayout.Button(new GUIContent("Properties", "Show behaviour tree properties"), EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                window = Window.Properties;
            }

            GUILayout.FlexibleSpace();

            DrawUpgradeToolbarButton();
            DrawClipboardToolbarButton();

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

            menu.AddSeparator("");
            AddTreeOpenMenuItems(menu);

            menu.AddSeparator("");
            if (hasTree)
            {
                int upgradableNodeCount = GetUpgradableNodeCount();
                if (upgradableNodeCount > 0)
                {
                    menu.AddItem(GetUpgradeButtonContent(upgradableNodeCount, "Upgrade All"), false, () =>
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
        private void DrawUpgradeToolbarButton()
        {
            int upgradableNodeCount = GetUpgradableNodeCount();
            if (upgradableNodeCount <= 0)
            {
                return;
            }

            if (GUILayout.Button(GetUpgradeButtonContent(upgradableNodeCount), EditorStyles.toolbarButton))
            {
                UpradeAllNode();
            }
        }

        /// <summary>
        /// Draws the shared clipboard toolbar button and opens its small menu when it has content.
        /// </summary>
        private void DrawClipboardToolbarButton()
        {
            GUIContent clipboardContent = GetClipboardButtonContent(Clipboard);
            using (new EditorGUI.DisabledScope(!Clipboard.HasContent))
            {
                if (GUILayout.Button(clipboardContent, EditorStyles.toolbarButton))
                {
                    ShowClipboardMenu(GUILayoutUtility.GetLastRect());
                }
            }
        }

        /// <summary>
        /// Shows clipboard status and maintenance actions.
        /// </summary>
        /// <param name="buttonRect">The rect of the toolbar button for anchoring.</param>
        private void ShowClipboardMenu(Rect buttonRect)
        {
            GenericMenu menu = new();
            menu.AddDisabledItem(GetClipboardStatusContent(Clipboard));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Clear Clipboard"), false, Clipboard.Clear);
            menu.DropDown(buttonRect);
        }

        /// <summary>
        /// Counts nodes in the current tree that can be upgraded.
        /// </summary>
        /// <returns>The number of nodes that currently support upgrade.</returns>
        internal int GetUpgradableNodeCount()
        {
            return tree ? GetUpgradableNodeCount(AllNodes) : 0;
        }

        /// <summary>
        /// Counts nodes that can be upgraded without caching UI state.
        /// </summary>
        /// <param name="nodes">The node list to inspect.</param>
        /// <returns>The number of nodes that currently support upgrade.</returns>
        internal static int GetUpgradableNodeCount(IEnumerable<TreeNode> nodes)
        {
            return nodes?.Count(node => node != null && node.CanUpgrade()) ?? 0;
        }

        /// <summary>
        /// Builds the upgrade button text with the current upgrade count.
        /// </summary>
        /// <param name="count">The number of upgradable nodes.</param>
        /// <param name="prefix">The button text prefix.</param>
        /// <returns>The toolbar or menu content for the upgrade action.</returns>
        internal static GUIContent GetUpgradeButtonContent(int count, string prefix = "Upgrade")
        {
            return new GUIContent($"{prefix} ({count})", $"Upgrade {count} node(s) to the latest version.");
        }

        /// <summary>
        /// Builds the shared clipboard button text.
        /// </summary>
        /// <param name="clipboard">The clipboard to describe.</param>
        /// <returns>The toolbar content for the clipboard button.</returns>
        internal static GUIContent GetClipboardButtonContent(Clipboard clipboard)
        {
            int count = clipboard?.HasContent == true ? clipboard.Count : 0;
            string text = count > 0 ? $"Clipboard ({count})" : "Clipboard";
            return new GUIContent(text, GetClipboardStatusText(clipboard));
        }

        /// <summary>
        /// Builds the shared clipboard status line.
        /// </summary>
        /// <param name="clipboard">The clipboard to describe.</param>
        /// <returns>The menu content for clipboard status.</returns>
        internal static GUIContent GetClipboardStatusContent(Clipboard clipboard)
        {
            return new GUIContent(GetClipboardStatusText(clipboard));
        }

        /// <summary>
        /// Builds a human-readable clipboard status message.
        /// </summary>
        /// <param name="clipboard">The clipboard to describe.</param>
        /// <returns>The clipboard status text.</returns>
        internal static string GetClipboardStatusText(Clipboard clipboard)
        {
            if (clipboard?.HasContent != true)
            {
                return "Clipboard is empty.";
            }

            string rootName = clipboard.treeNodes[0]?.name ?? "None";
            return $"Clipboard: {clipboard.Count} node(s), root: {rootName}";
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
            if (!TryGetTreeAssetDiskPaths(tree, out _, out _, out string folderPath))
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
            if (!TryGetTreeAssetDiskPaths(tree, out string assetPath, out _, out _))
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
            if (!TryGetTreeAssetDiskPaths(tree, out _, out string fullPath, out _))
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
            if (!TryGetTreeAssetDiskPaths(tree, out _, out _, out _))
            {
                return;
            }

            AssetDatabase.OpenAsset(tree);
        }

        /// <summary>
        /// Resolves the selected tree asset paths used by open and locate menu commands.
        /// </summary>
        /// <param name="selectedTree">The selected behaviour tree.</param>
        /// <param name="assetPath">The Unity project-relative asset path.</param>
        /// <param name="fullPath">The full disk path to the asset file.</param>
        /// <param name="folderPath">The full disk path to the asset's containing folder.</param>
        /// <returns>True when the tree has a valid asset path.</returns>
        internal static bool TryGetTreeAssetDiskPaths(
            BehaviourTreeData selectedTree,
            out string assetPath,
            out string fullPath,
            out string folderPath,
            bool showDialog = true)
        {
            if (!selectedTree)
            {
                assetPath = null;
                fullPath = null;
                folderPath = null;
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("No Tree Selected", "Please select a behaviour tree asset first.", "OK");
                }

                return false;
            }

            assetPath = AssetDatabase.GetAssetPath(selectedTree);
            if (!TryBuildTreeAssetDiskPaths(assetPath, out fullPath, out folderPath))
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Asset Path Not Found", "The selected behaviour tree is not saved as a project asset.", "OK");
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds disk paths from a Unity asset path without touching the file system.
        /// </summary>
        /// <param name="assetPath">The Unity project-relative asset path.</param>
        /// <param name="fullPath">The full disk path to the asset file.</param>
        /// <param name="folderPath">The full disk path to the asset's containing folder.</param>
        /// <returns>True when the asset path can be converted to disk paths.</returns>
        internal static bool TryBuildTreeAssetDiskPaths(string assetPath, out string fullPath, out string folderPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                fullPath = null;
                folderPath = null;
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                fullPath = null;
                folderPath = null;
                return false;
            }

            // Unity asset paths always use '/', while System.IO expects the platform separator.
            string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            folderPath = Path.GetDirectoryName(fullPath);
            return !string.IsNullOrEmpty(folderPath);
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
