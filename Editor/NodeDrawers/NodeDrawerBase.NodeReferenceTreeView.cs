using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    public abstract partial class NodeDrawerBase
    {
        /// <summary>
        /// Tree view for managing node reference lists with reordering support.
        /// </summary>
        public sealed class NodeReferenceTreeView : TreeView
        {
            private const string DragDataKey = "Amlos.AI.NodeReferenceTreeView";

            private readonly NodeDrawerBase host;
            private SerializedProperty listProperty;
            private GUIContent label;
            private TreeNode parentNode;
            private Func<TreeNode, INodeReference> createReference;
            private RightWindow addWindow;
            private System.Action onAddOverride;
            private System.Action onAddMenuOverride;
            private Action<int> onRemoveOverride;
            private int lastDataHash;

            /// <summary>
            /// Creates a node reference tree view.
            /// </summary>
            /// <param name="state">Tree view state.</param>
            /// <param name="host">Host used for callbacks and data access.</param>
            public NodeReferenceTreeView(TreeViewState state, NodeDrawerBase host) : base(state)
            {
                this.host = host;

                showBorder = true;
                showAlternatingRowBackgrounds = true;
                rowHeight = EditorGUIUtility.singleLineHeight + 2f;
                //useCustomRowHeight = true;
            }

            /// <summary>
            /// Configure the tree view with the latest list data.
            /// </summary>
            /// <param name="label">Header label.</param>
            /// <param name="listProperty">Serialized list property.</param>
            /// <param name="parentNode">Parent node for new entries.</param>
            /// <param name="createReference">Factory for new list references.</param>
            /// <param name="addWindow">Selection window to use for adding nodes.</param>
            /// <param name="onAddOverride">Optional add action override.</param>
            /// <param name="onAddMenuOverride">Optional right-click menu override.</param>
            /// <param name="onRemoveOverride">Optional remove action override.</param>
            public void SetData(
                GUIContent label,
                SerializedProperty listProperty,
                TreeNode parentNode,
                Func<TreeNode, INodeReference> createReference,
                RightWindow addWindow,
                System.Action onAddOverride = null,
                System.Action onAddMenuOverride = null,
                Action<int> onRemoveOverride = null)
            {
                this.label = label;
                this.listProperty = listProperty;
                this.parentNode = parentNode;
                this.createReference = createReference;
                this.addWindow = addWindow;
                this.onAddOverride = onAddOverride;
                this.onAddMenuOverride = onAddMenuOverride;
                this.onRemoveOverride = onRemoveOverride;

                ReloadIfNeeded();
            }

            /// <summary>
            /// Draw the tree view with header actions.
            /// </summary>
            public void Draw()
            {
                DrawHeader();
                DrawTree();
            }

            private void DrawHeader()
            {
                using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    EditorGUILayout.LabelField(label);

                    Rect addRect = GUILayoutUtility.GetRect(NodeListHeaderButtonWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(NodeListHeaderButtonWidth));
                    Rect removeRect = GUILayoutUtility.GetRect(NodeListHeaderButtonWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(NodeListHeaderButtonWidth));

                    if (GUI.Button(addRect, "Add", EditorStyles.toolbarButton))
                    {
                        if (onAddOverride != null)
                        {
                            onAddOverride();
                        }
                        else
                        {
                            host.AddNodeReferenceToList(listProperty, parentNode, createReference, addWindow);
                        }
                    }

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && addRect.Contains(Event.current.mousePosition))
                    {
                        if (onAddMenuOverride != null)
                        {
                            onAddMenuOverride();
                        }
                        else
                        {
                            host.ShowNodeListAddMenu(listProperty, parentNode, createReference, addWindow);
                        }
                        Event.current.Use();
                    }

                    if (GUI.Button(removeRect, "Remove", EditorStyles.toolbarButton))
                    {
                        if (onRemoveOverride != null)
                        {
                            onRemoveOverride(GetSelectedIndex());
                        }
                        else
                        {
                            host.RemoveNodeListEntry(listProperty, GetSelectedIndex());
                        }
                    }
                }
            }

            private void DrawTree()
            {
                if (listProperty == null || !listProperty.isArray)
                {
                    EditorGUILayout.HelpBox("Node list is missing or invalid.", MessageType.Warning);
                    return;
                }

                float desiredHeight = Mathf.Clamp(totalHeight + 4f, NodeListMinHeight, NodeListMaxHeight);
                Rect treeRect = GUILayoutUtility.GetRect(0f, desiredHeight, GUILayout.ExpandWidth(true));
                OnGUI(treeRect);
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };

                if (listProperty == null || !listProperty.isArray)
                {
                    SetupDepthsFromParentsAndChildren(root);
                    return root;
                }

                int idCounter = 1;
                for (int i = 0; i < listProperty.arraySize; i++)
                {
                    root.AddChild(new NodeReferenceTreeViewItem(idCounter++, 0, i));
                }

                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                if (listProperty == null || args.item is not NodeReferenceTreeViewItem listItem)
                {
                    base.RowGUI(args);
                    return;
                }

                listProperty.serializedObject.Update();

                if (listItem.Index < 0 || listItem.Index >= listProperty.arraySize)
                {
                    base.RowGUI(args);
                    return;
                }

                SerializedProperty referenceProperty = listProperty.GetArrayElementAtIndex(listItem.Index);
                if (referenceProperty.boxedValue is not INodeReference reference)
                {
                    base.RowGUI(args);
                    return;
                }

                TreeNode node = host.tree.GetNode(reference.UUID);
                SerializedProperty nodeProperty = host.tree.GetNodeProperty(node);

                float lineHeight = EditorGUIUtility.singleLineHeight;
                float lineSpacing = 2f;

                Rect position = args.rowRect;
                position.xMin += GetContentIndent(args.item);
                position.y += 2f;

                Rect singleLine = position;
                singleLine.height = lineHeight;

                Rect singleButton = singleLine;
                singleButton.width = 80f;
                singleLine.x += 80f;

                if (node == null)
                {
                    EditorGUI.LabelField(singleLine, "Outdated");
                    return;
                }

                string typeLabel = GetNodeTypeLabel(node);
                if (!string.IsNullOrEmpty(typeLabel))
                {
                    GUI.Label(singleButton, $"{args.row + 1}.\t{typeLabel}");
                }

                singleButton.y += lineHeight + lineSpacing;

                singleLine.width -= singleButton.width + 25f;

                if (nodeProperty == null)
                {
                    EditorGUI.LabelField(singleLine, "Outdated");
                    return;
                }

                SerializedProperty nameProperty = nodeProperty.FindPropertyRelative(nameof(TreeNode.name));
                {
                    const float buttonWidth = 60f;
                    const float buttonSpacing = 0f;

                    Rect openRect = new Rect(singleLine.xMax - buttonWidth, singleLine.y, buttonWidth, singleLine.height);
                    Rect deleteRect = new Rect(openRect.x - buttonSpacing - buttonWidth, singleLine.y, buttonWidth, singleLine.height);
                    Rect leftRect = new Rect(singleLine.x, singleLine.y, deleteRect.x - singleLine.x - buttonSpacing - 10, singleLine.height);
                    Rect nameRect = leftRect;
                    nameRect.width *= 0.5f;
                    Rect scriptRect = leftRect;
                    scriptRect.xMin = nameRect.xMax + buttonSpacing;

                    EditorGUI.BeginChangeCheck();
                    EditorGUI.DelayedTextField(nameRect, nameProperty, GUIContent.none);
                    if (EditorGUI.EndChangeCheck())
                    {
                        nameProperty.serializedObject.ApplyModifiedProperties();
                    }

                    var script = MonoScriptCache.Get(node.GetType());
                    using (GUIEnable.By(false))
                    {
                        EditorGUI.ObjectField(scriptRect, script, typeof(MonoScript), false);
                    }

                    if (GUI.Button(deleteRect, "Delete"))
                    {
                        if (onRemoveOverride != null)
                        {
                            onRemoveOverride(listItem.Index);
                        }
                        else
                        {
                            host.RemoveNodeListEntry(listProperty, listItem.Index);
                        }
                        return;
                    }

                    if (GUI.Button(openRect, "Open"))
                    {
                        host.editor.SelectedNode = node;
                    }
                }

                singleLine.xMin = position.x;

                if (reference is Probability.EventWeight)
                {
                    singleLine.y += lineHeight + lineSpacing;
                    EditorGUI.PropertyField(singleLine, referenceProperty.FindPropertyRelative(nameof(Probability.EventWeight.weight)));
                }

                if (reference is PseudoProbability.EventWeight pw)
                {
                    singleLine.y += lineHeight + lineSpacing;
                    GUIContent weightDefaultLable = new("Weight");
                    var variable = pw.weight;

                    VariableFieldDrawers.DrawVariable(singleLine, weightDefaultLable, variable, host.tree, new VariableType[] { VariableType.Int, VariableType.Generic }, VariableAccessFlag.Read);

                    referenceProperty.boxedValue = pw;
                    referenceProperty.serializedObject.ApplyModifiedProperties();
                    referenceProperty.serializedObject.Update();
                }

                if (NodeDrawerUtility.showUUID)
                {
                    singleLine.y += lineHeight + lineSpacing;
                    EditorGUI.LabelField(singleLine, "UUID", node.uuid);
                }
            }

            protected override float GetCustomRowHeight(int row, TreeViewItem item)
            {
                if (listProperty == null || item is not NodeReferenceTreeViewItem listItem)
                {
                    return rowHeight;
                }

                if (listItem.Index < 0 || listItem.Index >= listProperty.arraySize)
                {
                    return rowHeight;
                }

                object element = listProperty.GetArrayElementAtIndex(listItem.Index).boxedValue;
                return GetNodeListRowHeight(element);
            }

            protected override bool CanMultiSelect(TreeViewItem item) => false;

            protected override bool CanStartDrag(CanStartDragArgs args) => args.draggedItemIDs.Count > 0;

            protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DragDataKey, args.draggedItemIDs);
                DragAndDrop.StartDrag("Reorder Node List");
            }

            protected override void DoubleClickedItem(int id)
            {
                FindItem(id, rootItem);
                var clickedItem = FindItem(id, rootItem) as NodeReferenceTreeViewItem;
                if (clickedItem == null || listProperty == null)
                {
                    return;
                }
                if (clickedItem.Index < 0 || clickedItem.Index >= listProperty.arraySize)
                {
                    return;
                }
                SerializedProperty referenceProperty = listProperty.GetArrayElementAtIndex(clickedItem.Index);
                if (referenceProperty.boxedValue is not INodeReference reference)
                {
                    return;
                }
                TreeNode node = host.editor.tree.GetNode(reference.UUID);
                if (node != null)
                {
                    host.editor.SelectedNode = node;
                }
            }

            protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
            {
                if (!IsValidDrag())
                {
                    return DragAndDropVisualMode.None;
                }

                if (args.performDrop)
                {
                    IList<int> draggedIds = DragAndDrop.GetGenericData(DragDataKey) as IList<int>;
                    if (draggedIds == null || draggedIds.Count == 0)
                    {
                        return DragAndDropVisualMode.None;
                    }

                    var draggedItem = FindItem(draggedIds[0], rootItem) as NodeReferenceTreeViewItem;
                    if (draggedItem == null)
                    {
                        return DragAndDropVisualMode.None;
                    }

                    int oldIndex = draggedItem.Index;
                    int newIndex = GetDropIndex(args);

                    if (newIndex == oldIndex)
                    {
                        return DragAndDropVisualMode.None;
                    }

                    if (oldIndex < newIndex)
                    {
                        newIndex = Mathf.Max(0, newIndex - 1);
                    }

                    host.ReorderNodeList(listProperty, oldIndex, newIndex);
                    Reload();
                    SetSelection(new[] { draggedItem.id });
                }

                return DragAndDropVisualMode.Move;
            }

            private bool IsValidDrag()
            {
                return listProperty != null && DragAndDrop.GetGenericData(DragDataKey) is IList<int>;
            }

            private int GetDropIndex(DragAndDropArgs args)
            {
                int insertIndex = args.insertAtIndex;
                if (args.dragAndDropPosition == DragAndDropPosition.UponItem && args.parentItem is NodeReferenceTreeViewItem targetItem)
                {
                    insertIndex = targetItem.Index;
                }

                if (insertIndex < 0)
                {
                    insertIndex = listProperty.arraySize - 1;
                }

                return Mathf.Clamp(insertIndex, 0, Mathf.Max(0, listProperty.arraySize - 1));
            }

            private int GetSelectedIndex()
            {
                IList<int> selection = GetSelection();
                if (selection == null || selection.Count == 0)
                {
                    return -1;
                }

                var selectedItem = FindItem(selection[0], rootItem) as NodeReferenceTreeViewItem;
                return selectedItem?.Index ?? -1;
            }

            private void ReloadIfNeeded()
            {
                if (listProperty == null)
                {
                    if (lastDataHash != 0)
                    {
                        lastDataHash = 0;
                        Reload();
                    }
                    return;
                }

                int newHash = GetNodeListDataHash(listProperty);
                if (newHash == lastDataHash)
                {
                    return;
                }

                lastDataHash = newHash;
                Reload();
            }
        }

        /// <summary>
        /// Tree view item that caches the list index.
        /// </summary>
        private sealed class NodeReferenceTreeViewItem : TreeViewItem
        {
            /// <summary>
            /// Gets the index in the serialized list.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Creates a node reference tree view item.
            /// </summary>
            /// <param name="id">Tree view id.</param>
            /// <param name="depth">Tree view depth.</param>
            /// <param name="index">Index in the serialized list.</param>
            public NodeReferenceTreeViewItem(int id, int depth, int index) : base(id, depth)
            {
                Index = index;
            }
        }

        /// <summary>
        /// Draw a node list with a TreeView-backed UI.
        /// </summary>
        protected NodeReferenceTreeView DrawNodeList<T>(string labelName, SerializedProperty list) where T : INodeReference, new()
            => DrawNodeList<T>(new GUIContent(labelName), list);

        /// <summary>
        /// Draw a node list with a TreeView-backed UI.
        /// </summary>
        protected NodeReferenceTreeView DrawNodeList<T>(GUIContent label, SerializedProperty list) where T : INodeReference, new()
        {
            var treeView = GetNodeListTreeView(list);
            treeView.SetData(label, list, node, newNode => new T { UUID = newNode.uuid }, RightWindow.All);
            treeView.Draw();
            return treeView;
        }

        /// <summary>
        /// Get or create a cached TreeView for a list property.
        /// </summary>
        private NodeReferenceTreeView GetNodeListTreeView(SerializedProperty list)
        {
            var key = (list.serializedObject.targetObject, list.propertyPath);
            if (!nodeListViews.TryGetValue(key, out var treeView))
            {
                var state = new TreeViewState();
                treeView = new NodeReferenceTreeView(state, this);
                nodeListViews.Add(key, treeView);
            }

            return treeView;
        }

        /// <summary>
        /// Adds a new node reference entry using the selection window.
        /// </summary>
        /// <param name="list">The serialized list to update.</param>
        /// <param name="parentNode">The parent node for the new entry.</param>
        /// <param name="createReference">Factory for creating the node reference.</param>
        /// <param name="window">The selection window to open.</param>
        private void AddNodeReferenceToList(SerializedProperty list, TreeNode parentNode, Func<TreeNode, INodeReference> createReference, RightWindow window)
        {
            if (list == null || createReference == null)
            {
                return;
            }

            editor.OpenSelectionWindow(window, (newNode) =>
            {
                list.serializedObject.Update();
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).boxedValue = createReference(newNode);
                list.serializedObject.ApplyModifiedProperties();
                newNode.parent = parentNode;
                list.serializedObject.Update();
            });
        }

        /// <summary>
        /// Shows the add menu with clipboard-aware options.
        /// </summary>
        /// <param name="list">The serialized list to update.</param>
        /// <param name="parentNode">The parent node owning the list.</param>
        /// <param name="createReference">Factory for creating the node reference.</param>
        /// <param name="window">The selection window to open.</param>
        private void ShowNodeListAddMenu(SerializedProperty list, TreeNode parentNode, Func<TreeNode, INodeReference> createReference, RightWindow window)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Add"), false, () => AddNodeReferenceToList(list, parentNode, createReference, window));

            var slot = parentNode?.GetListSlot();
            if (slot is not null)
            {
                menu.AddItem(new GUIContent("Paste Under (at first)"), false, () => editor.clipboard.PasteAsFirst(editor.tree, parentNode, slot));
                menu.AddItem(new GUIContent("Paste Under (at last)"), false, () => editor.clipboard.PasteAsLast(editor.tree, parentNode, slot));
            }

            menu.ShowAsContext();
        }

        /// <summary>
        /// Remove a list entry by index with delete confirmation.
        /// </summary>
        private void RemoveNodeListEntry(SerializedProperty list, int index)
        {
            if (list == null || list.arraySize == 0)
            {
                return;
            }

            if (index < 0 || index >= list.arraySize)
            {
                index = list.arraySize - 1;
            }

            DeleteReference(() => RemoveFromList(list, index));
        }

        /// <summary>
        /// Apply a serialized array element reordering.
        /// </summary>
        private void ReorderNodeList(SerializedProperty list, int oldIndex, int newIndex)
        {
            if (list == null || newIndex < 0 || newIndex > list.arraySize || oldIndex == newIndex)
            {
                return;
            }

            list.serializedObject.Update();
            list.MoveArrayElement(oldIndex, newIndex);
            list.serializedObject.ApplyModifiedProperties();
            list.serializedObject.Update();
        }

        /// <summary>
        /// Compute a stable hash for a node list to avoid unnecessary reloads.
        /// </summary>
        private static int GetNodeListDataHash(SerializedProperty list)
        {
            var hash = new HashCode();
            hash.Add(list.arraySize);
            hash.Add(NodeDrawerUtility.showUUID);

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i).GetValue();
                if (element is INodeReference reference)
                {
                    hash.Add(reference.UUID);
                    hash.Add(reference.IsRawReference);
                }
                else
                {
                    hash.Add(element?.GetHashCode() ?? 0);
                }
            }

            return hash.ToHashCode();
        }

        /// <summary>
        /// Determine row height for a node list element.
        /// </summary>
        private static float GetNodeListRowHeight(object element)
        {
            bool isWeighted = element is Probability.EventWeight || element is PseudoProbability.EventWeight;
            int lineCount = 1;
            if (isWeighted)
            {
                lineCount += 1;
            }
            if (NodeDrawerUtility.showUUID)
            {
                lineCount += 1;
            }
            return (EditorGUIUtility.singleLineHeight + 2f) * lineCount + 2f;
        }

        /// <summary>
        /// Resolve a display label for a node type.
        /// </summary>
        private static string GetNodeTypeLabel(TreeNode node)
        {
            return node switch
            {
                Service => "Service",
                Nodes.Action => "Action",
                Call => "Call",
                Flow => "Flow",
                Arithmetic => "Math",
                DetermineBase => "Determine",
                _ => null
            };
        }
    }
}
