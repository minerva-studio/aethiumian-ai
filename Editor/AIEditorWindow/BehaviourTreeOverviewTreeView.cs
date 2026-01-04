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
            rowHeight = EditorGUIUtility.singleLineHeight + 4f;
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
                    if (childNode == null || childNode == node)
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

            Color old = GUI.contentColor;
            if (item.IsGroup)
            {
                GUI.contentColor = new Color(0.8f, 0.8f, 0.8f);
            }

            base.RowGUI(args);

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
    }
}