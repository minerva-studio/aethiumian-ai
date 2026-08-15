using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Aethiumian.AI.Editor.AIEditorWindow;
#if UNITY_6000_3_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
#endif

namespace Aethiumian.AI.Editor
{
    public abstract partial class NodeDrawerBase
    {
        /// <summary>
        /// Tree view for managing node reference lists with reordering support.
        /// </summary>
        public sealed class NodeReferenceTreeView : TreeView
        {
            private const float NodeListMinHeight = 24f;
            private const float NodeListMaxHeight = 320f;
            private const float NodeListHeaderButtonWidth = 20f;
            private const float NodeListIndexWidth = 28f;
            private const string DragDataKey = "Aethiumian.AI.NodeReferenceTreeView";

            private readonly NodeDrawerBase host;
            private SerializedProperty listProperty;
            private GUIContent label;
            private TreeNode parentNode;
            private Func<TreeNode, INodeReference> createReference;
            private NodeSelectionContext selectionContext;
            private System.Action<Rect> onAddOverride;
            private System.Action<Rect> onAddMenuOverride;
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
            /// <param name="selectionContext">Node catalogue to use for adding nodes.</param>
            /// <param name="onAddOverride">Optional add action override receiving the Add button rectangle.</param>
            /// <param name="onAddMenuOverride">Optional right-click menu override receiving the Add button rectangle.</param>
            public void SetData(
                GUIContent label,
                SerializedProperty listProperty,
                TreeNode parentNode,
                Func<TreeNode, INodeReference> createReference,
                NodeSelectionContext selectionContext,
                System.Action<Rect> onAddOverride = null,
                System.Action<Rect> onAddMenuOverride = null)
            {
                this.label = label;
                this.listProperty = listProperty;
                this.parentNode = parentNode;
                this.createReference = createReference;
                this.selectionContext = selectionContext;
                this.onAddOverride = onAddOverride;
                this.onAddMenuOverride = onAddMenuOverride;

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

                    if (GUI.Button(addRect, "+", EditorStyles.toolbarButton))
                    {
                        if (onAddOverride != null)
                        {
                            onAddOverride(addRect);
                        }
                        else
                        {
                            host.AddNodeReferenceToList(listProperty, parentNode, createReference, selectionContext, addRect);
                        }
                    }

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && addRect.Contains(Event.current.mousePosition))
                    {
                        if (onAddMenuOverride != null)
                        {
                            onAddMenuOverride(addRect);
                        }
                        else
                        {
                            host.ShowNodeListAddMenu(listProperty, parentNode, createReference, selectionContext, addRect);
                        }
                        Event.current.Use();
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
                    Rect outdatedRect = args.rowRect;
                    outdatedRect.xMin += GetContentIndent(args.item);
                    outdatedRect.y += 2f;
                    outdatedRect.height = EditorGUIUtility.singleLineHeight;
                    Rect outdatedIndexRect = new(
                        outdatedRect.x,
                        outdatedRect.y,
                        Mathf.Min(NodeListIndexWidth, outdatedRect.width),
                        outdatedRect.height);
                    Rect outdatedLabelRect = new(
                        outdatedIndexRect.xMax,
                        outdatedRect.y,
                        Mathf.Max(0f, outdatedRect.xMax - outdatedIndexRect.xMax),
                        outdatedRect.height);
                    EditorGUI.LabelField(outdatedIndexRect, $"{args.row + 1}.");
                    EditorGUI.LabelField(outdatedLabelRect, "Outdated node");
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

                float overflowWidth = HasStableOccurrence(reference, listItem.Index)
                    ? GraphInspectorLayout.OverflowWidth
                    : 0f;
                Rect indexRect = new(singleLine.x, singleLine.y, Mathf.Min(NodeListIndexWidth, singleLine.width), singleLine.height);
                Rect overflowRect = new(singleLine.xMax - overflowWidth, singleLine.y, overflowWidth, singleLine.height);
                Rect nameRect = new(
                    indexRect.xMax,
                    singleLine.y,
                    Mathf.Max(0f, overflowRect.x - indexRect.xMax),
                    singleLine.height);

                EditorGUI.LabelField(indexRect, $"{args.row + 1}.");

                SerializedProperty nameProperty = nodeProperty?.FindPropertyRelative(nameof(TreeNode.name));
                if (node == null || nameProperty == null)
                {
                    string outdatedLabel = reference.UUID == UUID.Empty ? "Outdated node" : $"Outdated node ({reference.UUID})";
                    EditorGUI.LabelField(nameRect, new GUIContent(outdatedLabel, outdatedLabel));
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.DelayedTextField(nameRect, nameProperty, GUIContent.none);
                    if (EditorGUI.EndChangeCheck())
                    {
                        nameProperty.serializedObject.ApplyModifiedProperties();
                    }
                }

                if (overflowWidth > 0f && GUI.Button(overflowRect, "⋮", EditorStyles.miniButton))
                {
                    ShowRowOverflow(reference.UUID, listItem.Index);
                }

                if (node == null || nameProperty == null)
                {
                    return;
                }

                singleLine.xMin = position.x;

                if (reference is Probability.EventWeight)
                {
                    singleLine.y += lineHeight + lineSpacing;
                    EditorGUI.PropertyField(singleLine, referenceProperty.FindPropertyRelative(nameof(Probability.EventWeight.weight)));
                }

                if (reference is PseudoProbability.EventWeight)
                {
                    singleLine.y += lineHeight + lineSpacing;
                    GUIContent weightDefaultLable = new("Weight");
                    SerializedProperty weightProperty = referenceProperty.FindPropertyRelative(nameof(PseudoProbability.EventWeight.weight));

                    VariableFieldDrawers.DrawVariable(singleLine, weightDefaultLable, weightProperty, new VariableType[] { VariableType.Int, VariableType.Generic }, VariableAccessFlag.Read);
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
                    insertIndex = listProperty.arraySize;
                }

                return Mathf.Clamp(insertIndex, 0, Mathf.Max(0, listProperty.arraySize));
            }

