using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aethiumian.AI.Editor
{
    /// <summary>Describes the topology command selected by one authored output port.</summary>
    internal enum GraphPortOperation
    {
        Connect,
        Replace,
        Insert,
    }

    /// <summary>Describes how one authored field is represented by canvas ports.</summary>
    internal enum GraphPortPresentationMode
    {
        Single,
        Ordered,
        Shared,
    }

    /// <summary>Describes the source-side geometry used by a canvas-only authored port.</summary>
    internal enum GraphPortAnchorKind
    {
        Output,
        Service,
        DistributedOutput,
        ChainedOutput,
        ConditionPredicate,
        ConditionTrue,
        ConditionFalse,
    }

    /// <summary>One canvas-only handle for an authored reference slot or shared collection field.</summary>
    internal sealed class GraphPortDescriptor
    {
        internal GraphPortDescriptor(
            UUID ownerUUID,
            string fieldName,
            int collectionIndex,
            GraphPortOperation operation,
            GraphPortPresentationMode presentationMode,
            GraphPresentationEndpoint source,
            GraphPresentationRelation relation,
            IReadOnlyList<GraphEdgeDescriptor> origins,
            bool isRaw,
            GraphPortAnchorKind anchorKind)
        {
            OwnerUUID = ownerUUID;
            FieldName = fieldName;
            CollectionIndex = collectionIndex;
            Operation = operation;
            PresentationMode = presentationMode;
            Source = source;
            Relation = relation;
            Origins = origins ?? Array.Empty<GraphEdgeDescriptor>();
            IsRaw = isRaw;
            AnchorKind = anchorKind;
        }

        internal UUID OwnerUUID { get; }
        internal string FieldName { get; }
        internal int CollectionIndex { get; }
        internal GraphPortOperation Operation { get; }
        internal GraphPortPresentationMode PresentationMode { get; }
        internal GraphPresentationEndpoint Source { get; }
        internal GraphPresentationRelation Relation { get; }
        internal IReadOnlyList<GraphEdgeDescriptor> Origins { get; }
        internal GraphEdgeDescriptor Origin => Origins.Count == 1 ? Origins[0] : null;
        internal bool IsRaw { get; }
        internal GraphPortAnchorKind AnchorKind { get; }
        internal int OutputIndex { get; private set; }
        internal int OutputCount { get; private set; }
        internal bool IsOccupied => Operation == GraphPortOperation.Replace;

        /// <summary>Gets whether this port is the canvas source for the authored relation origin.</summary>
        internal bool ContainsOrigin(GraphEdgeDescriptor origin) => origin != null && Origins.Contains(origin);

        /// <summary>Sets the derived ordinal used by ordered collection outputs.</summary>
        internal void SetOutputSlot(int index, int count)
        {
            OutputIndex = index;
            OutputCount = count;
        }
    }

    /// <summary>Describes the editor-only Entrance output without impersonating an authored reference slot.</summary>
    internal sealed class GraphEntrancePortDescriptor
    {
        internal GraphEntrancePortDescriptor(GraphPresentationItem entrance, GraphPresentationRelation relation)
        {
            Entrance = entrance ?? throw new ArgumentNullException(nameof(entrance));
            Relation = relation;
        }

        internal GraphPresentationItem Entrance { get; }
        internal GraphPresentationEndpoint Source => Entrance.Output;
        internal GraphPresentationRelation Relation { get; }
        internal GraphPortOperation Operation => Relation == null ? GraphPortOperation.Connect : GraphPortOperation.Replace;
    }

    /// <summary>One explicitly typed source for the shared canvas connection gesture.</summary>
    internal sealed class GraphConnectionSource
    {
        private GraphConnectionSource(GraphPortDescriptor authoredPort, GraphEntrancePortDescriptor entrancePort)
        {
            AuthoredPort = authoredPort;
            EntrancePort = entrancePort;
        }

        internal GraphPortDescriptor AuthoredPort { get; }
        internal GraphEntrancePortDescriptor EntrancePort { get; }
        internal bool IsEntrance => EntrancePort != null;
        internal GraphPortOperation Operation => IsEntrance ? EntrancePort.Operation : AuthoredPort.Operation;

        internal static GraphConnectionSource Authored(GraphPortDescriptor port) =>
            new(port ?? throw new ArgumentNullException(nameof(port)), null);

        internal static GraphConnectionSource Entrance(GraphEntrancePortDescriptor port) =>
            new(null, port ?? throw new ArgumentNullException(nameof(port)));
    }

    /// <summary>Builds canvas port handles from topology slots and authored presentation relations.</summary>
    internal static class GraphPortDescriptorBuilder
    {
        internal static IReadOnlyList<GraphPortDescriptor> Build(
            GraphTopology topology,
            GraphPresentation presentation,
            bool includeRawReferences)
        {
            if (topology == null || presentation == null)
            {
                return Array.Empty<GraphPortDescriptor>();
            }

            List<GraphPortDescriptor> result = new();
            Dictionary<GraphEdgeDescriptor, GraphPresentationRelation> relations = presentation.Relations
                .Where(relation => relation.Origin != null)
                .GroupBy(relation => relation.Origin)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                GraphPresentationItem item = presentation.Find(node.UUID);
                if (item == null)
                {
                    continue;
                }

                AppendPorts(topology, presentation.Relations, relations, node, item, includeRawReferences, result);
            }

            AssignOrderedOutputSlots(result);
            return result;
        }

        private static void AppendPorts(
            GraphTopology topology,
            IReadOnlyList<GraphPresentationRelation> presentationRelations,
            IReadOnlyDictionary<GraphEdgeDescriptor, GraphPresentationRelation> relations,
            GraphNodeDescriptor node,
            GraphPresentationItem item,
            bool includeRawReferences,
            ICollection<GraphPortDescriptor> ports)
        {
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(node.Node.GetType());
            foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
            {
                if (field.Name == nameof(TreeNode.parent))
                {
                    continue;
                }

                INodeReference reference = field.Get(node.Node);
                bool isRaw = reference?.IsRawReference == true || field.FieldType == typeof(RawNodeReference);
                if (isRaw && !includeRawReferences)
                {
                    continue;
                }

                GraphEdgeDescriptor edge = FindEdge(topology, node.UUID, field.Name, -1);
                ports.Add(CreatePort(
                    node.UUID,
                    field.Name,
                    -1,
                    edge == null ? GraphPortOperation.Connect : GraphPortOperation.Replace,
                    GraphPortPresentationMode.Single,
                    item,
                    edge,
                    relations,
                    isRaw,
                    GetSingleAnchorKind(node.Node, field.Name, isRaw)));
            }

            foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
            {
                if (node.Node is Aethiumian.AI.Nodes.Boolean or Constant)
                {
                    continue;
                }

                if (field.Name == nameof(ServiceHostNode.services) && !node.Node.CanEditServices())
                {
                    continue;
                }

                bool isRaw = field.ElementType == typeof(RawNodeReference);
                if (isRaw && !includeRawReferences)
                {
                    continue;
                }

                GraphPortPresentationMode mode = GetCollectionPresentationMode(node.Node, field.Name);
                List<GraphEdgeDescriptor> fieldEdges = topology.Edges
                    .Where(edge => edge.Source.UUID == node.UUID && edge.FieldName == field.Name)
                    .OrderBy(edge => edge.CollectionIndex)
                    .ToList();
                if (mode == GraphPortPresentationMode.Shared)
                {
                    ports.Add(new GraphPortDescriptor(
                        node.UUID,
                        field.Name,
                        -1,
                        GraphPortOperation.Insert,
                        mode,
                        new GraphPresentationEndpoint(item, GraphPresentationAnchorKind.Output),
                        null,
                        fieldEdges,
                        isRaw,
                        GetAnchorKind(field.Name, isRaw, mode)));
                    continue;
                }

                IList collection = field.Get(node.Node);
                int count = collection?.Count ?? 0;
                for (int index = 0; index < count; index++)
                {
                    GraphEdgeDescriptor edge = fieldEdges.FirstOrDefault(candidate => candidate.CollectionIndex == index);
                    GraphPortAnchorKind anchorKind = GetCollectionAnchorKind(node.Node, field.Name, isRaw, mode);
                    ports.Add(CreatePort(
                        node.UUID,
                        field.Name,
                        index,
                        GraphPortOperation.Replace,
                        mode,
                        item,
                        edge,
                        relations,
                        isRaw,
                        anchorKind));
                }

                GraphPresentationEndpoint appendSource = GetCollectionAppendSource(item, field.Name, mode, presentationRelations);
                ports.Add(CreatePort(
                    node.UUID,
                    field.Name,
                    -1,
                    GraphPortOperation.Insert,
                    mode,
                    item,
                    null,
                    relations,
                    isRaw,
                    GetCollectionAnchorKind(node.Node, field.Name, isRaw, mode),
                    appendSource));
            }
        }

        private static GraphPortDescriptor CreatePort(
            UUID ownerUUID,
            string fieldName,
            int index,
            GraphPortOperation operation,
            GraphPortPresentationMode mode,
            GraphPresentationItem item,
            GraphEdgeDescriptor edge,
            IReadOnlyDictionary<GraphEdgeDescriptor, GraphPresentationRelation> relations,
            bool isRaw,
            GraphPortAnchorKind anchorKind,
            GraphPresentationEndpoint? sourceOverride = null)
        {
            GraphPresentationRelation relation = edge != null && relations.TryGetValue(edge, out GraphPresentationRelation found)
                ? found
                : null;
            return new GraphPortDescriptor(
                ownerUUID,
                fieldName,
                index,
                operation,
                mode,
                sourceOverride ?? relation?.Source ?? new GraphPresentationEndpoint(item, GraphPresentationAnchorKind.Output),
                relation,
                edge == null ? Array.Empty<GraphEdgeDescriptor>() : new[] { edge },
                isRaw,
                anchorKind);
        }

        private static GraphEdgeDescriptor FindEdge(GraphTopology topology, UUID ownerUUID, string fieldName, int index)
        {
            return topology.Edges.FirstOrDefault(edge => edge.Source.UUID == ownerUUID
                && edge.FieldName == fieldName
                && edge.CollectionIndex == index);
        }

        /// <summary>Returns the explicit canvas semantics for one authored collection field.</summary>
        private static GraphPortPresentationMode GetCollectionPresentationMode(TreeNode node, string fieldName)
        {
            if (fieldName == nameof(ServiceHostNode.services)
                || node is Parallel or Probability or PseudoProbability)
            {
                return GraphPortPresentationMode.Shared;
            }

            return GraphPortPresentationMode.Ordered;
        }

        /// <summary>Derives source geometry from explicit field presentation semantics.</summary>
        private static GraphPortAnchorKind GetAnchorKind(
            string fieldName,
            bool isRaw,
            GraphPortPresentationMode mode)
        {
            if (fieldName == nameof(ServiceHostNode.services))
            {
                return GraphPortAnchorKind.Service;
            }

            return !isRaw && mode == GraphPortPresentationMode.Ordered
                ? GraphPortAnchorKind.DistributedOutput
                : GraphPortAnchorKind.Output;
        }

        /// <summary>Returns the owner-local anchor reserved for one singular Condition field.</summary>
        private static GraphPortAnchorKind GetSingleAnchorKind(TreeNode node, string fieldName, bool isRaw)
        {
            if (node is Condition)
            {
                return fieldName switch
                {
                    "condition" => GraphPortAnchorKind.ConditionPredicate,
                    "trueNode" => GraphPortAnchorKind.ConditionTrue,
                    "falseNode" => GraphPortAnchorKind.ConditionFalse,
                    _ => GetAnchorKind(fieldName, isRaw, GraphPortPresentationMode.Single),
                };
            }

            return GetAnchorKind(fieldName, isRaw, GraphPortPresentationMode.Single);
        }

        /// <summary>Derives collection anchor geometry without conflating execution chains with branch distribution.</summary>
        private static GraphPortAnchorKind GetCollectionAnchorKind(
            TreeNode node,
            string fieldName,
            bool isRaw,
            GraphPortPresentationMode mode)
        {
            if (node is Sequence or Aggregate or Loop && fieldName == "events")
            {
                return GraphPortAnchorKind.ChainedOutput;
            }

            return GetAnchorKind(fieldName, isRaw, mode);
        }

        /// <summary>Gets the source endpoint used by an Insert handle after a chained execution collection.</summary>
        private static GraphPresentationEndpoint GetCollectionAppendSource(
            GraphPresentationItem item,
            string fieldName,
            GraphPortPresentationMode mode,
            IReadOnlyList<GraphPresentationRelation> presentation)
        {
            if (mode != GraphPortPresentationMode.Ordered || fieldName != "events")
            {
                return new GraphPresentationEndpoint(item, GraphPresentationAnchorKind.Output);
            }

            if (item.Node.Node is Sequence && item.SequenceScope.Members.Count > 0)
            {
                return item.SequenceScope.Members[item.SequenceScope.Members.Count - 1].Completion;
            }

            if (item.Node.Node is Aggregate && item.AggregateScope.Members.Count > 0)
            {
                return item.AggregateScope.Members[item.AggregateScope.Members.Count - 1].Completion;
            }

            if (item.Node.Node is Loop)
            {
                GraphPresentationItem body = item.LoopScope.Body[item.LoopScope.Body.Count - 1];
                if (body.LoopPlaceholder == null)
                {
                    return body.Completion;
                }

                GraphPresentationRelation bodyStart = presentation.FirstOrDefault(relation =>
                    relation.Kind == GraphPresentationRelationKind.LoopBody
                    && relation.Target.Item == body);
                if (bodyStart != null)
                {
                    return bodyStart.Source;
                }
            }

            return new GraphPresentationEndpoint(item, GraphPresentationAnchorKind.Output);
        }

        /// <summary>Assigns stable visual slots to ordered occurrences and their append handle.</summary>
        private static void AssignOrderedOutputSlots(IReadOnlyList<GraphPortDescriptor> ports)
        {
            foreach (IGrouping<(UUID Owner, string Field), GraphPortDescriptor> group in ports
                .Where(port => port.PresentationMode == GraphPortPresentationMode.Ordered
                    && port.AnchorKind == GraphPortAnchorKind.DistributedOutput)
                .GroupBy(port => (port.OwnerUUID, port.FieldName)))
            {
                List<GraphPortDescriptor> ordered = group
                    .OrderBy(port => port.CollectionIndex < 0 ? int.MaxValue : port.CollectionIndex)
                    .ToList();
                for (int index = 0; index < ordered.Count; index++)
                {
                    ordered[index].SetOutputSlot(index, ordered.Count);
                }
            }
        }
    }
}
