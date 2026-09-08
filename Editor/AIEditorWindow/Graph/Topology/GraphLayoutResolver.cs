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
            if (node.Node is Decision decision)
            {
                return GraphPresentationMetrics.GetDecisionNodeSize(decision);
            }

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
            GraphPresentationLayout.Layout(presentation, arrangeFreeNodes: true);

            Dictionary<GraphPresentationItem, LayoutVertex> itemVertices = new();
            Dictionary<GraphPresentationItem, LayoutVertex> completionVertices = new();
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                GraphDecoratorStack decoratorStack = item.DecoratorStack;
                GraphPresentationItem layoutItem = decoratorStack?.Anchor ?? item;
                if (!itemVertices.TryGetValue(layoutItem, out LayoutVertex layoutVertex))
                {
                    layoutVertex = new LayoutVertex(layoutItem, isFlowCompletion: false, decoratorStack);
                    itemVertices.Add(layoutItem, layoutVertex);
                }

                itemVertices[item] = layoutVertex;
                if (decoratorStack != null)
                {
                    foreach (GraphPresentationItem badge in decoratorStack.Badges)
                    {
                        itemVertices[badge] = layoutVertex;
                    }

                    itemVertices[decoratorStack.Anchor] = layoutVertex;
                }

                if (item.FlowScope != null)
                {
                    completionVertices[item] = new LayoutVertex(item, isFlowCompletion: true);
                }
            }

            Dictionary<LayoutVertex, List<LayoutVertex>> children = new();
            Dictionary<LayoutVertex, List<LayoutVertex>> services = new();
            Dictionary<LayoutVertex, List<LayoutVertex>> conditionBranches = new();
            Dictionary<LayoutVertex, LayoutVertex> loopConditions = new();
            Dictionary<LayoutVertex, LayoutVertex> flowCompletions = new();
            HashSet<LayoutVertex> structuralIncoming = new();
            AddScopePlacementCandidates(
                presentation,
                itemVertices,
                completionVertices,
                children,
                conditionBranches,
                loopConditions,
                flowCompletions);
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (!relation.Target.IsValid || relation.Kind == GraphPresentationRelationKind.Raw)
                {
                    continue;
                }

                // Sequence failure exits share a visual rail but do not participate in authored placement.
                if (relation.Kind == GraphPresentationRelationKind.SequenceFailure)
                {
                    continue;
                }

                // Contextual return hints explain execution but never own spatial placement.
                if (relation.ContextualOwner != null)
                {
                    continue;
                }

                // Derived rails and completion marks describe execution only. Their geometry is
                // owned by the Flow scope and must never claim a free card placement slot.
                if (relation.Role is GraphPresentationRelationRole.DerivedCompletion
                    or GraphPresentationRelationRole.DerivedControl)
                {
                    continue;
                }

                GraphPresentationEndpoint sourceEndpoint = ResolveDecoratorContinuationEndpoint(
                    presentation, relation);
                LayoutVertex source = ResolveVertex(sourceEndpoint, itemVertices, completionVertices);
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
                    flowCompletions,
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
                    flowCompletions,
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
                    flowCompletions,
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
                    envelopes[headVertex].PlaceholderLeftExtent,
                    0f,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementFlowCompletions,
                    loopConditions,
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
                    unreachableX + envelope.PlaceholderLeftExtent,
                    unreachableY,
                    placementChildren,
                    placementServices,
                    placementConditionBranches,
                    placementFlowCompletions,
                    loopConditions,
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
                    pair.Key.ApplyLayoutPosition(pair.Value);
                }
            }

            GraphPresentationLayout.Layout(presentation);

            Dictionary<UUID, Vector2> result = new();
            foreach (KeyValuePair<LayoutVertex, Vector2> pair in positions)
            {
                if (!pair.Key.IsFlowCompletion)
                {
                    pair.Key.AddGeneratedPositions(result);
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
            HashSet<GraphDecoratorStack> measuredDecoratorStacks = new();
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                if (item.DecoratorStack != null)
                {
                    if (!measuredDecoratorStacks.Add(item.DecoratorStack))
                    {
                        continue;
                    }

                    rectangles.Add(new PresentationRect(
                        item.DecoratorStack.PlacementOwner?.DisplayName ?? "Decorator stack",
                        item.DecoratorStack.OwnBounds));
                    continue;
                }

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

        /// <summary>Maps a decorator continuation through the shared presentation resolver.</summary>
        private static GraphPresentationEndpoint ResolveDecoratorContinuationEndpoint(
            GraphPresentation presentation,
            GraphPresentationRelation relation)
        {
            return presentation?.ResolveContinuationSource(relation) ?? relation?.Source ?? default;
        }

        /// <summary>
        /// Adds first-class placement candidates from each Flow's owned, ordered members.
        /// Execution-only rails are intentionally absent from this graph; they are derived
        /// from the same final geometry after the owned cards have been placed.
        /// </summary>
        private static void AddScopePlacementCandidates(
            GraphPresentation presentation,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices,
            IDictionary<LayoutVertex, List<LayoutVertex>> children,
            IDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IDictionary<LayoutVertex, LayoutVertex> loopConditions,
            IDictionary<LayoutVertex, LayoutVertex> flowCompletions)
        {
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                GraphFlowScope scope = item?.FlowScope;
                if (scope == null
                    || !itemVertices.TryGetValue(item, out LayoutVertex owner)
                    || !completionVertices.TryGetValue(item, out LayoutVertex completion))
                {
                    continue;
                }

                flowCompletions[owner] = completion;
                if (scope is GraphSequenceScope || scope is GraphAggregateScope)
                {
                    AddOrderedPlacementChain(
                        owner,
                        scope.Members,
                        itemVertices,
                        completionVertices,
                        children);
                    continue;
                }

                switch (scope)
                {
                    case GraphConditionScope condition:
                        AddBranchPlacementCandidates(
                            owner,
                            new[] { condition.TrueBranch, condition.FalseBranch },
                            itemVertices,
                            conditionBranches);
                        break;
                    case GraphProbabilityScope probability:
                        AddBranchPlacementCandidates(
                            owner,
                            probability.Options.Select(option => option.Item),
                            itemVertices,
                            conditionBranches);
                        break;
                    case GraphDecisionScope decision:
                        AddBranchPlacementCandidates(
                            owner,
                            decision.Options.Select(option => option.Item),
                            itemVertices,
                            conditionBranches);
                        break;
                    case GraphParallelScope parallel:
                        AddBranchPlacementCandidates(
                            owner,
                            parallel.Branches,
                            itemVertices,
                            conditionBranches);
                        break;
                    case GraphLoopScope loop:
                        AddLoopPlacementCandidates(
                            owner,
                            loop,
                            itemVertices,
                            completionVertices,
                            children,
                            loopConditions);
                        break;
                    case GraphForEachScope forEach:
                        AddForEachPlacementCandidates(
                            owner,
                            forEach,
                            itemVertices,
                            completionVertices,
                            children);
                        break;
                }
            }
        }

        /// <summary>Adds one ordered member chain while preserving authored occurrence order.</summary>
        private static LayoutVertex AddOrderedPlacementChain(
            LayoutVertex start,
            IEnumerable<GraphPresentationItem> members,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices,
            IDictionary<LayoutVertex, List<LayoutVertex>> children)
        {
            LayoutVertex previous = start;
            HashSet<LayoutVertex> seen = new();
            foreach (GraphPresentationItem member in members ?? Array.Empty<GraphPresentationItem>())
            {
                LayoutVertex current = ResolveItemVertex(member, itemVertices);
                if (current == null || !seen.Add(current))
                {
                    continue;
                }

                AddPlacementCandidate(children, previous, current);
                previous = ResolveCompletionVertex(member, itemVertices, completionVertices) ?? current;
            }

            return previous;
        }

        /// <summary>Adds branch roots in their declared lane order.</summary>
        private static void AddBranchPlacementCandidates(
            LayoutVertex owner,
            IEnumerable<GraphPresentationItem> branches,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices,
            IDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches)
        {
            HashSet<LayoutVertex> seen = new();
            foreach (GraphPresentationItem branch in branches ?? Array.Empty<GraphPresentationItem>())
            {
                LayoutVertex candidate = ResolveItemVertex(branch, itemVertices);
                if (candidate == null || !seen.Add(candidate))
                {
                    continue;
                }

                AddPlacementCandidate(conditionBranches, owner, candidate);
            }
        }

        /// <summary>Adds mode-specific Loop condition and ordered Body placement candidates.</summary>
        private static void AddLoopPlacementCandidates(
            LayoutVertex owner,
            GraphLoopScope scope,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices,
            IDictionary<LayoutVertex, List<LayoutVertex>> children,
            IDictionary<LayoutVertex, LayoutVertex> loopConditions)
        {
            LayoutVertex condition = ResolveItemVertex(scope.Condition, itemVertices);
            bool hasEmbeddedPredicate = scope.PredicateRoot != null;
            LayoutVertex bodyStart;
            if (scope.Mode != Loop.LoopType.doWhile && !hasEmbeddedPredicate && condition != null)
            {
                AddPlacementCandidate(children, owner, condition);
                loopConditions[owner] = condition;
                bodyStart = condition;
            }
            else
            {
                bodyStart = owner;
            }

            LayoutVertex bodyEnd = AddOrderedPlacementChain(
                bodyStart,
                scope.Body,
                itemVertices,
                completionVertices,
                children);
            if (scope.Mode == Loop.LoopType.doWhile && !hasEmbeddedPredicate && condition != null)
            {
                AddPlacementCandidate(children, bodyEnd, condition);
                loopConditions[owner] = condition;
            }
        }

        /// <summary>Adds the check, body, and completion chain owned by a ForEach scope.</summary>
        private static void AddForEachPlacementCandidates(
            LayoutVertex owner,
            GraphForEachScope scope,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices,
            IDictionary<LayoutVertex, List<LayoutVertex>> children)
        {
            LayoutVertex check = ResolveItemVertex(scope.Check, itemVertices);
            LayoutVertex previous = check == null ? owner : check;
            if (check != null)
            {
                AddPlacementCandidate(children, owner, check);
            }

            LayoutVertex body = ResolveItemVertex(scope.Body, itemVertices);
            if (body != null)
            {
                AddPlacementCandidate(children, previous, body);
                previous = ResolveCompletionVertex(scope.Body, itemVertices, completionVertices) ?? body;
            }
        }

        /// <summary>Resolves an owned presentation item to its one canvas placement vertex.</summary>
        private static LayoutVertex ResolveItemVertex(
            GraphPresentationItem item,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices)
        {
            if (item == null)
            {
                return null;
            }

            if (itemVertices.TryGetValue(item, out LayoutVertex vertex))
            {
                return vertex;
            }

            return item.Parent != null && itemVertices.TryGetValue(FindRootItem(item), out vertex)
                ? vertex
                : null;
        }

        /// <summary>Resolves a member's completion endpoint to its placement vertex.</summary>
        private static LayoutVertex ResolveCompletionVertex(
            GraphPresentationItem item,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> itemVertices,
            IReadOnlyDictionary<GraphPresentationItem, LayoutVertex> completionVertices)
        {
            return item == null
                ? null
                : ResolveVertex(item.Completion, itemVertices, completionVertices)
                    ?? ResolveItemVertex(item, itemVertices);
        }

        /// <summary>Adds one placement candidate while keeping declaration order and uniqueness.</summary>
        private static void AddPlacementCandidate(
            IDictionary<LayoutVertex, List<LayoutVertex>> relation,
            LayoutVertex source,
            LayoutVertex target)
        {
            if (source == null || target == null || source == target)
            {
                return;
            }

            if (!relation.TryGetValue(source, out List<LayoutVertex> list))
            {
                list = new List<LayoutVertex>();
                relation.Add(source, list);
            }

            if (!list.Contains(target))
            {
                list.Add(target);
            }
        }

        /// <summary>
        /// Assigns first-placement ownership for one reachable or unreachable presentation subtree.
        /// </summary>
        private static void AssignPlacementOwnership(
            LayoutVertex root,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
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

                // Every Flow owner places its completion after its complete owned structure.
                if (flowCompletions.TryGetValue(current, out LayoutVertex completion)
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

        /// <summary>Measures one subtree around its main-flow alignment axis.</summary>
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

            float ownHalfWidth = vertex.Size.x * 0.5f;
            float mainContentLeft = ownHalfWidth;
            float mainContentRight = ownHalfWidth;
            float placeholderContentLeft = ownHalfWidth;
            float placeholderContentRight = ownHalfWidth;
            if (children.TryGetValue(vertex, out List<LayoutVertex> childNodes))
            {
                IncludePlacementGroup(
                    childNodes,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    envelopes,
                    ref mainContentLeft,
                    ref mainContentRight,
                    ref placeholderContentLeft,
                    ref placeholderContentRight);
            }

            if (conditionBranches.TryGetValue(vertex, out List<LayoutVertex> branchNodes))
            {
                IncludePlacementGroup(
                    branchNodes,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    envelopes,
                    ref mainContentLeft,
                    ref mainContentRight,
                    ref placeholderContentLeft,
                    ref placeholderContentRight);
            }

            if (flowCompletions.TryGetValue(vertex, out LayoutVertex completionVertex))
            {
                SubtreeEnvelope completionEnvelope = MeasureSubtree(
                    completionVertex,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    envelopes);
                IncludePlacementExtents(
                    completionEnvelope,
                    0f,
                    ref mainContentLeft,
                    ref mainContentRight,
                    ref placeholderContentLeft,
                    ref placeholderContentRight);
            }

            float mainLeading = GetHorizontalLeading(vertex);
            float mainTrailing = GetHorizontalTrailing(vertex);
            float mainLeftExtent = mainLeading + mainContentLeft;
            float mainRightExtent = mainTrailing + mainContentRight;
            float placeholderLeftExtent = mainLeading + placeholderContentLeft;
            float placeholderRightExtent = mainTrailing + placeholderContentRight;
            float serviceWidth = 0f;
            if (services.TryGetValue(vertex, out List<LayoutVertex> serviceNodes))
            {
                foreach (LayoutVertex service in serviceNodes)
                {
                    SubtreeEnvelope serviceEnvelope = MeasureSubtree(
                        service,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
                        envelopes);
                    serviceWidth = Mathf.Max(
                        serviceWidth,
                        serviceEnvelope.TotalWidth
                            + GraphPresentationMetrics.ServiceScopePadding * 2f);
                }
            }

            if (serviceWidth > 0f)
            {
                placeholderRightExtent += GraphPresentationMetrics.ServiceGap + serviceWidth;
            }

            SubtreeEnvelope envelope = new(
                mainLeftExtent,
                mainRightExtent,
                placeholderLeftExtent,
                placeholderRightExtent);
            envelopes[vertex] = envelope;
            return envelope;
        }

        /// <summary>Measures one vertical continuation or a horizontal full-placeholder group.</summary>
        private static void IncludePlacementGroup(
            IReadOnlyList<LayoutVertex> vertices,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
            IDictionary<LayoutVertex, SubtreeEnvelope> envelopes,
            ref float mainLeft,
            ref float mainRight,
            ref float placeholderLeft,
            ref float placeholderRight)
        {
            if (vertices == null || vertices.Count == 0)
            {
                return;
            }

            foreach (LayoutVertex vertex in vertices)
            {
                MeasureSubtree(
                    vertex,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    envelopes);
            }

            if (vertices.Count == 1)
            {
                IncludePlacementExtents(
                    envelopes[vertices[0]],
                    0f,
                    ref mainLeft,
                    ref mainRight,
                    ref placeholderLeft,
                    ref placeholderRight);
                return;
            }

            float groupWidth = vertices.Sum(vertex => envelopes[vertex].TotalWidth)
                + GraphPresentationMetrics.SiblingGap * (vertices.Count - 1);
            float offset = -groupWidth * 0.5f;
            foreach (LayoutVertex vertex in vertices)
            {
                IncludePlacementExtents(
                    envelopes[vertex],
                    offset + envelopes[vertex].PlaceholderLeftExtent,
                    ref mainLeft,
                    ref mainRight,
                    ref placeholderLeft,
                    ref placeholderRight);
                offset += envelopes[vertex].TotalWidth + GraphPresentationMetrics.SiblingGap;
            }
        }

        /// <summary>Unions a measured child around an explicit main-flow axis offset.</summary>
        private static void IncludePlacementExtents(
            SubtreeEnvelope envelope,
            float axisOffset,
            ref float mainLeft,
            ref float mainRight,
            ref float placeholderLeft,
            ref float placeholderRight)
        {
            if (envelope == null)
            {
                return;
            }

            mainLeft = Mathf.Max(mainLeft, envelope.MainLeftExtent - axisOffset);
            mainRight = Mathf.Max(mainRight, envelope.MainRightExtent + axisOffset);
            placeholderLeft = Mathf.Max(placeholderLeft, envelope.PlaceholderLeftExtent - axisOffset);
            placeholderRight = Mathf.Max(placeholderRight, envelope.PlaceholderRightExtent + axisOffset);
        }

        /// <summary>Returns the derived left clearance that belongs to one scope's full range.</summary>
        private static float GetHorizontalLeading(LayoutVertex vertex)
        {
            if (vertex == null || vertex.IsFlowCompletion)
            {
                return 0f;
            }

            return vertex.Item.FlowScope switch
            {
                GraphOrderedScope => GraphPresentationMetrics.SequenceRailOffset,
                GraphConditionScope => GraphPresentationMetrics.ConditionBracketOffset,
                GraphProbabilityScope => GraphPresentationMetrics.ProbabilityFanOffset,
                GraphLoopScope => GraphPresentationMetrics.LoopBodyFramePadding
                    + GraphPresentationMetrics.LoopReturnRailGap,
                GraphForEachScope => GraphPresentationMetrics.ForEachBodyFramePadding,
                _ => 0f,
            };
        }

        /// <summary>Returns the derived right clearance that belongs to one scope's full range.</summary>
        private static float GetHorizontalTrailing(LayoutVertex vertex)
        {
            if (vertex == null || vertex.IsFlowCompletion)
            {
                return 0f;
            }

            return vertex.Item.FlowScope switch
            {
                GraphConditionScope => GraphPresentationMetrics.ConditionBracketOffset,
                GraphProbabilityScope => GraphPresentationMetrics.ProbabilityFanOffset,
                GraphLoopScope => GraphPresentationMetrics.LoopBodyFramePadding
                    + GraphPresentationMetrics.LoopExitRailGap,
                GraphForEachScope => GraphPresentationMetrics.ForEachBodyFramePadding,
                _ => 0f,
            };
        }

        /// <summary>
        /// Places one measured subtree while keeping its reserved Service lane outside main flow lanes.
        /// </summary>
        private static void PlaceSubtree(
            LayoutVertex vertex,
            float axisX,
            float top,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> loopConditions,
            IReadOnlyDictionary<LayoutVertex, SubtreeEnvelope> envelopes,
            IDictionary<LayoutVertex, Vector2> positions,
            ref float bottom)
        {
            // Keep the common deep linear chain off the managed call stack. Complex
            // branch/scope layouts retain the established recursive semantics below.
            if (IsPlainLinearChain(vertex, children, services, conditionBranches, flowCompletions))
            {
                LayoutVertex current = vertex;
                float currentTop = top;
                while (current != null)
                {
                    Vector2 currentSize = current.Size;
                    positions[current] = new Vector2(
                        axisX - currentSize.x * 0.5f,
                        currentTop);
                    bottom = Mathf.Max(bottom, currentTop + currentSize.y);

                    if (!children.TryGetValue(current, out List<LayoutVertex> next) || next.Count == 0)
                    {
                        break;
                    }

                    LayoutVertex child = next[0];
                    currentTop += currentSize.y + GraphPresentationMetrics.LevelGap;
                    current = child;
                }

                return;
            }

            PlaceSubtreeRecursive(
                vertex,
                axisX,
                top,
                children,
                services,
                conditionBranches,
                flowCompletions,
                loopConditions,
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
            float axisX,
            float top,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> services,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> conditionBranches,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> flowCompletions,
            IReadOnlyDictionary<LayoutVertex, LayoutVertex> loopConditions,
            IReadOnlyDictionary<LayoutVertex, SubtreeEnvelope> envelopes,
            IDictionary<LayoutVertex, Vector2> positions,
            ref float bottom)
        {
            Vector2 size = vertex.Size;
            SubtreeEnvelope envelope = envelopes[vertex];
            positions[vertex] = new Vector2(axisX - size.x * 0.5f, top);
            bottom = Mathf.Max(bottom, top + size.y);
            float contentBottom = top + size.y;

            if (services.TryGetValue(vertex, out List<LayoutVertex> serviceNodes)
                && serviceNodes.Count > 0)
            {
                float serviceFrameLeft = axisX
                    + envelope.MainRightExtent
                    + GraphPresentationMetrics.ServiceGap;
                float serviceTop = top + GraphPresentationMetrics.ServiceScopeHeader;
                foreach (LayoutVertex service in serviceNodes)
                {
                    float serviceBottom = serviceTop;
                    float serviceAxis = serviceFrameLeft
                        + GraphPresentationMetrics.ServiceScopePadding
                        + envelopes[service].PlaceholderLeftExtent;
                    PlaceSubtree(
                        service,
                        serviceAxis,
                        serviceTop,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
                        loopConditions,
                        envelopes,
                        positions,
                        ref serviceBottom);
                    bottom = Mathf.Max(bottom, serviceBottom);
                    serviceTop = serviceBottom + GraphPresentationMetrics.ServiceGap;
                }

                contentBottom = Mathf.Max(
                    contentBottom,
                    serviceTop - GraphPresentationMetrics.ServiceGap
                        + GraphPresentationMetrics.ServiceScopePadding);
            }

            bottom = Mathf.Max(bottom, contentBottom);

            if (conditionBranches.TryGetValue(vertex, out List<LayoutVertex> branchNodes)
                && flowCompletions.TryGetValue(vertex, out LayoutVertex completionVertex))
            {
                float branchTop = contentBottom + GraphPresentationMetrics.LevelGap;
                float branchesBottom = branchTop;
                if (branchNodes.Count == 1)
                {
                    LayoutVertex branch = branchNodes[0];
                    float branchBottom = branchTop;
                    PlaceSubtree(
                        branch,
                        axisX,
                        branchTop,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
                        loopConditions,
                        envelopes,
                        positions,
                        ref branchBottom);
                    branchesBottom = Mathf.Max(branchesBottom, branchBottom);
                }
                else
                {
                    float groupWidth = branchNodes.Sum(branch => envelopes[branch].TotalWidth)
                        + GraphPresentationMetrics.SiblingGap * (branchNodes.Count - 1);
                    float groupLeft = axisX - groupWidth * 0.5f;
                    foreach (LayoutVertex branch in branchNodes)
                    {
                        float branchBottom = branchTop;
                        PlaceSubtree(
                            branch,
                            groupLeft + envelopes[branch].PlaceholderLeftExtent,
                            branchTop,
                            children,
                            services,
                            conditionBranches,
                            flowCompletions,
                            loopConditions,
                            envelopes,
                            positions,
                            ref branchBottom);
                        branchesBottom = Mathf.Max(branchesBottom, branchBottom);
                        groupLeft += envelopes[branch].TotalWidth + GraphPresentationMetrics.SiblingGap;
                    }
                }

                PlaceSubtree(
                    completionVertex,
                    axisX,
                    branchesBottom + GraphPresentationMetrics.LevelGap,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    loopConditions,
                    envelopes,
                    positions,
                    ref bottom);
            }
            else if (!vertex.IsFlowCompletion
                && vertex.Item.FlowScope is GraphOrderedScope
                && flowCompletions.TryGetValue(vertex, out LayoutVertex orderedCompletionVertex))
            {
                float structureBottom = contentBottom;
                if (children.TryGetValue(vertex, out List<LayoutVertex> orderedChildren)
                    && orderedChildren.Count > 0)
                {
                    LayoutVertex first = orderedChildren[0];
                    PlaceSubtree(
                        first,
                        axisX,
                        contentBottom + GraphPresentationMetrics.LevelGap,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
                        loopConditions,
                        envelopes,
                        positions,
                        ref structureBottom);
                }

                PlaceSubtree(
                    orderedCompletionVertex,
                    axisX,
                    structureBottom + GraphPresentationMetrics.FlowCompletionGap,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    loopConditions,
                    envelopes,
                    positions,
                    ref bottom);
            }
            else if (!vertex.IsFlowCompletion
                && (vertex.Item.LoopScope != null || vertex.Item.ForEachScope != null)
                && flowCompletions.TryGetValue(vertex, out LayoutVertex loopCompletionVertex))
            {
                float structureBottom = contentBottom;
                float childTop = contentBottom + GraphPresentationMetrics.LevelGap;
                if (loopConditions.TryGetValue(vertex, out LayoutVertex loopCondition)
                    && vertex.Item.LoopScope?.Mode != Loop.LoopType.doWhile)
                {
                    PlaceSubtree(
                        loopCondition,
                        axisX,
                        childTop,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
                        loopConditions,
                        envelopes,
                        positions,
                        ref structureBottom);
                }
                else if (children.TryGetValue(vertex, out List<LayoutVertex> loopChildren)
                    && loopChildren.Count > 0)
                {
                    LayoutVertex first = loopChildren[0];
                    if (vertex.Item.LoopScope?.PredicateRoot != null
                        && vertex.Item.LoopScope.Mode != Loop.LoopType.doWhile)
                    {
                        float predicateBottom = vertex.Item.LoopScope.PredicateBounds.yMax
                            - vertex.Item.Position.y;
                        childTop = top + Mathf.Max(
                            size.y + GraphPresentationMetrics.LevelGap,
                            predicateBottom + GraphPresentationMetrics.LevelGap);
                    }

                    PlaceSubtree(
                        first,
                        axisX,
                        childTop,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
                        loopConditions,
                        envelopes,
                        positions,
                        ref structureBottom);
                }

                if (vertex.Item.LoopScope?.PredicateRoot != null
                    && vertex.Item.LoopScope.Mode == Loop.LoopType.doWhile)
                {
                    structureBottom += vertex.Item.LoopScope.PredicateBounds.height
                        + GraphPresentationMetrics.LevelGap;
                }

                PlaceSubtree(
                    loopCompletionVertex,
                    axisX + (envelope.MainRightExtent - envelope.MainLeftExtent) * 0.5f,
                    structureBottom + GraphPresentationMetrics.FlowCompletionGap,
                    children,
                    services,
                    conditionBranches,
                    flowCompletions,
                    loopConditions,
                    envelopes,
                    positions,
                    ref bottom);
            }
            else if (children.TryGetValue(vertex, out List<LayoutVertex> childNodes) && childNodes.Count > 0)
            {
                float childTop = contentBottom + GraphPresentationMetrics.LevelGap;
                if (childNodes.Count == 1)
                {
                    LayoutVertex child = childNodes[0];
                    PlaceSubtree(
                        child,
                        axisX,
                        childTop,
                        children,
                        services,
                        conditionBranches,
                        flowCompletions,
                        loopConditions,
                        envelopes,
                        positions,
                        ref bottom);
                }
                else
                {
                    float groupWidth = childNodes.Sum(child => envelopes[child].TotalWidth)
                        + GraphPresentationMetrics.SiblingGap * (childNodes.Count - 1);
                    float groupLeft = axisX - groupWidth * 0.5f;
                    foreach (LayoutVertex child in childNodes)
                    {
                        PlaceSubtree(
                            child,
                            groupLeft + envelopes[child].PlaceholderLeftExtent,
                            childTop,
                            children,
                            services,
                            conditionBranches,
                            flowCompletions,
                            loopConditions,
                            envelopes,
                            positions,
                            ref bottom);
                        groupLeft += envelopes[child].TotalWidth + GraphPresentationMetrics.SiblingGap;
                    }
                }
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

        /// <summary>Measured horizontal ownership around a main-flow axis and full placeholder.</summary>
        private sealed class SubtreeEnvelope
        {
            internal SubtreeEnvelope(
                float mainLeftExtent,
                float mainRightExtent,
                float placeholderLeftExtent,
                float placeholderRightExtent)
            {
                MainLeftExtent = mainLeftExtent;
                MainRightExtent = mainRightExtent;
                PlaceholderLeftExtent = placeholderLeftExtent;
                PlaceholderRightExtent = placeholderRightExtent;
            }

            internal float MainLeftExtent { get; }
            internal float MainRightExtent { get; }
            internal float PlaceholderLeftExtent { get; }
            internal float PlaceholderRightExtent { get; }
            internal float MainWidth => MainLeftExtent + MainRightExtent;
            internal float TotalWidth => PlaceholderLeftExtent + PlaceholderRightExtent;
        }

        /// <summary>
        /// One real or presentation-only vertex used by deterministic layout.
        /// </summary>
        private sealed class LayoutVertex
        {
            internal LayoutVertex(
                GraphPresentationItem item,
                bool isFlowCompletion,
                GraphDecoratorStack decoratorStack = null)
            {
                Item = item ?? throw new ArgumentNullException(nameof(item));
                IsFlowCompletion = isFlowCompletion;
                DecoratorStack = decoratorStack;
            }

            internal GraphPresentationItem Item { get; }
            internal bool IsFlowCompletion { get; }
            internal GraphDecoratorStack DecoratorStack { get; }
            internal Vector2 Size => IsFlowCompletion
                ? Item.FlowScope.CompletionSize
                : DecoratorStack?.OwnBounds.size ?? Item.Size;

            /// <summary>Applies a layout-unit position to its canonical presentation owner.</summary>
            internal void ApplyLayoutPosition(Vector2 position)
            {
                if (DecoratorStack != null)
                {
                    DecoratorStack.ApplyOwnLayoutPosition(position);
                    return;
                }

                Item.Position = position;
                if (Item.Node != null)
                {
                    Item.Node.Position = position;
                }
            }

            /// <summary>Writes generated positions for every authored decorator in this layout unit.</summary>
            internal void AddGeneratedPositions(IDictionary<UUID, Vector2> positions)
            {
                if (DecoratorStack != null)
                {
                    Vector2 ownerPosition = DecoratorStack.Anchor.Position;
                    if (DecoratorStack.Anchor.Node != null)
                    {
                        positions[DecoratorStack.Anchor.Node.UUID] = ownerPosition;
                    }

                    foreach (GraphPresentationItem badge in DecoratorStack.Badges)
                    {
                        if (badge.Node != null)
                        {
                            positions[badge.Node.UUID] = ownerPosition;
                        }
                    }

                    return;
                }

                if (Item.Node != null)
                {
                    positions[Item.Node.UUID] = Item.Position;
                }
            }
        }
    }
}