            /// <summary>Returns whether a row has enough stable identity for overflow commands.</summary>
            private bool HasStableOccurrence(INodeReference reference, int index)
            {
                return reference?.UUID != UUID.Empty
                    && parentNode != null
                    && parentNode.uuid != UUID.Empty
                    && index >= 0
                    && !string.IsNullOrEmpty(GetRelativeNodePropertyPath(listProperty?.propertyPath));
            }

            /// <summary>Shows commands for one authored occurrence captured by stable identity.</summary>
            private void ShowRowOverflow(UUID expectedTargetUuid, int expectedIndex)
            {
                UUID ownerUuid = parentNode.uuid;
                string fieldName = GetRelativeNodePropertyPath(listProperty.propertyPath);
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Open"), false, () =>
                {
                    TreeNode currentNode = host.tree.GetNode(expectedTargetUuid);
                    if (currentNode != null)
                    {
                        host.editor.SelectedNode = currentNode;
                    }
                });
                menu.AddItem(new GUIContent("Delete"), false, () =>
                {
                    TreeNode ResolveCurrentOccurrence()
                    {
                        return host.ResolveNodeListOccurrence(ownerUuid, fieldName, expectedIndex, expectedTargetUuid);
                    }

                    bool RemoveCurrentOccurrence()
                    {
                        return host.tree.TryDisconnectReference(
                            ownerUuid,
                            fieldName,
                            expectedIndex,
                            $"Remove node reference from {fieldName}",
                            expectedTargetUuid);
                    }

                    host.ConfirmDeleteReference(
                        ResolveCurrentOccurrence,
                        RemoveCurrentOccurrence,
                        host.editor.Refresh,
                        host.editor.TreeModule.ShowConnectionRejectedNotification);
                });
                menu.ShowAsContext();
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
            treeView.SetData(label, list, node, newNode => new T { UUID = newNode.uuid }, NodeSelectionContext.Nodes);
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
        /// Adds a new node reference entry using the selection dropdown.
        /// </summary>
        /// <param name="list">The serialized list to update.</param>
        /// <param name="parentNode">The parent node for the new entry.</param>
        /// <param name="createReference">Factory for creating the node reference.</param>
        /// <param name="context">The node catalogue to display.</param>
        /// <param name="anchor">The button rectangle used to anchor the dropdown.</param>
        private void AddNodeReferenceToList(
            SerializedProperty list,
            TreeNode parentNode,
            Func<TreeNode, INodeReference> createReference,
            NodeSelectionContext context,
            Rect anchor = default)
        {
            if (list == null || createReference == null)
            {
                return;
            }

            string relativeListPath = GetRelativeNodePropertyPath(list.propertyPath);
            if (string.IsNullOrEmpty(relativeListPath))
            {
                return;
            }
            UUID ownerUUID = parentNode?.uuid ?? UUID.Empty;
            editor.OpenNodeChoiceDropdown(context, choice =>
            {
                TreeNode currentOwner = tree.GetNode(ownerUUID);
                if (currentOwner == null)
                {
                    return;
                }

                if (!editor.TreeModule.CommitChoiceToCollection(
                    choice,
                    context,
                    currentOwner.uuid,
                    relativeListPath,
                    -1,
                    "Add node reference"))
                {
                    editor.TreeModule.ShowConnectionRejectedNotification();
                }
            },
            anchor,
            candidate => candidate != null
                && tree.CanInsertReference(ownerUUID, relativeListPath, candidate.uuid, allowMoveExisting: true));
        }

