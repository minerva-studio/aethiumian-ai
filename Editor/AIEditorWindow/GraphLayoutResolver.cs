using Aethiumian.AI.References;
using Aethiumian.AI.Visual;
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
            Dictionary<UUID, Vector2> result = new();
            Dictionary<UUID, List<GraphNodeDescriptor>> children = new();
            Dictionary<UUID, List<GraphNodeDescriptor>> services = new();
            HashSet<UUID> assigned = new();
            foreach (GraphEdgeDescriptor edge in topology.Edges)
            {
                if (edge.Target == null || edge.Kind == GraphEdgeKind.Raw)
                {
                    continue;
                }

                Dictionary<UUID, List<GraphNodeDescriptor>> targetMap = edge.Kind == GraphEdgeKind.Service ? services : children;
                if (!targetMap.TryGetValue(edge.Source.UUID, out List<GraphNodeDescriptor> list))
                {
                    list = new List<GraphNodeDescriptor>();
                    targetMap.Add(edge.Source.UUID, list);
                }

                // A repeated reference remains a repeated edge, but one node has one layout position.
                if (!list.Contains(edge.Target))
                {
                    list.Add(edge.Target);
                }
            }

            // The first declaration-order path owns placement. Later parents still render their edge.
            Dictionary<UUID, List<GraphNodeDescriptor>> placementChildren = new();
            Queue<GraphNodeDescriptor> queue = new();
            GraphNodeDescriptor head = topology.FindNode(tree.headNodeUUID);
            if (head != null)
            {
                queue.Enqueue(head);
                assigned.Add(head.UUID);
            }

            while (queue.Count > 0)
            {
                GraphNodeDescriptor current = queue.Dequeue();
                if (children.TryGetValue(current.UUID, out List<GraphNodeDescriptor> candidates))
                {
                    foreach (GraphNodeDescriptor candidate in candidates)
                    {
                        if (!assigned.Add(candidate.UUID))
                        {
                            continue;
                        }

                        if (!placementChildren.TryGetValue(current.UUID, out List<GraphNodeDescriptor> list))
                        {
                            list = new List<GraphNodeDescriptor>();
                            placementChildren.Add(current.UUID, list);
                        }

                        list.Add(candidate);
                        queue.Enqueue(candidate);
                    }
                }

                if (!services.TryGetValue(current.UUID, out List<GraphNodeDescriptor> serviceCandidates))
                {
                    continue;
                }

                foreach (GraphNodeDescriptor service in serviceCandidates)
                {
                    if (assigned.Add(service.UUID))
                    {
                        queue.Enqueue(service);
                    }
                }
            }

            float reachableBottom = 0f;
            if (head != null)
            {
                Dictionary<UUID, float> subtreeWidths = new();
                MeasureSubtree(head, placementChildren, subtreeWidths);
                PlaceSubtree(head, 0f, 0f, placementChildren, subtreeWidths, result, ref reachableBottom);
            }

            // Services form a side rail and do not consume the host's main child lanes.
            Dictionary<UUID, float> serviceSubtreeWidths = new();
            bool placedService;
            do
            {
                placedService = false;
                foreach (GraphNodeDescriptor host in topology.Nodes)
                {
                    if (!result.TryGetValue(host.UUID, out Vector2 hostPosition)
                        || !services.TryGetValue(host.UUID, out List<GraphNodeDescriptor> attachedServices))
                    {
                        continue;
                    }

                    Vector2 hostSize = GetNodeSize(host);
                    float serviceX = hostPosition.x + hostSize.x + ServiceGap;
                    float serviceY = hostPosition.y;
                    foreach (GraphNodeDescriptor service in attachedServices)
                    {
                        if (result.ContainsKey(service.UUID))
                        {
                            serviceY = Mathf.Max(serviceY, result[service.UUID].y + GetNodeSize(service).y + ServiceGap);
                            continue;
                        }

                        MeasureSubtree(service, placementChildren, serviceSubtreeWidths);
                        float serviceBottom = serviceY;
                        PlaceSubtree(service, serviceX, serviceY, placementChildren, serviceSubtreeWidths, result, ref serviceBottom);
                        reachableBottom = Mathf.Max(reachableBottom, serviceBottom);
                        serviceY = serviceBottom + ServiceGap;
                        placedService = true;
                    }
                }
            }
            while (placedService);

            // Authored but unreachable nodes are deliberately separated from the executable flow.
            int unreachableIndex = 0;
            float unreachableY = reachableBottom + 2f * LevelGap;
            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                if (result.ContainsKey(node.UUID))
                {
                    continue;
                }

                Vector2 size = GetNodeSize(node);
                result[node.UUID] = new Vector2(unreachableIndex * (size.x + UnreachableGap), unreachableY);
                unreachableIndex++;
            }

            return result;
        }

        private static float MeasureSubtree(
            GraphNodeDescriptor node,
            IReadOnlyDictionary<UUID, List<GraphNodeDescriptor>> children,
            IDictionary<UUID, float> widths)
        {
            if (widths.TryGetValue(node.UUID, out float existing))
            {
                return existing;
            }

            float ownWidth = GetNodeSize(node).x;
            if (!children.TryGetValue(node.UUID, out List<GraphNodeDescriptor> childNodes) || childNodes.Count == 0)
            {
                widths[node.UUID] = ownWidth;
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
            widths[node.UUID] = width;
            return width;
        }

        private static void PlaceSubtree(
            GraphNodeDescriptor node,
            float left,
            float top,
            IReadOnlyDictionary<UUID, List<GraphNodeDescriptor>> children,
            IReadOnlyDictionary<UUID, float> widths,
            IDictionary<UUID, Vector2> positions,
            ref float bottom)
        {
            Vector2 size = GetNodeSize(node);
            float subtreeWidth = widths[node.UUID];
            positions[node.UUID] = new Vector2(left + (subtreeWidth - size.x) * 0.5f, top);
            bottom = Mathf.Max(bottom, top + size.y);

            if (!children.TryGetValue(node.UUID, out List<GraphNodeDescriptor> childNodes) || childNodes.Count == 0)
            {
                return;
            }

            float childrenWidth = 0f;
            foreach (GraphNodeDescriptor child in childNodes)
            {
                childrenWidth += widths[child.UUID];
            }

            childrenWidth += SiblingGap * (childNodes.Count - 1);
            float childLeft = left + (subtreeWidth - childrenWidth) * 0.5f;
            float childTop = top + size.y + LevelGap;
            foreach (GraphNodeDescriptor child in childNodes)
            {
                PlaceSubtree(child, childLeft, childTop, children, widths, positions, ref bottom);
                childLeft += widths[child.UUID] + SiblingGap;
            }
        }
    }
}
