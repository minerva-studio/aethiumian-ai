using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
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
        private const float IconPadding = 4f;
        private const float ConditionBadgeScale = 0.75f;

        private sealed class OverviewItem : TreeViewItem
        {
            public TreeNode Node { get; set; }
            public bool IsGroup { get; set; }
            public bool IsUnreachableRoot { get; set; }
        }

        private TreeNodeModule treeNodeModule;


        private int idCounter;

        private Texture conditionQuestionIcon;
        private Texture conditionTrueIcon;
        private Texture conditionFalseIcon;
        private Texture serviceGroupIcon;


        private BehaviourTreeData tree => treeNodeModule.tree;
        private HashSet<TreeNode> reachableNodes => treeNodeModule.ReachableNodes;
        private TreeNode SelectedNode { get => treeNodeModule?.SelectedNode; }
        private TreeNode editorHeadNode => treeNodeModule?.EditorHeadNode;
        private bool showService => treeNodeModule.EditorSetting.overviewShowService;


        public BehaviourTreeOverviewTreeView(TreeViewState state) : base(state)
        {
            showBorder = true;
            showAlternatingRowBackgrounds = true;
            rowHeight = EditorGUIUtility.singleLineHeight + 4;
        }

        public void SetData(TreeNodeModule treeNodeModule)
        {
            this.treeNodeModule = treeNodeModule;

            Reload();

            int? id = FindIdByNode(treeNodeModule.SelectedNode);
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

            TreeNode mainRoot = GetLocalRoot();
            if (editorHeadNode != null)
            {
                var headItem = new OverviewItem
                {
                    id = idCounter++,
                    displayName = editorHeadNode.name,
                    Node = editorHeadNode,
                    IsGroup = false,
                    IsUnreachableRoot = false,
                    children = new List<TreeViewItem>()
                };

                if (mainRoot != null)
                {
                    headItem.AddChild(BuildNodeSubTree(mainRoot, isUnreachableRoot: false));
                }

                root.AddChild(headItem);
            }
            else if (mainRoot != null)
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

            var (overrideIcon, defaultIcon) = GetRowIcons(item);
            DrawRowIcons(ref centeredRect, defaultIcon, overrideIcon);

            Color old = GUI.contentColor;
            if (item.IsGroup)
            {
                GUI.contentColor = new Color(0.8f, 0.8f, 0.8f);
            }

            EditorGUI.LabelField(centeredRect, item.displayName);

            GUI.contentColor = old;
        }

        private TreeNode GetLocalRoot()
        {
            if (tree == null)
                return null;

            if (treeNodeModule.mode == TreeNodeModule.Mode.Global)
                return tree.Head;

            if (SelectedNode == null || SelectedNode == editorHeadNode)
                return tree.Head;

            if (SelectedNode == tree.Head)
                return tree.Head;

            return treeNodeModule.SelectedNodeParent ?? SelectedNode;
        }




        #region Icons

        /// <summary>
        /// Gets the override and default icons for a specific overview row.
        /// </summary>
        /// <param name="item">The row item providing node and grouping data.</param>
        /// <returns>
        /// The override icon (condition branch) and the default icon, each of which may be <c>null</c>.
        /// </returns>
        /// <remarks>
        /// Returns <c>null</c> icons when the item cannot be resolved or no icon is available.
        /// No exceptions are expected for valid inputs.
        /// </remarks>
        private (Texture overrideIcon, Texture defaultIcon) GetRowIcons(OverviewItem item)
        {
            if (item == null)
            {
                return (null, null);
            }

            Texture defaultIcon = GetRowDefaultIcon(item);
            if (IsServiceGroup(item))
            {
                return (null, defaultIcon);
            }

            Texture overrideIcon = GetConditionChildIcon(item.Node);
            return (overrideIcon, defaultIcon);
        }

        /// <summary>
        /// Draws the base icon with an optional condition badge and advances the layout rectangle.
        /// </summary>
        /// <param name="centeredRect">The rectangle used for row layout; updated after drawing.</param>
        /// <param name="baseIcon">The main icon to render for the row.</param>
        /// <param name="badgeIcon">The smaller badge icon to overlay at the bottom-right.</param>
        /// <remarks>
        /// Returns without changes when both icons are <c>null</c>.
        /// No exceptions are expected for valid inputs.
        /// </remarks>
        private static void DrawRowIcons(ref Rect centeredRect, Texture baseIcon, Texture badgeIcon)
        {
            if (baseIcon == null && badgeIcon == null)
            {
                return;
            }

            float iconSize = centeredRect.height;
            Texture resolvedBaseIcon = baseIcon ?? badgeIcon;
            Rect iconRect = new Rect(centeredRect.x, centeredRect.y, iconSize, iconSize);
            GUI.DrawTexture(iconRect, resolvedBaseIcon, ScaleMode.ScaleToFit, true);

            if (baseIcon != null && badgeIcon != null)
            {
                float badgeSize = iconSize * ConditionBadgeScale;
                Rect badgeRect = new Rect(
                    iconRect.x + iconRect.width - badgeSize,
                    iconRect.y + iconRect.height - badgeSize,
                    badgeSize,
                    badgeSize);
                GUI.DrawTexture(badgeRect, badgeIcon, ScaleMode.ScaleToFit, true);
            }

            centeredRect.x += iconRect.width + IconPadding;
            centeredRect.width -= iconRect.width + IconPadding;
        }

        /// <summary>
        /// Gets the default icon texture used for a specific overview row.
        /// </summary>
        /// <param name="item">The row item providing node and grouping data.</param>
        /// <returns>The resolved default icon texture, or <c>null</c> if no icon is available.</returns>
        /// <remarks>
        /// Returns <c>null</c> when the node cannot be resolved or no script icon is available.
        /// </remarks>
        private Texture GetRowDefaultIcon(OverviewItem item)
        {
            if (item == null)
                return null;

            if (IsServiceGroup(item))
                return serviceGroupIcon ? serviceGroupIcon : serviceGroupIcon = GetServiceGroupIcon();

            return GetScriptIcon(item.Node);
        }
        /// <summary>
        /// Gets the icon texture used for the service group row.
        /// </summary>
        /// <returns>The service group icon texture, or <c>null</c> if no script icon is available.</returns>
        /// <remarks>
        /// Returns <c>null</c> when the service node script asset cannot be resolved.
        /// </remarks>
        private static Texture GetServiceGroupIcon()
        {
            MonoScript script = MonoScriptCache.Get(typeof(Service));
            if (script == null)
                return null;

            return AssetPreview.GetMiniThumbnail(script);
        }

        /// <summary>
        /// Resolves the icon texture for a child node under a condition branch.
        /// </summary>
        /// <param name="node">The node to evaluate against condition slots.</param>
        /// <returns>
        /// The condition-specific icon texture, or <c>null</c> if the node is not a condition child.
        /// </returns>
        /// <remarks>
        /// Returns <c>null</c> when the tree is unavailable or when the node is not referenced by a condition.
        /// </remarks>
        private Texture GetConditionChildIcon(TreeNode node)
        {
            if (tree == null || node == null)
            {
                return null;
            }

            TreeNode parent = GetStrictParentNode(node);
            if (parent is not Condition condition)
            {
                return null;
            }

            if (condition.condition.IsPointTo(node))
            {
                return conditionQuestionIcon ??= GetEditorIcon("d__Help", "_Help");
            }

            if (condition.trueNode.IsPointTo(node))
            {
                return conditionTrueIcon ??= GetEditorIcon("TestPassed", "d_TestPassed");
            }

            if (condition.falseNode.IsPointTo(node))
            {
                return conditionFalseIcon ??= GetEditorIcon("TestFailed", "d_TestFailed");
            }

            return null;
        }

        /// <summary>
        /// Finds the strict parent node by skipping service nodes in the parent chain.
        /// </summary>
        /// <param name="node">The node whose strict parent should be resolved.</param>
        /// <returns>The first non-service parent node, or <c>null</c> if none is found.</returns>
        /// <remarks>
        /// Returns <c>null</c> when the tree is unavailable or the input node is <c>null</c>.
        /// </remarks>
        private TreeNode GetStrictParentNode(TreeNode node)
        {
            if (tree == null || node == null)
            {
                return null;
            }

            TreeNode parent = tree.GetParent(node);
            while (parent is Service)
            {
                parent = tree.GetParent(parent);
            }

            return parent;
        }

        /// <summary>
        /// Gets a script icon texture from the mono script cache for a node type.
        /// </summary>
        /// <param name="node">The node used to resolve the script asset.</param>
        /// <returns>The script icon texture, or <c>null</c> when no script is found.</returns>
        /// <remarks>
        /// Returns <c>null</c> when no cached mono script is available for the node type.
        /// </remarks>
        private static Texture GetScriptIcon(TreeNode node)
        {
            if (node == null)
            {
                return null;
            }

            MonoScript script = MonoScriptCache.Get(node.GetType());
            if (script == null)
            {
                return null;
            }

            return AssetPreview.GetMiniThumbnail(script);
        }

        /// <summary>
        /// Loads an editor icon texture by name with an optional fallback.
        /// </summary>
        /// <param name="primaryName">The primary editor icon name.</param>
        /// <param name="fallbackName">The fallback editor icon name if the primary is missing.</param>
        /// <returns>The resolved icon texture, or <c>null</c> if no icon is found.</returns>
        /// <remarks>
        /// Returns <c>null</c> when neither icon name resolves to a valid editor icon.
        /// </remarks>
        private static Texture GetEditorIcon(string primaryName, string fallbackName)
        {
            Texture primaryIcon = EditorGUIUtility.IconContent(primaryName)?.image;
            if (primaryIcon != null)
            {
                return primaryIcon;
            }
            return EditorGUIUtility.IconContent(fallbackName)?.image;
        }

        #endregion

        protected override void SingleClickedItem(int id)
        {
            if (FindItem(id, rootItem) is not OverviewItem item)
            {
                return;
            }

            var node = item.IsGroup ? item.Node : item.Node;
            if (node != null)
            {
                treeNodeModule.SelectNode(node);
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
            treeNodeModule.CreateRightClickMenu(node, menu);
            menu.ShowAsContext();
        }

        protected override bool CanMultiSelect(TreeViewItem item) => false;

        protected override bool CanRename(TreeViewItem item)
        {
            if (item is not OverviewItem overviewItem)
            {
                return false;
            }

            if (overviewItem.IsGroup || overviewItem.Node == editorHeadNode)
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
            HandleKeyboardShortcuts(Event.current);
        }

        /// <summary>
        /// Handles keyboard shortcuts for the overview tree view.
        /// </summary>
        /// <param name="evt">The current GUI event to evaluate.</param>
        /// <returns><c>true</c> when a shortcut was handled; otherwise, <c>false</c>.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        public bool HandleKeyboardShortcuts(Event evt)
        {
            if (evt == null || evt.type != EventType.KeyDown)
            {
                return false;
            }

            if (EditorGUIUtility.editingTextField)
            {
                return false;
            }

            TreeNode selected = ResolveKeyboardTargetNode();
            if (evt.control && evt.keyCode == KeyCode.C)
            {
                if (selected != null)
                {
                    treeNodeModule.WriteClipboard(selected);
                    evt.Use();
                    AIEditorWindow.Instance.ShowNotification(new GUIContent($"Copy '{selected.name}' to clipboard"));
                    return true;
                }
            }

            if (evt.control && evt.keyCode == KeyCode.V)
            {
                if (selected != null && TryPasteFromClipboard(selected))
                {
                    evt.Use();
                    return true;
                }
            }

            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                if (selected != null)
                {
                    treeNodeModule.TryDeleteNode(selected);
                    evt.Use();
                    return true;
                }
            }

            if (evt.keyCode == KeyCode.F2)
            {
                TreeViewItem currentSelection = GetSelection().Count == 1 ? FindItem(GetSelection()[0], rootItem) : null;
                if (currentSelection != null && CanRename(currentSelection))
                {
                    BeginRename(currentSelection);
                    evt.Use();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the node that should be used for keyboard-driven actions.
        /// </summary>
        /// <returns>The selected node when available; otherwise, <c>null</c>.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        private TreeNode ResolveKeyboardTargetNode()
        {
            if (SelectedNode != null && SelectedNode != editorHeadNode)
            {
                return SelectedNode;
            }

            if (GetSelection().Count != 1)
            {
                return null;
            }

            if (FindItem(GetSelection()[0], rootItem) is not OverviewItem selectedItem)
            {
                return null;
            }

            if (selectedItem.IsGroup || selectedItem.Node == null || selectedItem.Node == editorHeadNode)
            {
                return null;
            }

            return selectedItem.Node;
        }

        /// <summary>
        /// Attempts to paste the current clipboard contents using the provided target node.
        /// </summary>
        /// <param name="node">The node that will receive the pasted content.</param>
        /// <returns><c>true</c> if the clipboard content was pasted; otherwise, <c>false</c>.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        private bool TryPasteFromClipboard(TreeNode node)
        {
            var clipboard = treeNodeModule.clipboard;
            if (node == null || tree == null || clipboard == null || !clipboard.HasContent)
            {
                return false;
            }

            if (clipboard.TypeMatch(typeof(Service)))
            {
                return TryPasteServiceFromClipboard(node);
            }

            var nodeReferenceSlots = node.ToReferenceSlots();
            var listSlot = nodeReferenceSlots.OfType<INodeReferenceListSlot>().FirstOrDefault();
            int index = -1;
            var parent = tree.GetParent(node);
            if (parent != null)
            {
                // use parent slots
                var parentReference = parent.ToReferenceSlots();
                var parentSlots = parentReference.OfType<INodeReferenceListSlot>().FirstOrDefault();
                if (parentSlots != null)
                {
                    listSlot = parentSlots;
                    // get index of original node in parent
                    index = listSlot.IndexOf(node);
                    node = parent;
                }
            }

            if (listSlot != null)
            {
                clipboard.PasteAt(tree, node, listSlot, index + 1);
                AIEditorWindow.Instance.ShowNotification(new GUIContent($"Paste '{clipboard.treeNodes[0].name}' from clipboard to {node.name}.{listSlot.Name}[{index + 1}]"));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts to paste a service node from the clipboard into the target host node.
        /// </summary>
        /// <param name="host">The node that will receive the service.</param>
        /// <returns><c>true</c> if the service was pasted; otherwise, <c>false</c>.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        private bool TryPasteServiceFromClipboard(TreeNode host)
        {
            var clipboard = treeNodeModule.clipboard;
            if (host == null || tree == null || clipboard == null || !clipboard.HasContent)
            {
                return false;
            }

            List<TreeNode> content = clipboard.Content;
            if (content.Count == 0 || content[0] is not Service rootService)
            {
                return false;
            }

            for (int i = 0; i < content.Count; i++)
            {
                content[i].name = tree.GenerateNewNodeName(content[i].name);
            }

            Undo.RecordObject(tree, $"Paste service {rootService.name} under {host.name}");
            tree.AddRange(content, false);

            host.services ??= new List<NodeReference>();
            host.services.Add(rootService.ToReference());
            rootService.parent = host;

            EditorUtility.SetDirty(tree);
            return true;
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

            if (draggedNode == null)
            {
                return DragAndDropVisualMode.Rejected;
            }

            //if (args.parentItem is not OverviewItem parentItem)
            //{
            //    return DragAndDropVisualMode.Rejected;
            //}

            var (targetParent, isServiceDropGroup) = ResolveDropTargetParent(args.parentItem as OverviewItem);
            //if (targetParent == null)
            //{
            //    return DragAndDropVisualMode.Rejected;
            //}

            if (targetParent == draggedNode)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (WouldCreateCycle(draggedNode, targetParent))
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (draggedNode is Service && targetParent is Service)
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (draggedNode is not Service && isServiceDropGroup)
            {
                return DragAndDropVisualMode.Rejected;
            }

            int normalizedInsertIndex = NormalizeInsertIndex(args.parentItem as OverviewItem, args.insertAtIndex, isServiceDropGroup);

            if (args.performDrop)
            {
                if (draggedNode is Service service)
                {
                    ApplyMoveService(service, targetParent, normalizedInsertIndex, isServiceDropGroup);
                }
                else
                {
                    ApplyMoveOrRebindWithSlotMenu(draggedNode, targetParent, normalizedInsertIndex);
                }
            }

            return DragAndDropVisualMode.Move;
        }

        private static int NormalizeInsertIndex(OverviewItem parentItem, int insertAtIndex, bool isServiceDropGroup)
        {
            if (insertAtIndex < 0 || parentItem == null)
            {
                return insertAtIndex;
            }

            if (isServiceDropGroup)
            {
                return insertAtIndex;
            }

            // if first child is service group, adjust index
            if (parentItem.children != null
                && parentItem.children.Count > 0
                && parentItem.children[0] is OverviewItem first
                && first.IsGroup
                && string.Equals(first.displayName, "Service", StringComparison.Ordinal))
            {
                insertAtIndex--;
            }

            return insertAtIndex < 0 ? 0 : insertAtIndex;
        }

        private bool WouldCreateCycle(TreeNode draggedNode, TreeNode targetParent)
        {
            if (draggedNode == null || targetParent == null || tree == null)
            {
                return false;
            }

            if (draggedNode == targetParent)
            {
                return true;
            }

            // same parent no cycle (since is already under the parent)
            if (draggedNode.parent.UUID == targetParent.UUID)
            {
                return false;
            }

            var visited = new HashSet<UUID>();
            var stack = new Stack<TreeNode>();
            stack.Push(draggedNode);

            while (stack.Count > 0)
            {
                TreeNode current = stack.Pop();
                if (current == null)
                {
                    continue;
                }

                if (!visited.Add(current.UUID))
                {
                    continue;
                }

                if (current.UUID == targetParent.UUID)
                {
                    return true;
                }

                var refs = current.GetChildrenReference();
                for (int i = 0; i < refs.Count; i++)
                {
                    NodeReference reference = refs[i];
                    if (reference == null || reference.UUID == UUID.Empty)
                    {
                        continue;
                    }

                    TreeNode child = tree.GetNode(reference.UUID);
                    if (child == null)
                    {
                        continue;
                    }

                    stack.Push(child);
                }
            }

            return false;
        }

        private (TreeNode targetParent, bool isServiceDropGroup) ResolveDropTargetParent(OverviewItem parentItem)
        {
            if (parentItem == null || parentItem.Node == null)
            {
                return (null, false);
            }

            // Service Group
            if (parentItem.IsGroup && string.Equals(parentItem.displayName, "Service", StringComparison.Ordinal))
            {
                return (parentItem.Node, true);
            }

            return (parentItem.Node, false);
        }

        private void ApplyMoveService(Service draggedService, TreeNode targetHost, int insertAtIndex, bool isServiceDropGroup)
        {
            if (draggedService == null || targetHost == null || tree == null)
            {
                return;
            }

            TreeNode oldHost = tree.GetParent(draggedService);
            if (oldHost == null)
            {
                return;
            }

            // must be a service group drop
            int oldIndex = oldHost.services?.FindIndex(s => s != null && s.UUID == draggedService.UUID) ?? -1;

            int targetIndex;
            if (insertAtIndex < 0)
            {
                targetIndex = targetHost.services?.Count ?? 0;
            }
            else
            {
                int count = targetHost.services?.Count ?? 0;
                targetIndex = Mathf.Clamp(insertAtIndex, 0, count);
            }

            Undo.RecordObject(tree, $"Move service {draggedService.name}");

            // remove from old host
            oldHost.services?.RemoveAll(r => r != null && r.UUID == draggedService.UUID);

            // reordering adjustment
            if (oldHost == targetHost && oldIndex >= 0 && targetIndex > oldIndex)
            {
                targetIndex--;
            }

            targetHost.services ??= new List<NodeReference>();
            targetHost.services.Insert(targetIndex, draggedService.ToReference());

            // update parent reference
            draggedService.parent = new NodeReference(targetHost.UUID);

            EditorUtility.SetDirty(tree);
            ReloadAndReveal(draggedService);
        }

        private void ApplyMoveOrRebindWithSlotMenu(TreeNode draggedNode, TreeNode targetParent, int insertAtIndex)
        {
            if (draggedNode == null || tree == null)
            {
                return;
            }

#nullable enable
            TreeNode? oldParent = tree.GetParent(draggedNode);

            if (oldParent == targetParent && TryReorderInSameParent(oldParent, draggedNode, insertAtIndex))
            {
                return;
            }

            // set null parent (detached)
            if (targetParent == null)
            {
                Undo.RecordObject(tree, $"Detach node {draggedNode.name}");
                draggedNode.DetachFrom(oldParent);
                draggedNode.parent = null;
                EditorUtility.SetDirty(tree);
                ReloadAndReveal(draggedNode);
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
                draggedNode.DetachFrom(oldParent);
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
                    draggedNode.DetachFrom(oldParent);
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
#nullable disable
        }

        private bool TryReorderInSameParent(TreeNode parent, TreeNode draggedNode, int insertAtIndex)
        {
            if (parent == null || tree == null)
            {
                return false;
            }
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



        /// <summary>
        /// Determines whether the item represents the service group row.
        /// </summary>
        /// <param name="item">The row item to evaluate.</param>
        /// <returns><c>true</c> when the item represents the service group; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Returns <c>false</c> when the item is <c>null</c> or does not match the service group criteria.
        /// </remarks>
        private static bool IsServiceGroup(OverviewItem item)
        {
            if (item == null)
            {
                return false;
            }

            return item.IsGroup && string.Equals(item.displayName, "Service", StringComparison.Ordinal);
        }
    }
}
