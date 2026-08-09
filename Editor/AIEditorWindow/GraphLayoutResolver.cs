using Aethiumian.AI.References;
using Aethiumian.AI.Visual;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Resolves persisted, legacy, and deterministic generated graph positions.
    /// </summary>
    internal static class GraphLayoutResolver
    {
        private const int UnreachableColumns = 4;

        /// <summary>
        /// Applies the current layout, legacy graph coordinates, or generated positions to a topology.
        /// This method only changes the in-memory snapshot.
        /// </summary>
        /// <param name="tree">The source behaviour tree.</param>
        /// <param name="topology">The topology snapshot to position.</param>
        internal static void Resolve(BehaviourTreeData tree, GraphTopology topology)
        {
            if (!tree || topology == null)
            {
                return;
            }

            Dictionary<UUID, Vector2> generated = GenerateDeterministicPositions(tree, topology);
            Dictionary<UUID, Vector2> legacy = ReadLegacyPositions(tree);
            GraphLayoutData persisted = tree.GraphLayout;

            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                if (persisted != null
                    && persisted.Version == GraphLayoutData.CurrentVersion
                    && persisted.TryGetPosition(node.UUID, out Vector2 stored))
                {
                    node.Position = stored;
                }
                else if (legacy.TryGetValue(node.UUID, out Vector2 oldPosition))
                {
                    node.Position = oldPosition;
                }
                else if (generated.TryGetValue(node.UUID, out Vector2 generatedPosition))
                {
                    node.Position = generatedPosition;
                }
                else
                {
                    node.Position = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Reads old serialized graph coordinates without using old connections as topology.
        /// </summary>
        /// <param name="tree">The source behaviour tree.</param>
        /// <returns>UUID keyed legacy positions.</returns>
        internal static Dictionary<UUID, Vector2> ReadLegacyPositions(BehaviourTreeData tree)
        {
            Dictionary<UUID, Vector2> result = new();
            if (!tree)
            {
                return result;
            }

            Graph legacyGraph = tree.LegacyGraph;
            if (legacyGraph?.graphNodes == null)
            {
                return result;
            }

            foreach (GraphNode graphNode in legacyGraph.graphNodes)
            {
                if (graphNode == null || result.ContainsKey(graphNode.uuid))
                {
                    continue;
                }

                result.Add(graphNode.uuid, graphNode.rect.position);
            }

            return result;
        }

        /// <summary>
        /// Creates the serialized layout representation for an explicit layout write.
        /// </summary>
        /// <param name="topology">The positioned topology snapshot.</param>
        /// <returns>A current-version layout containing active UUIDs only.</returns>
        internal static GraphLayoutData CreateLayout(GraphTopology topology)
        {
            if (topology == null)
            {
                return GraphLayoutData.Create(System.Array.Empty<GraphLayoutEntry>());
            }

            List<GraphLayoutEntry> entries = new(topology.Nodes.Count);
            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                entries.Add(new GraphLayoutEntry(node.UUID, node.Position));
            }

            return GraphLayoutData.Create(entries);
        }

        /// <summary>
        /// Replaces snapshot positions with deterministic generated positions.
        /// Legacy and persisted coordinates are deliberately ignored by this explicit action.
        /// </summary>
        /// <param name="tree">The source behaviour tree.</param>
        /// <param name="topology">The topology snapshot to relayout.</param>
        internal static void ApplyAutoLayout(BehaviourTreeData tree, GraphTopology topology)
        {
            if (!tree || topology == null)
            {
                return;
            }

            Dictionary<UUID, Vector2> generated = GenerateDeterministicPositions(tree, topology);
            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                if (generated.TryGetValue(node.UUID, out Vector2 position))
                {
                    node.Position = position;
                }
            }
        }

        /// <summary>
        /// Gets the native visual size used by both layout and rendering.
        /// </summary>
        /// <param name="node">The graph node.</param>
        /// <returns>The unscaled canvas size.</returns>
        internal static Vector2 GetNodeSize(GraphNodeDescriptor node)
        {
            return node.Shape switch
            {
                GraphNodeShape.Flow => GraphPresentationMetrics.FlowNodeSize,
                GraphNodeShape.Branch => GraphPresentationMetrics.BranchNodeSize,
                GraphNodeShape.Service => GraphPresentationMetrics.ServiceNodeSize,
                _ => GraphPresentationMetrics.NormalNodeSize,
            };
        }

        private static Dictionary<UUID, Vector2> GenerateDeterministicPositions(BehaviourTreeData tree, GraphTopology topology)
        {
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            Dictionary<GraphPresentationItem, LayoutVertex> itemVertices = new();
            Dictionary<GraphPresentationItem, LayoutVertex> completionVertices = new();
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                itemVertices[item] = new LayoutVertex(item, isFlowCompletion: false);
                if (item.FlowScope != null)
                {
                    completionVertices[item] = new LayoutVertex(item, isFlowCompletion: true);
                }
            }

            Dictionary<LayoutVertex, List<LayoutVertex>> children = new();
            Dictionary<LayoutVertex, List<LayoutVertex>> services = new();
            Dictionary<LayoutVertex, List<LayoutVertex>> conditionBranches = new();
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (!relation.Target.IsValid || relation.Kind == GraphPresentationRelationKind.Raw)
                {
                    continue;
                }

                LayoutVertex source = ResolveVertex(relation.Source, itemVertices, completionVertices);
                LayoutVertex target = ResolveVertex(relation.Target, itemVertices, completionVertices);
                if (source == null || target == null || source == target)
                {
                    continue;
                }

                Dictionary<LayoutVertex, List<LayoutVertex>> targetMap;
                if (relation.Kind == GraphPresentationRelationKind.Service)
                {
                    targetMap = services;
                }
                else if (relation.Kind is GraphPresentationRelationKind.ConditionTrue or GraphPresentationRelationKind.ConditionFalse
                    && source.Item.ConditionScope != null)
                {
                    targetMap = conditionBranches;
                }
                else
                {
                    targetMap = children;
                }
                if (!targetMap.TryGetValue(source, out List<LayoutVertex> list))
                {
                    list = new List<LayoutVertex>();
                    targetMap.Add(source, list);
                }

                // Repeated relations still render independently, but one presentation vertex has one position.
                if (!list.Contains(target))
                {
                    list.Add(target);
                }
            }

            // The first declaration-order path owns placement. Later parents still render their edge.
            Dictionary<LayoutVertex, List<LayoutVertex>> placementChildren = new();
            Dictionary<LayoutVertex, List<LayoutVertex>> placementServices = new();
            Dictionary<LayoutVertex, List<LayoutVertex>> placementConditionBranches = new();
            Dictionary<LayoutVertex, LayoutVertex> placementConditionCompletions = new();
            HashSet<LayoutVertex> assigned = new();
            GraphNodeDescriptor head = topology.FindNode(tree.headNodeUUID);
            GraphPresentationItem headItem = FindRootItem(presentation.Find(head?.UUID ?? UUID.Empty));
            LayoutVertex headVertex = headItem != null && itemVertices.TryGetValue(headItem, out LayoutVertex resolvedHead)
                ? resolvedHead
                : null;
            if (headVertex != null)
            {
                AssignPlacementOwnership(
                    headVertex,
                    children,
                    services,
                    conditionBranches,
                    completionVertices,
                    assigned,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementConditionCompletions);
            }

            List<LayoutVertex> unreachableRoots = new();
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                if (item.Node == null)
                {
                    continue;
                }

                LayoutVertex vertex = itemVertices[item];
                if (assigned.Contains(vertex))
                {
                    continue;
                }

                unreachableRoots.Add(vertex);
                AssignPlacementOwnership(
                    vertex,
                    children,
                    services,
                    conditionBranches,
                    completionVertices,
                    assigned,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementConditionCompletions);
            }

            float reachableBottom = 0f;
            Dictionary<LayoutVertex, Vector2> positions = new();
            Dictionary<LayoutVertex, SubtreeEnvelope> envelopes = new();
            if (headVertex != null)
            {
                MeasureSubtree(
                    headVertex,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementConditionCompletions,
                    envelopes);
                PlaceSubtree(
                    headVertex,
                    0f,
                    0f,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementConditionCompletions,
                    envelopes,
                    positions,
                    ref reachableBottom);
            }

            // Authored but unreachable nodes are deliberately separated from the executable flow.
            int unreachableIndex = 0;
            float unreachableTop = reachableBottom + 2f * GraphPresentationMetrics.LevelGap;
            float unreachableRowHeight = 0f;
            float unreachableX = 0f;
            float unreachableY = unreachableTop;
            foreach (LayoutVertex vertex in unreachableRoots)
            {
                SubtreeEnvelope envelope = MeasureSubtree(
                    vertex,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementConditionCompletions,
                    envelopes);
                if (unreachableIndex > 0 && unreachableIndex % UnreachableColumns == 0)
                {
                    unreachableX = 0f;
                    unreachableY += unreachableRowHeight + GraphPresentationMetrics.UnreachableGap;
                    unreachableRowHeight = 0f;
                }

                float subtreeBottom = unreachableY;
                PlaceSubtree(
                    vertex,
                    unreachableX,
                    unreachableY,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementConditionCompletions,
                    envelopes,
                    positions,
                    ref subtreeBottom);
                unreachableX += envelope.TotalWidth + GraphPresentationMetrics.UnreachableGap;
                unreachableRowHeight = Mathf.Max(unreachableRowHeight, subtreeBottom - unreachableY);
                unreachableIndex++;
            }

            foreach (KeyValuePair<LayoutVertex, Vector2> pair in positions)
            {
                if (!pair.Key.IsFlowCompletion)
                {
                    pair.Key.Item.Position = pair.Value;
                    if (pair.Key.Item.Node != null)
                    {
                        pair.Key.Item.Node.Position = pair.Value;
                    }
                }
            }

            GraphPresentationLayout.Layout(presentation);

            Dictionary<UUID, Vector2> result = new();
            foreach (KeyValuePair<LayoutVertex, Vector2> pair in positions)
            {
                if (!pair.Key.IsFlowCompletion && pair.Key.Item?.Node != null)
                {
                    result[pair.Key.Item.Node.UUID] = pair.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Finds illegal overlaps between visible presentation cards, placeholders, and Flow completion markers.
        /// Scope rails and brackets are excluded because they are allowed to contain their members.
        /// </summary>
        /// <param name="presentation">A positioned presentation snapshot.</param>
        /// <returns>Stable descriptions of every intersecting visible pair.</returns>
        internal static IReadOnlyList<string> FindPresentationOverlaps(GraphPresentation presentation)
        {
            List<PresentationRect> rectangles = new();
            if (presentation == null)
            {
                return Array.Empty<string>();
            }

            GraphPresentationLayout.Layout(presentation);
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                if (item.Node != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.Node.DisplayName,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.Placeholder != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.Placeholder.Title,
                        new Rect(item.Position, item.Size)));
                }
            }

            foreach (GraphFlowScope scope in presentation.CompletionScopes)
            {
                rectangles.Add(new PresentationRect(
                    $"END · {scope.Owner.Node?.DisplayName ?? "Flow"}",
                    new Rect(scope.CompletionPosition, scope.CompletionSize)));
            }

            List<string> overlaps = new();
            for (int first = 0; first < rectangles.Count; first++)
            {
                for (int second = first + 1; second < rectangles.Count; second++)
                {
                    if (OverlapsWithArea(rectangles[first].Bounds, rectangles[second].Bounds))
                    {
                        overlaps.Add($"{rectangles[first].Name} overlaps {rectangles[second].Name}");
                    }
                }
            }

            return overlaps;
        }

        /// <summary>
        /// Resolves an embedded presentation item to the top-level item that owns its canvas position.
        /// </summary>
        /// <param name="item">The item to resolve.</param>
        /// <returns>The top-level item, or null when the input is null.</returns>
        private static GraphPresentationItem FindRootItem(GraphPresentationItem item)
        {
            while (item?.Parent != null)
            {
                item = item.Parent;
            }

            return item;
        }

        private static LayoutVertex ResolveVertex(
            GraphPresentationEndpoint endpoint,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices)
        {
            GraphPresentationItem item = FindRootItem(endpoint.Item);
            if (item == null)
            {
                return null;
            }

            return endpoint.Anchor == GraphPresentationAnchorKind.FlowComplete
                ? completionVertices.TryGetValue(item, out LayoutVertex completion) ? completion : null
                : itemVertices.TryGetValue(item, out LayoutVertex vertex) ? vertex : null;
        }

        /// <summary>
        /// Assigns first-placement ownership for one reachable or unreachable presentation subtree.
        /// </summary>
        private static void AssignPlacementOwnership(
            LayoutVertex root,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices,
            ISet<LayoutVertex> assigned,
            IDictionary<LayoutVertex, List<LayoutVertex>> placementChildren,
            IDictionary<LayoutVertex, List<LayoutVertex>> placementServices,
            IDictionary<LayoutVertex, List<LayoutVertex>> placementConditionBranches,
            IDictionary<LayoutVertex, LayoutVertex> placementConditionCompletions)
        {
            Queue<LayoutVertex> queue = new();
            assigned.Add(root);
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                LayoutVertex current = queue.Dequeue();
                if (current.Item.ConditionScope != null)
                {
                    if (!placementConditionBranches.TryGetValue(current, out List<LayoutVertex> placedBranches))
                    {
                        placedBranches = new List<LayoutVertex>();
                        placementConditionBranches.Add(current, placedBranches);
                    }

                    if (conditionBranches.TryGetValue(current, out List<LayoutVertex> branchCandidates))
                    {
                        foreach (LayoutVertex candidate in branchCandidates)
                        {
                            if (!assigned.Add(candidate))
                            {
                                continue;
                            }

                            placedBranches.Add(candidate);
                            queue.Enqueue(candidate);
                        }
                    }

                    if (completionVertices.TryGetValue(current.Item, out LayoutVertex completion)
                        && assigned.Add(completion))
                    {
                        placementConditionCompletions[current] = completion;
                        queue.Enqueue(completion);
                    }
                }

                if (children.TryGetValue(current, out List<LayoutVertex> childCandidates))
                {
                    foreach (LayoutVertex candidate in childCandidates)
                    {
                        if (!assigned.Add(candidate))
                        {
                            continue;
                        }

                        if (!placementChildren.TryGetValue(current, out List<LayoutVertex> placedChildren))
                        {
                            placedChildren = new List<LayoutVertex>();
                            placementChildren.Add(current, placedChildren);
                        }

                        placedChildren.Add(candidate);
                        queue.Enqueue(candidate);
                    }
                }

                if (!services.TryGetValue(current, out List<LayoutVertex> serviceCandidates))
                {
                    continue;
                }

                foreach (LayoutVertex service in serviceCandidates)
                {
                    if (!assigned.Add(service))
                    {
                        continue;
                    }

                    if (!placementServices.TryGetValue(current, out List<LayoutVertex> placedServices))
                    {
                        placedServices = new List<LayoutVertex>();
                        placementServices.Add(current, placedServices);
                    }

                    placedServices.Add(service);
                    queue.Enqueue(service);
                }
            }
        }

        /// <summary>
        /// Measures the main flow and reserved Service lane of one owned presentation subtree.
        /// </summary>
        private static SubtreeEnvelope MeasureSubtree(
            LayoutVertex vertex,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> conditionCompletions,
            IDictionary<LayoutVertex, SubtreeEnvelope> envelopes)
        {
            if (envelopes.TryGetValue(vertex, out SubtreeEnvelope existing))
            {
                return existing;
            }

            float ownWidth = vertex.Size.x;
            float childrenWidth = 0f;
            if (children.TryGetValue(vertex, out List<LayoutVertex> childNodes))
            {
                for (int index = 0; index < childNodes.Count; index++)
                {
                    childrenWidth += MeasureSubtree(
                        childNodes[index],
                        children,
                        services,
                        conditionBranches,
                        conditionCompletions,
                        envelopes).TotalWidth;
                    if (index > 0)
                    {
                        childrenWidth += GraphPresentationMetrics.SiblingGap;
                    }
                }
            }

            if (conditionBranches.TryGetValue(vertex, out List<LayoutVertex> branchNodes))
            {
                float branchesWidth = 0f;
                for (int index = 0; index < branchNodes.Count; index++)
                {
                    branchesWidth += MeasureSubtree(
                        branchNodes[index],
                        children,
                        services,
                        conditionBranches,
                        conditionCompletions,
                        envelopes).TotalWidth;
                    if (index > 0)
                    {
                        branchesWidth += GraphPresentationMetrics.SiblingGap;
                    }
                }

                childrenWidth = Mathf.Max(childrenWidth, branchesWidth);
            }

            if (conditionCompletions.TryGetValue(vertex, out LayoutVertex completionVertex))
            {
                childrenWidth = Mathf.Max(
                    childrenWidth,
                    MeasureSubtree(
                        completionVertex,
                        children,
                        services,
                        conditionBranches,
                        conditionCompletions,
                        envelopes).TotalWidth);
            }

            float mainWidth = Mathf.Max(ownWidth, childrenWidth);
            float serviceWidth = 0f;
            if (services.TryGetValue(vertex, out List<LayoutVertex> serviceNodes))
            {
                foreach (LayoutVertex service in serviceNodes)
                {
                    serviceWidth = Mathf.Max(
                        serviceWidth,
                        MeasureSubtree(
                            service,
                            children,
                            services,
                            conditionBranches,
                            conditionCompletions,
                            envelopes).TotalWidth);
                }
            }

            SubtreeEnvelope envelope = new(mainWidth, serviceWidth);
            envelopes[vertex] = envelope;
            return envelope;
        }

        /// <summary>
        /// Places one measured subtree while keeping its reserved Service lane outside main flow lanes.
        /// </summary>
        private static void PlaceSubtree(
            LayoutVertex vertex,
            float left,
            float top,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> conditionCompletions,
            IReadOnlyDictionary<LayoutVertex, SubtreeEnvelope> envelopes,
            IDictionary<LayoutVertex, Vector2> positions,
            ref float bottom)
        {
            Vector2 size = vertex.Size;
            SubtreeEnvelope envelope = envelopes[vertex];
            positions[vertex] = new Vector2(left + (envelope.MainWidth - size.x) * 0.5f, top);
            bottom = Mathf.Max(bottom, top + size.y);

            if (conditionBranches.TryGetValue(vertex, out List<LayoutVertex> branchNodes)
                && conditionCompletions.TryGetValue(vertex, out LayoutVertex completionVertex))
            {
                float branchesWidth = 0f;
                foreach (LayoutVertex branch in branchNodes)
                {
                    branchesWidth += envelopes[branch].TotalWidth;
                }

                branchesWidth += GraphPresentationMetrics.SiblingGap * Mathf.Max(0, branchNodes.Count - 1);
                float branchLeft = left + (envelope.MainWidth - branchesWidth) * 0.5f;
                float branchTop = top + size.y + GraphPresentationMetrics.LevelGap;
                float branchesBottom = branchTop;
                foreach (LayoutVertex branch in branchNodes)
                {
                    float branchBottom = branchTop;
                    PlaceSubtree(
                        branch,
                        branchLeft,
                        branchTop,
                        children,
                        services,
                        conditionBranches,
                        conditionCompletions,
                        envelopes,
                        positions,
                        ref branchBottom);
                    branchesBottom = Mathf.Max(branchesBottom, branchBottom);
                    branchLeft += envelopes[branch].TotalWidth + GraphPresentationMetrics.SiblingGap;
                }

                float completionLeft = left + (envelope.MainWidth - envelopes[completionVertex].TotalWidth) * 0.5f;
                PlaceSubtree(
                    completionVertex,
                    completionLeft,
                    branchesBottom + GraphPresentationMetrics.LevelGap,
                    children,
                    services,
                    conditionBranches,
                    conditionCompletions,
                    envelopes,
                    positions,
                    ref bottom);
            }
            else if (children.TryGetValue(vertex, out List<LayoutVertex> childNodes) && childNodes.Count > 0)
            {
                float childrenWidth = 0f;
                foreach (LayoutVertex child in childNodes)
                {
                    childrenWidth += envelopes[child].TotalWidth;
                }

                childrenWidth += GraphPresentationMetrics.SiblingGap * (childNodes.Count - 1);
                float childLeft = left + (envelope.MainWidth - childrenWidth) * 0.5f;
                float childTop = top + size.y + GraphPresentationMetrics.LevelGap;
                foreach (LayoutVertex child in childNodes)
                {
                    PlaceSubtree(
                        child,
                        childLeft,
                        childTop,
                        children,
                        services,
                        conditionBranches,
                        conditionCompletions,
                        envelopes,
                        positions,
                        ref bottom);
                    childLeft += envelopes[child].TotalWidth + GraphPresentationMetrics.SiblingGap;
                }
            }

            if (!services.TryGetValue(vertex, out List<LayoutVertex> serviceNodes) || serviceNodes.Count == 0)
            {
                return;
            }

            float serviceLeft = left + envelope.MainWidth + GraphPresentationMetrics.ServiceGap;
            float serviceTop = top;
            foreach (LayoutVertex service in serviceNodes)
            {
                float serviceBottom = serviceTop;
                PlaceSubtree(
                    service,
                    serviceLeft,
                    serviceTop,
                    children,
                    services,
                    conditionBranches,
                    conditionCompletions,
                    envelopes,
                    positions,
                    ref serviceBottom);
                bottom = Mathf.Max(bottom, serviceBottom);
                serviceTop = serviceBottom + GraphPresentationMetrics.ServiceGap;
            }
        }

        /// <summary>Returns true only when two rectangles overlap with positive area.</summary>
        private static bool OverlapsWithArea(Rect first, Rect second)
        {
            return first.xMin < second.xMax
                && first.xMax > second.xMin
                && first.yMin < second.yMax
                && first.yMax > second.yMin;
        }

        /// <summary>One visible rectangle used by the read-only collision audit.</summary>
        private readonly struct PresentationRect
        {
            internal PresentationRect(string name, Rect bounds)
            {
                Name = name;
                Bounds = bounds;
            }

            internal string Name { get; }
            internal Rect Bounds { get; }
        }

        /// <summary>Measured horizontal ownership for a main subtree and its auxiliary Service lane.</summary>
        private sealed class SubtreeEnvelope
        {
            internal SubtreeEnvelope(float mainWidth, float serviceWidth)
            {
                MainWidth = mainWidth;
                ServiceWidth = serviceWidth;
            }

            internal float MainWidth { get; }
            internal float ServiceWidth { get; }
            internal float TotalWidth => MainWidth
                + (ServiceWidth > 0f ? GraphPresentationMetrics.ServiceGap + ServiceWidth : 0f);
        }

        /// <summary>
        /// One real or presentation-only vertex used by deterministic layout.
        /// </summary>
        private sealed class LayoutVertex
        {
            internal LayoutVertex(GraphPresentationItem item, bool isFlowCompletion)
            {
                Item = item ?? throw new ArgumentNullException(nameof(item));
                IsFlowCompletion = isFlowCompletion;
            }

            internal GraphPresentationItem Item { get; }
            internal bool IsFlowCompletion { get; }
            internal Vector2 Size => IsFlowCompletion ? Item.FlowScope.CompletionSize : Item.Size;
        }
    }
}
