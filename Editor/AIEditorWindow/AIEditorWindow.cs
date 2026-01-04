using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
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
            assetReference,
            properties,
            settings
        }
        public enum RightWindow
        {
            None,
            Composite,
            All,
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
            tree.RegenerateTable();
            GetAllNode();
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

        private bool UpdateSelectTree()
        {
            var newTree = (BehaviourTreeData)EditorGUILayout.ObjectField("Behaviour Tree", tree, typeof(BehaviourTreeData), false);
            if (newTree != tree)
            {
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

        void OnGUI()
        {
            Initialize();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Space(5);

            GetAllNode();

            if (tree && window == Window.graph)
            {
                graph.DrawGraph();
            }

            #region Draw Header
            GUILayout.Toolbar(-1, new string[] { "" });
            if (!UpdateSelectTree())
            {
                //DrawNewBTWindow();
                //EndWindow();
                //return;
            }

            if (editorSetting.enableGraph)
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
            GUI.enabled = !editorSetting.safeMode;
            switch (window)
            {
                case Window.nodes:
                    treeWindow.DrawTree();
                    break;
                case Window.assetReference:
                    DrawAssetReferenceTable();
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

            if (window != Window.variables)
            {
                variableTable.Reset();
            }

            EndWindow();

            if (GUI.changed) Repaint();

            static void EndWindow()
            {
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUI.enabled = true;
            }
        }

        private void DrawAssetReferenceTable()
        {
            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Asset References", EditorStyles.boldLabel);
            if (!tree)
            {
                DrawNewBTWindow();
                GUILayout.EndVertical();
                return;
            }

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
                EditorGUILayout.LabelField(item.Asset.Exist()?.name ?? string.Empty, GUILayout.Width(200));
                var state = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.ObjectField(tree.GetAsset(item.UUID), typeof(UnityEngine.Object), false);
                GUI.enabled = state;
                EditorGUILayout.LabelField(item.UUID);
                item.UpdateUUID();
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
                editorSetting.useSerializationPropertyDrawer = EditorGUILayout.Toggle(content, editorSetting.useSerializationPropertyDrawer);
                editorSetting.overviewHierachyIndentLevel = EditorGUILayout.IntField("Overview Indent", editorSetting.overviewHierachyIndentLevel);
                editorSetting.HierachyColor = EditorGUILayout.ColorField("Hierachy color", editorSetting.HierachyColor);
                editorSetting.DrawCommonNodesEditor();

                using (ButtonIndent())
                    if (GUILayout.Button("Reset common nodes", GUILayout.Height(30), GUILayout.Width(200))) editorSetting.InitializeCommonNodes();

                EditorUtility.SetDirty(this);
                SerializedObject obj = new(this);
                SerializedProperty property = obj.FindProperty(nameof(clipboard));
                EditorGUILayout.PropertyField(property);
                using (ButtonIndent())
                    if (GUILayout.Button("Clear clipboard", GUILayout.Height(30), GUILayout.Width(200))) clipboard.Clear();

                Header("Variable Table");
                editorSetting.variableTableEntryWidth = EditorGUILayout.IntField("Variable Entry Width", editorSetting.variableTableEntryWidth);

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
                            tree.ReLink();
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
                EditorGUILayout.TextField("2024 Minerva Game Studio, Documentation see: <a href=\"https://github.com/minerva-studio/aethiumian-ai/blob/main/DOC_EN.md\">Documentation link</a>", style);

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



        public override void SaveChanges()
        {
            if (tree) AssetDatabase.SaveAssetIfDirty(tree);
            base.SaveChanges();
        }



        private void OnValidate()
        {
            SaveChanges();
        }


        void OnLostFocus()
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

        /// <summary>
        /// A node that only use as a placeholder for AIE
        /// </summary>
        [DoNotRelease]
        internal class EditorHeadNode : TreeNode
        {
            public EditorHeadNode()
            {
                name = "HEAD";
            }

            public override State Execute()
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