        /// <summary>Converts a serialized node property path into an owner-relative field path.</summary>
        /// <param name="propertyPath">The serialized path containing the node array index.</param>
        /// <returns>The relative field path, or an empty path when it cannot be resolved.</returns>
        private static string GetRelativeNodePropertyPath(string propertyPath)
        {
            const string separator = "].";
            int separatorIndex = propertyPath?.IndexOf(separator, StringComparison.Ordinal) ?? -1;
            return separatorIndex < 0 ? string.Empty : propertyPath[(separatorIndex + separator.Length)..];
        }

        /// <summary>
        /// Shows the add menu with clipboard-aware options.
        /// </summary>
        /// <param name="list">The serialized list to update.</param>
        /// <param name="parentNode">The parent node owning the list.</param>
        /// <param name="createReference">Factory for creating the node reference.</param>
        /// <param name="context">The node catalogue to display.</param>
        /// <param name="anchor">The button rectangle used to anchor the dropdown.</param>
        private void ShowNodeListAddMenu(
            SerializedProperty list,
            TreeNode parentNode,
            Func<TreeNode, INodeReference> createReference,
            NodeSelectionContext context,
            Rect anchor)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Add"), false, () => AddNodeReferenceToList(list, parentNode, createReference, context, anchor));

            var slot = parentNode?.GetListSlot();
            if (slot is not null)
            {
                menu.AddItem(new GUIContent("Paste Under (at first)"), false, () => PasteFromClipboard(slot, 0));
                menu.AddItem(new GUIContent("Paste Under (at last)"), false, () => PasteFromClipboard(slot, slot.Count));
            }

            menu.ShowAsContext();

            void PasteFromClipboard(INodeReferenceListSlot targetSlot, int index)
            {
                TreeNode pasted = editor.TreeModule.PasteAt(parentNode, targetSlot, index);
                if (pasted != null)
                {
                    editor.Refresh();
                }
                else if (editor.TreeModule.CanPasteStructure)
                {
                    editor.TreeModule.ShowConnectionRejectedNotification();
                }
            }
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
                var element = list.GetArrayElementAtIndex(i).GetAIValue();
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

    }
}
