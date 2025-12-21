using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Amlos.AI.Editor
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
        private bool nodeFoldout;
        [SerializeField]
        private bool variableFoldout;

        [SerializeField]
        private bool displayHidden;
        [SerializeField]
        private Vector2 scrollPos;
        [SerializeField]
        private Vector2 nodeRect;
        [SerializeField]
        private Vector2 varRect;

        private enum LayoutMode { Vertical, Horizontal }
        [SerializeField]
        private LayoutMode layoutMode = LayoutMode.Vertical;

        // TreeView state and selection
        private TreeViewState stackTreeState;
        private NodeStackTreeView stackTreeView;
        private TreeNode selectedNodeOverride;

        private void OnValidate()
        {
            SelectGameObject();
        }

        private void SelectGameObject()
        {
            var newSelected = Selection.activeGameObject;
            if (!newSelected) return;
            if (!newSelected.TryGetComponent(out AI aI)) return;

            if (selected != aI)
            {
                selected = aI;
            }
        }

        [MenuItem("Window/Aethiumian AI/AI Runtime Inspector")]
        public static AIInspector ShowWindow()
        {
            var window = GetWindow(typeof(AIInspector), false, "AI Inspector");
            window.name = "AI Inspector";
            return window as AIInspector;
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

        private void Draw()
        {
            SelectGameObject();
            //GUILayout.Toolbar(-1, new[] { "" });
            if (!selected)
            {
                EditorGUILayout.LabelField("You must select an AI to view AI status");
            }
            else if (!selected.data)
            {
                EditorGUILayout.LabelField("AI do not have a behaviour tree data.");
            }
            else if (selected.behaviourTree == null)
            {
                EditorGUILayout.LabelField("AI is not initialized");
            }
            else
            {
                DrawInspectorWindowHeader(selected.gameObject, ref inspectorLocked);
                EditorGUILayout.LabelField($"Instance of {selected.data.name}");
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Game Object", selected.gameObject, typeof(GameObject), true);

                using (EditorGUIIndent.Increase)
                using (new GUIScrollView(ref scrollPos))
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawWindow();
                }
            }
            GUILayout.FlexibleSpace();
            if (selected && selected.data && selected.behaviourTree != null)
                DrawToolbar();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject(this), typeof(MonoScript), false);
        }

        private void DrawTree()
        {
            treeFoldout = DrawSeparator(treeFoldout, "Tree");
            if (treeFoldout)
            {
                try
                {
                    // Debugging toggles
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Behaviour Tree", selected.data, typeof(BehaviourTreeData), true);
                    if (GUILayout.Button("Open Editor"))
                    {
                        AIEditorWindow.ShowWindow().Load(selected.data);
                    }
                    EditorGUILayout.LabelField("Head");
                    NodeDrawerUtility.DrawNodeBaseInfo(selected.data, selected.data.Head, true);
                    EditorGUILayout.Space(8);
                    selected.behaviourTree.Debugging = EditorGUILayout.Toggle("Debug", selected.behaviourTree.Debugging);
                    if (selected.behaviourTree.IsRunning && selected.behaviourTree.MainStack != null)
                    {
                        selected.behaviourTree.MainStack.IsPaused = EditorGUILayout.Toggle("Pause", selected.behaviourTree.MainStack.IsPaused);
                    }
                    EditorGUILayout.Space(12);
                }
                catch { }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawWindow()
        {
            DrawTree();

            if (layoutMode == LayoutMode.Horizontal)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawNodeFieldStatus();
                    DrawVariable();
                }
            }
            else
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawNodeFieldStatus();
                    DrawVariable();
                }
            }
        }

        private void DrawNodeFieldStatus()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                // Active stacks tree
                DrawStacks();
                // Node inspector
                DrawInspector(selectedNodeOverride ?? selected.behaviourTree.CurrentStage.Node);
            }
        }

        private void DrawVariable()
        {
            BeginVerticleAndSetWindowColor();
            variableFoldout = DrawSeparator(variableFoldout, "Variables");
            using (new EditorGUI.DisabledScope(selected.behaviourTree == null || !selected.behaviourTree.IsRunning))
                if (variableFoldout)
                {
                    EditorGUILayout.LabelField("Instance", EditorStyles.boldLabel);
                    DrawVariableTable(selected.behaviourTree?.EditorVariables);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Static", EditorStyles.boldLabel);
                    DrawVariableTable(selected.behaviourTree?.EditorStaticVariables);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);
                    DrawVariableTable(BehaviourTree.EditorGlobalVariables);
                }
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var newLayoutIndex = GUILayout.Toolbar((int)layoutMode, new[] { nameof(LayoutMode.Vertical), nameof(LayoutMode.Horizontal) }, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (newLayoutIndex != (int)layoutMode)
                {
                    layoutMode = (LayoutMode)newLayoutIndex;
                }

                GUILayout.FlexibleSpace();
            }

            if (!selected.behaviourTree.IsRunning)
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
                int index = GUILayout.Toolbar(-1, new[] { (selected.behaviourTree.IsPaused ? "Continue" : "Pause"), "Restart" });
                switch (index)
                {
                    case 0:
                        if (!selected.behaviourTree.IsRunning)
                            break;
                        if (selected.behaviourTree.IsPaused)
                        {
                            selected.Continue();
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
                    var newVal = EditorFieldDrawers.DrawField(variable.Name.ToTitleCase(), variable.Value, variable.ObjectType);
                    if (variable.Value == null)
                    {
                        if (newVal != null)
                        {
                            variable.SetValue(newVal);
                        }
                        continue;
                    }
                    if (!variable.Value.Equals(newVal))
                    {
                        variable.SetValue(newVal);
                    }
                }
            }
        }





        #region Inspector

        private void DrawInspector(TreeNode node)
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

                        NodeDrawerUtility.DrawNodeBaseInfo(selected.data, node);
                        foreach (var fieldInfo in GetAllFields(node.GetType(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            DrawMember(node, fieldInfo);
                        }
                    }
                }
                catch { }
                EditorGUILayout.Space(12);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawMember(TreeNode node, FieldInfo fieldInfo)
        {
            if (fieldInfo.Name == nameof(node.name)) return;
            if (fieldInfo.Name == nameof(node.uuid)) return;
            if (fieldInfo.Name == nameof(node.services)) return;
            if (fieldInfo.Name == nameof(node.behaviourTree)) return;

            if (IsDelegateLike(fieldInfo.FieldType)) return;

            var property = ResolveAutoProperty(fieldInfo);
            if (property != null && Attribute.IsDefined(property, typeof(Amlos.AI.AIInspectorIgnoreAttribute), true)) return;

            if (Attribute.IsDefined(fieldInfo, typeof(Amlos.AI.AIInspectorIgnoreAttribute), true)) return;

            if (Attribute.IsDefined(fieldInfo, typeof(DisplayIfAttribute)) && !displayHidden)
            {
                try
                {
                    if (!ConditionalFieldAttribute.IsTrue(node, fieldInfo)) return;
                }
                catch (Exception)
                {
                    EditorGUILayout.LabelField(NormalizeFieldLabel(fieldInfo).ToTitleCase(), "DisplayIf attribute breaks, ask for help");
                    return;
                }
            }

            DrawField(node, fieldInfo);
        }

        private void DrawField(TreeNode node, FieldInfo fieldInfo)
        {
            var labelName = NormalizeFieldLabel(fieldInfo).ToTitleCase();

            var value = fieldInfo.GetValue(node);
            if (value is VariableBase variablefield)
            {
                VariableFieldDrawers.DrawVariable("[Var] " + labelName, variablefield, selected.data);
            }
            else if (value is INodeReference nodeReference)
            {
                var referTo = nodeReference.Node;
                if (referTo != null)
                {
                    EditorGUILayout.LabelField(labelName, $"Node {referTo.name} ({referTo.uuid})");
                }
                else EditorGUILayout.LabelField(labelName, "Node (null)");
            }
            else if (value != null)
            {
                fieldInfo.SetValue(node, EditorFieldDrawers.DrawField(labelName, value, fieldInfo.FieldType));
            }
            else
            {
                EditorGUILayout.LabelField(labelName, "null");
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





        private void DrawStacks()
        {
            stackFoldout = DrawSeparator(stackFoldout, "Stack");
            if (stackFoldout)
            {
                try
                {
                    EnsureTreeView();
                    stackTreeView?.SetItems(BuildStackTreeItems());
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
                catch { }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        internal void Load(AI ai)
        {
            selected = ai;

        }





        #region Helper

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
        private List<TreeViewItem> BuildStackTreeItems()
        {
            var items = new List<TreeViewItem>();
            int id = 1;

            // Main stack
            var mainGroup = new TreeViewItem { id = id++, depth = 1, displayName = "Main Stack" };
            items.Add(mainGroup);
            foreach (var node in EnumerateMainStackNodes())
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

            // Services
            var servicesGroupItems = EnumerateServiceStacks();
            if (servicesGroupItems.Count > 0)
            {
                //var services = new TreeViewItem { id = id++, depth = 1, displayName = "Services" };
                //items.Add(services);
                for (int i = 0; i < servicesGroupItems.Count; i++)
                {
                    var group = servicesGroupItems[i];
                    var serviceItem = new TreeViewItem
                    {
                        id = id++,
                        depth = 1,
                        displayName = string.IsNullOrEmpty(group.name) ? $"Service {i}" : group.name
                    };
                    items.Add(serviceItem);

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
            }

            return items;
        }

        private string MakeNodeLabel(TreeNode node)
        {
            if (!node) return "(null)";
            var typeName = node.GetType().Name;
            var n = string.IsNullOrWhiteSpace(node.name) ? typeName : node.name;
            return $"{n} [{typeName}]";
        }

        private IEnumerable<TreeNode> EnumerateMainStackNodes()
        {
            if (selected == null || selected.behaviourTree == null) yield break;
            var nodes = selected.behaviourTree.MainStack?.Nodes;
            if (nodes == null) yield break;

            foreach (var node in nodes.Reverse())
            {
                if (!node) continue;
                yield return node;
            }
        }

        private struct ServiceGroup
        {
            public string name;
            public List<TreeNode> nodes;
        }

        private List<ServiceGroup> EnumerateServiceStacks()
        {
            var result = new List<ServiceGroup>();
            var bt = selected != null ? selected.behaviourTree : null;
            if (bt == null) return result;

            var services = bt.ServiceStacks;
            if (services == null) return result;

            int index = 0;
            foreach (var pair in services)
            {
                var svc = pair.Key;
                var svcStack = pair.Value;
                if (svc == null || svcStack == null)
                {
                    index++;
                    continue;
                }

                var group = new ServiceGroup
                {
                    name = string.IsNullOrWhiteSpace(svc.name) ? $"Service {index}" : svc.name,
                    nodes = new List<TreeNode>()
                };

                var frames = svcStack?.Nodes;
                if (frames != null)
                {
                    foreach (var node in frames)
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