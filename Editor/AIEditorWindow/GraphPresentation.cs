using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Describes the editor-only presentation role of a graph item.
    /// </summary>
    internal enum GraphPresentationKind
    {
        Card,
        Sequence,
        Decision,
        Condition,
        ReferenceProxy,
        Missing,
    }

    /// <summary>
    /// A named slot inside a semantic Flow container.
    /// </summary>
    internal sealed class GraphPresentationSlot
    {
        internal GraphPresentationSlot(string label, int index, GraphEdgeDescriptor edge, GraphPresentationItem content)
        {
            Label = label;
            Index = index;
            Edge = edge;
            Content = content;
        }

        /// <summary>Gets the field/index label shown beside the slot.</summary>
        internal string Label { get; }

        /// <summary>Gets the collection index, or -1 for a single reference.</summary>
        internal int Index { get; }

        /// <summary>Gets the source topology edge, if the slot is referenced.</summary>
        internal GraphEdgeDescriptor Edge { get; }

        /// <summary>Gets the slot presentation content.</summary>
        internal GraphPresentationItem Content { get; }
    }

    /// <summary>
    /// A recursively nested, editor-only graph presentation item.
    /// </summary>
    internal sealed class GraphPresentationItem
    {
        private readonly List<GraphPresentationSlot> slots = new();

        internal GraphPresentationItem(
            GraphPresentationKind kind,
            GraphNodeDescriptor node,
            UUID targetUUID,
            string warning,
            bool isRoot = false)
        {
            Kind = kind;
            Node = node;
            TargetUUID = targetUUID;
            Warning = warning;
            IsRoot = isRoot;
            Position = node?.Position ?? Vector2.zero;
        }

        /// <summary>Gets the item's semantic presentation kind.</summary>
        internal GraphPresentationKind Kind { get; }

        /// <summary>Gets the source node represented by this item.</summary>
        internal GraphNodeDescriptor Node { get; }

        /// <summary>Gets the referenced UUID for proxy or missing items.</summary>
        internal UUID TargetUUID { get; }

        /// <summary>Gets the parent container, or null for a root item.</summary>
        internal GraphPresentationItem Parent { get; private set; }

        /// <summary>Gets whether this item is a top-level movable item.</summary>
        internal bool IsRoot { get; }

        /// <summary>Gets the diagnostic warning shown for this item.</summary>
        internal string Warning { get; }

        /// <summary>Gets the computed canvas position.</summary>
        internal Vector2 Position { get; set; }

        /// <summary>Gets the computed unscaled canvas size.</summary>
        internal Vector2 Size { get; set; }

        /// <summary>Gets all semantic slots in declaration order.</summary>
        internal IReadOnlyList<GraphPresentationSlot> Slots => slots;

        /// <summary>Gets whether this item represents a semantic container.</summary>
        internal bool IsContainer => Kind is GraphPresentationKind.Sequence
            or GraphPresentationKind.Decision
            or GraphPresentationKind.Condition;

        /// <summary>Adds a named slot and assigns its child ownership.</summary>
        internal void AddSlot(GraphPresentationSlot slot)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            slots.Add(slot);
            if (slot.Content != null)
            {
                slot.Content.Parent = this;
            }
        }
    }

    /// <summary>
    /// Complete editor-only semantic presentation of a graph topology.
    /// </summary>
    internal sealed class GraphPresentation
    {
        private readonly Dictionary<UUID, GraphPresentationItem> primaryByUUID;
        private readonly List<GraphPresentationItem> roots;
        private readonly List<GraphEdgeDescriptor> externalEdges;

        internal GraphPresentation(
            List<GraphPresentationItem> roots,
            Dictionary<UUID, GraphPresentationItem> primaryByUUID,
            List<GraphEdgeDescriptor> externalEdges)
        {
            this.roots = roots;
            this.primaryByUUID = primaryByUUID;
            this.externalEdges = externalEdges;
        }

        /// <summary>Gets top-level items that may be moved as a unit.</summary>
        internal IReadOnlyList<GraphPresentationItem> Roots => roots;

        /// <summary>Gets edges that remain in the global edge layer.</summary>
        internal IReadOnlyList<GraphEdgeDescriptor> ExternalEdges => externalEdges;

        /// <summary>Finds the single primary presentation item for a UUID.</summary>
        internal GraphPresentationItem Find(UUID uuid)
        {
            return primaryByUUID.TryGetValue(uuid, out GraphPresentationItem item) ? item : null;
        }

        /// <summary>Moves a root and all nested presentation descendants in memory.</summary>
        internal void MoveRoot(UUID uuid, Vector2 position)
        {
            GraphPresentationItem item = Find(uuid);
            if (item == null || !item.IsRoot)
            {
                return;
            }

            Vector2 delta = position - item.Position;
            Offset(item, delta);
        }

        private static void Offset(GraphPresentationItem item, Vector2 delta)
        {
            item.Position += delta;
            foreach (GraphPresentationSlot slot in item.Slots)
            {
                if (slot.Content != null)
                {
                    Offset(slot.Content, delta);
                }
            }
        }
    }

    /// <summary>
    /// Converts the authoritative topology snapshot into semantic Flow containers.
    /// </summary>
    internal static class GraphPresentationBuilder
    {
        /// <summary>
        /// Builds a presentation without modifying the source tree or topology descriptors.
        /// </summary>
        /// <param name="topology">The authoritative graph snapshot.</param>
        /// <returns>A recursively nested presentation.</returns>
        internal static GraphPresentation Build(GraphTopology topology)
        {
            if (topology == null)
            {
                return new GraphPresentation(new List<GraphPresentationItem>(), new Dictionary<UUID, GraphPresentationItem>(), new List<GraphEdgeDescriptor>());
            }

            Dictionary<UUID, List<GraphEdgeDescriptor>> outgoing = BuildOutgoing(topology);
            Dictionary<UUID, GraphPresentationItem> primary = new();
            HashSet<GraphEdgeDescriptor> internalEdges = new();
            List<GraphPresentationItem> roots = new();

            GraphNodeDescriptor head = null;
            for (int i = 0; i < topology.Nodes.Count; i++)
            {
                if (topology.Nodes[i].IsHead)
                {
                    head = topology.Nodes[i];
                    break;
                }
            }

            if (head != null)
            {
                roots.Add(BuildItem(head, outgoing, primary, internalEdges, new HashSet<UUID>(), isRoot: true));
            }

            for (int i = 0; i < topology.Nodes.Count; i++)
            {
                GraphNodeDescriptor descriptor = topology.Nodes[i];
                if (primary.ContainsKey(descriptor.UUID))
                {
                    continue;
                }

                roots.Add(BuildItem(descriptor, outgoing, primary, internalEdges, new HashSet<UUID>(), isRoot: true));
            }

            List<GraphEdgeDescriptor> externalEdges = new();
            foreach (GraphEdgeDescriptor edge in topology.Edges)
            {
                if (!internalEdges.Contains(edge))
                {
                    externalEdges.Add(edge);
                }
            }

            return new GraphPresentation(roots, primary, externalEdges);
        }

        private static GraphPresentationItem BuildItem(
            GraphNodeDescriptor descriptor,
            IReadOnlyDictionary<UUID, List<GraphEdgeDescriptor>> outgoing,
            IDictionary<UUID, GraphPresentationItem> primary,
            ISet<GraphEdgeDescriptor> internalEdges,
            ISet<UUID> path,
            bool isRoot)
        {
            if (descriptor == null)
            {
                return CreateMissing(UUID.Empty, "Missing node");
            }

            if (path.Contains(descriptor.UUID))
            {
                return CreateProxy(descriptor, "Cycle reference");
            }

            if (primary.ContainsKey(descriptor.UUID))
            {
                return CreateProxy(descriptor, "Repeated or multi-parent reference");
            }

            GraphPresentationKind kind = GetKind(descriptor.Node);
            GraphPresentationItem item = new(kind, descriptor, descriptor.UUID, descriptor.Warning, isRoot);
            primary.Add(descriptor.UUID, item);

            if (!item.IsContainer)
            {
                return item;
            }

            HashSet<UUID> childPath = new(path) { descriptor.UUID };
            if (!outgoing.TryGetValue(descriptor.UUID, out List<GraphEdgeDescriptor> edges))
            {
                AddEmptySlots(item);
                return item;
            }

            switch (kind)
            {
                case GraphPresentationKind.Sequence:
                case GraphPresentationKind.Decision:
                    for (int i = 0; i < edges.Count; i++)
                    {
                        GraphEdgeDescriptor edge = edges[i];
                        AddChildSlot(item, edge, $"{(kind == GraphPresentationKind.Sequence ? i + 1 : i + 1)}", i, outgoing, primary, internalEdges, childPath);
                    }
                    if (edges.Count == 0)
                    {
                        AddEmptySlots(item);
                    }
                    break;
                case GraphPresentationKind.Condition:
                    AddSingleFieldSlot(item, edges, "condition", "Condition", outgoing, primary, internalEdges, childPath);
                    AddSingleFieldSlot(item, edges, "trueNode", "True", outgoing, primary, internalEdges, childPath);
                    AddSingleFieldSlot(item, edges, "falseNode", "False", outgoing, primary, internalEdges, childPath);
                    break;
            }

            return item;
        }

        private static void AddChildSlot(
            GraphPresentationItem parent,
            GraphEdgeDescriptor edge,
            string label,
            int index,
            IReadOnlyDictionary<UUID, List<GraphEdgeDescriptor>> outgoing,
            IDictionary<UUID, GraphPresentationItem> primary,
            ISet<GraphEdgeDescriptor> internalEdges,
            ISet<UUID> path)
        {
            GraphPresentationItem content = CreateSlotContent(edge, outgoing, primary, internalEdges, path, out bool isInternal);
            if (isInternal)
            {
                internalEdges.Add(edge);
            }

            parent.AddSlot(new GraphPresentationSlot(label, index, edge, content));
        }

        private static void AddSingleFieldSlot(
            GraphPresentationItem parent,
            IReadOnlyList<GraphEdgeDescriptor> edges,
            string fieldName,
            string label,
            IReadOnlyDictionary<UUID, List<GraphEdgeDescriptor>> outgoing,
            IDictionary<UUID, GraphPresentationItem> primary,
            ISet<GraphEdgeDescriptor> internalEdges,
            ISet<UUID> path)
        {
            GraphEdgeDescriptor edge = null;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].Label == fieldName)
                {
                    edge = edges[i];
                    break;
                }
            }

            if (edge == null)
            {
                parent.AddSlot(new GraphPresentationSlot(label, -1, null, CreateMissing(UUID.Empty, $"{label} is empty")));
                return;
            }

            GraphPresentationItem content = CreateSlotContent(edge, outgoing, primary, internalEdges, path, out bool isInternal);
            if (isInternal)
            {
                internalEdges.Add(edge);
            }

            parent.AddSlot(new GraphPresentationSlot(label, -1, edge, content));
        }

        private static GraphPresentationItem CreateSlotContent(
            GraphEdgeDescriptor edge,
            IReadOnlyDictionary<UUID, List<GraphEdgeDescriptor>> outgoing,
            IDictionary<UUID, GraphPresentationItem> primary,
            ISet<GraphEdgeDescriptor> internalEdges,
            ISet<UUID> path,
            out bool isInternal)
        {
            isInternal = false;
            if (edge == null || edge.Target == null)
            {
                return CreateMissing(edge?.TargetUUID ?? UUID.Empty, edge == null ? "Missing reference" : edge.Label);
            }

            if (path.Contains(edge.Target.UUID))
            {
                return CreateProxy(edge.Target, "Cycle reference");
            }

            if (primary.ContainsKey(edge.Target.UUID))
            {
                return CreateProxy(edge.Target, "Repeated or multi-parent reference");
            }

            GraphPresentationItem content = BuildItem(edge.Target, outgoing, primary, internalEdges, path, isRoot: false);
            isInternal = content.Kind != GraphPresentationKind.ReferenceProxy;
            return content;
        }

        private static Dictionary<UUID, List<GraphEdgeDescriptor>> BuildOutgoing(GraphTopology topology)
        {
            Dictionary<UUID, List<GraphEdgeDescriptor>> outgoing = new();
            foreach (GraphEdgeDescriptor edge in topology.Edges)
            {
                if (edge.Kind != GraphEdgeKind.Child)
                {
                    continue;
                }

                if (!outgoing.TryGetValue(edge.Source.UUID, out List<GraphEdgeDescriptor> edges))
                {
                    edges = new List<GraphEdgeDescriptor>();
                    outgoing.Add(edge.Source.UUID, edges);
                }

                edges.Add(edge);
            }

            return outgoing;
        }

        private static GraphPresentationKind GetKind(TreeNode node)
        {
            return node switch
            {
                Sequence => GraphPresentationKind.Sequence,
                Decision => GraphPresentationKind.Decision,
                Condition => GraphPresentationKind.Condition,
                _ => GraphPresentationKind.Card,
            };
        }

        private static GraphPresentationItem CreateProxy(GraphNodeDescriptor descriptor, string reason)
        {
            return new GraphPresentationItem(GraphPresentationKind.ReferenceProxy, descriptor, descriptor.UUID, reason);
        }

        private static GraphPresentationItem CreateMissing(UUID uuid, string warning)
        {
            return new GraphPresentationItem(GraphPresentationKind.Missing, null, uuid, warning);
        }

        private static void AddEmptySlots(GraphPresentationItem item)
        {
            item.AddSlot(new GraphPresentationSlot("No events", -1, null, CreateMissing(UUID.Empty, "No events")));
        }
    }

    /// <summary>
    /// Computes deterministic in-memory positions for semantic presentation items.
    /// </summary>
    internal static class GraphPresentationLayout
    {
        private const float ContainerMinWidth = 320f;
        private const float HeaderHeight = 48f;
        private const float Padding = 16f;
        private const float SlotGap = 12f;
        private const float SlotLabelWidth = 72f;
        private const float BranchGap = 18f;
        private const float PlaceholderWidth = 220f;
        private const float PlaceholderHeight = 52f;

        /// <summary>Measures and positions every presentation item without touching descriptors.</summary>
        /// <param name="presentation">The presentation to layout.</param>
        internal static void Layout(GraphPresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            foreach (GraphPresentationItem root in presentation.Roots)
            {
                Measure(root);
                root.Position = root.Node?.Position ?? Vector2.zero;
                PositionChildren(root);
            }
        }

        /// <summary>Returns the default size for a non-container presentation item.</summary>
        internal static Vector2 GetItemSize(GraphPresentationItem item)
        {
            if (item == null || item.Node == null)
            {
                return new Vector2(PlaceholderWidth, PlaceholderHeight);
            }

            return GraphLayoutResolver.GetNodeSize(item.Node);
        }

        private static Vector2 Measure(GraphPresentationItem item)
        {
            if (item == null)
            {
                return new Vector2(PlaceholderWidth, PlaceholderHeight);
            }

            if (!item.IsContainer)
            {
                item.Size = GetItemSize(item);
                return item.Size;
            }

            float width = ContainerMinWidth;
            float height = HeaderHeight + Padding;
            if (item.Kind is GraphPresentationKind.Sequence or GraphPresentationKind.Decision)
            {
                for (int i = 0; i < item.Slots.Count; i++)
                {
                    Vector2 childSize = Measure(item.Slots[i].Content);
                    width = Mathf.Max(width, Padding * 2f + SlotLabelWidth + childSize.x);
                    height += Mathf.Max(PlaceholderHeight, childSize.y) + SlotGap;
                }

                item.Size = new Vector2(width, Mathf.Max(height, HeaderHeight + Padding + PlaceholderHeight));
                return item.Size;
            }

            Vector2 predicateSize = Measure(GetSlotContent(item, "Condition"));
            Vector2 trueSize = Measure(GetSlotContent(item, "True"));
            Vector2 falseSize = Measure(GetSlotContent(item, "False"));
            float branchesWidth = trueSize.x + BranchGap + falseSize.x;
            width = Mathf.Max(width, Padding * 2f + Mathf.Max(predicateSize.x, branchesWidth));
            height += predicateSize.y + SlotGap + Mathf.Max(trueSize.y, falseSize.y) + Padding;
            item.Size = new Vector2(width, height);
            return item.Size;
        }

        private static void PositionChildren(GraphPresentationItem item)
        {
            if (item == null || !item.IsContainer)
            {
                return;
            }

            if (item.Kind is GraphPresentationKind.Sequence or GraphPresentationKind.Decision)
            {
                float y = HeaderHeight + Padding;
                for (int i = 0; i < item.Slots.Count; i++)
                {
                    GraphPresentationSlot slot = item.Slots[i];
                    GraphPresentationItem child = slot.Content;
                    child.Position = item.Position + new Vector2(Padding + SlotLabelWidth, y);
                    PositionChildren(child);
                    y += Mathf.Max(PlaceholderHeight, child.Size.y) + SlotGap;
                }

                return;
            }

            GraphPresentationItem predicate = GetSlotContent(item, "Condition");
            GraphPresentationItem trueItem = GetSlotContent(item, "True");
            GraphPresentationItem falseItem = GetSlotContent(item, "False");
            predicate.Position = item.Position + new Vector2((item.Size.x - predicate.Size.x) * 0.5f, HeaderHeight + Padding);
            PositionChildren(predicate);

            float branchY = predicate.Position.y - item.Position.y + predicate.Size.y + SlotGap;
            float branchWidth = trueItem.Size.x + BranchGap + falseItem.Size.x;
            float branchX = (item.Size.x - branchWidth) * 0.5f;
            trueItem.Position = item.Position + new Vector2(branchX, branchY);
            falseItem.Position = item.Position + new Vector2(branchX + trueItem.Size.x + BranchGap, branchY);
            PositionChildren(trueItem);
            PositionChildren(falseItem);
        }

        private static GraphPresentationItem GetSlotContent(GraphPresentationItem item, string label)
        {
            for (int i = 0; i < item.Slots.Count; i++)
            {
                if (item.Slots[i].Label == label)
                {
                    return item.Slots[i].Content;
                }
            }

            return new GraphPresentationItem(GraphPresentationKind.Missing, null, UUID.Empty, $"{label} is empty");
        }
    }
}
