using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Semantic shape used by a graph node card.
    /// </summary>
    internal enum GraphNodeShape
    {
        Normal,
        Flow,
        Branch,
        Service,
    }

    /// <summary>
    /// Relationship rendered between two graph nodes.
    /// </summary>
    internal enum GraphEdgeKind
    {
        /// <summary>Ordinary authored child reference.</summary>
        Child,
        /// <summary>Service attachment reference.</summary>
        Service,
        /// <summary>Optional raw reference.</summary>
        Raw,
    }

    /// <summary>Describes whether an authored UUID is empty, resolved, or dangling.</summary>
    internal enum GraphReferenceState
    {
        Resolved,
        Empty,
        Missing,
    }

    /// <summary>
    /// Immutable description of one node in a graph snapshot.
    /// </summary>
    internal sealed class GraphNodeDescriptor
    {
        internal GraphNodeDescriptor(TreeNode node, bool isHead)
        {
            Node = node;
            UUID = node.uuid;
            DisplayName = string.IsNullOrWhiteSpace(node.name) ? NodeMenuCache.Shared.GetDisplayName(node.GetType()) : node.name;
            NodeType = node.GetType();
            Shape = ClassifyShape(node);
            IsHead = isHead;
        }

        /// <summary>
        /// Gets the source node instance.
        /// </summary>
        internal TreeNode Node { get; }

        /// <summary>
        /// Gets the stable node UUID.
        /// </summary>
        internal UUID UUID { get; }

        /// <summary>
        /// Gets the display name.
        /// </summary>
        internal string DisplayName { get; }

        /// <summary>
        /// Gets the runtime node type.
        /// </summary>
        internal Type NodeType { get; }

        /// <summary>
        /// Gets the semantic card shape.
        /// </summary>
        internal GraphNodeShape Shape { get; }

        /// <summary>
        /// Gets whether this is the configured tree head.
        /// </summary>
        internal bool IsHead { get; }

        /// <summary>
        /// Gets whether the node is reachable from the configured head.
        /// </summary>
        internal bool IsReachable { get; set; }

        /// <summary>
        /// Gets or sets the warning shown on the node card.
        /// </summary>
        internal string Warning { get; set; }

        /// <summary>
        /// Gets or sets the resolved editor-only canvas position.
        /// </summary>
        internal Vector2 Position { get; set; }

        /// <summary>
        /// Gets whether this node has a warning.
        /// </summary>
        internal bool HasWarning => !string.IsNullOrEmpty(Warning);

        internal static GraphNodeShape ClassifyShape(TreeNode node)
        {
            if (node is Service)
            {
                return GraphNodeShape.Service;
            }

            if (node is Sequence or Parallel or Loop or ForEach)
            {
                return GraphNodeShape.Flow;
            }

            if (node is Decision or Condition or Probability or PseudoProbability)
            {
                return GraphNodeShape.Branch;
            }

            return GraphNodeShape.Normal;
        }
    }

    /// <summary>
    /// Immutable description of one reference in a graph snapshot.
    /// </summary>
    internal sealed class GraphEdgeDescriptor
    {
        internal GraphEdgeDescriptor(GraphNodeDescriptor source, GraphNodeDescriptor target, AuthoredReferenceSnapshot reference, GraphEdgeKind kind, string label, int occurrenceId)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            if (reference.Address.OwnerUUID != source.UUID)
            {
                throw new ArgumentException("The authored reference owner must match the graph edge source.", nameof(reference));
            }

            Target = target;
            Reference = reference;
            Kind = kind;
            Label = label ?? string.Empty;
            OccurrenceId = occurrenceId;
        }

        /// <summary>
        /// Gets the source descriptor.
        /// </summary>
        internal GraphNodeDescriptor Source { get; }

        /// <summary>
        /// Gets the target descriptor, or null when the UUID is missing.
        /// </summary>
        internal GraphNodeDescriptor Target { get; }

        /// <summary>
        /// Gets the authored occurrence address and expected value.
        /// </summary>
        internal AuthoredReferenceSnapshot Reference { get; }

        /// <summary>
        /// Gets the edge semantic kind.
        /// </summary>
        internal GraphEdgeKind Kind { get; }

        /// <summary>
        /// Gets the field/index label.
        /// </summary>
        internal string Label { get; }

        /// <summary>
        /// Gets the current graph resolution state of the authored reference.
        /// </summary>
        internal GraphReferenceState ReferenceState => Reference.IsEmpty
            ? GraphReferenceState.Empty
            : Target == null ? GraphReferenceState.Missing : GraphReferenceState.Resolved;

        /// <summary>
        /// Gets the stable occurrence identifier for this snapshot reference.
        /// Duplicate references intentionally receive different identifiers.
        /// </summary>
        internal int OccurrenceId { get; }

    }

    /// <summary>
    /// Topology snapshot used by the native graph canvas.
    /// </summary>
    internal sealed class GraphTopology
    {
        internal GraphTopology(BehaviourTreeData tree, List<GraphNodeDescriptor> nodes, List<GraphEdgeDescriptor> edges)
        {
            Tree = tree;
            Nodes = new ReadOnlyCollection<GraphNodeDescriptor>(nodes);
            Edges = new ReadOnlyCollection<GraphEdgeDescriptor>(edges);
        }

        /// <summary>Gets the authoritative tree used to create this editor snapshot.</summary>
        internal BehaviourTreeData Tree { get; }

        /// <summary>
        /// Gets all non-null authored nodes in serialized order.
        /// </summary>
        internal IReadOnlyList<GraphNodeDescriptor> Nodes { get; }

        /// <summary>
        /// Gets all authored references in accessor and collection order.
        /// </summary>
        internal IReadOnlyList<GraphEdgeDescriptor> Edges { get; }

        /// <summary>
        /// Finds a node descriptor by UUID.
        /// </summary>
        /// <param name="uuid">The UUID to look up.</param>
        /// <returns>The matching descriptor or null.</returns>
        internal GraphNodeDescriptor FindNode(UUID uuid)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].UUID == uuid)
                {
                    return Nodes[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Reads BehaviourTreeData references without consulting the legacy graph lists.
    /// </summary>
    internal sealed class GraphTopologyBuilder
    {
        private readonly BehaviourTreeData tree;

        /// <summary>
        /// Initializes a topology builder for a tree.
        /// </summary>
        /// <param name="tree">The authoritative behaviour tree data.</param>
        internal GraphTopologyBuilder(BehaviourTreeData tree)
        {
            this.tree = tree;
        }

        /// <summary>
        /// Builds a topology snapshot with raw references hidden by default.
        /// </summary>
        /// <returns>A topology snapshot.</returns>
        internal GraphTopology Build()
        {
            return Build(false);
        }

        /// <summary>
        /// Builds a topology snapshot from node accessors.
        /// </summary>
        /// <param name="includeRawReferences">Whether raw references should become dotted edges.</param>
        /// <returns>A topology snapshot.</returns>
        internal GraphTopology Build(bool includeRawReferences)
        {
            if (!tree || tree.nodes == null)
            {
                return new GraphTopology(tree, new List<GraphNodeDescriptor>(), new List<GraphEdgeDescriptor>());
            }

            List<GraphNodeDescriptor> nodes = new();
            Dictionary<UUID, GraphNodeDescriptor> byUUID = new();
            foreach (TreeNode node in tree.nodes)
            {
                if (node == null || byUUID.ContainsKey(node.uuid))
                {
                    continue;
                }

                GraphNodeDescriptor descriptor = new(node, node.uuid == tree.headNodeUUID);
                nodes.Add(descriptor);
                byUUID.Add(node.uuid, descriptor);
            }

            List<GraphEdgeDescriptor> edges = new();
            foreach (GraphNodeDescriptor source in nodes)
            {
                AppendEdges(source, byUUID, includeRawReferences, edges);
            }

            MarkReachability(tree.headNodeUUID, byUUID, edges);
            return new GraphTopology(tree, nodes, edges);
        }

        /// <summary>
        /// Convenience entry point for callers that do not need a retained builder.
        /// </summary>
        /// <param name="tree">The authoritative behaviour tree data.</param>
        /// <param name="includeRawReferences">Whether raw references should be included.</param>
        /// <returns>A topology snapshot.</returns>
        internal static GraphTopology Build(BehaviourTreeData tree, bool includeRawReferences = false)
        {
            return new GraphTopologyBuilder(tree).Build(includeRawReferences);
        }

        private static void AppendEdges(
            GraphNodeDescriptor source,
            IReadOnlyDictionary<UUID, GraphNodeDescriptor> byUUID,
            bool includeRawReferences,
            ICollection<GraphEdgeDescriptor> edges)
        {
            GraphEdgeCollector collector = new(source, byUUID, includeRawReferences, edges.Count);
            foreach (GraphEdgeDescriptor edge in collector.Collect())
            {
                edges.Add(edge);
            }
        }

        /// <summary>Identifies direct raw-reference slots whose null value is not a visible child occurrence.</summary>
        private static bool IsRawReferenceSlot(TreeNode owner, string fieldName)
        {
            FieldInfo field = owner?.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Type valueType = field?.FieldType;
            if (valueType == null)
            {
                return false;
            }

            if (valueType.IsArray)
            {
                valueType = valueType.GetElementType();
            }
            else if (valueType.IsGenericType)
            {
                valueType = valueType.GetGenericArguments().FirstOrDefault();
            }

            return valueType != null && typeof(RawNodeReference).IsAssignableFrom(valueType);
        }

        /// <summary>Collects every graph edge authored by one source node.</summary>
        private sealed class GraphEdgeCollector : NodeMemberVisitor
        {
            private readonly GraphNodeDescriptor source;
            private readonly IReadOnlyDictionary<UUID, GraphNodeDescriptor> byUUID;
            private readonly bool includeRawReferences;
            private readonly int occurrenceBase;
            private readonly List<GraphEdgeDescriptor> edges = new();

            /// <summary>Creates a source-scoped graph edge collector.</summary>
            internal GraphEdgeCollector(
                GraphNodeDescriptor source,
                IReadOnlyDictionary<UUID, GraphNodeDescriptor> byUUID,
                bool includeRawReferences,
                int occurrenceBase)
            {
                this.source = source;
                this.byUUID = byUUID;
                this.includeRawReferences = includeRawReferences;
                this.occurrenceBase = occurrenceBase;
            }

            /// <summary>Collects visited and null collection occurrences in authored order.</summary>
            internal IReadOnlyList<GraphEdgeDescriptor> Collect()
            {
                NodeDescriptorProvider.Get(source.Node.GetType()).VisitMembers(source.Node, this);
                AppendNullCollectionOccurrences();
                RestoreNullOccurrenceOrder();
                return edges;
            }

            protected override void OnNodeReference(string path, INodeReference reference)
            {
                string rootName = path;
                int separator = path.IndexOfAny(new[] { '.', '[' });
                if (separator >= 0)
                {
                    rootName = path.Substring(0, separator);
                }

                int index = -1;
                int openBracket = path.LastIndexOf('[');
                if (openBracket >= 0 && path.EndsWith("]", StringComparison.Ordinal))
                {
                    int.TryParse(path.Substring(openBracket + 1, path.Length - openBracket - 2), out index);
                }

                AppendEdge(reference, new NodeReferenceAddress(source.UUID, rootName, index));
            }

            protected override void OnVariableBinding(string path, IVariableBinding binding)
            {
            }

            /// <summary>Adds null collection entries skipped by normal member traversal.</summary>
            private void AppendNullCollectionOccurrences()
            {
                foreach (INodeReferenceSlot slot in NodeReferenceStructureProvider.GetSlots(source.Node))
                {
                    if (slot.Name == nameof(TreeNode.parent)
                        || IsRawReferenceSlot(source.Node, slot.Name)
                        || slot is not INodeReferenceListSlot list)
                    {
                        continue;
                    }

                    for (int index = 0; index < list.Count; index++)
                    {
                        if (list.GetReference(index) == null)
                        {
                            AppendEdge(null, new NodeReferenceAddress(source.UUID, slot.Name, index));
                        }
                    }
                }
            }

            /// <summary>Restores null occurrences alongside visited authored references.</summary>
            private void RestoreNullOccurrenceOrder()
            {
                List<string> fieldOrder = NodeReferenceStructureProvider.GetSlots(source.Node)
                    .Select(slot => slot.Name)
                    .ToList();
                List<GraphEdgeDescriptor> nullOccurrences = edges
                    .Where(edge => edge.Reference.IsNull)
                    .ToList();
                edges.RemoveAll(edge => edge.Reference.IsNull);
                foreach (GraphEdgeDescriptor nullOccurrence in nullOccurrences)
                {
                    int nullFieldOrder = fieldOrder.IndexOf(nullOccurrence.Reference.Address.FieldName);
                    int insertionIndex = edges.FindIndex(edge =>
                    {
                        int edgeFieldOrder = fieldOrder.IndexOf(edge.Reference.Address.FieldName);
                        return edgeFieldOrder > nullFieldOrder
                            || edgeFieldOrder == nullFieldOrder
                            && edge.Reference.Address.Index > nullOccurrence.Reference.Address.Index;
                    });
                    edges.Insert(insertionIndex < 0 ? edges.Count : insertionIndex, nullOccurrence);
                }
            }

            /// <summary>Creates one edge from an authored reference value and address.</summary>
            private void AppendEdge(INodeReference reference, NodeReferenceAddress address)
            {
                if (address.FieldName == nameof(TreeNode.parent))
                {
                    return;
                }

                bool isRawReference = reference?.IsRawReference == true
                    || IsRawReferenceSlot(source.Node, address.FieldName);
                if (isRawReference && !includeRawReferences || reference == null && address.Index < 0)
                {
                    return;
                }

                bool isService = address.FieldName == nameof(ServiceHostNode.services);
                GraphEdgeKind kind = isRawReference
                    ? GraphEdgeKind.Raw
                    : isService ? GraphEdgeKind.Service : GraphEdgeKind.Child;
                AuthoredReferenceSnapshot snapshot = new(
                    address,
                    reference?.UUID ?? UUID.Empty,
                    reference == null);
                GraphNodeDescriptor target = snapshot.IsEmpty
                    || !byUUID.TryGetValue(snapshot.TargetUUID, out GraphNodeDescriptor found)
                    ? null
                    : found;
                string label = BuildLabel(
                    source.Node,
                    address.FieldName,
                    address.Index,
                    kind,
                    reference);
                edges.Add(new GraphEdgeDescriptor(
                    source,
                    target,
                    snapshot,
                    kind,
                    label,
                    occurrenceBase + edges.Count));

                if (!snapshot.IsEmpty && target == null)
                {
                    string warning = $"Missing target {snapshot.TargetUUID} ({label})";
                    source.Warning = string.IsNullOrEmpty(source.Warning)
                        ? warning
                        : source.Warning + ", " + warning;
                }
            }
        }

        private static string BuildLabel(TreeNode source, string fieldName, int index, GraphEdgeKind kind, INodeReference reference)
        {
            if (kind == GraphEdgeKind.Service)
            {
                return index >= 0 ? $"Service [{index}]" : "Service";
            }

            string label = index >= 0 ? $"{fieldName} [{index}]" : fieldName;
            if (reference is Probability.EventWeight probability)
            {
                label += $" ({probability.Weight})";
            }
            else if (reference is PseudoProbability.EventWeight pseudoProbability)
            {
                label += pseudoProbability.weight == null || pseudoProbability.weight.IsConstant
                    ? $" ({Mathf.Max(0, pseudoProbability.weight?.Constant ?? 0)})"
                    : " (dynamic)";
            }

            return label;
        }

        private static void MarkReachability(
            UUID headUUID,
            IReadOnlyDictionary<UUID, GraphNodeDescriptor> byUUID,
            IReadOnlyList<GraphEdgeDescriptor> edges)
        {
            if (!byUUID.TryGetValue(headUUID, out GraphNodeDescriptor head))
            {
                return;
            }

            Dictionary<UUID, List<GraphEdgeDescriptor>> outgoing = new();
            foreach (GraphEdgeDescriptor edge in edges)
            {
                if (edge.Target == null || edge.Kind == GraphEdgeKind.Raw)
                {
                    continue;
                }

                if (!outgoing.TryGetValue(edge.Source.UUID, out List<GraphEdgeDescriptor> list))
                {
                    list = new List<GraphEdgeDescriptor>();
                    outgoing.Add(edge.Source.UUID, list);
                }

                list.Add(edge);
            }

            Stack<GraphNodeDescriptor> pending = new();
            HashSet<UUID> visited = new();
            pending.Push(head);
            while (pending.Count > 0)
            {
                GraphNodeDescriptor current = pending.Pop();
                if (!visited.Add(current.UUID))
                {
                    continue;
                }

                current.IsReachable = true;
                if (!outgoing.TryGetValue(current.UUID, out List<GraphEdgeDescriptor> children))
                {
                    continue;
                }

                for (int i = children.Count - 1; i >= 0; i--)
                {
                    pending.Push(children[i].Target);
                }
            }
        }
    }
}
