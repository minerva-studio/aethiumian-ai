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
        Wrap,
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
        DecoratorChild,
        DistributedOutput,
        ChainedOutput,
        ConditionPredicate,
        ConditionTrue,
        ConditionFalse,
        DecisionPrepend,
        DecisionOption,
        DecisionAppend,
    }

    /// <summary>One canvas-only handle for an authored reference slot or shared collection field.</summary>
    internal sealed class GraphPortDescriptor
    {
        private GraphPortDescriptor(
            NodeReferenceAddress address,
            GraphPortOperation operation,
            GraphPortPresentationMode presentationMode,
            GraphPresentationEndpoint source,
            GraphPresentationRelation relation,
            IReadOnlyList<GraphEdgeDescriptor> origins,
            bool isRaw,
            GraphPortAnchorKind anchorKind)
        {
            Address = address;
            Operation = operation;
            PresentationMode = presentationMode;
            Source = source;
            Relation = relation;
            Origins = origins ?? Array.Empty<GraphEdgeDescriptor>();
            IsRaw = isRaw;
            AnchorKind = anchorKind;
        }

        /// <summary>Creates a scalar or indexed authored port.</summary>
        internal static GraphPortDescriptor ForSlot(
            NodeReferenceAddress address,
            GraphPortOperation operation,
            GraphPortPresentationMode presentationMode,
            GraphPresentationEndpoint source,
            GraphPresentationRelation relation,
            bool isRaw,
            GraphPortAnchorKind anchorKind)
        {
            GraphEdgeDescriptor edge = relation?.AuthoredEdge;
            return new GraphPortDescriptor(
                address,
                operation,
                presentationMode,
                source,
                relation,
                edge == null ? Array.Empty<GraphEdgeDescriptor>() : new[] { edge },
                isRaw,
                anchorKind);
        }

        /// <summary>Creates one shared collection insertion port.</summary>
        internal static GraphPortDescriptor ForSharedCollection(
            NodeReferenceAddress address,
            GraphPresentationEndpoint source,
            IReadOnlyList<GraphEdgeDescriptor> origins,
            bool isRaw,
            GraphPortAnchorKind anchorKind) => new(
                address,
                GraphPortOperation.Insert,
                GraphPortPresentationMode.Shared,
                source,
                null,
                origins,
                isRaw,
                anchorKind);

        internal NodeReferenceAddress Address { get; }
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
        internal bool IsDecisionOption => AnchorKind == GraphPortAnchorKind.DecisionOption;

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
            return topology == null || presentation == null
                ? Array.Empty<GraphPortDescriptor>()
                : new GraphPortCollector(topology, presentation, includeRawReferences).Collect();
        }

        /// <summary>Collects all canvas ports for one topology and presentation snapshot.</summary>
        private sealed class GraphPortCollector
        {
            private readonly GraphTopology topology;
            private readonly GraphPresentation presentation;
            private readonly bool includeRawReferences;
            private readonly IReadOnlyDictionary<GraphEdgeDescriptor, GraphPresentationRelation> relations;
            private readonly List<GraphPortDescriptor> ports = new();

            /// <summary>Creates a snapshot-scoped port collector.</summary>
            internal GraphPortCollector(
                GraphTopology topology,
                GraphPresentation presentation,
                bool includeRawReferences)
            {
                this.topology = topology;
                this.presentation = presentation;
                this.includeRawReferences = includeRawReferences;
                relations = presentation.Relations
                    .Where(relation => relation.AuthoredEdge != null)
                    .GroupBy(relation => relation.AuthoredEdge)
                    .ToDictionary(group => group.Key, group => group.First());
            }

            /// <summary>Builds every authored port and assigns ordered output slots.</summary>
            internal IReadOnlyList<GraphPortDescriptor> Collect()
            {
                foreach (GraphNodeDescriptor node in topology.Nodes)
                {
                    GraphPresentationItem item = presentation.Find(node.UUID);
                    if (item != null)
                    {
                        AppendPorts(node, item);
                    }
                }

                AssignOrderedOutputSlots(ports);
                return ports;
            }

            /// <summary>Adds scalar and collection ports for one authored node.</summary>
            private void AppendPorts(GraphNodeDescriptor node, GraphPresentationItem item)
            {
                foreach (INodeReferenceSlot slot in NodeReferenceStructureProvider.GetSlots(node.Node)
                    .Where(candidate => candidate is INodeReferenceSingleSlot))
                {
                    if (slot.Name == nameof(TreeNode.parent)
                        || node.Node is Loop loop
                        && loop.loopType == Loop.LoopType.@for
                        && slot.Name == nameof(Loop.condition))
                    {
                        continue;
                    }

                    INodeReference reference = ((INodeReferenceSingleSlot)slot).GetReference();
                    bool isRaw = reference?.IsRawReference == true;
                    if (isRaw && !includeRawReferences)
                    {
                        continue;
                    }

                    GraphEdgeDescriptor edge = FindEdge(node.UUID, slot.Name, -1);
                    ports.Add(CreatePort(
                        new NodeReferenceAddress(node.UUID, slot.Name, -1),
                        (edge == null || !edge.Reference.HasRemovableValue)
                            && node.Node is Decorator && slot.Name == nameof(Decorator.node)
                            ? GraphPortOperation.Wrap
                            : edge == null || !edge.Reference.HasRemovableValue
                                ? GraphPortOperation.Connect
                                : GraphPortOperation.Replace,
                        GraphPortPresentationMode.Single,
                        item,
                        edge,
                        isRaw,
                        GetSingleAnchorKind(node.Node, slot.Name, isRaw)));
                }

                foreach (INodeReferenceListSlot field in NodeReferenceStructureProvider.GetListSlots(node.Node))
                {
                    if (node.Node is Aethiumian.AI.Nodes.Boolean or Constant
                        || field.Name == nameof(ServiceHostNode.services) && !node.Node.CanEditServices())
                    {
                        continue;
                    }

                    bool isRaw = field.Count > 0 && field.GetReference(0)?.IsRawReference == true;
                    if (isRaw && !includeRawReferences)
                    {
                        continue;
                    }

                    GraphPortPresentationMode mode = GetCollectionPresentationMode(node.Node, field.Name);
                    List<GraphEdgeDescriptor> fieldEdges = topology.Edges
                        .Where(edge => edge.Source.UUID == node.UUID
                            && edge.Reference.Address.FieldName == field.Name)
                        .OrderBy(edge => edge.Reference.Address.Index)
                        .ToList();
                    if (mode == GraphPortPresentationMode.Shared)
                    {
                        ports.Add(GraphPortDescriptor.ForSharedCollection(
                            new NodeReferenceAddress(node.UUID, field.Name, -1),
                            new GraphPresentationEndpoint(item, GraphPresentationAnchorKind.Output),
                            fieldEdges,
                            isRaw,
                            GetAnchorKind(field.Name, isRaw, mode)));
                        continue;
                    }

                    int count = field.Count;
                    bool isDecisionEvents = node.Node is Decision && field.Name == nameof(Decision.events);
                    if (isDecisionEvents)
                    {
                        ports.Add(CreatePort(
                            new NodeReferenceAddress(node.UUID, field.Name, 0),
                            GraphPortOperation.Insert,
                            mode,
                            item,
                            null,
                            isRaw,
                            GraphPortAnchorKind.DecisionPrepend));
                    }

                    for (int index = 0; index < count; index++)
                    {
                        GraphEdgeDescriptor edge = fieldEdges.FirstOrDefault(
                            candidate => candidate.Reference.Address.Index == index);
                        GraphPortAnchorKind anchorKind = isDecisionEvents
                            ? GraphPortAnchorKind.DecisionOption
                            : GetCollectionAnchorKind(node.Node, field.Name, isRaw, mode);
                        ports.Add(CreatePort(
                            new NodeReferenceAddress(node.UUID, field.Name, index),
                            GraphPortOperation.Replace,
                            mode,
                            item,
                            edge,
                            isRaw,
                            anchorKind));
                    }

                    GraphPresentationEndpoint appendSource = GetCollectionAppendSource(
                        item,
                        field.Name,
                        mode,
                        presentation.Relations);
                    ports.Add(CreatePort(
                        new NodeReferenceAddress(node.UUID, field.Name, -1),
                        GraphPortOperation.Insert,
                        mode,
                        item,
                        null,
                        isRaw,
                        isDecisionEvents
                            ? GraphPortAnchorKind.DecisionAppend
                            : GetCollectionAnchorKind(node.Node, field.Name, isRaw, mode),
                        appendSource));
                }
            }

            /// <summary>Creates one port using the collector's relation lookup and presentation.</summary>
            private GraphPortDescriptor CreatePort(
                NodeReferenceAddress address,
                GraphPortOperation operation,
                GraphPortPresentationMode mode,
                GraphPresentationItem item,
                GraphEdgeDescriptor edge,
                bool isRaw,
                GraphPortAnchorKind anchorKind,
                GraphPresentationEndpoint? sourceOverride = null)
            {
                GraphPresentationRelation relation = edge != null
                    && relations.TryGetValue(edge, out GraphPresentationRelation found)
                    ? found
                    : null;
                GraphPresentationEndpoint relationSource = relation != null
                    ? presentation.ResolveContinuationSource(relation)
                    : new GraphPresentationEndpoint(item, GraphPresentationAnchorKind.Output);
                return GraphPortDescriptor.ForSlot(
                    address,
                    operation,
                    mode,
                    sourceOverride ?? relationSource,
                    relation,
                    isRaw,
                    anchorKind);
            }

            /// <summary>Finds one exact topology edge for the collector's snapshot.</summary>
            private GraphEdgeDescriptor FindEdge(UUID ownerUUID, string fieldName, int index)
            {
                return topology.Edges.FirstOrDefault(edge => edge.Source.UUID == ownerUUID
                    && edge.Reference.Address.FieldName == fieldName
                    && edge.Reference.Address.Index == index);
            }
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
            if (node is Decorator && fieldName == nameof(Decorator.node))
            {
                return GraphPortAnchorKind.DecoratorChild;
            }

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
                .GroupBy(port => (port.Address.OwnerUUID, port.Address.FieldName)))
            {
                List<GraphPortDescriptor> ordered = group
                    .OrderBy(port => port.Address.Index < 0 ? int.MaxValue : port.Address.Index)
                    .ToList();
                for (int index = 0; index < ordered.Count; index++)
                {
                    ordered[index].SetOutputSlot(index, ordered.Count);
                }
            }
        }
    }
}
