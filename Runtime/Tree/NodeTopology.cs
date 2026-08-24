using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
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

    /// <summary>Identifies one authored node-reference location.</summary>
    internal readonly struct NodeReferenceAddress
    {
        /// <summary>Creates an authored node-reference address.</summary>
        internal NodeReferenceAddress(UUID ownerUUID, string fieldName, int index)
        {
            OwnerUUID = ownerUUID;
            FieldName = fieldName ?? string.Empty;
            Index = index;
        }

        /// <summary>Gets the UUID of the node owning this location.</summary>
        internal UUID OwnerUUID { get; }

        /// <summary>Gets the authored field name containing this location.</summary>
        internal string FieldName { get; }

        /// <summary>Gets the scalar sentinel, collection occurrence, or insertion index.</summary>
        internal int Index { get; }
    }

    /// <summary>
    /// Immutable address and expected-value snapshot for one authored reference occurrence.
    /// </summary>
    internal readonly struct AuthoredReferenceSnapshot
    {
        /// <summary>Creates an authored reference occurrence snapshot.</summary>
        internal AuthoredReferenceSnapshot(
            NodeReferenceAddress address,
            UUID targetUUID,
            bool isNull)
        {
            Address = address;
            TargetUUID = targetUUID;
            IsNull = isNull;
        }

        /// <summary>Gets the authored location captured by this snapshot.</summary>
        internal NodeReferenceAddress Address { get; }

        /// <summary>Gets the expected authored target UUID.</summary>
        internal UUID TargetUUID { get; }

        /// <summary>Gets whether the authored reference object was null when captured.</summary>
        internal bool IsNull { get; }

        /// <summary>Gets whether this occurrence belongs to a collection.</summary>
        internal bool IsCollection => Address.Index >= 0;

        /// <summary>Gets whether this occurrence has no authored target UUID.</summary>
        internal bool IsEmpty => TargetUUID == UUID.Empty;

        /// <summary>Gets whether this occurrence represents removable authored data.</summary>
        internal bool HasRemovableValue => IsCollection || !IsEmpty;
    }

    /// <summary>Describes one authored owning reference occurrence.</summary>
    internal readonly struct NodeReferenceOccurrence
    {
        /// <summary>Creates one resolved owning occurrence.</summary>
        internal NodeReferenceOccurrence(
            TreeNode owner,
            TreeNode target,
            NodeReferenceAddress address,
            NodeOwnershipKind kind)
        {
            if (owner == null || address.OwnerUUID != owner.uuid)
            {
                throw new ArgumentException("The occurrence address must identify its owner.", nameof(address));
            }

            Owner = owner;
            Target = target;
            Address = address;
            Kind = kind;
        }

        internal TreeNode Owner { get; }
        internal TreeNode Target { get; }
        internal NodeReferenceAddress Address { get; }
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
                NodeTopologyVisitor visitor = new(
                    owner,
                    byUUID,
                    incoming,
                    outgoing,
                    rawIncoming);
                NodeDescriptorProvider.Get(owner.GetType()).VisitMembers(owner, visitor);
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

        /// <summary>Checks for cycles after ignoring one structural edge that will be removed atomically.</summary>
        internal bool WouldCreateCycleAfterRemovingOccurrence(
            TreeNode owner,
            TreeNode candidate,
            NodeReferenceOccurrence removedOccurrence)
        {
            if (owner == null || candidate == null || owner == candidate) return true;
            HashSet<UUID> visited = new();
            Stack<TreeNode> pending = new();
            pending.Push(candidate);
            while (pending.Count > 0)
            {
                TreeNode current = pending.Pop();
                if (current == null || !visited.Add(current.uuid)) continue;
                if (current == owner) return true;
                foreach (NodeReferenceOccurrence occurrence in GetOutgoing(current))
                {
                    // Ignore only the exact edge scheduled for removal; all other paths remain safety-checked.
                    if (removedOccurrence.Target != null
                        && occurrence.Owner.uuid == removedOccurrence.Owner.uuid
                        && occurrence.Address.FieldName == removedOccurrence.Address.FieldName
                        && occurrence.Address.Index == removedOccurrence.Address.Index
                        && occurrence.Target.uuid == removedOccurrence.Target.uuid) continue;
                    pending.Push(occurrence.Target);
                }
            }
            return HasCycleFrom(candidate, removedOccurrence);
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

        private bool HasCycleFrom(TreeNode start, NodeReferenceOccurrence ignoredOccurrence)
        {
            return HasCycleIgnoringOccurrence(
                start,
                new HashSet<UUID>(),
                new HashSet<UUID>(),
                ignoredOccurrence);
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

        private bool HasCycleIgnoringOccurrence(
            TreeNode node,
            HashSet<UUID> visited,
            HashSet<UUID> active,
            NodeReferenceOccurrence ignoredOccurrence)
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
                if (IsSameOccurrence(occurrence, ignoredOccurrence))
                {
                    continue;
                }

                if (HasCycleIgnoringOccurrence(occurrence.Target, visited, active, ignoredOccurrence))
                {
                    return true;
                }
            }

            active.Remove(node.uuid);
            return false;
        }

        private static bool IsSameOccurrence(
            NodeReferenceOccurrence left,
            NodeReferenceOccurrence right)
        {
            return left.Owner != null
                && right.Owner != null
                && left.Target != null
                && right.Target != null
                && left.Owner.uuid == right.Owner.uuid
                && left.Address.FieldName == right.Address.FieldName
                && left.Address.Index == right.Address.Index
                && left.Target.uuid == right.Target.uuid;
        }

        /// <summary>Collects resolved ownership occurrences for one source node.</summary>
        private sealed class NodeTopologyVisitor : NodeMemberVisitor
        {
            private readonly TreeNode owner;
            private readonly IReadOnlyDictionary<UUID, TreeNode> byUUID;
            private readonly IDictionary<UUID, List<NodeReferenceOccurrence>> incoming;
            private readonly IDictionary<UUID, List<NodeReferenceOccurrence>> outgoing;
            private readonly IDictionary<UUID, int> rawIncoming;

            public NodeTopologyVisitor(
                TreeNode owner,
                IReadOnlyDictionary<UUID, TreeNode> byUUID,
                IDictionary<UUID, List<NodeReferenceOccurrence>> incoming,
                IDictionary<UUID, List<NodeReferenceOccurrence>> outgoing,
                IDictionary<UUID, int> rawIncoming)
            {
                this.owner = owner;
                this.byUUID = byUUID;
                this.incoming = incoming;
                this.outgoing = outgoing;
                this.rawIncoming = rawIncoming;
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

                AddOccurrence(new NodeReferenceAddress(owner.uuid, rootName, index), reference);
            }

            protected override void OnVariableBinding(string path, IVariableBinding binding)
            {
            }

            /// <summary>Adds one resolved reference to the shared topology dictionaries.</summary>
            private void AddOccurrence(NodeReferenceAddress address, INodeReference reference)
            {
                if (address.FieldName == nameof(TreeNode.parent)
                    || reference == null
                    || !byUUID.TryGetValue(reference.UUID, out TreeNode target))
                {
                    return;
                }

                if (reference.IsRawReference)
                {
                    rawIncoming[target.uuid]++;
                    return;
                }

                NodeOwnershipKind kind = address.FieldName == nameof(ServiceHostNode.services)
                    ? NodeOwnershipKind.Service
                    : NodeOwnershipKind.Structural;
                NodeReferenceOccurrence occurrence = new(owner, target, address, kind);
                incoming[target.uuid].Add(occurrence);
                outgoing[owner.uuid].Add(occurrence);
            }
        }
    }
}
