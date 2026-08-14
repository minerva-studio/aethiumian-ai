using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>Candidate sources included in one node selection catalogue.</summary>
    [Flags]
    internal enum NodeSelectionSources
    {
        None = 0,
        Existing = 1 << 0,
        Create = 1 << 1,
        Clipboard = 1 << 2,
        Mixed = Existing | Create | Clipboard,
    }

    /// <summary>Kind of result returned by the node selection catalogue.</summary>
    internal enum NodeSelectionChoiceKind
    {
        ExistingNode,
        CreateType,
        PasteRoot,
    }

    /// <summary>
    /// Stable, mutation-free result emitted by <see cref="NodeSelectionDropdown"/>.
    /// </summary>
    internal readonly struct NodeSelectionChoice
    {
        private NodeSelectionChoice(NodeSelectionChoiceKind kind, UUID existingNodeUUID, Type createType)
        {
            Kind = kind;
            ExistingNodeUUID = existingNodeUUID;
            CreateType = createType;
        }

        internal NodeSelectionChoiceKind Kind { get; }
        internal UUID ExistingNodeUUID { get; }
        internal Type CreateType { get; }

        internal static NodeSelectionChoice Existing(UUID uuid) => new(NodeSelectionChoiceKind.ExistingNode, uuid, null);
        internal static NodeSelectionChoice Create(Type type) => new(NodeSelectionChoiceKind.CreateType, UUID.Empty, type);
        internal static NodeSelectionChoice Paste() => new(NodeSelectionChoiceKind.PasteRoot, UUID.Empty, null);
    }

    /// <summary>
    /// AdvancedDropdown used by the legacy Nodes editor for node creation and reference selection.
    /// </summary>
    internal sealed class NodeSelectionDropdown : AdvancedDropdown
    {
        private const float MinimumWidth = 280f;
        private const float MinimumHeight = 240f;

        private readonly BehaviourTreeData tree;
        private readonly Clipboard clipboard;
        private readonly NodeSelectionContext selectionContext;
        private readonly Action<NodeSelectionChoice> selectionCallback;
        private readonly Func<TreeNode, bool> existingNodeFilter;
        private readonly NodeSelectionSources sources;
        private readonly NodeMenuCache menuCache;

        /// <summary>
        /// Initializes a node selection dropdown for one legacy editor selection request.
        /// </summary>
        /// <param name="tree">The behaviour tree that supplies existing nodes.</param>
        /// <param name="clipboard">Clipboard source for optional paste entries.</param>
        /// <param name="selectionContext">The node catalogue to display.</param>
        /// <param name="selectionCallback">The one-shot callback for the selected choice.</param>
        /// <param name="existingNodeFilter">Optional caller-owned validation for existing-node entries.</param>
        /// <param name="sources">Candidate sources included in the catalogue.</param>
        internal NodeSelectionDropdown(
            BehaviourTreeData tree,
            Clipboard clipboard,
            NodeSelectionContext selectionContext,
            Action<NodeSelectionChoice> selectionCallback,
            Func<TreeNode, bool> existingNodeFilter = null,
            NodeSelectionSources sources = NodeSelectionSources.Mixed)
            : base(new AdvancedDropdownState())
        {
            this.tree = tree ?? throw new ArgumentNullException(nameof(tree));
            this.clipboard = clipboard;
            this.selectionContext = selectionContext;
            this.selectionCallback = selectionCallback;
            this.existingNodeFilter = existingNodeFilter;
            this.sources = sources;
            menuCache = NodeMenuCache.Shared;
            minimumSize = new Vector2(MinimumWidth, MinimumHeight);
        }

        /// <inheritdoc />
        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new(GetRootTitle());

            if (sources.HasFlag(NodeSelectionSources.Clipboard))
            {
                AddClipboardItem(root);
            }
            if (sources.HasFlag(NodeSelectionSources.Create))
            {
                AddCreationMenu(root);
            }
            if (sources.HasFlag(NodeSelectionSources.Existing))
            {
                AddExistingNodes(root);
            }

            return root;
        }

        /// <inheritdoc />
        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            switch (item)
            {
                case NodeSelectionTypeItem typeItem:
                    selectionCallback?.Invoke(NodeSelectionChoice.Create(typeItem.NodeType));
                    break;
                case NodeSelectionExistingItem existingItem:
                    selectionCallback?.Invoke(NodeSelectionChoice.Existing(existingItem.NodeUUID));
                    break;
                case NodeSelectionPasteItem:
                    selectionCallback?.Invoke(NodeSelectionChoice.Paste());
                    break;
            }
        }

        /// <summary>
        /// Gets the root label for the active selection context.
        /// </summary>
        /// <returns>The root label.</returns>
        private string GetRootTitle()
        {
            if (sources == NodeSelectionSources.Existing)
            {
                return selectionContext == NodeSelectionContext.Services ? "Select Existing Service" : "Select Existing Node";
            }
            if (sources == NodeSelectionSources.Create)
            {
                return selectionContext == NodeSelectionContext.Services ? "Create Service" : "Create Node";
            }

            return selectionContext == NodeSelectionContext.Services ? "Services" : "Nodes";
        }

        /// <summary>
        /// Adds the compatible clipboard paste entry to the dropdown root.
        /// </summary>
        private void AddClipboardItem(AdvancedDropdownItem root)
        {
            if (clipboard == null || selectionContext == NodeSelectionContext.Services || !clipboard.HasSingleRootContent || clipboard.TypeMatch(typeof(Service)))
            {
                return;
            }

            string rootName = clipboard.Root?.name ?? "Clipboard";
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
            List<TreeNode> nodes = (tree?.EditorNodes ?? Enumerable.Empty<TreeNode>())
                .Where(node => node != null && IsAllowedExistingNode(node))
                .OrderBy(node => node.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (nodes.Count == 0)
            {
                return;
            }

            HashSet<TreeNode> reachable = GetReachableNodes(tree);
            if (sources == NodeSelectionSources.Existing)
            {
                AddExistingEntries(root, nodes.Where(reachable.Contains));
                AddExistingGroup(root, "Non-reachables", nodes.Where(node => !reachable.Contains(node)));
                return;
            }

            AdvancedDropdownItem existingRoot = new("Existing Nodes");
            AddExistingGroup(existingRoot, "Reachables", nodes.Where(reachable.Contains));
            AddExistingGroup(existingRoot, "Non-reachables", nodes.Where(node => !reachable.Contains(node)));
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
            AddExistingEntries(group, entries);

            parent.AddChild(group);
        }

        /// <summary>Adds existing nodes directly below the provided catalogue item.</summary>
        private void AddExistingEntries(AdvancedDropdownItem parent, IEnumerable<TreeNode> nodes)
        {
            foreach (TreeNode node in nodes)
            {
                parent.AddChild(new NodeSelectionExistingItem(BuildExistingNodeLabel(node), node));
            }
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

            return true;
        }

        /// <summary>
        /// Determines whether an existing node is valid for this selection context.
        /// </summary>
        /// <param name="node">The candidate existing node.</param>
        /// <returns>True when the node can be selected from this dropdown.</returns>
        private bool IsAllowedExistingNode(TreeNode node)
        {
            if (node == null || existingNodeFilter?.Invoke(node) == false)
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

        /// <summary>
        /// Builds the reachable-node set directly from the behaviour tree data.
        /// </summary>
        private static HashSet<TreeNode> GetReachableNodes(BehaviourTreeData tree)
        {
            HashSet<TreeNode> reachable = new();
            if (tree == null) return reachable;
            Stack<TreeNode> pending = new();
            if (tree.Head != null) pending.Push(tree.Head);
            while (pending.Count > 0)
            {
                TreeNode node = pending.Pop();
                if (node == null || !reachable.Add(node)) continue;
                foreach (NodeReference childReference in node.GetChildrenReference())
                {
                    TreeNode child = tree.GetNode(childReference);
                    if (child != null) pending.Push(child);
                }
            }
            return reachable;
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
                NodeUUID = node.uuid;
            }

            internal UUID NodeUUID { get; }
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
