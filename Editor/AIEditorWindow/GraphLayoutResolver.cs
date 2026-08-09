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
        private const float SiblingGap = 56f;
        private const float LevelGap = 92f;
        private const float ServiceGap = 28f;
        private const float UnreachableGap = 80f;
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
                GraphNodeShape.Flow => new Vector2(250f, 54f),
                GraphNodeShape.Branch => new Vector2(190f, 82f),
                GraphNodeShape.Service => new Vector2(176f, 48f),
                _ => new Vector2(220f, 82f),
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
                itemVertices[item] = new LayoutVertex(item, isSequenceCompletion: false);
                if (item.SequenceScope != null)
                {
                    completionVertices[item] = new LayoutVertex(item, isSequenceCompletion: true);
                }
            }

            Dictionary<LayoutVertex, List<LayoutVertex>> children = new();
            Dictionary<LayoutVertex, List<LayoutVertex>> services = new();
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

                Dictionary<LayoutVertex, List<LayoutVertex>> targetMap = relation.Kind == GraphPresentationRelationKind.Service
                    ? services
                    : children;
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
            Queue<LayoutVertex> queue = new();
            HashSet<LayoutVertex> assigned = new();
            GraphNodeDescriptor head = topology.FindNode(tree.headNodeUUID);
            GraphPresentationItem headItem = FindRootItem(presentation.Find(head?.UUID ?? UUID.Empty));
            LayoutVertex headVertex = headItem != null && itemVertices.TryGetValue(headItem, out LayoutVertex resolvedHead)
                ? resolvedHead
                : null;
            if (headVertex != null)
            {
                queue.Enqueue(headVertex);
                assigned.Add(headVertex);
            }

            while (queue.Count > 0)
            {
                LayoutVertex current = queue.Dequeue();
                if (children.TryGetValue(current, out List<LayoutVertex> candidates))
                {
                    foreach (LayoutVertex candidate in candidates)
                    {
                        if (!assigned.Add(candidate))
                        {
                            continue;
                        }

                        if (!placementChildren.TryGetValue(current, out List<LayoutVertex> list))
                        {
                            list = new List<LayoutVertex>();
                            placementChildren.Add(current, list);
                        }

                        list.Add(candidate);
                        queue.Enqueue(candidate);
                    }
                }

                if (!services.TryGetValue(current, out List<LayoutVertex> serviceCandidates))
                {
                    continue;
                }

                foreach (LayoutVertex service in serviceCandidates)
                {
                    if (assigned.Add(service))
                    {
                        queue.Enqueue(service);
                    }
                }
            }

            float reachableBottom = 0f;
            Dictionary<LayoutVertex, Vector2> positions = new();
            if (headVertex != null)
            {
                Dictionary<LayoutVertex, float> subtreeWidths = new();
                MeasureSubtree(headVertex, placementChildren, subtreeWidths);
                PlaceSubtree(headVertex, 0f, 0f, placementChildren, subtreeWidths, positions, ref reachableBottom);
            }

            // Services form a side rail and do not consume the host's main child lanes.
            Dictionary<LayoutVertex, float> serviceSubtreeWidths = new();
            bool placedService;
            do
            {
                placedService = false;
                foreach (KeyValuePair<LayoutVertex, List<LayoutVertex>> pair in services)
                {
                    LayoutVertex host = pair.Key;
                    if (!positions.TryGetValue(host, out Vector2 hostPosition))
                    {
                        continue;
                    }

                    Vector2 hostSize = host.Size;
                    float serviceX = hostPosition.x + hostSize.x + ServiceGap;
                    float serviceY = hostPosition.y;
                    foreach (LayoutVertex service in pair.Value)
                    {
                        if (positions.ContainsKey(service))
                        {
                            serviceY = Mathf.Max(serviceY, positions[service].y + service.Size.y + ServiceGap);
                            continue;
                        }

                        MeasureSubtree(service, placementChildren, serviceSubtreeWidths);
                        float serviceBottom = serviceY;
                        PlaceSubtree(service, serviceX, serviceY, placementChildren, serviceSubtreeWidths, positions, ref serviceBottom);
                        reachableBottom = Mathf.Max(reachableBottom, serviceBottom);
                        serviceY = serviceBottom + ServiceGap;
                        placedService = true;
                    }
                }
            }
            while (placedService);

            // Authored but unreachable nodes are deliberately separated from the executable flow.
            int unreachableIndex = 0;
            float unreachableTop = reachableBottom + 2f * LevelGap;
            float unreachableRowHeight = 0f;
            float unreachableX = 0f;
            float unreachableY = unreachableTop;
            foreach (GraphPresentationItem item in presentation.Roots)
            {
                LayoutVertex vertex = itemVertices[item];
                if (positions.ContainsKey(vertex))
                {
                    continue;
                }

                Vector2 size = vertex.Size;
                if (unreachableIndex > 0 && unreachableIndex % UnreachableColumns == 0)
                {
                    unreachableX = 0f;
                    unreachableY += unreachableRowHeight + UnreachableGap;
                    unreachableRowHeight = 0f;
                }

                positions[vertex] = new Vector2(unreachableX, unreachableY);
                unreachableX += size.x + UnreachableGap;
                unreachableRowHeight = Mathf.Max(unreachableRowHeight, size.y);
                unreachableIndex++;
            }

            Dictionary<UUID, Vector2> result = new();
            foreach (KeyValuePair<LayoutVertex, Vector2> pair in positions)
            {
                if (!pair.Key.IsSequenceCompletion && pair.Key.Item?.Node != null)
                {
                    result[pair.Key.Item.Node.UUID] = pair.Value;
                }
            }

            return result;
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

            return endpoint.Anchor == GraphPresentationAnchorKind.SequenceComplete
                ? completionVertices.TryGetValue(item, out LayoutVertex completion) ? completion : null
                : itemVertices.TryGetValue(item, out LayoutVertex vertex) ? vertex : null;
        }

        private static float MeasureSubtree(
            LayoutVertex vertex,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IDictionary<LayoutVertex, float> widths)
        {
            if (widths.TryGetValue(vertex, out float existing))
            {
                return existing;
            }

            float ownWidth = vertex.Size.x;
            if (!children.TryGetValue(vertex, out List<LayoutVertex> childNodes) || childNodes.Count == 0)
            {
                widths[vertex] = ownWidth;
                return ownWidth;
            }

            float childrenWidth = 0f;
            for (int i = 0; i < childNodes.Count; i++)
            {
                childrenWidth += MeasureSubtree(childNodes[i], children, widths);
                if (i > 0)
                {
                    childrenWidth += SiblingGap;
                }
            }

            float width = Mathf.Max(ownWidth, childrenWidth);
            widths[vertex] = width;
            return width;
        }

        private static void PlaceSubtree(
            LayoutVertex vertex,
            float left,
            float top,
            IReadOnlyDictionary<LayoutVertex, List<LayoutVertex>> children,
            IReadOnlyDictionary<LayoutVertex, float> widths,
            IDictionary<LayoutVertex, Vector2> positions,
            ref float bottom)
        {
            Vector2 size = vertex.Size;
            float subtreeWidth = widths[vertex];
            positions[vertex] = new Vector2(left + (subtreeWidth - size.x) * 0.5f, top);
            bottom = Mathf.Max(bottom, top + size.y);

            if (!children.TryGetValue(vertex, out List<LayoutVertex> childNodes) || childNodes.Count == 0)
            {
                return;
            }

            float childrenWidth = 0f;
            foreach (LayoutVertex child in childNodes)
            {
                childrenWidth += widths[child];
            }

            childrenWidth += SiblingGap * (childNodes.Count - 1);
            float childLeft = left + (subtreeWidth - childrenWidth) * 0.5f;
            float childTop = top + size.y + LevelGap;
            foreach (LayoutVertex child in childNodes)
            {
                PlaceSubtree(child, childLeft, childTop, children, widths, positions, ref bottom);
                childLeft += widths[child] + SiblingGap;
            }
        }

        /// <summary>
        /// One real or presentation-only vertex used by deterministic layout.
        /// </summary>
        private sealed class LayoutVertex
        {
            internal LayoutVertex(GraphPresentationItem item, bool isSequenceCompletion)
            {
                Item = item ?? throw new ArgumentNullException(nameof(item));
                IsSequenceCompletion = isSequenceCompletion;
            }

            internal GraphPresentationItem Item { get; }
            internal bool IsSequenceCompletion { get; }
            internal Vector2 Size => IsSequenceCompletion ? GraphSequenceScope.CompletionSize : Item.Size;
        }
    }
}
