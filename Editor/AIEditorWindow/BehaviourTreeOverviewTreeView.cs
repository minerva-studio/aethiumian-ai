using Amlos.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Amlos.AI.Editor
{
    internal sealed class BehaviourTreeOverviewTreeView : TreeView
    {
        private sealed class OverviewItem : TreeViewItem
        {
            public TreeNode Node { get; set; }
            public bool IsGroup { get; set; }
            public bool IsUnreachableRoot { get; set; }
        }

        private BehaviourTreeData tree;
        private HashSet<TreeNode> reachableNodes;
        private TreeNode selectedNode;

        private bool showService;
        private Action<TreeNode> onSelectNode;
        private Action<TreeNode, GenericMenu> buildContextMenu;

        private Func<TreeNode> getLocalRoot;
        private int idCounter;

        public BehaviourTreeOverviewTreeView(TreeViewState state) : base(state)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            rowHeight = EditorGUIUtility.singleLineHeight + 4;
        }

        public void SetData(
            BehaviourTreeData tree,
            HashSet<TreeNode> reachableNodes,
            TreeNode selectedNode,
            TreeNodeModule.Mode mode,
            bool showService,
            TreeNode editorHeadNode,
            Func<TreeNode> getSelectedNodeParent,
            Action<TreeNode> onSelectNode,
            Action<TreeNode, GenericMenu> buildContextMenu)
        {
            this.tree = tree;
            this.reachableNodes = reachableNodes;
            this.selectedNode = selectedNode;
            this.showService = showService;
            this.onSelectNode = onSelectNode;
            this.buildContextMenu = buildContextMenu;

            getLocalRoot = () =>
            {
                if (tree == null)
                {
                    return null;
                }

                if (mode == TreeNodeModule.Mode.Global)
                {
                    return tree.Head;
                }

                if (selectedNode == null || selectedNode == editorHeadNode)
                {
                    return tree.Head;
                }

                if (selectedNode == tree.Head)
                {
                    return tree.Head;
                }

                return getSelectedNodeParent?.Invoke() ?? selectedNode;
            };

            Reload();

            int? id = FindIdByNode(selectedNode);
            if (id.HasValue)
            {
                SetSelection(new List<int> { id.Value }, TreeViewSelectionOptions.RevealAndFrame);
            }
        }

        protected override TreeViewItem BuildRoot()
        {
            idCounter = 1;
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root", children = new List<TreeViewItem>() };
            if (!tree)
            {
                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            TreeNode mainRoot = getLocalRoot?.Invoke();
            if (mainRoot != null)
            {
                root.AddChild(BuildNodeSubTree(mainRoot, isUnreachableRoot: false));
            }

            if (reachableNodes != null && tree.EditorNodes != null)
            {
                var unreachables = tree.EditorNodes.Where(n => n != null && !reachableNodes.Contains(n)).ToList();
                for (int i = 0; i < unreachables.Count; i++)
                {
                    var node = unreachables[i];
                    if (node == null || node == mainRoot)
                    {
                        continue;
                    }

                    root.AddChild(BuildNodeSubTree(node, isUnreachableRoot: true));
                }
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        private TreeViewItem BuildNodeSubTree(TreeNode node, bool isUnreachableRoot)
        {
            var item = new OverviewItem
            {
                id = idCounter++,
                displayName = node?.name ?? "<Null>",
                Node = node,
                IsGroup = false,
                IsUnreachableRoot = isUnreachableRoot,
                children = new List<TreeViewItem>()
            };

            if (node == null)
            {
                return item;
            }

            if (showService && node.services != null && node.services.Count > 0)
            {
                var serviceGroup = new OverviewItem
                {
                    id = idCounter++,
                    displayName = "Service",
                    Node = node,
                    IsGroup = true,
                    IsUnreachableRoot = isUnreachableRoot,
                    children = new List<TreeViewItem>()
                };

                for (int i = 0; i < node.services.Count; i++)
                {
                    var serviceNode = tree.GetNode(node.services[i].UUID);
                    if (serviceNode == null)
                    {
                        continue;
                    }

                    serviceGroup.AddChild(BuildNodeSubTree(serviceNode, isUnreachableRoot));
                }

                if (serviceGroup.hasChildren)
                {
                    item.AddChild(serviceGroup);
                }
            }

            var children = node.GetChildrenReference();
            if (children != null && children.Count > 0)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    TreeNode childNode = tree.GetNode(children[i].UUID);
                    if (childNode == null || childNode == node || childNode is Service)
                    {
                        continue;
                    }

                    item.AddChild(BuildNodeSubTree(childNode, isUnreachableRoot));
                }
            }

            return item;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item is not OverviewItem item)
            {
                base.RowGUI(args);
                return;
            }

            if (item.IsUnreachableRoot && !item.IsGroup)
            {
                EditorGUI.DrawRect(args.rowRect, new Color(1f, 0.2f, 0.2f, 0.12f));
            }

            Rect centeredRect = args.rowRect;
            centeredRect.y += (rowHeight - EditorGUIUtility.singleLineHeight) * 0.5f;
            centeredRect.height = EditorGUIUtility.singleLineHeight;

            float indent = GetContentIndent(item);
            centeredRect.x += indent;
            centeredRect.width -= indent;

            Color old = GUI.contentColor;
            if (item.IsGroup)
            {
                GUI.contentColor = new Color(0.8f, 0.8f, 0.8f);
            }

            EditorGUI.LabelField(centeredRect, item.displayName);

            GUI.contentColor = old;
        }

        protected override void SingleClickedItem(int id)
        {
            if (FindItem(id, rootItem) is not OverviewItem item)
            {
                return;
            }

            var node = item.IsGroup ? item.Node : item.Node;
            if (node != null)
            {
                onSelectNode?.Invoke(node);
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            if (FindItem(id, rootItem) is not OverviewItem item)
            {
                return;
            }

            if (item.hasChildren)
            {
                SetExpanded(id, !IsExpanded(id));
                return;
            }

            SingleClickedItem(id);
        }

        protected override void ContextClickedItem(int id)
        {
            if (FindItem(id, rootItem) is not OverviewItem item)
            {
                return;
            }

            var node = item.IsGroup ? item.Node : item.Node;
            if (node == null)
            {
                return;
            }

            GenericMenu menu = new();
            buildContextMenu?.Invoke(node, menu);
            menu.ShowAsContext();
        }

        protected override bool CanMultiSelect(TreeViewItem item) => false;

        protected override bool CanRename(TreeViewItem item)
        {
            if (item is not OverviewItem overviewItem)
            {
                return false;
            }

            if (overviewItem.IsGroup)
            {
                return false;
            }

            return overviewItem.Node != null;
        }

        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (!args.acceptedRename)
            {
                return;
            }

            if (tree == null)
            {
                return;
            }

            if (FindItem(args.itemID, rootItem) is not OverviewItem item)
            {
                return;
            }

            if (item.Node == null || item.IsGroup)
            {
                return;
            }

            string newName = (args.newName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(newName))
            {
                return;
            }

            if (string.Equals(item.Node.name, newName, StringComparison.Ordinal))
            {
                return;
            }

            Undo.RecordObject(tree, $"Rename node {item.Node.name}");
            item.Node.name = newName;
            EditorUtility.SetDirty(tree);

            item.displayName = newName;
            Reload();

            int? id = FindIdByNode(item.Node);
            if (id.HasValue)
            {
                SetSelection(new List<int> { id.Value }, TreeViewSelectionOptions.RevealAndFrame);
            }
        }

        protected override void KeyEvent()
        {
            var evt = Event.current;
            if (evt == null)
            {
                base.KeyEvent();
                return;
            }

            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.F2)
            {
                TreeViewItem currentSelection = GetSelection().Count == 1 ? FindItem(GetSelection()[0], rootItem) : null;
                if (currentSelection != null && CanRename(currentSelection))
                {
                    BeginRename(currentSelection);
                    evt.Use();
                    return;
                }
            }

            base.KeyEvent();
        }


        #region Drag

        protected override bool CanStartDrag(CanStartDragArgs args)
        {
            if (args.draggedItemIDs == null || args.draggedItemIDs.Count != 1)
            {
                return false;
            }

            if (FindItem(args.draggedItemIDs[0], rootItem) is not OverviewItem item)
            {
                return false;
            }

            if (item.Node == null || item.IsGroup)
            {
                return false;
            }

            return true;
        }

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            if (args.draggedItemIDs == null || args.draggedItemIDs.Count != 1)
            {
                return;
            }

            if (FindItem(args.draggedItemIDs[0], rootItem) is not OverviewItem item)
            {
                return;
            }

            if (item.Node == null || item.IsGroup)
            {
                return;
            }

            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(nameof(BehaviourTreeOverviewTreeView), item.Node);
            DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
            DragAndDrop.StartDrag(item.displayName);
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            if (tree == null)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (DragAndDrop.GetGenericData(nameof(BehaviourTreeOverviewTreeView)) is not TreeNode draggedNode)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (draggedNode == null || draggedNode is Service)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (args.parentItem is not OverviewItem parentItem)
            {
                return DragAndDropVisualMode.Rejected;
            }

            TreeNode targetParent = ResolveDropTargetParent(parentItem, args.insertAtIndex);
            if (targetParent == null)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (targetParent is Service)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (targetParent == draggedNode)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (draggedNode.IsParentOf(targetParent))
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (args.performDrop)
            {
                ApplyMoveOrRebindWithSlotMenu(draggedNode, targetParent, args.insertAtIndex);
            }

            return DragAndDropVisualMode.Move;
        }

        private TreeNode ResolveDropTargetParent(OverviewItem parentItem, int insertAtIndex)
        {
            if (parentItem == null || parentItem.Node == null)
            {
                return null;
            }

            if (parentItem.IsGroup)
            {
                return parentItem.Node;
            }

            return parentItem.Node;
        }

        private void ApplyMoveOrRebindWithSlotMenu(TreeNode draggedNode, TreeNode targetParent, int insertAtIndex)
        {
            if (draggedNode == null || targetParent == null || tree == null)
            {
                return;
            }

            TreeNode oldParent = tree.GetParent(draggedNode);
            if (oldParent == null)
            {
                return;
            }

            if (oldParent == targetParent && TryReorderInSameParent(oldParent, draggedNode, insertAtIndex))
            {
                return;
            }

            var targetSlots = targetParent
                .ToReferenceSlots()
                .Where(SlotCanAcceptDraggedNode)
                .ToList();

            if (targetSlots.Count == 0)
            {
                return;
            }

            if (targetSlots.Count == 1)
            {
                Undo.RecordObject(tree, $"Move node {draggedNode.name}");
                DetachFromOldParent(oldParent, draggedNode);
                AttachToSlot(targetSlots[0], draggedNode, insertAtIndex);
                draggedNode.parent = targetParent;
                EditorUtility.SetDirty(tree);
                ReloadAndReveal(draggedNode);
                return;
            }

            GenericMenu menu = new();
            for (int i = 0; i < targetSlots.Count; i++)
            {
                var slot = targetSlots[i];
                menu.AddItem(new GUIContent(slot.Name), false, () =>
                {
                    Undo.RecordObject(tree, $"Move node {draggedNode.name}");
                    DetachFromOldParent(oldParent, draggedNode);
                    AttachToSlot(slot, draggedNode, insertAtIndex);
                    draggedNode.parent = targetParent;
                    EditorUtility.SetDirty(tree);
                    ReloadAndReveal(draggedNode);
                });
            }

            menu.ShowAsContext();

            bool SlotCanAcceptDraggedNode(INodeReferenceSlot slot)
            {
                if (slot == null)
                {
                    return false;
                }

                if (slot.Contains(draggedNode))
                {
                    return false;
                }

                return slot is INodeReferenceSingleSlot || slot is INodeReferenceListSlot;
            }
        }

        private bool TryReorderInSameParent(TreeNode parent, TreeNode draggedNode, int insertAtIndex)
        {
            var slots = parent.ToReferenceSlots();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] is not INodeReferenceListSlot listSlot)
                {
                    continue;
                }

                int oldIndex = listSlot.IndexOf(draggedNode);
                if (oldIndex < 0)
                {
                    continue;
                }

                int newIndex = insertAtIndex < 0 ? listSlot.Count : Mathf.Clamp(insertAtIndex, 0, listSlot.Count);
                if (newIndex == oldIndex || newIndex == oldIndex + 1)
                {
                    return true;
                }

                Undo.RecordObject(tree, $"Reorder node {draggedNode.name}");

                listSlot.Remove(draggedNode);
                if (newIndex > oldIndex)
                {
                    newIndex--;
                }
                listSlot.Insert(newIndex, draggedNode);

                draggedNode.parent = parent;

                EditorUtility.SetDirty(tree);
                ReloadAndReveal(draggedNode);
                return true;
            }

            return false;
        }

        private static void DetachFromOldParent(TreeNode oldParent, TreeNode draggedNode)
        {
            var oldSlots = oldParent.ToReferenceSlots();
            for (int i = 0; i < oldSlots.Count; i++)
            {
                var slot = oldSlots[i];
                if (!slot.Contains(draggedNode))
                {
                    continue;
                }

                if (slot is INodeReferenceSingleSlot single)
                {
                    single.Clear();
                    return;
                }

                if (slot is INodeReferenceListSlot list)
                {
                    list.Remove(draggedNode);
                    return;
                }
            }
        }

        private static void AttachToSlot(INodeReferenceSlot slot, TreeNode draggedNode, int insertAtIndex)
        {
            if (slot is INodeReferenceSingleSlot single)
            {
                single.Set(draggedNode);
                return;
            }

            if (slot is INodeReferenceListSlot list)
            {
                int index = insertAtIndex < 0 ? list.Count : Mathf.Clamp(insertAtIndex, 0, list.Count);
                list.Insert(index, draggedNode);
            }
        }

        private void ReloadAndReveal(TreeNode node)
        {
            Reload();

            int? id = FindIdByNode(node);
            if (id.HasValue)
            {
                SetSelection(new List<int> { id.Value }, TreeViewSelectionOptions.RevealAndFrame);
            }
        }

        private int? FindIdByNode(TreeNode node)
        {
            if (node == null || rootItem == null)
            {
                return null;
            }

            var rows = GetRows();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is OverviewItem oi && oi.Node == node && !oi.IsGroup)
                {
                    return oi.id;
                }
            }
            return null;
        }

        #endregion
    }
}