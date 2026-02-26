using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
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
            properties,
            settings
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

        public Clipboard clipboard = new();
        TreeNodeModule treeWindow;
        VariableTableModule variableTable;
        GraphModule graph;

        private Vector2 settingWindowScroll;
        private bool undoEventRegistered;

        public IReadOnlyList<TreeNode> AllNodes => tree.EditorNodes;
        public TreeNode SelectedNode
        {
            get => treeWindow?.SelectedNode;
            set { treeWindow?.SelectNode(value); }
        }
        public TreeNode SelectedNodeParent => treeWindow?.SelectedNodeParent;

        public static AIEditorWindow Instance { get; set; }


        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/Aethiumian AI/AI Editor")]
        public static AIEditorWindow ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            var window = GetWindow(typeof(AIEditorWindow), false, "AI Editor");
            window.name = "AI Editor";
            return window as AIEditorWindow;

        }

        void OnGUI()
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

                #region Draw Header  
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    DrawWindowToolbar();
                }
                if (window == Window.settings)
                {
                    DrawSettings();
                    return;
                }
                DrawBehaviourTreeSelection();
                #endregion

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
                        case Window.settings:
                            DrawSettings();
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

        public void Load(BehaviourTreeData data)
        {
            tree = data;
            SelectedNode = data.Head;
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
        /// <summary>
        /// Draws the window selection tabs inside the header toolbar.
        /// </summary>
        /// <returns>No return value.</returns>
        private void DrawWindowToolbar()
        {
            if (editorSetting.enableGraph)
            {
                window = (Window)EditorGUILayout.Popup(
                    (int)window,
                    new[] { "Tree", "Graph", "Variable Table", "Tree Properties", "Editor Settings" },
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(120f));
            }
            else
            {
                window = (Window)EditorGUILayout.Popup(
                    (int)(window == Window.nodes ? window : (window - 1)),
                    new[] { "Tree", "Variable Table", "Tree Properties", "Editor Settings" },
                    EditorStyles.toolbarPopup,
                    GUILayout.Width(120f));
                if ((int)window == -1)
                {
                    window = Window.nodes;
                }
                if ((int)window > 0)
                {
                    window++;
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
            {
                Refresh();
            }
            Rect maintenanceRect = GUILayoutUtility.GetRect(new GUIContent(""), EditorStyles.toolbarDropDown);
            if (EditorGUI.DropdownButton(maintenanceRect, new GUIContent(""), FocusType.Passive, EditorStyles.toolbarDropDown))
            {
                ShowMaintenanceMenu(maintenanceRect);
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
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Upgrade All"));
                menu.AddSeparator("");
                menu.AddDisabledItem(new GUIContent("Clear All Null Reference"));
                menu.AddDisabledItem(new GUIContent("Fix Null Parent issue"));
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
                EditorUtility.SetDirty(editorSetting);
            });

            menu.DropDown(buttonRect);
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

        private void DrawSettings()
        {
            EditorUtility.SetDirty(editorSetting);
            using (new EditorGUI.IndentLevelScope(1))
            using (new GUIScrollView(ref settingWindowScroll))
            using (new GUILayout.VerticalScope())
            {
                Header("Tree", false);
                var content = new GUIContent("Property Drawer (Experimental)", "Enable property drawer, which support redo/undo operation. The migration is still in progress; some issues still exist in undo recording");
                editorSetting.DrawCommonNodesEditor();

                using (ButtonIndent())
                    if (GUILayout.Button("Reset common nodes", GUILayout.Height(30), GUILayout.Width(200))) editorSetting.InitializeCommonNodes();

                EditorUtility.SetDirty(this);
                SerializedObject obj = new(this);
                SerializedProperty property = obj.FindProperty(nameof(clipboard));
                EditorGUILayout.PropertyField(property);
                using (ButtonIndent())
                    if (GUILayout.Button("Clear clipboard", GUILayout.Height(30), GUILayout.Width(200))) clipboard.Clear();

                Header("Graph (Experimental)");
                using (ButtonIndent())
                {
                    if (!editorSetting.enableGraph && GUILayout.Button("Enable Graph View", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        editorSetting.enableGraph = true;
                    }
                    if (editorSetting.enableGraph && GUILayout.Button("Disable Graph View", GUILayout.Height(30), GUILayout.Width(200)))
                    {
                        editorSetting.enableGraph = false;
                    }
                    if (editorSetting.enableGraph)
                    {
                        if (GUILayout.Button("Recreate Graph", GUILayout.Height(30), GUILayout.Width(200))) graph.CreateGraph();
                    }
                }

                Header("Debug");
                editorSetting.debugMode = EditorGUILayout.Toggle("Debug Mode", editorSetting.debugMode);
                if (editorSetting.debugMode)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(this), typeof(MonoScript), false);
                        EditorGUILayout.ObjectField("Window", this, typeof(AIEditorWindow), false);
                        EditorGUILayout.ObjectField("Setting", setting, typeof(AISetting), false);
                        EditorGUILayout.ObjectField("Editor Setting", editorSetting, typeof(AIEditorSetting), false);
                    }
                    using (ButtonIndent())
                    {
                        if (GUILayout.Button("Clear All Null Reference", GUILayout.Height(30), GUILayout.Width(200)))
                            foreach (var node in AllNodes) NodeFactory.FillNull(node);

                        if (GUILayout.Button("Refresh Tree Window", GUILayout.Height(30), GUILayout.Width(200)))
                        {
                            tree.RegenerateTable();
                            SelectedNode = tree.Head;
                        }
                        if (GUILayout.Button("Fix Null Parent issue", GUILayout.Height(30), GUILayout.Width(200)))
                        {
                            tree.Relink();
                        }
                    }
                }


                Header("Other");
                using (new EditorGUI.DisabledScope(false))
                    editorSetting.safeMode = EditorGUILayout.Toggle("Enable Safe Mode", editorSetting.safeMode);
                using (ButtonIndent())
                    if (GUILayout.Button("Reset Settings", GUILayout.Height(30), GUILayout.Width(200))) editorSetting = AIEditorSetting.ResetSettings();

                Header("Credit");
                GUILayout.FlexibleSpace();
                GUIStyle style = new() { richText = true };
                EditorGUILayout.TextField("2026 Minerva Game Studio, Documentation see: <a href=\"https://github.com/minerva-studio/aethiumian-ai/blob/main/DOC_EN.md\">Documentation link</a>", style);

            }

            static IDisposable ButtonIndent()
            {
                EditorGUILayout.HorizontalScope horizontalScope = new EditorGUILayout.HorizontalScope();
                GUILayout.Space(20);
                return horizontalScope;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Header(string title, bool space = true)
        {
            if (space) EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

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
            foreach (var node in AllNodes)
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
            Instance = this;
        }

        private void Awake()
        {
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
    }
}
