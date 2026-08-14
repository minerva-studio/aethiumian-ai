using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aethiumian.AI
{
    /// <summary>Identifies the authored edge kind that owns a referenced node.</summary>
    internal enum NodeOwnershipKind
    {
        Structural,
        Service,
    }

    /// <summary>Describes one authored owning reference occurrence.</summary>
    internal readonly struct NodeReferenceOccurrence
    {
        internal NodeReferenceOccurrence(
            TreeNode owner,
            TreeNode target,
            string fieldName,
            int index,
            NodeOwnershipKind kind)
        {
            Owner = owner;
            Target = target;
            FieldName = fieldName;
            Index = index;
            Kind = kind;
        }

        internal TreeNode Owner { get; }
        internal TreeNode Target { get; }
        internal string FieldName { get; }
        internal int Index { get; }
        internal NodeOwnershipKind Kind { get; }
    }

    /// <summary>
    /// Read-only snapshot of authored node ownership edges.
    /// Parent metadata and Raw references are deliberately excluded from the edge graph.
    /// </summary>
    internal sealed class NodeTopologySnapshot
    {
        private readonly IReadOnlyList<TreeNode> nodes;
        private readonly Dictionary<UUID, List<NodeReferenceOccurrence>> incoming;
        private readonly Dictionary<UUID, List<NodeReferenceOccurrence>> outgoing;
        private readonly Dictionary<UUID, int> rawIncoming;

        private NodeTopologySnapshot(
            IReadOnlyList<TreeNode> nodes,
            Dictionary<UUID, List<NodeReferenceOccurrence>> incoming,
            Dictionary<UUID, List<NodeReferenceOccurrence>> outgoing,
            Dictionary<UUID, int> rawIncoming)
        {
            this.nodes = nodes;
            this.incoming = incoming;
            this.outgoing = outgoing;
            this.rawIncoming = rawIncoming;
        }

        /// <summary>Builds a snapshot from the current authored node list.</summary>
        /// <param name="sourceNodes">The nodes to scan.</param>
        /// <returns>A topology snapshot for the supplied nodes.</returns>
        internal static NodeTopologySnapshot Create(IEnumerable<TreeNode> sourceNodes)
        {
            List<TreeNode> nodes = sourceNodes?.Where(node => node != null).ToList() ?? new List<TreeNode>();
            Dictionary<UUID, TreeNode> byUUID = nodes.ToDictionary(node => node.uuid);
            Dictionary<UUID, List<NodeReferenceOccurrence>> incoming = new();
            Dictionary<UUID, List<NodeReferenceOccurrence>> outgoing = new();
            Dictionary<UUID, int> rawIncoming = new();
            foreach (TreeNode node in nodes)
            {
                incoming[node.uuid] = new List<NodeReferenceOccurrence>();
                outgoing[node.uuid] = new List<NodeReferenceOccurrence>();
                rawIncoming[node.uuid] = 0;
            }

            foreach (TreeNode owner in nodes)
            {
                NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
                foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
                {
                    AddOccurrence(
                        owner,
                        field.Name,
                        -1,
                        field.Get(owner),
                        byUUID,
                        incoming,
                        outgoing,
                        rawIncoming);
                }

                foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
                {
                    if (field.Get(owner) is not IList entries)
                    {
                        continue;
                    }

                    for (int index = 0; index < entries.Count; index++)
                    {
                        if (entries[index] is INodeReference reference)
                        {
                            AddOccurrence(
                                owner,
                                field.Name,
                                index,
                                reference,
                                byUUID,
                                incoming,
                                outgoing,
                                rawIncoming);
                        }
                    }
                }
            }

            return new NodeTopologySnapshot(nodes, incoming, outgoing, rawIncoming);
        }

        /// <summary>Returns every authored occurrence pointing to the target.</summary>
        /// <param name="target">The target node.</param>
        /// <returns>Incoming owning occurrences in authored scan order.</returns>
        internal IReadOnlyList<NodeReferenceOccurrence> GetIncoming(TreeNode target)
        {
            return target != null && incoming.TryGetValue(target.uuid, out List<NodeReferenceOccurrence> occurrences)
                ? occurrences
                : Array.Empty<NodeReferenceOccurrence>();
        }

        /// <summary>Returns every authored owning occurrence leaving the owner.</summary>
        /// <param name="owner">The source node.</param>
        /// <returns>Outgoing owning occurrences in authored scan order.</returns>
        internal IReadOnlyList<NodeReferenceOccurrence> GetOutgoing(TreeNode owner)
        {
            return owner != null && outgoing.TryGetValue(owner.uuid, out List<NodeReferenceOccurrence> occurrences)
                ? occurrences
                : Array.Empty<NodeReferenceOccurrence>();
        }

        /// <summary>Returns the number of Raw incoming references to a target.</summary>
        /// <param name="target">The target node.</param>
        /// <returns>The Raw occurrence count.</returns>
        internal int GetRawIncomingCount(TreeNode target)
        {
            return target != null && rawIncoming.TryGetValue(target.uuid, out int count) ? count : 0;
        }

        /// <summary>Determines whether a node has ambiguous or stale parent metadata.</summary>
        /// <param name="target">The node to inspect.</param>
        /// <returns>True when authored incoming ownership cannot unambiguously establish its parent.</returns>
        internal bool HasInvalidParentMetadata(TreeNode target)
        {
            IReadOnlyList<NodeReferenceOccurrence> occurrences = GetIncoming(target);
            if (occurrences.Count > 1)
            {
                return true;
            }

            return occurrences.Count == 1
                && (target?.parent?.UUID ?? UUID.Empty) != occurrences[0].Owner.uuid;
        }

        /// <summary>Determines whether attaching a candidate below an owner creates a cycle.</summary>
        /// <param name="owner">The destination owner.</param>
        /// <param name="candidate">The candidate subtree root.</param>
        /// <returns>True when the assignment would create or retain a cycle.</returns>
        internal bool WouldCreateCycle(TreeNode owner, TreeNode candidate)
        {
            if (owner == null || candidate == null || owner == candidate)
            {
                return true;
            }

            HashSet<UUID> visited = new();
            Stack<TreeNode> pending = new();
            pending.Push(candidate);
            while (pending.Count > 0)
            {
                TreeNode current = pending.Pop();
                if (current == null || !visited.Add(current.uuid))
                {
                    continue;
                }

                if (current == owner)
                {
                    return true;
                }

                foreach (NodeReferenceOccurrence occurrence in GetOutgoing(current))
                {
                    pending.Push(occurrence.Target);
                }
            }

            return HasCycleFrom(candidate);
        }

        /// <summary>Validates all authored ownership and parent relationships.</summary>
        /// <returns>Human-readable structural validation errors.</returns>
        internal IReadOnlyList<string> GetValidationErrors()
        {
            List<string> errors = new();
            foreach (TreeNode node in nodes)
            {
                IReadOnlyList<NodeReferenceOccurrence> owners = GetIncoming(node);
                UUID declaredParent = node.parent?.UUID ?? UUID.Empty;
                if (owners.Count > 1)
                {
                    string ownerList = string.Join(
                        ", ",
                        owners.Select(occurrence => $"{occurrence.Owner.name} ({occurrence.Owner.uuid})"));
                    errors.Add($"Node {node.name} ({node.uuid}) has {owners.Count} owning incoming references: {ownerList}. Declared parent: {declaredParent}.");
                }
                else if (owners.Count == 1 && declaredParent != owners[0].Owner.uuid)
                {
                    errors.Add($"Node {node.name} ({node.uuid}) declares parent {declaredParent}, but its owning incoming parent is {owners[0].Owner.name} ({owners[0].Owner.uuid}).");
                }
            }

            HashSet<UUID> visited = new();
            HashSet<UUID> active = new();
            bool hasCycle = false;
            foreach (TreeNode node in nodes)
            {
                if (HasCycle(node, visited, active))
                {
                    hasCycle = true;
                    break;
                }
            }

            if (hasCycle)
            {
                errors.Add("Node topology contains at least one authored ownership cycle.");
            }

            return errors;
        }

        private bool HasCycleFrom(TreeNode start)
        {
            return HasCycle(start, new HashSet<UUID>(), new HashSet<UUID>());
        }

        private bool HasCycle(TreeNode node, HashSet<UUID> visited, HashSet<UUID> active)
        {
            if (node == null)
            {
                return false;
            }

            if (active.Contains(node.uuid))
            {
                return true;
            }

            if (!visited.Add(node.uuid))
            {
                return false;
            }

            active.Add(node.uuid);
            foreach (NodeReferenceOccurrence occurrence in GetOutgoing(node))
            {
                if (HasCycle(occurrence.Target, visited, active))
                {
                    return true;
                }
            }

            active.Remove(node.uuid);
            return false;
        }

        private static void AddOccurrence(
            TreeNode owner,
            string fieldName,
            int index,
            INodeReference reference,
            IReadOnlyDictionary<UUID, TreeNode> byUUID,
            IDictionary<UUID, List<NodeReferenceOccurrence>> incoming,
            IDictionary<UUID, List<NodeReferenceOccurrence>> outgoing,
            IDictionary<UUID, int> rawIncoming)
        {
            if (fieldName == nameof(TreeNode.parent) || reference == null)
            {
                return;
            }

            if (!byUUID.TryGetValue(reference.UUID, out TreeNode target))
            {
                return;
            }

            if (reference.IsRawReference)
            {
                rawIncoming[target.uuid]++;
                return;
            }

            NodeOwnershipKind kind = fieldName == nameof(ServiceHostNode.services)
                ? NodeOwnershipKind.Service
                : NodeOwnershipKind.Structural;
            NodeReferenceOccurrence occurrence = new(owner, target, fieldName, index, kind);
            incoming[target.uuid].Add(occurrence);
            outgoing[owner.uuid].Add(occurrence);
        }
    }
}
