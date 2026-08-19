using Aethiumian.AI.References;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Visual;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using BooleanNode = Aethiumian.AI.Nodes.Boolean;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Resolves persisted or deterministic generated graph positions.
    /// </summary>
    internal static class GraphLayoutResolver
    {
        /// <summary>
        /// Applies the current layout or generated positions to a topology.
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
            GraphLayoutData persisted = tree.GraphLayout;

            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                if (persisted != null
                    && persisted.HasSupportedPositions
                    && persisted.TryGetPosition(node.UUID, out Vector2 stored))
                {
                    node.Position = stored;
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
        /// <summary>
        /// Creates the serialized layout representation for an explicit layout write.
        /// </summary>
        /// <param name="topology">The positioned topology snapshot.</param>
        /// <returns>A current-version layout containing active UUIDs only.</returns>
        internal static GraphLayoutData CreateLayout(
            GraphTopology topology,
            GraphLayoutData previous = null,
            IReadOnlyDictionary<UUID, bool> followOverrides = null,
            Vector2? entrancePosition = null,
            Vector2? exitPosition = null)
        {
            if (topology == null)
            {
                return GraphLayoutData.Create(System.Array.Empty<GraphLayoutEntry>(), entrancePosition: entrancePosition, exitPosition: exitPosition);
            }

            List<GraphLayoutEntry> entries = new(topology.Nodes.Count);
            List<GraphServiceLayoutEntry> services = new();
            HashSet<UUID> active = topology.Nodes.Select(node => node.UUID).ToHashSet();
            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                entries.Add(new GraphLayoutEntry(node.UUID, node.Position));
                if (node.Node is Service)
                {
                    bool followParent = followOverrides != null && followOverrides.TryGetValue(node.UUID, out bool value)
                        ? value
                        : previous?.GetServiceFollowParent(node.UUID) ?? true;
                    services.Add(new GraphServiceLayoutEntry(node.UUID, followParent));
                }
            }

            Vector2? resolvedEntrance = entrancePosition
                ?? (previous?.HasEntrancePosition == true ? previous.EntrancePosition : null);
            Vector2? resolvedExit = exitPosition
                ?? (previous?.HasExitPosition == true ? previous.ExitPosition : null);
            IEnumerable<GraphGroupLayoutEntry> groups = previous?.Groups
                .Select(group => new GraphGroupLayoutEntry(group.UUID, group.Title, group.Color,
                    group.Members.Where(active.Contains)))
                .Where(group => group.Members.Count > 0);
            return GraphLayoutData.Create(entries, services, resolvedEntrance, resolvedExit, groups);
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
            if (node.Node is Decorator)
            {
                return GraphPresentationMetrics.DecoratorNodeSize;
            }

            if (node.Node is BooleanNode)
            {
                return GraphPresentationMetrics.BooleanNodeSize;
            }

            if (node.Node is Constant)
            {
                return GraphPresentationMetrics.ConstantNodeSize;
            }

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
            HashSet<LayoutVertex> structuralIncoming = new();
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (!relation.Target.IsValid || relation.Kind == GraphPresentationRelationKind.Raw)
                {
                    continue;
                }

                // Contextual return hints explain execution but never own spatial placement.
                if (relation.ContextualOwner != null)
                {
                    continue;
                }

                LayoutVertex source = ResolveVertex(relation.Source, itemVertices, completionVertices);
                LayoutVertex target = ResolveVertex(relation.Target, itemVertices, completionVertices);
                if (source == null || target == null || source == target)
                {
                    continue;
                }

                if (relation.Role == GraphPresentationRelationRole.AuthoredReference
                    && !target.IsFlowCompletion)
                {
                    structuralIncoming.Add(target);
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
                else if (relation.Kind == GraphPresentationRelationKind.ProbabilityBranch
                    && source.Item.ProbabilityScope != null)
                {
                    targetMap = conditionBranches;
                }
                else if (relation.Kind == GraphPresentationRelationKind.DecisionBranch
                    && source.Item.DecisionScope != null)
                {
                    targetMap = conditionBranches;
                }
                else if (relation.Kind == GraphPresentationRelationKind.ParallelBranch
                    && source.Item.ParallelScope != null)
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
            Dictionary<LayoutVertex, LayoutVertex> placementFlowCompletions = new();
            HashSet<LayoutVertex> assigned = new();
            GraphNodeDescriptor head = topology.FindNode(tree.headNodeUUID);
            GraphPresentationItem headItem = FindRootItem(presentation.Find(head?.UUID ?? UUID.Empty));
            LayoutVertex headVertex = headItem != null && itemVertices.TryGetValue(headItem, out LayoutVertex resolvedHead)
                ? resolvedHead
                : null;
            bool headOwnsPlacement = headVertex != null && !structuralIncoming.Contains(headVertex);
            if (headOwnsPlacement)
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
                    placementFlowCompletions);
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

                if (structuralIncoming.Contains(vertex))
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
                    placementFlowCompletions);
            }

            // Cycles have no structural root. Keep them editable by assigning their first
            // declaration-order vertex after every complete component has been claimed.
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
                    placementFlowCompletions);
            }

            float reachableBottom = 0f;
            Dictionary<LayoutVertex, Vector2> positions = new();
            Dictionary<LayoutVertex, SubtreeEnvelope> envelopes = new();
            if (headOwnsPlacement)
            {
                MeasureSubtree(
                    headVertex,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementFlowCompletions,
                    envelopes);
                PlaceSubtree(
                    headVertex,
                    0f,
                    0f,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementFlowCompletions,
                    envelopes,
                    positions,
                    ref reachableBottom);
            }

            // Disconnected roots receive stable initial positions below the executable flow. This is
            // an Auto Layout default only; it is not a persistent grouping or an editing constraint.
            float unreachableTop = reachableBottom + 2f * GraphPresentationMetrics.LevelGap;
            // Use the executable envelope when available, but retain room for two ordinary cards.
            // Each disconnected subtree can enlarge the row instead of being forced into a column count.
            float unreachableRowWidth = Mathf.Max(
                headVertex != null && envelopes.TryGetValue(headVertex, out SubtreeEnvelope headEnvelope)
                    ? headEnvelope.TotalWidth
                    : 0f,
                2f * GraphPresentationMetrics.NormalNodeSize.x + GraphPresentationMetrics.UnreachableGap);
            foreach (LayoutVertex vertex in unreachableRoots)
            {
                unreachableRowWidth = Mathf.Max(
                    unreachableRowWidth,
                    MeasureSubtree(
                        vertex,
                        placementChildren,
                        placementServices,
                        placementConditionBranches,
                        placementFlowCompletions,
                        envelopes).TotalWidth);
            }

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
                    placementFlowCompletions,
                    envelopes);
                if (unreachableX > 0f
                    && unreachableX + envelope.TotalWidth > unreachableRowWidth)
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
                    placementFlowCompletions,
                    envelopes,
                    positions,
                    ref subtreeBottom);
                unreachableX += envelope.TotalWidth + GraphPresentationMetrics.UnreachableGap;
                unreachableRowHeight = Mathf.Max(unreachableRowHeight, subtreeBottom - unreachableY);
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
            AlignLoopContinuationSubtrees(
                presentation,
                completionVertices,
                placementChildren,
                placementServices,
                placementConditionBranches,
                placementFlowCompletions,
                positions);
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
        /// Aligns persisted continuation subtrees with the final derived Loop completion geometry.
        /// </summary>
        private static void AlignLoopContinuationSubtrees(
            GraphPresentation presentation,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
            IDictionary<LayoutVertex, Vector2> positions)
        {
            List<(LayoutVertex Completion, Vector2 Delta)> adjustments = new();
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                if (item.LoopScope == null
                    || !completionVertices.TryGetValue(item, out LayoutVertex completion)
                    || !positions.TryGetValue(completion, out Vector2 layoutPosition))
                {
                    continue;
                }

                Vector2 delta = item.LoopScope.CompletionPosition - layoutPosition;
                if (delta.sqrMagnitude > Mathf.Epsilon)
                {
                    adjustments.Add((completion, delta));
                }
            }

            foreach ((LayoutVertex completion, Vector2 delta) in adjustments)
            {
                ShiftPlacementSubtree(
                    completion,
                    delta,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    positions);
            }
        }

        /// <summary>Moves one owned placement subtree without changing presentation topology.</summary>
        private static void ShiftPlacementSubtree(
            LayoutVertex vertex,
            Vector2 delta,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
            IDictionary<LayoutVertex, Vector2> positions)
        {
            Stack<LayoutVertex> pending = new();
            HashSet<LayoutVertex> visited = new();
            pending.Push(vertex);
            while (pending.Count > 0)
            {
                LayoutVertex current = pending.Pop();
                if (!visited.Add(current))
                {
                    continue;
                }

                if (positions.TryGetValue(current, out Vector2 position))
                {
                    Vector2 shifted = position + delta;
                    positions[current] = shifted;
                    if (!current.IsFlowCompletion)
                    {
                        current.Item.Position = shifted;
                        if (current.Item.Node != null)
                        {
                            current.Item.Node.Position = shifted;
                        }
                    }
                }

                PushPlacementTargets(current, children, pending);
                PushPlacementTargets(current, services, pending);
                PushPlacementTargets(current, conditionBranches, pending);
                if (flowCompletions.TryGetValue(current, out LayoutVertex completion))
                {
                    pending.Push(completion);
                }
            }
        }

        /// <summary>Pushes one list-valued placement relation category onto the traversal stack.</summary>
        private static void PushPlacementTargets(
            LayoutVertex vertex,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> relation,
            Stack<LayoutVertex> pending)
        {
            if (!relation.TryGetValue(vertex, out List<LayoutVertex> targets))
            {
                return;
            }

            foreach (LayoutVertex target in targets)
            {
                pending.Push(target);
            }
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
                else if (item.LoopPlaceholder != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.LoopPlaceholder.Title,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.LoopJunction != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.LoopJunction.Title,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.ProbabilityPlaceholder != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.ProbabilityPlaceholder.Title,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.DecisionPlaceholder != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.DecisionPlaceholder.Title,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.ServicePlaceholder != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.ServicePlaceholder.Title,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.ParallelPlaceholder != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.ParallelPlaceholder.Title,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.ForEachPlaceholder != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.ForEachPlaceholder.Title,
                        new Rect(item.Position, item.Size)));
                }
                else if (item.ForEachJunction != null)
                {
                    rectangles.Add(new PresentationRect(
                        item.ForEachJunction.Title,
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
            IDictionary<LayoutVertex, LayoutVertex> placementFlowCompletions)
        {
            Queue<LayoutVertex> queue = new();
            assigned.Add(root);
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                LayoutVertex current = queue.Dequeue();
                if (current.Item.FlowScope is GraphConditionScope or GraphProbabilityScope or GraphDecisionScope or GraphParallelScope)
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

                }

                // Structured Flow owners place completion after their complete derived structure.
                if (current.Item.FlowScope is GraphConditionScope or GraphLoopScope or GraphProbabilityScope or GraphDecisionScope or GraphParallelScope or GraphForEachScope
                    && completionVertices.TryGetValue(current.Item, out LayoutVertex completion)
                    && assigned.Add(completion))
                {
                    placementFlowCompletions[current] = completion;
                    queue.Enqueue(completion);
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
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
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
                        flowCompletions,
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
                        flowCompletions,
                        envelopes).TotalWidth;
                    if (index > 0)
                    {
                        branchesWidth += GraphPresentationMetrics.SiblingGap;
                    }
                }

                childrenWidth = Mathf.Max(childrenWidth, branchesWidth);
            }

            if (flowCompletions.TryGetValue(vertex, out LayoutVertex completionVertex))
            {
                childrenWidth = Mathf.Max(
                    childrenWidth,
                    MeasureSubtree(
                        completionVertex,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
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
                            flowCompletions,
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
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
            IReadOnlyDictionary<LayoutVertex, SubtreeEnvelope> envelopes,
            IDictionary<LayoutVertex, Vector2> positions,
            ref float bottom)
        {
            // Keep the common deep linear chain off the managed call stack. Complex
            // branch/scope layouts retain the established recursive semantics below.
            if (IsPlainLinearChain(vertex, children, services, conditionBranches, flowCompletions))
            {
                LayoutVertex current = vertex;
                float currentLeft = left;
                float currentTop = top;
                while (current != null)
                {
                    SubtreeEnvelope currentEnvelope = envelopes[current];
                    Vector2 currentSize = current.Size;
                    positions[current] = new Vector2(
                        currentLeft + (currentEnvelope.MainWidth - currentSize.x) * 0.5f,
                        currentTop);
                    bottom = Mathf.Max(bottom, currentTop + currentSize.y);

                    if (!children.TryGetValue(current, out List<LayoutVertex> next) || next.Count == 0)
                    {
                        break;
                    }

                    LayoutVertex child = next[0];
                    float childWidth = envelopes[child].TotalWidth;
                    currentLeft += (currentEnvelope.MainWidth - childWidth) * 0.5f;
                    currentTop += currentSize.y + GraphPresentationMetrics.LevelGap;
                    current = child;
                }

                return;
            }

            PlaceSubtreeRecursive(
                vertex,
                left,
                top,
                children,
                services,
                conditionBranches,
                flowCompletions,
                envelopes,
                positions,
                ref bottom);
        }

        /// <summary>Returns true when a subtree is a single child-only chain without auxiliary layout semantics.</summary>
        private static bool IsPlainLinearChain(
            LayoutVertex vertex,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions)
        {
            LayoutVertex current = vertex;
            HashSet<LayoutVertex> visited = new();
            while (current != null && visited.Add(current))
            {
                if (services.ContainsKey(current)
                    || conditionBranches.ContainsKey(current)
                    || flowCompletions.ContainsKey(current)
                    || current.Item.LoopScope != null
                    || current.Item.ForEachScope != null)
                {
                    return false;
                }

                if (!children.TryGetValue(current, out List<LayoutVertex> next) || next.Count == 0)
                {
                    return true;
                }

                if (next.Count != 1)
                {
                    return false;
                }

                current = next[0];
            }

            return false;
        }

        private static void PlaceSubtreeRecursive(
            LayoutVertex vertex,
            float left,
            float top,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
            IReadOnlyDictionary<LayoutVertex, SubtreeEnvelope> envelopes,
            IDictionary<LayoutVertex, Vector2> positions,
            ref float bottom)
        {
            Vector2 size = vertex.Size;
            SubtreeEnvelope envelope = envelopes[vertex];
            positions[vertex] = new Vector2(left + (envelope.MainWidth - size.x) * 0.5f, top);
            bottom = Mathf.Max(bottom, top + size.y);

            if (conditionBranches.TryGetValue(vertex, out List<LayoutVertex> branchNodes)
                && flowCompletions.TryGetValue(vertex, out LayoutVertex completionVertex))
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
                        flowCompletions,
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
                    flowCompletions,
                    envelopes,
                    positions,
                    ref bottom);
            }
            else if ((vertex.Item.LoopScope != null || vertex.Item.ForEachScope != null)
                && flowCompletions.TryGetValue(vertex, out LayoutVertex loopCompletionVertex))
            {
                float structureBottom = top + size.y;
                if (children.TryGetValue(vertex, out List<LayoutVertex> loopChildren) && loopChildren.Count > 0)
                {
                    float childrenWidth = 0f;
                    foreach (LayoutVertex child in loopChildren)
                    {
                        childrenWidth += envelopes[child].TotalWidth;
                    }

                    childrenWidth += GraphPresentationMetrics.SiblingGap * (loopChildren.Count - 1);
                    float childLeft = left + (envelope.MainWidth - childrenWidth) * 0.5f;
                    float childTop = top + size.y + GraphPresentationMetrics.LevelGap;
                    foreach (LayoutVertex child in loopChildren)
                    {
                        PlaceSubtree(
                            child,
                            childLeft,
                            childTop,
                            children,
                            services,
                            conditionBranches,
                            flowCompletions,
                            envelopes,
                            positions,
                            ref structureBottom);
                        childLeft += envelopes[child].TotalWidth + GraphPresentationMetrics.SiblingGap;
                    }
                }

                float completionLeft = left + (envelope.MainWidth - envelopes[loopCompletionVertex].TotalWidth) * 0.5f;
                PlaceSubtree(
                    loopCompletionVertex,
                    completionLeft,
                    structureBottom + GraphPresentationMetrics.FlowCompletionGap,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
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
                        flowCompletions,
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
                    flowCompletions,
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
