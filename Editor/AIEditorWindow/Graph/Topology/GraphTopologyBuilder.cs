using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        internal GraphEdgeDescriptor(
            GraphNodeDescriptor source,
            GraphNodeDescriptor target,
            UUID targetUUID,
            GraphEdgeKind kind,
            string label,
            bool isMissing,
            int occurrenceId = -1,
            string fieldName = null,
            int collectionIndex = -1)
        {
            Source = source;
            Target = target;
            TargetUUID = targetUUID;
            Kind = kind;
            Label = label;
            IsMissingTarget = isMissing;
            OccurrenceId = occurrenceId;
            FieldName = fieldName ?? string.Empty;
            CollectionIndex = collectionIndex;
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
        /// Gets the referenced UUID even when no target exists.
        /// </summary>
        internal UUID TargetUUID { get; }

        /// <summary>
        /// Gets the edge semantic kind.
        /// </summary>
        internal GraphEdgeKind Kind { get; }

        /// <summary>
        /// Gets the field/index label.
        /// </summary>
        internal string Label { get; }

        /// <summary>
        /// Gets whether the target UUID does not resolve to a node.
        /// </summary>
        internal bool IsMissingTarget { get; }

        /// <summary>
        /// Gets the stable occurrence identifier for this snapshot reference.
        /// Duplicate references intentionally receive different identifiers.
        /// </summary>
        internal int OccurrenceId { get; }

        /// <summary>Gets the authored field name without presentation text.</summary>
        internal string FieldName { get; }

        /// <summary>Gets the authored collection index, or -1 for a scalar reference.</summary>
        internal int CollectionIndex { get; }
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
            NodeDescriptorProvider.Get(source.Node.GetType()).VisitMembers(
                source.Node,
                new GraphReferenceVisitor(source, byUUID, includeRawReferences, edges));
        }

        private sealed class GraphReferenceVisitor : NodeMemberVisitor
        {
            private readonly GraphNodeDescriptor source;
            private readonly IReadOnlyDictionary<UUID, GraphNodeDescriptor> byUUID;
            private readonly bool includeRawReferences;
            private readonly ICollection<GraphEdgeDescriptor> edges;

            public GraphReferenceVisitor(
                GraphNodeDescriptor source,
                IReadOnlyDictionary<UUID, GraphNodeDescriptor> byUUID,
                bool includeRawReferences,
                ICollection<GraphEdgeDescriptor> edges)
            {
                this.source = source;
                this.byUUID = byUUID;
                this.includeRawReferences = includeRawReferences;
                this.edges = edges;
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

                AppendEdge(
                    source,
                    reference,
                    rootName,
                    index,
                    rootName == nameof(TreeNode.parent),
                    includeRawReferences,
                    byUUID,
                    edges);
            }

            protected override void OnVariableBinding(string path, IVariableBinding binding)
            {
            }
        }

        private static void AppendEdge(
            GraphNodeDescriptor source,
            INodeReference reference,
            string fieldName,
            int index,
            bool isParentOrServiceField,
            bool includeRawReferences,
            IReadOnlyDictionary<UUID, GraphNodeDescriptor> byUUID,
            ICollection<GraphEdgeDescriptor> edges)
        {
            if (reference == null
                || reference.UUID == UUID.Empty
                || isParentOrServiceField)
            {
                return;
            }

            if (reference.IsRawReference && !includeRawReferences)
            {
                return;
            }

            string rootName = fieldName;
            int separator = fieldName.IndexOfAny(new[] { '.', '[' });
            if (separator >= 0)
            {
                rootName = fieldName.Substring(0, separator);
            }

            bool isService = rootName == nameof(ServiceHostNode.services);
            GraphEdgeKind kind = reference.IsRawReference
                ? GraphEdgeKind.Raw
                : isService ? GraphEdgeKind.Service : GraphEdgeKind.Child;
            GraphNodeDescriptor target = byUUID.TryGetValue(reference.UUID, out GraphNodeDescriptor found) ? found : null;
            bool missing = target == null;
            string label = BuildLabel(source.Node, fieldName, index, kind, reference);
            edges.Add(new GraphEdgeDescriptor(
                source,
                target,
                reference.UUID,
                kind,
                label,
                missing,
                edges.Count,
                fieldName,
                index));

            if (missing)
            {
                string warning = $"Missing target {reference.UUID} ({label})";
                source.Warning = string.IsNullOrEmpty(source.Warning) ? warning : source.Warning + ", " + warning;
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
