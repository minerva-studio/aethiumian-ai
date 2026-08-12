using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// AdvancedDropdown used by the legacy Nodes editor for node creation and reference selection.
    /// </summary>
    internal sealed class NodeSelectionDropdown : AdvancedDropdown
    {
        private readonly TreeNodeModule module;
        private readonly NodeSelectionContext selectionContext;
        private readonly bool rawReference;
        private readonly SelectNodeEvent selectionCallback;
        private readonly NodeMenuCache menuCache;

        /// <summary>
        /// Initializes a node selection dropdown for one legacy editor selection request.
        /// </summary>
        /// <param name="module">The tree editor module that owns the selection request.</param>
        /// <param name="selectionContext">The node catalogue to display.</param>
        /// <param name="selectionCallback">The one-shot callback for the selected node.</param>
        /// <param name="rawReference">Whether the request must not alter parent links.</param>
        internal NodeSelectionDropdown(
            TreeNodeModule module,
            NodeSelectionContext selectionContext,
            SelectNodeEvent selectionCallback,
            bool rawReference)
            : base(new AdvancedDropdownState())
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            this.selectionContext = selectionContext;
            this.selectionCallback = selectionCallback;
            this.rawReference = rawReference;
            menuCache = NodeMenuCache.Shared;
            minimumSize = new Vector2(380f, 420f);
        }

        /// <inheritdoc />
        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new(GetRootTitle());

            if (!rawReference)
            {
                AddClipboardItem(root);
                AddCreationMenu(root);
            }

            AddExistingNodes(root);

            return root;
        }

        /// <inheritdoc />
        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            switch (item)
            {
                case NodeSelectionTypeItem typeItem:
                    module.CreateAndSelectNode(typeItem.NodeType, selectionCallback);
                    break;
                case NodeSelectionExistingItem existingItem:
                    module.TrySelectExistingNode(existingItem.Node, selectionCallback, rawReference);
                    break;
                case NodeSelectionPasteItem:
                    module.PasteSubTree(selectionCallback);
                    break;
            }
        }

        /// <summary>
        /// Gets the root label for the active selection context.
        /// </summary>
        /// <returns>The root label.</returns>
        private string GetRootTitle()
        {
            return selectionContext == NodeSelectionContext.Services ? "Services" : "Nodes";
        }

        /// <summary>
        /// Adds the compatible clipboard paste entry to the dropdown root.
        /// </summary>
        private void AddClipboardItem(AdvancedDropdownItem root)
        {
            if (!module.CanPasteForSelection(selectionContext))
            {
                return;
            }

            string rootName = module.clipboard.Root?.name ?? "Clipboard";
            root.AddChild(new NodeSelectionPasteItem($"Paste ({rootName})"));
        }

        /// <summary>
        /// Adds the canonical node creation menu to the dropdown root.
        /// </summary>
        private void AddCreationMenu(AdvancedDropdownItem root)
        {
            NodeCreationMenuContext context = selectionContext == NodeSelectionContext.Services
                ? NodeCreationMenuContext.Services
                : NodeCreationMenuContext.Nodes;
            NodeCreationMenuFolder creationRoot = menuCache.BuildCreationMenu(context);
            AddFolderEntries(root, creationRoot);
        }

        /// <summary>
        /// Copies a creation-menu folder hierarchy into AdvancedDropdown items.
        /// </summary>
        /// <param name="parent">The dropdown item that receives the folder entries.</param>
        /// <param name="folder">The menu-cache folder to copy.</param>
        /// <returns>True when the folder contains at least one visible entry.</returns>
        private bool AddFolderEntries(AdvancedDropdownItem parent, NodeCreationMenuFolder folder)
        {
            bool hasEntries = false;
            foreach (Type type in folder.Types.OrderBy(menuCache.GetDisplayName))
            {
                if (!IsAllowedCreationType(type))
                {
                    continue;
                }

                parent.AddChild(new NodeSelectionTypeItem(BuildTypeLabel(type), type));
                hasEntries = true;
            }

            foreach (NodeCreationMenuFolder child in folder.Children.OrderBy(item => item.Name))
            {
                AdvancedDropdownItem childItem = new(child.Name);
                if (!AddFolderEntries(childItem, child))
                {
                    continue;
                }

                parent.AddChild(childItem);
                hasEntries = true;
            }

            return hasEntries;
        }

        /// <summary>
        /// Adds existing tree nodes grouped by reachability.
        /// </summary>
        private void AddExistingNodes(AdvancedDropdownItem root)
        {
            List<TreeNode> nodes = (module.tree?.EditorNodes ?? Enumerable.Empty<TreeNode>())
                .Where(node => node != null && IsAllowedExistingNode(node))
                .OrderBy(node => node.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (nodes.Count == 0)
            {
                return;
            }

            AdvancedDropdownItem existingRoot = new("Existing Nodes");
            AddExistingGroup(existingRoot, "Reachables", nodes.Where(module.ReachableNodes.Contains));
            AddExistingGroup(existingRoot, "Non-reachables", nodes.Where(node => !module.ReachableNodes.Contains(node)));
            if (existingRoot.children.Any())
            {
                root.AddChild(existingRoot);
            }
        }

        /// <summary>
        /// Adds one reachability group to the existing-node section.
        /// </summary>
        /// <param name="parent">The existing-node section item.</param>
        /// <param name="groupName">The group label.</param>
        /// <param name="nodes">The nodes to add to the group.</param>
        private void AddExistingGroup(AdvancedDropdownItem parent, string groupName, IEnumerable<TreeNode> nodes)
        {
            List<TreeNode> entries = nodes.ToList();
            if (entries.Count == 0)
            {
                return;
            }

            AdvancedDropdownItem group = new(groupName);
            foreach (TreeNode node in entries)
            {
                group.AddChild(new NodeSelectionExistingItem(BuildExistingNodeLabel(node), node));
            }

            parent.AddChild(group);
        }

        /// <summary>
        /// Determines whether a node type is valid for this selection context.
        /// </summary>
        /// <param name="type">The candidate node type.</param>
        /// <returns>True when the type can be created from this dropdown.</returns>
        private bool IsAllowedCreationType(Type type)
        {
            if (type == null || !NodeMenuCache.IsCreatableNodeType(type))
            {
                return false;
            }

            if (selectionContext == NodeSelectionContext.Services)
            {
                return typeof(Service).IsAssignableFrom(type);
            }

            if (typeof(Service).IsAssignableFrom(type))
            {
                return false;
            }

            if (module.SelectedNode is Service && Attribute.IsDefined(type, typeof(DisableServiceCallAttribute)))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether an existing node is valid for this selection context.
        /// </summary>
        /// <param name="node">The candidate existing node.</param>
        /// <returns>True when the node can be selected from this dropdown.</returns>
        private bool IsAllowedExistingNode(TreeNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (selectionContext == NodeSelectionContext.Services)
            {
                return node is Service;
            }

            return node is not Service;
        }

        /// <summary>
        /// Builds the display label for a creatable node type.
        /// </summary>
        /// <param name="type">The node type.</param>
        /// <returns>The menu-cache display name.</returns>
        private string BuildTypeLabel(Type type)
        {
            return menuCache.GetDisplayName(type);
        }

        /// <summary>
        /// Builds the display label for an existing node.
        /// </summary>
        /// <param name="node">The existing node.</param>
        /// <returns>The node name followed by its type display name.</returns>
        private string BuildExistingNodeLabel(TreeNode node)
        {
            string displayName = menuCache.GetDisplayName(node.GetType());
            return string.IsNullOrWhiteSpace(node.name) ? displayName : $"{node.name} — {displayName}";
        }

        private sealed class NodeSelectionTypeItem : AdvancedDropdownItem
        {
            /// <summary>
            /// Creates a creatable node item.
            /// </summary>
            /// <param name="label">The item label.</param>
            /// <param name="nodeType">The node type represented by the item.</param>
            internal NodeSelectionTypeItem(string label, Type nodeType) : base(label)
            {
                NodeType = nodeType;
            }

            internal Type NodeType { get; }
        }

        private sealed class NodeSelectionExistingItem : AdvancedDropdownItem
        {
            /// <summary>
            /// Creates an existing-node item.
            /// </summary>
            /// <param name="label">The item label.</param>
            /// <param name="node">The existing node represented by the item.</param>
            internal NodeSelectionExistingItem(string label, TreeNode node) : base(label)
            {
                Node = node;
            }

            internal TreeNode Node { get; }
        }

        private sealed class NodeSelectionPasteItem : AdvancedDropdownItem
        {
            /// <summary>
            /// Creates a clipboard paste item.
            /// </summary>
            /// <param name="label">The item label.</param>
            internal NodeSelectionPasteItem(string label) : base(label) { }
        }
    }
}
