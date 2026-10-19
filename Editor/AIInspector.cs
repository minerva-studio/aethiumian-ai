using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
#if !UNITY_6000_3_OR_NEWER
using UnityEditor.IMGUI.Controls;
#endif
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
#endif

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// AI editor window
    /// </summary>
    public class AIInspector : EditorWindow
    {
        private AI selected;

        [SerializeField]
        private bool inspectorLocked;

        [SerializeField]
        private bool treeFoldout;
        [SerializeField]
        private bool stackFoldout;
        [SerializeField]
        private bool eventLogFoldout;
        [SerializeField]
        private bool nodeFoldout;
        [SerializeField]
        private bool variableFoldout;

        [SerializeField]
        private bool displayHidden;
        [SerializeField]
        private Vector2 scrollPos;
        [SerializeField]
        private Vector2 eventLogScrollPos;
        [SerializeField]
        private bool eventLogAutoScroll = true;

        // TreeView state and selection
        private TreeViewState stackTreeState;
        private NodeStackTreeView stackTreeView;
        private TreeNode selectedNodeOverride;
        private BehaviourTree selectedTree;
        [SerializeField]
        private int selectedTreeIndex;

        internal AI SelectedAI => selected;

        internal bool SelectionLocked
        {
            get => inspectorLocked;
            set => inspectorLocked = value;
        }


        #region Window API

        [MenuItem("Window/Aethiumian AI/AI Inspector")]
        private static void OpenNewWindowFromMenu()
        {
            AIInspector inspector = CreateWindow<AIInspector>();
            inspector.InitializeWindow();
            inspector.FollowUnitySelection();
            inspector.Show();
            inspector.Focus();
        }

        /// <summary>
        /// Opens or focuses an unbound runtime inspector window.
        /// </summary>
        /// <returns>The runtime inspector window used by the request.</returns>
        public static AIInspector ShowWindow()
        {
            if (TryGetSelectedAIFromUnitySelection(out AI selectedAI))
            {
                return ShowWindow(selectedAI);
            }

            if (!TryGetOpenWindow(null, out AIInspector inspector))
            {
                inspector = CreateWindow<AIInspector>();
            }

            inspector.InitializeWindow();
            inspector.Show();
            inspector.Focus();
            return inspector;
        }

        /// <summary>
        /// Opens or focuses the runtime inspector for the provided AI component.
        /// </summary>
        /// <param name="ai">The AI component to inspect.</param>
        /// <returns>The runtime inspector window used by the request.</returns>
        public static AIInspector ShowWindow(AI ai)
        {
            if (!ai)
            {
                return ShowWindow();
            }

            if (!TryGetOpenWindow(ai, out AIInspector inspector))
            {
                inspector = CreateWindow<AIInspector>();
                inspector.Load(ai);
            }
            else
            {
                inspector.InitializeWindow();
            }

            inspector.Show();
            inspector.Focus();
            return inspector;
        }

        /// <summary>
        /// Tries to find an open runtime inspector for the requested AI component.
        /// </summary>
        /// <param name="ai">The AI component to match, or null for an unbound inspector.</param>
        /// <param name="inspector">The matching inspector window.</param>
        /// <returns>True when a matching open window exists.</returns>
        public static bool TryGetOpenWindow(AI ai, out AIInspector inspector)
        {
            AIInspector[] inspectors = Resources.FindObjectsOfTypeAll<AIInspector>();
            foreach (AIInspector candidate in inspectors)
            {
                if (!candidate)
                {
                    continue;
                }

                if (candidate.selected == ai)
                {
                    inspector = candidate;
                    return true;
                }
            }

            inspector = null;
            return false;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeWindow();
        }

        private void Update()
        {
            Repaint();
        }

        private void OnGUI()
        {
            var wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            Draw();
            EditorGUIUtility.wideMode = wideMode;
        }

        private void OnSelectionChange()
        {
            FollowUnitySelection();
            Repaint();
        }

        #endregion

        #region Selection State

        internal void Load(AI ai)
        {
            SetSelectedAI(ai);
        }

        internal void FollowUnitySelection()
        {
            if (inspectorLocked)
            {
                return;
            }

            if (TryGetSelectedAIFromUnitySelection(out AI ai))
            {
                SetSelectedAI(ai);
            }
        }

        private void InitializeWindow()
        {
            UpdateWindowTitle();
        }

        private void SetSelectedAI(AI ai)
        {
            if (selected == ai)
            {
                UpdateWindowTitle();
                return;
            }

            selected = ai;
            selectedTree = null;
            selectedTreeIndex = 0;
            selectedNodeOverride = null;
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            string title = selected ? $"AI Inspector - {selected.gameObject.name}" : "AI Inspector";
            AIEditorTitleContent.ApplyInspectorTitle(this, title);
        }

        private static bool TryGetSelectedAIFromUnitySelection(out AI ai)
        {
            if (Selection.activeObject is AI selectedAI)
            {
                ai = selectedAI;
                return ai;
            }

            GameObject selectedGameObject = Selection.activeGameObject;
            if (!selectedGameObject && Selection.activeObject is GameObject objectAsset)
            {
                selectedGameObject = objectAsset;
            }

            ai = selectedGameObject ? selectedGameObject.GetComponent<AI>() : null;
            return ai;
        }

        #endregion

        #region Tree Selection

        /// <summary>
        /// Resolves the currently selected behaviour tree instance.
        /// </summary>
        /// <returns>The behaviour tree instance to display.</returns>
        private BehaviourTree ResolveActiveTree()
        {
            if (selected == null || selected.BehaviourTree == null)
            {
                return null;
            }

            List<TreeSelectionItem> treeItems = BuildTreeSelectionItems(selected.BehaviourTree);
            if (treeItems.Count == 0)
            {
                return selected.BehaviourTree;
            }

            if (selectedTree == null || treeItems.All(item => item.Tree != selectedTree))
            {
                selectedTree = selected.BehaviourTree;
                selectedTreeIndex = 0;
                selectedNodeOverride = null;
            }

            selectedTreeIndex = Mathf.Clamp(selectedTreeIndex, 0, treeItems.Count - 1);
            string[] labels = treeItems.Select(item => item.Label).ToArray();
            int newIndex = EditorGUILayout.Popup("Behaviour Tree", selectedTreeIndex, labels);
            if (newIndex != selectedTreeIndex)
            {
                selectedTreeIndex = newIndex;
                selectedTree = treeItems[selectedTreeIndex].Tree;
                selectedNodeOverride = null;
            }

            return selectedTree;
        }

        /// <summary>
        /// Builds selection items for the active behaviour tree and its running subtrees.
        /// </summary>
        /// <param name="rootTree">The root behaviour tree for the selected AI instance.</param>
        /// <returns>A list of tree selection items in display order.</returns>
        private static List<TreeSelectionItem> BuildTreeSelectionItems(BehaviourTree rootTree)
        {
            var items = new List<TreeSelectionItem>();
            if (rootTree == null)
            {
                return items;
            }

            string rootLabel = rootTree.Prototype ? rootTree.Prototype.name : "Main Behaviour Tree";
            items.Add(new TreeSelectionItem
            {
                Tree = rootTree,
                Label = $"Main: {rootLabel}"
            });

            foreach (var subtree in rootTree.RunningSubtrees)
            {
                if (subtree?.RuntimeTree == null)
                {
                    continue;
                }

                var treeLabel = subtree.RuntimeTree.Prototype ? subtree.RuntimeTree.Prototype.name : "Subtree";
                var nodeLabel = string.IsNullOrWhiteSpace(subtree.name) ? "Subtree" : subtree.name;
                items.Add(new TreeSelectionItem
                {
                    Tree = subtree.RuntimeTree,
                    Label = $"Subtree: {nodeLabel} ({treeLabel})"
                });
            }

            return items;
        }

        /// <summary>
        /// Represents a behaviour tree entry for the selection dropdown.
        /// </summary>
        private struct TreeSelectionItem
        {
            /// <summary>
            /// Gets the behaviour tree instance.
            /// </summary>
            public BehaviourTree Tree { get; set; }

            /// <summary>
            /// Gets the label shown in the dropdown.
            /// </summary>
            public string Label { get; set; }
        }

        #endregion

        #region Main Drawing

        /// <summary>
        /// Draws the AI instance selection dropdown.
        /// </summary>
        /// <returns>No return value.</returns>
        private void DrawAISelection()
        {
            AI[] aiInstances = FindObjectsByType<AI>(FindObjectsSortMode.None);
            if (aiInstances.Length == 0)
            {
                EditorGUILayout.HelpBox("No AI instances found in the scene.", MessageType.Info);
                return;
            }

            int currentIndex = Array.IndexOf(aiInstances, selected);
            string[] labels = aiInstances
                .Select(ai => $"{ai.name} ({(ai.Data ? ai.Data.name : "Missing Data")})")
                .ToArray();
            int newIndex = EditorGUILayout.Popup("AI Instance", currentIndex, labels);
            if (newIndex != currentIndex && newIndex >= 0 && newIndex < aiInstances.Length)
            {
                SetSelectedAI(aiInstances[newIndex]);
            }
        }

        private void Draw()
        {
            DrawInspectorWindowHeader(selected ? selected.gameObject : null, ref inspectorLocked);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Game Object", selected ? selected.gameObject : null, typeof(GameObject), true);
            DrawAISelection();
            if (!selected)
            {
                EditorGUILayout.LabelField("You must select an AI to view AI status");
            }
            else if (!selected.Data)
            {
                EditorGUILayout.LabelField("AI do not have a behaviour tree data.");
            }
            else if (selected.BehaviourTree == null)
            {
                EditorGUILayout.LabelField("AI is not initialized");
            }
            else
            {
                var activeTree = ResolveActiveTree();
                if (activeTree == null)
                {
                    EditorGUILayout.LabelField("Behaviour tree instance is not available.");
                    return;
                }
                if (activeTree.Prototype)
                    EditorGUILayout.LabelField($"Instance of {activeTree.Prototype.name}");
                else
                    EditorGUILayout.LabelField($"Unknown instance");

                using var indent = IndentScope.Increase;
                using var scrollScope = new EditorGUILayout.ScrollViewScope(scrollPos);
                scrollPos = scrollScope.scrollPosition;
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawWindow(activeTree);
                }
            }
            GUILayout.FlexibleSpace();
            if (selected && selected.Data && selected.BehaviourTree != null)
                DrawToolbar();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(this), typeof(MonoScript), false);
        }

        private void DrawTree(BehaviourTree activeTree)
        {
            treeFoldout = DrawSeparator(treeFoldout, "Tree");
            if (treeFoldout)
            {
                try
                {
                    if (activeTree == null)
                    {
                        EditorGUILayout.LabelField("Behaviour Tree is not available.");
                        return;
                    }

                    // Debugging toggles
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Behaviour Tree", activeTree.Prototype, typeof(BehaviourTreeData), true);
                    if (GUILayout.Button("Open Editor"))
                    {
                        if (activeTree.Prototype)
                        {
                            AIEditorWindow.ShowWindow(activeTree.Prototype);
                        }
                    }
                    EditorGUILayout.LabelField("Head");
                    if (activeTree.Prototype)
                    {
                        NodeDrawerUtility.DrawNodeBaseInfo(activeTree.Prototype, activeTree.Prototype.Head, true);
                    }
                    EditorGUILayout.Space(8);
                    activeTree.Debugging = EditorGUILayout.Toggle("Debug", activeTree.Debugging);
                    if (activeTree.AIComponent != null)
                    {
                        bool paused = EditorGUILayout.Toggle("Pause", activeTree.AIComponent.IsPaused);
                        if (paused != activeTree.AIComponent.IsPaused)
                        {
                            if (paused) activeTree.AIComponent.Pause();
                            else activeTree.AIComponent.Resume();
                        }
                    }
                    EditorGUILayout.Space(12);
                }
                catch (Exception exception)
                {
                    EditorGUILayout.HelpBox($"Tree display failed: {exception.Message}", MessageType.Error);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawWindow(BehaviourTree activeTree)
        {
            if (DrawInitializationFailure(activeTree))
            {
                return;
            }

            DrawTree(activeTree);

            using (new EditorGUILayout.VerticalScope())
            {
                DrawNodeFieldStatus(activeTree);
                DrawVariable(activeTree);
            }
        }

        private static bool DrawInitializationFailure(BehaviourTree activeTree)
        {
            if (activeTree == null || !activeTree.IsFaulted || activeTree.IsInitialized)
            {
                return false;
            }

            Exception exception = activeTree.InitializationException;
            string message = exception == null
                ? "No initialization error details are available."
                : $"{exception.GetType().Name}: {exception.Message}";
            EditorGUILayout.HelpBox($"Behaviour Tree initialization failed.\n{message}", MessageType.Error);
            return true;
        }

        private void DrawNodeFieldStatus(BehaviourTree activeTree)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                // Active stacks tree
                DrawStacks(activeTree);
                DrawEventLog(activeTree);
                // Node inspector
                DrawInspector(activeTree, selectedNodeOverride ?? activeTree.CurrentStage.Node);
            }
        }

        private void DrawVariable(BehaviourTree activeTree)
        {
            BeginVerticleAndSetWindowColor();
            variableFoldout = DrawSeparator(variableFoldout, "Variables");
            using (new EditorGUI.DisabledScope(activeTree == null || !activeTree.IsRunning))
                if (variableFoldout)
                {
                    EditorGUILayout.LabelField("Instance", EditorStyles.boldLabel);
                    DrawVariableTable(activeTree?.Variables);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Static", EditorStyles.boldLabel);
                    DrawVariableTable(activeTree?.StaticVariables);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);
                    DrawVariableTable(BehaviourTree.GlobalVariables);
                }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            if (selected.BehaviourTree.IsFaulted && !selected.BehaviourTree.IsInitialized)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    GUILayout.Button("Initialization Failed");
                }
                return;
            }

            if (!selected.BehaviourTree.IsRunning)
            {
                if (!Application.isPlaying)
                {
                    GUILayout.Toolbar(-1, new[] { "" });
                }
                else if (GUILayout.Button("Start"))
                {
                    selected.StartBehaviourTree();

                }
            }
            else
            {
                int index = GUILayout.Toolbar(-1, new[] { (selected.IsPaused ? "Resume" : "Pause"), "Restart" });
                switch (index)
                {
                    case 0:
                        if (!selected.BehaviourTree.IsRunning)
                            break;
                        if (selected.IsPaused)
                        {
                            selected.Resume();
                        }
                        else
                        {
                            selected.Pause();
                        }

                        break;
                    case 1:
                        selected.Reload();

                        break;
                    default:
                        break;
                }
            }
        }

        private static void DrawVariableTable(VariableTable table)
        {
            using (new GUILayout.VerticalScope())
            {
                if (table == null)
                {
                    EditorGUILayout.LabelField("Variable Table is null");
                    return;
                }
                foreach (var variable in table)
                {
                    if (variable is null) continue;
                    if (AIInspectorRuntimeFieldDrawer.DrawField(new GUIContent(variable.Name.ToTitleCase()), variable.Value, variable.ObjectType, out object newVal))
                    {
                        variable.SetValue(newVal);
                    }
                }
            }
        }

        #endregion





        #region Node Inspector

        private void DrawInspector(BehaviourTree activeTree, TreeNode node)
        {
            nodeFoldout = DrawSeparator(nodeFoldout, "Node");
            if (nodeFoldout)
            {
                try
                {
                    if (!displayHidden) if (GUILayout.Button("Display Hidden Field")) displayHidden = true;
                    if (displayHidden) if (GUILayout.Button("Hide Hidden Field")) displayHidden = false;

                    if (node != null)
                    {
                        EditorGUILayout.Space(12);

                        if (activeTree?.Prototype)
                        {
                            NodeDrawerUtility.DrawNodeBaseInfo(activeTree.Prototype, node);
                        }
                        foreach (var fieldInfo in GetAllFields(node.GetType(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            DrawMember(activeTree, node, fieldInfo);
                        }
                    }
                }
                catch (Exception exception)
                {
                    EditorGUILayout.HelpBox($"Node inspector failed: {exception.Message}", MessageType.Error);
                }
                EditorGUILayout.Space(12);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMember(BehaviourTree activeTree, TreeNode node, FieldInfo fieldInfo)
        {
            if (fieldInfo.Name == nameof(node.name)) return;
            if (fieldInfo.Name == nameof(node.uuid)) return;
            if (fieldInfo.Name == nameof(ServiceHostNode.services)) return;
            if (fieldInfo.Name == nameof(node.behaviourTree)) return;

            if (IsDelegateLike(fieldInfo.FieldType)) return;

            var property = ResolveAutoProperty(fieldInfo);
            if (property != null && Attribute.IsDefined(property, typeof(Aethiumian.AI.AIInspectorIgnoreAttribute), true)) return;

            if (Attribute.IsDefined(fieldInfo, typeof(Aethiumian.AI.AIInspectorIgnoreAttribute), true)) return;

            try
            {
                if (!NodeDrawerFieldMetadata.ShouldDraw(
                        node,
                        fieldInfo,
                        includeConditionalHidden: displayHidden))
                {
                    return;
                }
            }
            catch (Exception)
            {
                EditorGUILayout.LabelField(NormalizeFieldLabel(fieldInfo).ToTitleCase(), "DisplayIf attribute breaks, ask for help");
                return;
            }

            DrawField(activeTree, node, fieldInfo);
        }

        private void DrawField(BehaviourTree activeTree, TreeNode node, FieldInfo fieldInfo)
        {
            var labelName = NormalizeFieldLabel(fieldInfo).ToTitleCase();

            var value = fieldInfo.GetValue(node);
            bool readOnly = NodeDrawerFieldMetadata.IsReadOnly(fieldInfo);
            using (new EditorGUI.DisabledScope(readOnly))
            {
                if (AIInspectorRuntimeFieldDrawer.DrawField(new GUIContent(labelName), value, fieldInfo.FieldType, out object newValue) && !readOnly)
                {
                    fieldInfo.SetValue(node, newValue);
                }
            }
        }






        private static IEnumerable<FieldInfo> GetAllFields(Type type, BindingFlags flags)
        {
            var list = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                var levelFlags = flags | BindingFlags.DeclaredOnly;
                var fields = t.GetFields(levelFlags);
                if (fields != null && fields.Length > 0)
                {
                    list.AddRange(fields);
                }
            }
            list.Reverse();
            return list;
        }

        private static PropertyInfo ResolveAutoProperty(FieldInfo fieldInfo)
        {
            var name = fieldInfo.Name;
            if (string.IsNullOrEmpty(name)) return null;
            if (!name.StartsWith("<", StringComparison.Ordinal) || !name.EndsWith(">k__BackingField", StringComparison.Ordinal))
            {
                return null;
            }

            var propName = name.Substring(1, name.Length - 1 - ">k__BackingField".Length);
            var declaringType = fieldInfo.DeclaringType;
            if (declaringType == null) return null;

            var property = declaringType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)
                           ?? declaringType.GetProperty(propName, BindingFlags.NonPublic | BindingFlags.Instance);
            return property;
        }

        private static string NormalizeFieldLabel(FieldInfo fieldInfo)
        {
            var property = ResolveAutoProperty(fieldInfo);
            if (property != null) return property.Name;
            return fieldInfo.Name;
        }

        private static bool IsDelegateLike(Type type)
        {
            return type != null && typeof(Delegate).IsAssignableFrom(type);
        }

        #endregion





        #region Runtime Stack Drawing

        private void DrawStacks(BehaviourTree activeTree)
        {
            stackFoldout = DrawSeparator(stackFoldout, "Stack");
            if (stackFoldout)
            {
                try
                {
                    EnsureTreeView();
                    stackTreeView?.SetItems(BuildStackTreeItems(activeTree));
                    var treeRect = GUILayoutUtility.GetRect(0, 200, GUILayout.ExpandWidth(true));
                    stackTreeView?.OnGUI(treeRect);

                    // Controls under tree
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select Current"))
                        {
                            selectedNodeOverride = null;
                        }
                        GUILayout.FlexibleSpace();
                        var selName = selectedNodeOverride ? selectedNodeOverride.name : "(current)";
                        EditorGUILayout.LabelField("Selected:", selName);
                    }
                }
                catch (Exception exception)
                {
                    EditorGUILayout.HelpBox($"Stack display failed: {exception.Message}", MessageType.Error);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        #endregion





        #region Event Log

        private const float EVENT_ROW_HEIGHT = 46f;
        private const float EVENT_VIEW_HEIGHT = 280f;
        private static readonly string[] EventTypeLabels = Enum.GetNames(typeof(BehaviourTree.NodeCallStack.EventType));
        private static readonly int AllEventTypesMask = (1 << EventTypeLabels.Length) - 1;
        private static readonly string[] SeverityFilterLabels = { "All levels", "Debug (blue)", "Info (green)", "Warning (amber)", "Error (red)" };
        private static readonly string[] StackFilterLabels = { "All stacks", "Main", "Service", "Branch" };

        [SerializeField] private string eventLogSearch = string.Empty;
        [SerializeField] private int eventLogTypeMask = AllEventTypesMask;
        [SerializeField] private int eventLogSeverityFilter;
        [SerializeField] private int eventLogStackFilter;
        [SerializeField] private bool eventLogSelectedNodeOnly;
        private BehaviourTree eventLogTree;
        // Keep an immutable selection snapshot so queue eviction does not change the detail being read.
        private BehaviourTree.StackEventRecord? selectedEvent;
        private Vector2 eventDetailScrollPos;
        private GUIStyle eventNodeStyle;
        private GUIStyle eventMetadataStyle;
        private GUIStyle eventBadgeStyle;
        private GUIStyle eventDetailStyle;

        /// <summary>Draws searchable recorded events; filters never change the tree's event history.</summary>
        private void DrawEventLog(BehaviourTree activeTree)
        {
            eventLogFoldout = DrawSeparator(eventLogFoldout, "Event Log");
            if (!eventLogFoldout)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            if (eventLogTree != activeTree)
            {
                eventLogTree = activeTree;
                selectedEvent = null;
                eventLogScrollPos = Vector2.zero;
                eventDetailScrollPos = Vector2.zero;
            }
            if (!selectedNodeOverride) eventLogSelectedNodeOnly = false;

            if (activeTree == null)
            {
                EditorGUILayout.HelpBox("Behaviour Tree is not available.", MessageType.Info);
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            eventNodeStyle ??= new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            eventMetadataStyle ??= new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            eventBadgeStyle ??= new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            eventDetailStyle ??= new GUIStyle(EditorStyles.wordWrappedLabel) { richText = false };

            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                eventLogSearch = EditorGUILayout.TextField(eventLogSearch, EditorStyles.toolbarSearchField);
                using (new EditorGUI.DisabledScope(!selectedNodeOverride))
                {
                    eventLogSelectedNodeOnly = GUILayout.Toggle(eventLogSelectedNodeOnly, new GUIContent("Selected node", "Show records for the node selected in the Stack or Event Log."), EditorStyles.toolbarButton, GUILayout.Width(100));
                }
                if (GUILayout.Button(new GUIContent("Reset", "Reset search and filters without deleting events."), EditorStyles.toolbarButton, GUILayout.Width(44)))
                {
                    eventLogSearch = string.Empty;
                    eventLogTypeMask = AllEventTypesMask;
                    eventLogSeverityFilter = 0;
                    eventLogStackFilter = 0;
                    eventLogSelectedNodeOnly = false;
                    GUI.FocusControl(null);
                    GUI.changed = true;
                }
            }
            EditorGUILayout.LabelField("Search node, event, stack or detail · all words must match", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string[] selectedTypes = EventTypeLabels.Where((label, index) => (eventLogTypeMask & (1 << index)) != 0).ToArray();
                string filterLabel = selectedTypes.Length == EventTypeLabels.Length ? "All events" : selectedTypes.Length == 0 ? "No events" : string.Join(", ", selectedTypes);
                Rect eventFilterRect = GUILayoutUtility.GetRect(new GUIContent(filterLabel), EditorStyles.popup, GUILayout.MinWidth(95), GUILayout.ExpandWidth(true));
                if (EditorGUI.DropdownButton(eventFilterRect, new GUIContent(filterLabel, "Choose event types. Multiple types can be selected."), FocusType.Keyboard))
                    BuildEventTypeFilterMenu().DropDown(eventFilterRect);
                eventLogSeverityFilter = EditorGUILayout.Popup(eventLogSeverityFilter, SeverityFilterLabels, GUILayout.MinWidth(90));
                eventLogStackFilter = EditorGUILayout.Popup(eventLogStackFilter, StackFilterLabels, GUILayout.MinWidth(80));
            }
            if (EditorGUI.EndChangeCheck()) eventLogScrollPos = Vector2.zero;

            var events = activeTree.StackEvents;
            string[] words = eventLogSearch.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            TreeNode nodeFilter = eventLogSelectedNodeOnly ? selectedNodeOverride : null;
            var visibleEvents = events.Where(item => MatchesStackEvent(item, words, eventLogTypeMask, eventLogSeverityFilter, eventLogStackFilter, nodeFilter)).ToList();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{visibleEvents.Count} / {events.Count} events", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                eventLogAutoScroll = GUILayout.Toggle(eventLogAutoScroll, new GUIContent("Follow latest", "Turn off to read history. Selecting a record or scrolling up also turns this off."), EditorStyles.miniButton, GUILayout.Width(94));
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(44)))
                {
                    activeTree.ClearStackEvents();
                    visibleEvents.Clear();
                    selectedEvent = null;
                }
            }

            Rect viewport = GUILayoutUtility.GetRect(0, EVENT_VIEW_HEIGHT, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(viewport, EditorGUIUtility.isProSkin ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.86f, 0.86f, 0.86f));
            if (Event.current.type == EventType.ScrollWheel && viewport.Contains(Event.current.mousePosition) && Event.current.delta.y < 0)
                eventLogAutoScroll = false;
            float contentHeight = visibleEvents.Count * EVENT_ROW_HEIGHT;
            if (eventLogAutoScroll) eventLogScrollPos.y = Mathf.Max(0, contentHeight - viewport.height);
            float previousScrollY = eventLogScrollPos.y;
            bool pointerInViewport = viewport.Contains(Event.current.mousePosition);
            EventType inputType = Event.current.type;
            eventLogScrollPos = GUI.BeginScrollView(viewport, eventLogScrollPos, new Rect(0, 0, Mathf.Max(0, viewport.width - 16), contentHeight));
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(eventLogScrollPos.y / EVENT_ROW_HEIGHT));
            int lastRow = Mathf.Min(visibleEvents.Count, firstRow + Mathf.CeilToInt(viewport.height / EVENT_ROW_HEIGHT) + 1);
            for (int i = firstRow; i < lastRow; i++)
                DrawStackEventRow(new Rect(0, i * EVENT_ROW_HEIGHT, viewport.width - 16, EVENT_ROW_HEIGHT), visibleEvents[i], i, pointerInViewport);
            GUI.EndScrollView();
            if ((inputType == EventType.MouseDown || inputType == EventType.MouseDrag || inputType == EventType.ScrollWheel) && eventLogScrollPos.y < previousScrollY)
                eventLogAutoScroll = false;

            if (visibleEvents.Count == 0)
            {
                GUI.Label(new Rect(viewport.x + 12, viewport.y + 12, viewport.width - 24, 48), events.Count == 0 ? "No recorded events yet." : "No matching events. Change the search or reset filters.", EditorStyles.wordWrappedLabel);
            }

            if (selectedEvent.HasValue) DrawStackEventDetails(selectedEvent.Value);
            else EditorGUILayout.LabelField("Select an event to read its full details and inspect its node.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        /// <summary>Edits independent type selections, with labels derived from the runtime event enum.</summary>
        private GenericMenu BuildEventTypeFilterMenu()
        {
            var menu = new GenericMenu();
            bool allSelected = eventLogTypeMask == AllEventTypesMask;
            menu.AddItem(new GUIContent("All events"), allSelected, () =>
            {
                eventLogTypeMask = AllEventTypesMask;
                eventLogScrollPos = Vector2.zero;
                Repaint();
            });
            menu.AddItem(new GUIContent("No events"), eventLogTypeMask == 0, () =>
            {
                eventLogTypeMask = 0;
                eventLogScrollPos = Vector2.zero;
                Repaint();
            });
            menu.AddSeparator(string.Empty);
            for (int i = 0; i < EventTypeLabels.Length; i++)
            {
                int bit = 1 << i;
                menu.AddItem(new GUIContent(EventTypeLabels[i]), (eventLogTypeMask & bit) != 0, () =>
                {
                    eventLogTypeMask ^= bit;
                    eventLogScrollPos = Vector2.zero;
                    Repaint();
                });
            }
            return menu;
        }

        /// <summary>Combines event type, severity, stack and node filters with an ordinal, case-insensitive all-words search.</summary>
        /// <param name="eventTypeMask">Bit n selects EventType n; zero selects none and -1 selects all types.</param>
        /// <param name="severityFilter">Zero includes every level; otherwise the value is one plus EventSeverity.</param>
        /// <param name="stackFilter">Zero includes every stack; otherwise the value is one plus the stack type.</param>
        /// <param name="nodeFilter">Null includes every node; a node matches its recorded UUID.</param>
        internal static bool MatchesStackEvent(BehaviourTree.StackEventRecord item, string[] words, int eventTypeMask, int severityFilter, int stackFilter, TreeNode nodeFilter)
        {
            if (severityFilter != 0 && (int)item.Context.Severity != severityFilter - 1) return false;
            if (stackFilter != 0 && (int)item.StackType != stackFilter - 1) return false;
            if (nodeFilter && item.NodeUUID != nodeFilter.uuid) return false;
            if ((eventTypeMask & (1 << (int)item.EventType)) == 0) return false;
            if (words.Length == 0) return true;
            string searchable = $"{item.Context.Severity}\n{item.NodeName}\n{item.NodeType}\n{item.NodeUUID}\n{item.TreeName}\n{item.StackName}\n{item.StackType}\n{item.StackId}\n{item.EventType}\n{item.Result}\n{item.StackState}\n{item.Frame}\n{item.Time.ToString("0.00", CultureInfo.InvariantCulture)}\n{item.Detail}\n{item.SequenceId}\n{item.Context.Reason}\n{item.SourceName}\n{item.SourceType}\n{item.SourceUUID}\n{item.Context.CauseEventId}\n{item.Context.RequestedResult}\n{item.Context.Phase}\n{item.Context.Policy}\n{(item.Context.Accepted.HasValue ? (item.Context.Accepted.Value ? "Accepted" : "Rejected") : string.Empty)}";
            return words.All(word => searchable.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>Draws a two-line row with separate timing, event, node and result columns.</summary>
        private void DrawStackEventRow(Rect rect, BehaviourTree.StackEventRecord item, int index, bool pointerInViewport)
        {
            bool isSelected = selectedEvent.HasValue && selectedEvent.Value.SequenceId == item.SequenceId;
            if (isSelected) EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.17f, 0.29f, 0.42f) : new Color(0.65f, 0.79f, 0.94f));
            else if (index % 2 == 0) EditorGUI.DrawRect(rect, new Color(1, 1, 1, EditorGUIUtility.isProSkin ? 0.035f : 0.18f));
            Color accent = item.Context.Severity switch
            {
                BehaviourTree.NodeCallStack.EventSeverity.Error => new Color(0.95f, 0.36f, 0.32f),
                BehaviourTree.NodeCallStack.EventSeverity.Warning => new Color(0.92f, 0.65f, 0.24f),
                BehaviourTree.NodeCallStack.EventSeverity.Info => new Color(0.29f, 0.72f, 0.49f),
                _ => new Color(0.36f, 0.61f, 0.78f)
            };
            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 3, 3, rect.height - 6), accent);
            float timeWidth = rect.width < 430 ? 60 : 76;
            float nodeX = rect.x + timeWidth + 86;
            float resultWidth = item.Result.HasValue ? 62 : 0;
            GUI.Label(new Rect(rect.x + 10, rect.y + 4, timeWidth - 10, 19), $"{item.Time:0.00}s", EditorStyles.label);
            GUI.Label(new Rect(rect.x + 10, rect.y + 23, timeWidth - 10, 17), $"#{item.Frame}", eventMetadataStyle);
            var badge = new Rect(rect.x + timeWidth, rect.y + 6, 78, 19);
            EditorGUI.DrawRect(badge, new Color(accent.r, accent.g, accent.b, EditorGUIUtility.isProSkin ? 0.22f : 0.32f));
            GUI.Label(badge, new GUIContent(item.EventType.ToString(), $"Level: {item.Context.Severity}"), eventBadgeStyle);
            string nodeName = item.NodeName == "(null)" ? "(stack event)" : item.NodeName;
            GUI.Label(new Rect(nodeX, rect.y + 3, Mathf.Max(0, rect.xMax - nodeX - resultWidth - 8), 21), new GUIContent(nodeName, nodeName), eventNodeStyle);
            if (item.Result.HasValue)
                GUI.Label(new Rect(rect.xMax - resultWidth - 4, rect.y + 4, resultWidth, 19), item.Result.Value.ToString(), eventBadgeStyle);
            string summary = item.Context.Reason ?? item.Context.Phase;
            if (item.Context.Accepted.HasValue) summary += item.Context.Accepted.Value ? " / Accepted" : " / Rejected";
            if (item.Context.RequestedResult.HasValue) summary += $" / {item.Context.RequestedResult}";
            if (!string.IsNullOrEmpty(item.SourceName)) summary += $" / by {item.SourceName}";
            if (string.IsNullOrEmpty(summary)) summary = string.IsNullOrWhiteSpace(item.Detail) ? item.NodeType : item.Detail.Split('\n')[0].TrimEnd('\r');
            string metadata = $"{item.StackType} #{item.StackId}  |  {summary}";
            GUI.Label(new Rect(nodeX, rect.y + 24, Mathf.Max(0, rect.xMax - nodeX - 8), 17), new GUIContent(metadata, metadata), eventMetadataStyle);
            bool contextClick = Event.current.type == EventType.ContextClick;
            if (pointerInViewport && rect.Contains(Event.current.mousePosition) && (contextClick || Event.current.type == EventType.MouseDown && Event.current.button == 0))
            {
                selectedEvent = item;
                eventDetailScrollPos = Vector2.zero;
                eventLogAutoScroll = false;
                if (item.Node) selectedNodeOverride = item.Node;
                GUI.FocusControl(null);
                if (contextClick) BuildStackEventContextMenu(item).ShowAsContext();
                Event.current.Use();
                Repaint();
            }
        }

        /// <summary>Finds a retained Push/Pop counterpart in the same stack using nesting order, not node-name proximity.</summary>
        /// <returns>The counterpart's history index, or -1 if either record cannot be matched from retained history.</returns>
        /// <remarks>Start resets a stack lifetime. Redirect is not a boundary; every physical exit has a Pop record.</remarks>
        internal static int FindStackEventPair(IReadOnlyList<BehaviourTree.StackEventRecord> events, long sequenceId)
        {
            int selectedIndex = -1;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].SequenceId == sequenceId)
                {
                    selectedIndex = i;
                    break;
                }
            }
            if (selectedIndex < 0) return -1;
            var selected = events[selectedIndex];
            bool backwards = selected.EventType == BehaviourTree.NodeCallStack.EventType.Pop;
            if (!backwards && selected.EventType != BehaviourTree.NodeCallStack.EventType.Push) return -1;
            int direction = backwards ? -1 : 1;
            int depth = 0;
            for (int i = selectedIndex + direction; i >= 0 && i < events.Count; i += direction)
            {
                var candidate = events[i];
                if (candidate.StackId != selected.StackId) continue;
                if (candidate.EventType == BehaviourTree.NodeCallStack.EventType.Start) return -1;
                if (candidate.EventType != BehaviourTree.NodeCallStack.EventType.Push && candidate.EventType != BehaviourTree.NodeCallStack.EventType.Pop) continue;
                if ((candidate.EventType == BehaviourTree.NodeCallStack.EventType.Pop) == backwards)
                {
                    depth++;
                }
                else if (depth > 0)
                {
                    depth--;
                }
                else
                {
                    return candidate.NodeUUID == selected.NodeUUID ? i : -1;
                }
            }
            return -1;
        }

        /// <summary>Builds row actions without adding controls to the list or detail pane.</summary>
        private GenericMenu BuildStackEventContextMenu(BehaviourTree.StackEventRecord item)
        {
            var menu = new GenericMenu();
            BehaviourTree contextTree = eventLogTree;
            var events = contextTree.StackEvents;
            if (item.EventType == BehaviourTree.NodeCallStack.EventType.Push || item.EventType == BehaviourTree.NodeCallStack.EventType.Pop)
            {
                int pairIndex = FindStackEventPair(events, item.SequenceId);
                string counterpart = item.EventType == BehaviourTree.NodeCallStack.EventType.Pop ? "Push" : "Pop";
                if (pairIndex < 0)
                {
                    menu.AddDisabledItem(new GUIContent($"No matching {counterpart} in retained history"));
                }
                else
                {
                    string[] words = eventLogSearch.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    bool hidden = !MatchesStackEvent(events[pairIndex], words, eventLogTypeMask, eventLogSeverityFilter, eventLogStackFilter, eventLogSelectedNodeOnly ? selectedNodeOverride : null);
                    menu.AddItem(new GUIContent($"Go to matching {counterpart}" + (hidden ? " (show all events)" : string.Empty)), false, () =>
                    {
                        // A running tree may evict the counterpart while its context menu is open.
                        if (eventLogTree != contextTree)
                        {
                            ShowNotification(new GUIContent("The inspected tree has changed."));
                            return;
                        }
                        var retained = contextTree.StackEvents;
                        int currentPairIndex = FindStackEventPair(retained, item.SequenceId);
                        if (currentPairIndex < 0)
                        {
                            ShowNotification(new GUIContent("Matching event is no longer in retained history."));
                            return;
                        }
                        SelectStackEvent(contextTree, retained[currentPairIndex].SequenceId);
                    });
                }
                menu.AddSeparator(string.Empty);
            }
            if (item.Context.CauseEventId != 0)
            {
                long causeId = item.Context.CauseEventId;
                if (!events.Any(record => record.SequenceId == causeId))
                    menu.AddDisabledItem(new GUIContent("Cause is no longer in retained history"));
                else
                    menu.AddItem(new GUIContent("Go to redirect cause"), false, () =>
                    {
                        SelectStackEvent(contextTree, causeId);
                    });
                menu.AddSeparator(string.Empty);
            }
            menu.AddItem(new GUIContent("Copy event"), false, () => EditorGUIUtility.systemCopyBuffer = FormatStackEventDetails(item));
            return menu;
        }

        /// <summary>Navigates to a retained event, resetting only filters that hide the target.</summary>
        private void SelectStackEvent(BehaviourTree contextTree, long sequenceId)
        {
            if (eventLogTree != contextTree)
            {
                ShowNotification(new GUIContent("The inspected tree has changed."));
                return;
            }
            var retained = contextTree.StackEvents;
            int targetIndex = -1;
            for (int i = 0; i < retained.Count; i++)
                if (retained[i].SequenceId == sequenceId) { targetIndex = i; break; }
            if (targetIndex < 0)
            {
                ShowNotification(new GUIContent("Event is no longer in retained history."));
                return;
            }
            var target = retained[targetIndex];
            string[] words = eventLogSearch.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var visible = retained.Where(record => MatchesStackEvent(record, words, eventLogTypeMask, eventLogSeverityFilter, eventLogStackFilter, eventLogSelectedNodeOnly ? selectedNodeOverride : null)).ToList();
            int visibleIndex = visible.FindIndex(record => record.SequenceId == sequenceId);
            if (visibleIndex < 0)
            {
                eventLogSearch = string.Empty;
                eventLogTypeMask = AllEventTypesMask;
                eventLogSeverityFilter = 0;
                eventLogStackFilter = 0;
                eventLogSelectedNodeOnly = false;
                visibleIndex = targetIndex;
            }
            selectedEvent = target;
            if (target.Node) selectedNodeOverride = target.Node;
            eventLogAutoScroll = false;
            eventLogScrollPos = new Vector2(0, Mathf.Max(0, visibleIndex * EVENT_ROW_HEIGHT - (EVENT_VIEW_HEIGHT - EVENT_ROW_HEIGHT) / 2));
            eventDetailScrollPos = Vector2.zero;
            Repaint();
        }

        /// <summary>Shows selectable, wrapped snapshot data, including details that do not fit in a list row.</summary>
        private void DrawStackEventDetails(BehaviourTree.StackEventRecord item)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Event details", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(46)))
                        EditorGUIUtility.systemCopyBuffer = FormatStackEventDetails(item);
                }
                using (var scroll = new EditorGUILayout.ScrollViewScope(eventDetailScrollPos, GUILayout.Height(140)))
                {
                    eventDetailScrollPos = scroll.scrollPosition;
                    string text = FormatStackEventDetails(item);
                    float width = Mathf.Max(100, position.width - 70);
                    EditorGUILayout.SelectableLabel(text, eventDetailStyle, GUILayout.Height(eventDetailStyle.CalcHeight(new GUIContent(text), width)), GUILayout.ExpandWidth(true));
                }
            }
        }

        /// <summary>Formats the complete recorded values for the detail pane and clipboard.</summary>
        private static string FormatStackEventDetails(BehaviourTree.StackEventRecord item)
        {
            return $"{item.EventType} #{item.SequenceId}  |  {item.Time:0.00}s  |  Frame {item.Frame}\nLevel: {item.Context.Severity}"
                + (string.IsNullOrEmpty(item.Context.Reason) ? string.Empty : $"\nReason: {item.Context.Reason}")
                + (item.Context.Accepted.HasValue ? $"\nRequest: {(item.Context.Accepted.Value ? "Accepted" : "Rejected")}" : string.Empty)
                + (item.Context.RequestedResult.HasValue ? $"\nRequested result: {item.Context.RequestedResult}" : string.Empty)
                + (string.IsNullOrEmpty(item.SourceName) ? string.Empty : $"\nSource: {item.SourceName} ({item.SourceType}) / {item.SourceUUID}")
                + (item.Context.CauseEventId == 0 ? string.Empty : $"\nCause: Redirect #{item.Context.CauseEventId}")
                + (string.IsNullOrEmpty(item.Context.Phase) ? string.Empty : $"\nPhase: {item.Context.Phase}\nHandling: {item.Context.Policy}")
                + (string.IsNullOrWhiteSpace(item.Detail) ? string.Empty : $"\n\n{item.Detail}\n")
                + $"\nNode: {item.NodeName} ({item.NodeType})\nTree: {item.TreeName}\nStack: {item.StackType} #{item.StackId} / {item.StackName}\nState: {item.StackState}  |  Result: {(item.Result.HasValue ? item.Result.Value.ToString() : "—")}\nNode ID: {item.NodeUUID}";
        }

        #endregion


        #region GUI Helpers

        private static void DrawInspectorWindowHeader(GameObject go, ref bool locked)
        {
            // Attempt to use Unity's internal InspectorTitlebar if available
            // This is how Unity's InspectorWindow actually draws object headers
            try
            {
                var editorType = typeof(UnityEditor.Editor);
                var method = editorType.GetMethod("DrawHeaderGUI",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                if (method != null)
                {
                    // DrawHeaderGUI is Unity's internal method for object headers
                    // It handles icon, name, active toggle, static flags, tag/layer, etc.
                    var editor = UnityEditor.Editor.CreateEditor(go);
                    if (editor != null)
                    {
                        // Call internal header drawing
                        method.Invoke(null, new object[] { editor });
                        UnityEngine.Object.DestroyImmediate(editor);
                        return;
                    }
                }
            }
            catch
            {
                // Fallback if reflection fails
            }

            // Fallback: manual drawing similar to above
            var topSepColor = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 1f)
                : new Color(0.6f, 0.6f, 0.6f, 1f);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), topSepColor);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(22f)))
            {
                var content = EditorGUIUtility.ObjectContent(go, typeof(GameObject));
                var iconSize = 16f;
                var iconRect = GUILayoutUtility.GetRect(iconSize, iconSize, GUILayout.ExpandWidth(false));
                if (content.image != null)
                {
                    GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit);
                }

                GUILayout.Label(content.text, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                locked = GUILayout.Toggle(locked, GUIContent.none, "IN LockButton", GUILayout.Width(20f));
            }

            var bottomSepColor = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f, 1f)
                : new Color(0.5f, 0.5f, 0.5f, 1f);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true)), bottomSepColor);
        }

        private static void BeginVerticleAndSetWindowColor()
        {
            var colorStyle = new GUIStyle();
            colorStyle.normal.background = Texture2D.whiteTexture;
            var baseColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(64 / 255f, 64 / 255f, 64 / 255f);
            GUILayout.BeginVertical(colorStyle);
            GUI.backgroundColor = baseColor;
        }

        private static bool DrawSeparator(bool foldout, string content)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, Color.black);
            return EditorGUILayout.BeginFoldoutHeaderGroup(foldout, content, EditorStyles.foldoutHeader);
        }

        #endregion





        #region Stack Tree View

        // Ensure and build the stacks treeview
        private void EnsureTreeView()
        {
            stackTreeState ??= new TreeViewState();
            if (stackTreeView == null)
            {
                stackTreeView = new NodeStackTreeView(stackTreeState, this, OnTreeSelectionChanged);
            }
        }

        private void OnTreeSelectionChanged(TreeNode node)
        {
            selectedNodeOverride = node;
        }

        // Build hierarchical items for the treeview without heavy reflection
        private List<TreeViewItem> BuildStackTreeItems(BehaviourTree activeTree)
        {
            var items = new List<TreeViewItem>();
            int id = 1;

            var stackGroups = EnumerateActiveStacks(activeTree);
            for (int i = 0; i < stackGroups.Count; i++)
            {
                var group = stackGroups[i];
                var stackItem = new TreeViewItem
                {
                    id = id++,
                    depth = 1,
                    displayName = string.IsNullOrEmpty(group.name) ? $"Stack {i}" : group.name
                };
                items.Add(stackItem);

                foreach (var node in group.nodes)
                {
                    if (!node) continue;
                    items.Add(new NodeTreeItem
                    {
                        id = id++,
                        depth = 2,
                        displayName = MakeNodeLabel(node),
                        Node = node
                    });
                }
            }

            return items;
        }

        private string MakeNodeLabel(TreeNode node)
        {
            if (!node) return "(null)";
            var typeName = NodeMenuCache.Shared.GetDisplayName(node.GetType());
            var n = string.IsNullOrWhiteSpace(node.name) ? typeName : node.name;
            return $"{n} [{typeName}]";
        }

        private struct StackGroup
        {
            public string name;
            public List<TreeNode> nodes;
        }

        private List<StackGroup> EnumerateActiveStacks(BehaviourTree activeTree)
        {
            var result = new List<StackGroup>();
            if (activeTree == null) return result;

            var stacks = activeTree.ActiveStacks;
            if (stacks == null) return result;

            int index = 0;
            foreach (var pair in stacks)
            {
                var stack = pair.Key;
                var metadata = pair.Value;
                if (stack == null)
                {
                    index++;
                    continue;
                }

                var group = new StackGroup
                {
                    name = string.IsNullOrWhiteSpace(metadata.Label) ? $"{metadata.Type} {index}" : $"{metadata.Type}: {metadata.Label}",
                    nodes = new List<TreeNode>()
                };

                var frames = stack.Nodes;
                if (frames != null)
                {
                    foreach (var node in frames.Reverse())
                    {
                        if (!node) continue;
                        group.nodes.Add(node);
                    }
                }

                result.Add(group);
                index++;
            }

            return result;
        }

        // TreeView types
        private class NodeTreeItem : TreeViewItem
        {
            public TreeNode Node { get; set; }
        }

        private class NodeStackTreeView : TreeView
        {
            private readonly AIInspector owner;
            private readonly Action<TreeNode> onSelection;

            private List<TreeViewItem> cachedItems;

            public NodeStackTreeView(TreeViewState state, AIInspector owner, Action<TreeNode> onSelection)
                : base(state)
            {
                this.owner = owner;
                this.onSelection = onSelection;
                showAlternatingRowBackgrounds = true;
            }

            public void SetItems(List<TreeViewItem> items)
            {
                cachedItems = items ?? new List<TreeViewItem>();
                Reload();
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem
                {
                    id = 0,
                    depth = -1,
                    displayName = "Root"
                };

                var items = cachedItems ?? new List<TreeViewItem>();
                SetupParentsAndChildrenFromDepths(root, items);
                return root;
            }

            protected override void SingleClickedItem(int id)
            {
                if (FindItem(id, rootItem) is NodeTreeItem item)
                {
                    onSelection?.Invoke(item.Node);
                }
            }

            protected override void DoubleClickedItem(int id)
            {
                if (FindItem(id, rootItem) is NodeTreeItem item)
                {
                    onSelection?.Invoke(item.Node);
                }
            }
        }

        #endregion
    }
}
