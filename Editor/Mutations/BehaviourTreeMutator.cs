using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Editor.Exporting;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Randomization;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Mutations
{
    // Unity owns generated assembly outputs; this editor API remains source-only.
    /// <summary>Provides legacy single-step editor mutations for behaviour-tree assets.</summary>
    /// <remarks>Use <see cref="BehaviourTreeEditTransaction"/> for new multi-step authoring workflows.</remarks>
    public static class BehaviourTreeMutator
    {
        private const string DeprecatedMessage = "Use BehaviourTreeEditTransaction for new behaviour-tree authoring workflows.";

        /// <summary>Creates and attaches one default node, then saves the asset.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="request">The type and attachment request.</param>
        /// <returns>A mutation result describing the created node or the validation error.</returns>
        [Obsolete(DeprecatedMessage, false)]
        public static BehaviourTreeAddResult AddNode(BehaviourTreeData tree, BehaviourTreeAddRequest request)
        {
            return AddNode(tree, request, save: true);
        }

        /// <summary>Creates and attaches one node with optional persistence for a transaction owner.</summary>
        internal static BehaviourTreeAddResult AddNode(
            BehaviourTreeData tree,
            BehaviourTreeAddRequest request,
            bool save)
        {
            if (tree == null)
            {
                return Failure<BehaviourTreeAddResult>("Behaviour tree is null.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                return Failure<BehaviourTreeAddResult>("A concrete node type is required.");
            }

            if (!TryResolveCreatableType(request.Type, out Type nodeType, out string typeError))
            {
                return Failure<BehaviourTreeAddResult>(typeError);
            }

            TreeNode node;
            try
            {
                node = NodeFactory.Create(nodeType);
            }
            catch (Exception exception)
            {
                return Failure<BehaviourTreeAddResult>($"Unable to create node type '{request.Type}': {exception.Message}");
            }

            node.name = string.IsNullOrWhiteSpace(request.Name)
                ? tree.GenerateNewNodeName(node)
                : request.Name;

            if (!TryValidateAttachment(tree, node, request, out TreeNode owner, out INodeReferenceListSlot collection, out string attachmentError))
            {
                return Failure<BehaviourTreeAddResult>(attachmentError);
            }

            string undoName = $"Add AI graph node {node.name}";
            bool changed;
            try
            {
                if (owner == null)
                {
                    changed = tree.TryAddAndSetHead(
                        new[] { node },
                        node.uuid,
                        undoName);
                }
                else if (collection != null)
                {
                    changed = tree.TryAddAndInsertReference(
                        new NodeReferenceAddress(owner.uuid, request.Field, request.Index),
                        new[] { node },
                        node.uuid,
                        undoName);
                }
                else
                {
                    changed = tree.TryAddAndSetReference(
                        new NodeReferenceAddress(owner.uuid, request.Field, -1),
                        new[] { node },
                        node.uuid,
                        undoName);
                }
            }
            catch (Exception exception)
            {
                return Failure<BehaviourTreeAddResult>($"Unable to attach node '{node.name}': {exception.Message}");
            }

            if (!changed)
            {
                return Failure<BehaviourTreeAddResult>($"The requested attachment for node type '{nodeType.Name}' is invalid.");
            }

            BehaviourTreeAddResult result = CompleteSuccess<BehaviourTreeAddResult>(tree, save);
            if (!result.Success)
            {
                return result;
            }

            result.CreatedNodeId = node.uuid;
            result.CreatedNodeName = node.name;
            result.CreatedNodeType = node.GetType().Name;
            result.Location = CreateLocation(owner == null
                ? BehaviourTreeNodeLocationKind.Head
                : BehaviourTreeNodeLocationKind.Reference,
                owner?.uuid ?? UUID.Empty,
                owner == null ? null : request.Field,
                owner == null ? -1 : ResolveCommittedIndex(owner, request.Field, node, request.Index));
            return result;
        }

        /// <summary>Deletes selected nodes using the same decorator-unwrapping semantics as the Graph editor.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="nodeIds">The authored node UUIDs selected for deletion.</param>
        /// <returns>A mutation result describing the deletion or the validation error.</returns>
        [Obsolete(DeprecatedMessage, false)]
        public static BehaviourTreeRemoveResult RemoveNodes(BehaviourTreeData tree, IReadOnlyList<UUID> nodeIds)
        {
            return RemoveNodes(tree, nodeIds, save: true);
        }

        /// <summary>Deletes selected nodes with optional persistence for a transaction owner.</summary>
        internal static BehaviourTreeRemoveResult RemoveNodes(
            BehaviourTreeData tree,
            IReadOnlyList<UUID> nodeIds,
            bool save)
        {
            if (tree == null)
            {
                return Failure<BehaviourTreeRemoveResult>("Behaviour tree is null.");
            }

            if (nodeIds == null || nodeIds.Count == 0 || nodeIds.Any(uuid => uuid == UUID.Empty))
            {
                return Failure<BehaviourTreeRemoveResult>("At least one non-empty node UUID is required.");
            }

            UUID[] distinctIds = nodeIds.Distinct().ToArray();
            List<TreeNode> nodes = new();
            foreach (UUID id in distinctIds)
            {
                TreeNode node = tree.GetNode(id);
                if (node == null)
                {
                    return Failure<BehaviourTreeRemoveResult>($"Node '{id}' was not found in the behaviour tree.");
                }

                nodes.Add(node);
            }

            IReadOnlyList<string> topologyErrors = tree.GetStructureValidationErrors();
            if (topologyErrors.Count > 0)
            {
                return Failure<BehaviourTreeRemoveResult>("Cannot delete nodes from an invalid structural topology: "
                    + string.Join(" | ", topologyErrors));
            }

            string undoName = distinctIds.Length == 1
                ? $"Delete AI graph node {nodes[0].name}"
                : $"Delete {distinctIds.Length} AI graph nodes";
            bool changed;
            try
            {
                changed = tree.TryDeleteNodesWithDecoratorUnwrap(distinctIds.ToHashSet(), undoName);
            }
            catch (Exception exception)
            {
                return Failure<BehaviourTreeRemoveResult>($"Unable to delete selected nodes: {exception.Message}");
            }

            if (!changed)
            {
                return Failure<BehaviourTreeRemoveResult>("The selected nodes could not be deleted.");
            }

            BehaviourTreeRemoveResult result = CompleteSuccess<BehaviourTreeRemoveResult>(tree, save);
            if (result.Success)
            {
                result.RemovedNodeIds = distinctIds;
            }

            return result;
        }

        /// <summary>Reorders a node within its authored reference collection.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="request">The node and destination collection index.</param>
        /// <returns>A mutation result describing the reordered node.</returns>
        [Obsolete(DeprecatedMessage, false)]
        public static BehaviourTreeRearrangeResult ReorderNode(BehaviourTreeData tree, BehaviourTreeReorderRequest request)
        {
            return ReorderNode(tree, request, save: true);
        }

        /// <summary>Reorders one node with optional persistence for a transaction owner.</summary>
        internal static BehaviourTreeRearrangeResult ReorderNode(
            BehaviourTreeData tree,
            BehaviourTreeReorderRequest request,
            bool save)
        {
            if (!TryGetNodeAndIncoming(tree, request?.NodeId ?? UUID.Empty, out TreeNode node, out NodeReferenceOccurrence occurrence, out string error))
            {
                return Failure<BehaviourTreeRearrangeResult>(error);
            }

            if (request.Index < 0
                || !NodeReferenceStructureProvider.GetListSlots(occurrence.Owner)
                    .Any(slot => string.Equals(slot.Name, occurrence.Address.FieldName, StringComparison.Ordinal)))
            {
                return Failure<BehaviourTreeRearrangeResult>("reorder requires a non-negative index in the node's owning collection.");
            }

            INodeReferenceListSlot collection = NodeReferenceStructureProvider.GetListSlots(occurrence.Owner)
                .First(slot => string.Equals(slot.Name, occurrence.Address.FieldName, StringComparison.Ordinal));
            if (request.Index >= collection.Count)
            {
                return Failure<BehaviourTreeRearrangeResult>($"index {request.Index} is outside collection '{occurrence.Address.FieldName}'.");
            }

            if (request.Index == occurrence.Address.Index)
            {
                return Failure<BehaviourTreeRearrangeResult>("The node is already at the requested collection index.");
            }

            bool changed;
            try
            {
                changed = tree.TryReorderReference(
                    occurrence.Address,
                    request.Index,
                    $"Reorder AI graph node {node.name}");
            }
            catch (Exception exception)
            {
                return Failure<BehaviourTreeRearrangeResult>($"Unable to reorder node '{node.name}': {exception.Message}");
            }

            return changed
                ? SaveRearrangement(
                    tree,
                    node,
                    occurrence,
                    occurrence.Owner,
                    occurrence.Address.FieldName,
                    request.Index,
                    save)
                : Failure<BehaviourTreeRearrangeResult>($"Unable to reorder node '{node.name}'.");
        }

        /// <summary>Moves a node to a different structural or Service reference slot.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="request">The node and destination slot.</param>
        /// <returns>A mutation result describing the moved node.</returns>
        [Obsolete(DeprecatedMessage, false)]
        public static BehaviourTreeRearrangeResult MoveNode(BehaviourTreeData tree, BehaviourTreeMoveRequest request)
        {
            return MoveNode(tree, request, save: true);
        }

        /// <summary>Moves one node with optional persistence for a transaction owner.</summary>
        internal static BehaviourTreeRearrangeResult MoveNode(
            BehaviourTreeData tree,
            BehaviourTreeMoveRequest request,
            bool save)
        {
            if (!TryGetNode(tree, request?.NodeId ?? UUID.Empty, out TreeNode node, out string error))
            {
                return Failure<BehaviourTreeRearrangeResult>(error);
            }

            if (request.TargetParent == UUID.Empty || string.IsNullOrWhiteSpace(request.Field))
            {
                return Failure<BehaviourTreeRearrangeResult>("target_parent and field are required when moving a node.");
            }

            TreeNode targetParent = tree.GetNode(request.TargetParent);
            if (targetParent == null)
            {
                return Failure<BehaviourTreeRearrangeResult>($"Target parent '{request.TargetParent}' was not found in the behaviour tree.");
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(tree.EditorNodes);
            if (targetParent == node || topology.WouldCreateCycle(targetParent, node))
            {
                return Failure<BehaviourTreeRearrangeResult>("The requested move would create a cycle.");
            }

            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(node);
            if (incoming.Count != 1 || topology.HasInvalidParentMetadata(node))
            {
                return Failure<BehaviourTreeRearrangeResult>($"Node '{node.name}' must have exactly one valid structural or Service owner.");
            }

            NodeReferenceOccurrence sourceOccurrence = incoming.SingleOrDefault();
            if (!TryResolveDestination(targetParent, request.Field, request.Index, out INodeReferenceListSlot collection, out string destinationError))
            {
                return Failure<BehaviourTreeRearrangeResult>(destinationError);
            }

            bool changed;
            try
            {
                changed = collection != null
                    ? tree.TryInsertReference(
                        new NodeReferenceAddress(targetParent.uuid, request.Field, request.Index),
                        node.uuid,
                        true,
                        $"Move AI graph node {node.name}")
                    : tree.TrySetReference(
                        new NodeReferenceAddress(targetParent.uuid, request.Field, -1),
                        node.uuid,
                        true,
                        $"Move AI graph node {node.name}");
            }
            catch (Exception exception)
            {
                return Failure<BehaviourTreeRearrangeResult>($"Unable to move node '{node.name}': {exception.Message}");
            }

            return changed
                ? SaveRearrangement(tree, node, sourceOccurrence, targetParent, request.Field, request.Index, save)
                : Failure<BehaviourTreeRearrangeResult>($"Unable to move node '{node.name}' to '{targetParent.name}.{request.Field}'.");
        }

        /// <summary>Detaches a uniquely-owned node while keeping it in the authored node list.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="nodeId">The authored UUID to detach.</param>
        /// <returns>A mutation result describing the detached node.</returns>
        [Obsolete(DeprecatedMessage, false)]
        public static BehaviourTreeRearrangeResult DetachNode(BehaviourTreeData tree, UUID nodeId)
        {
            return DetachNode(tree, nodeId, save: true);
        }

        /// <summary>Detaches one node with optional persistence for a transaction owner.</summary>
        internal static BehaviourTreeRearrangeResult DetachNode(BehaviourTreeData tree, UUID nodeId, bool save)
        {
            if (!TryGetNodeAndIncoming(tree, nodeId, out TreeNode node, out NodeReferenceOccurrence occurrence, out string error))
            {
                return Failure<BehaviourTreeRearrangeResult>(error);
            }

            bool changed;
            try
            {
                changed = tree.TryDetachTarget(node.uuid, $"Detach AI graph node {node.name}");
            }
            catch (Exception exception)
            {
                return Failure<BehaviourTreeRearrangeResult>($"Unable to detach node '{node.name}': {exception.Message}");
            }

            return changed
                ? SaveRearrangement(tree, node, occurrence, null, null, -1, save)
                : Failure<BehaviourTreeRearrangeResult>($"Unable to detach node '{node.name}'.");
        }

        /// <summary>Moves an existing node to the tree Head.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="nodeId">The authored UUID to make Head.</param>
        /// <returns>A mutation result describing the new Head.</returns>
        [Obsolete(DeprecatedMessage, false)]
        public static BehaviourTreeRearrangeResult SetHead(BehaviourTreeData tree, UUID nodeId)
        {
            return SetHead(tree, nodeId, save: true);
        }

        /// <summary>Sets the Head with optional persistence for a transaction owner.</summary>
        internal static BehaviourTreeRearrangeResult SetHead(BehaviourTreeData tree, UUID nodeId, bool save)
        {
            if (!TryGetNode(tree, nodeId, out TreeNode node, out string error))
            {
                return Failure<BehaviourTreeRearrangeResult>(error);
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(tree.EditorNodes);
            NodeReferenceOccurrence sourceOccurrence = topology.GetIncoming(node).SingleOrDefault();
            if (node is Service)
            {
                return Failure<BehaviourTreeRearrangeResult>("A Service cannot become the tree Head.");
            }

            if (!tree.CanSetHead(node.uuid, allowMoveExisting: true))
            {
                return Failure<BehaviourTreeRearrangeResult>($"Node '{node.name}' cannot become the tree Head.");
            }

            bool changed;
            try
            {
                changed = tree.TryMoveToHead(node.uuid, $"Set AI graph Head to {node.name}");
            }
            catch (Exception exception)
            {
                return Failure<BehaviourTreeRearrangeResult>($"Unable to set node '{node.name}' as Head: {exception.Message}");
            }

            return changed
                ? SaveRearrangement(tree, node, sourceOccurrence, null, "$head", -1, save)
                : Failure<BehaviourTreeRearrangeResult>($"Unable to set node '{node.name}' as Head.");
        }

        /// <summary>Resolves one authored node for a topology mutation.</summary>
        private static bool TryGetNode(BehaviourTreeData tree, UUID nodeId, out TreeNode node, out string error)
        {
            node = null;
            if (tree == null)
            {
                error = "Behaviour tree is null.";
                return false;
            }

            if (nodeId == UUID.Empty || (node = tree.GetNode(nodeId)) == null)
            {
                error = $"Node '{nodeId}' was not found in the behaviour tree.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Resolves a node and its unique structural or Service owner.</summary>
        private static bool TryGetNodeAndIncoming(BehaviourTreeData tree, UUID nodeId, out TreeNode node, out NodeReferenceOccurrence occurrence, out string error)
        {
            occurrence = default;
            if (!TryGetNode(tree, nodeId, out node, out error))
            {
                return false;
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(tree.EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(node);
            if (incoming.Count != 1 || topology.HasInvalidParentMetadata(node))
            {
                error = $"Node '{node.name}' must have exactly one valid structural or Service owner.";
                return false;
            }

            occurrence = incoming[0];
            return true;
        }

        /// <summary>Validates and resolves a destination scalar or collection slot.</summary>
        private static bool TryResolveDestination(TreeNode targetParent, string fieldName, int index, out INodeReferenceListSlot collection, out string error)
        {
            collection = NodeReferenceStructureProvider.GetListSlots(targetParent)
                .FirstOrDefault(slot => string.Equals(slot.Name, fieldName, StringComparison.Ordinal));
            if (collection != null)
            {
                if (index < -1 || index > collection.Count)
                {
                    error = $"index {index} is outside collection '{fieldName}' on '{targetParent.name}'.";
                    return false;
                }

                error = null;
                return true;
            }

            if (index >= 0)
            {
                error = $"field '{fieldName}' is not a collection and cannot use index {index}.";
                return false;
            }

            if (!NodeReferenceStructureProvider.TryGetReference(targetParent, fieldName, out INodeReference reference))
            {
                error = $"Reference field '{fieldName}' was not found on '{targetParent.GetType().Name}'.";
                return false;
            }

            if (reference?.IsRawReference == true)
            {
                error = $"Raw reference field '{fieldName}' cannot change node ownership.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Saves a successful rearrangement and records its source and destination.</summary>
        private static BehaviourTreeRearrangeResult SaveRearrangement(
            BehaviourTreeData tree,
            TreeNode node,
            NodeReferenceOccurrence source,
            TreeNode targetParent,
            string targetField,
            int targetIndex,
            bool save)
        {
            BehaviourTreeRearrangeResult result = CompleteSuccess<BehaviourTreeRearrangeResult>(tree, save);
            if (!result.Success)
            {
                return result;
            }

            result.NodeId = node.uuid;
            result.Source = source.Owner == null
                ? CreateLocation(BehaviourTreeNodeLocationKind.Detached, UUID.Empty, null, -1)
                : CreateLocation(
                    BehaviourTreeNodeLocationKind.Reference,
                    source.Owner.uuid,
                    source.Address.FieldName,
                    source.Address.Index);
            BehaviourTreeNodeLocationKind destinationKind = targetParent != null
                ? BehaviourTreeNodeLocationKind.Reference
                : string.Equals(targetField, "$head", StringComparison.Ordinal)
                    ? BehaviourTreeNodeLocationKind.Head
                    : BehaviourTreeNodeLocationKind.Detached;
            result.Destination = CreateLocation(
                destinationKind,
                targetParent?.uuid ?? UUID.Empty,
                targetParent == null && destinationKind == BehaviourTreeNodeLocationKind.Head ? null : targetField,
                targetParent == null && destinationKind != BehaviourTreeNodeLocationKind.Reference
                    ? -1
                    : ResolveCommittedIndex(targetParent, targetField, node, targetIndex));
            return result;
        }

        /// <summary>Creates a stable node-location DTO for a mutation result.</summary>
        private static BehaviourTreeNodeLocation CreateLocation(
            BehaviourTreeNodeLocationKind kind,
            UUID ownerNodeId,
            string field,
            int index)
        {
            return new BehaviourTreeNodeLocation
            {
                Kind = kind,
                OwnerNodeId = ownerNodeId,
                Field = field,
                Index = index,
            };
        }

        /// <summary>Resolves the authored destination index after a collection mutation.</summary>
        private static int ResolveCommittedIndex(TreeNode targetParent, string targetField, TreeNode node, int requestedIndex)
        {
            if (targetParent == null || string.IsNullOrWhiteSpace(targetField) || requestedIndex >= 0)
            {
                return requestedIndex;
            }

            INodeReferenceListSlot collection = NodeReferenceStructureProvider.GetListSlots(targetParent)
                .FirstOrDefault(slot => string.Equals(slot.Name, targetField, StringComparison.Ordinal));
            return collection?.IndexOf(node) ?? requestedIndex;
        }

        private static bool TryValidateAttachment(BehaviourTreeData tree, TreeNode node, BehaviourTreeAddRequest request, out TreeNode owner, out INodeReferenceListSlot collection, out string error)
        {
            owner = null;
            collection = null;
            error = null;

            bool hasParent = request.ParentNode != UUID.Empty;
            bool hasField = !string.IsNullOrWhiteSpace(request.Field);
            if (!hasParent && !hasField)
            {
                if (request.Index >= 0)
                {
                    error = "index cannot be supplied when adding a new Head.";
                    return false;
                }

                if (tree.headNodeUUID != UUID.Empty && tree.GetNode(tree.headNodeUUID) != null)
                {
                    error = "The behaviour tree already has a Head; specify parent_node and field to attach another node.";
                    return false;
                }

                if (node is Service)
                {
                    error = "A Service cannot be added as the tree Head.";
                    return false;
                }

                return true;
            }

            if (!hasParent || !hasField)
            {
                error = "parent_node and field must be supplied together, or both omitted for a new Head.";
                return false;
            }

            owner = tree.GetNode(request.ParentNode);
            if (owner == null)
            {
                error = $"Parent node '{request.ParentNode}' was not found in the behaviour tree.";
                return false;
            }

            if (request.Index < -1)
            {
                error = "index must be -1 or a non-negative collection position.";
                return false;
            }

            collection = NodeReferenceStructureProvider.GetListSlots(owner)
                .FirstOrDefault(slot => string.Equals(slot.Name, request.Field, StringComparison.Ordinal));
            if (collection != null)
            {
                if (request.Index > collection.Count)
                {
                    error = $"index {request.Index} is outside collection '{request.Field}' on '{owner.name}'.";
                    return false;
                }

                return true;
            }

            if (request.Index >= 0)
            {
                error = $"field '{request.Field}' is not a collection and cannot use index {request.Index}.";
                return false;
            }

            if (!NodeReferenceStructureProvider.TryGetReference(owner, request.Field, out _))
            {
                error = $"Reference field '{request.Field}' was not found on '{owner.GetType().Name}'.";
                return false;
            }

            return true;
        }

        private static bool TryResolveCreatableType(string value, out Type result, out string error)
        {
            result = null;
            error = null;
            string candidate = value.Trim();
            IReadOnlyList<Type> types = NodeMenuCache.Shared.AllNodeTypes;
            Type[] matches = types
                .Where(type => string.Equals(type.Name, candidate, StringComparison.Ordinal)
                    || string.Equals(type.FullName, candidate, StringComparison.Ordinal)
                    || string.Equals(type.AssemblyQualifiedName, candidate, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                error = $"Creatable node type '{value}' was not found. Use a concrete editor node short name or clrType.";
                return false;
            }

            if (matches.Length > 1)
            {
                error = $"Node type '{value}' is ambiguous; use its full clrType.";
                return false;
            }

            result = matches[0];
            return true;
        }

        private static T CompleteSuccess<T>(BehaviourTreeData tree, bool save)
            where T : BehaviourTreeMutationResult, new()
        {
            if (!save)
            {
                return new T
                {
                    Success = true,
                    Saved = false,
                    HeadNodeId = tree.headNodeUUID,
                    Diagnostics = Array.Empty<string>(),
                };
            }

            try
            {
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                // The existing editor mutation APIs have already recorded one Undo group;
                // use it to restore the in-memory graph when persistence fails.
                try
                {
                    Undo.PerformUndo();
                    tree.RegenerateTable();
                }
                catch (Exception rollbackException)
                {
                    return Failure<T>($"Mutation and rollback both failed. Save error: {exception.Message}; rollback error: {rollbackException.Message}");
                }

                return Failure<T>($"Mutation succeeded in memory but AssetDatabase.SaveAssets failed: {exception.Message}");
            }

            return new T
            {
                Success = true,
                Saved = true,
                HeadNodeId = tree.headNodeUUID,
                Diagnostics = Array.Empty<string>(),
            };
        }

        private static T Failure<T>(string message) where T : BehaviourTreeMutationResult, new()
        {
            return new T
            {
                Success = false,
                Error = message ?? "Behaviour-tree mutation failed.",
            };
        }
    }

    /// <summary>Coordinates one unsaved sequence of behaviour-tree edits.</summary>
    public sealed class BehaviourTreeEditContext
    {
        private readonly BehaviourTreeData tree;
        private readonly string undoName;
        private string failure;

        internal BehaviourTreeEditContext(BehaviourTreeData tree, string undoName)
        {
            this.tree = tree;
            this.undoName = undoName;
        }

        internal string Failure => failure;

        /// <summary>Creates and attaches one node without saving the asset.</summary>
        public BehaviourTreeAddResult AddNode(BehaviourTreeAddRequest request)
        {
            return Apply(() => BehaviourTreeMutator.AddNode(tree, request, save: false));
        }

        /// <summary>Removes selected nodes without saving the asset.</summary>
        public BehaviourTreeRemoveResult RemoveNodes(IReadOnlyList<UUID> nodeIds)
        {
            return Apply(() => BehaviourTreeMutator.RemoveNodes(tree, nodeIds, save: false));
        }

        /// <summary>Reorders one node without saving the asset.</summary>
        public BehaviourTreeRearrangeResult ReorderNode(BehaviourTreeReorderRequest request)
        {
            return Apply(() => BehaviourTreeMutator.ReorderNode(tree, request, save: false));
        }

        /// <summary>Moves one node without saving the asset.</summary>
        public BehaviourTreeRearrangeResult MoveNode(BehaviourTreeMoveRequest request)
        {
            return Apply(() => BehaviourTreeMutator.MoveNode(tree, request, save: false));
        }

        /// <summary>Detaches one node without saving the asset.</summary>
        public BehaviourTreeRearrangeResult DetachNode(UUID nodeId)
        {
            return Apply(() => BehaviourTreeMutator.DetachNode(tree, nodeId, save: false));
        }

        /// <summary>Moves one existing node to Head without saving the asset.</summary>
        public BehaviourTreeRearrangeResult SetHead(UUID nodeId)
        {
            return Apply(() => BehaviourTreeMutator.SetHead(tree, nodeId, save: false));
        }

        /// <summary>Updates non-identity authored fields on one existing node.</summary>
        public void EditNode<TNode>(UUID nodeId, Action<TNode> edit) where TNode : TreeNode
        {
            EnsureUsable();
            if (edit == null)
            {
                Fail("A node edit callback is required.");
                return;
            }

            TNode node = tree.GetNode(nodeId) as TNode;
            if (node == null)
            {
                Fail($"Node '{nodeId}' was not found as {typeof(TNode).Name}.");
                return;
            }

            UUID[] identities = SnapshotNodeIdentities();
            UUID head = tree.headNodeUUID;
            string[] topology = SnapshotOwnershipTopology();
            Undo.RecordObject(tree, undoName);
            edit(node);
            EnsureTopologyIdentityUnchanged(identities, head, topology);
            tree.RegenerateTable();
            EditorUtility.SetDirty(tree);
        }

        /// <summary>Updates tree-level authored settings without exposing the serialized object or node collection.</summary>
        public void EditSettings(Action<BehaviourTreeSettingsEditContext> edit)
        {
            EnsureUsable();
            if (edit == null)
            {
                Fail("A tree settings edit callback is required.");
                return;
            }

            BehaviourTreeSettingsEditContext settings = new BehaviourTreeSettingsEditContext(tree);
            Undo.RecordObject(tree, undoName);
            edit(settings);
            settings.Apply();
            EditorUtility.SetDirty(tree);
        }

        /// <summary>Creates one authored variable with a fresh UUID without saving the asset.</summary>
        public VariableData CreateVariable(VariableType variableType, string name = null)
        {
            EnsureUsable();
            string effectiveName = string.IsNullOrWhiteSpace(name)
                ? tree.GenerateNewVariableName(variableType.ToString())
                : tree.GenerateNewVariableName(name.Trim());
            Undo.RecordObject(tree, undoName);
            VariableData variable = tree.CreateNewVariable(variableType, effectiveName);
            tree.SerializedObject.Update();
            EditorUtility.SetDirty(tree);
            return variable;
        }

        /// <summary>Edits one authored variable while preserving its UUID.</summary>
        public void EditVariable(UUID variableId, Action<VariableData> edit)
        {
            EnsureUsable();
            if (edit == null)
            {
                Fail("A variable edit callback is required.");
                return;
            }

            VariableData variable = tree.GetVariable(variableId);
            if (variable == null || variable.IsStandardVariable)
            {
                Fail($"Variable '{variableId}' was not found as an authored tree variable.");
                return;
            }

            UUID identity = variable.UUID;
            Undo.RecordObject(tree, undoName);
            edit(variable);
            if (variable.UUID != identity)
            {
                Fail("Variable edit callbacks cannot change variable identity.");
            }

            tree.SerializedObject.Update();
            EditorUtility.SetDirty(tree);
        }

        /// <summary>Removes one authored variable without saving the asset.</summary>
        public void RemoveVariable(UUID variableId)
        {
            EnsureUsable();
            int index = FindAuthoredVariableIndex(variableId);
            if (index < 0)
            {
                Fail($"Variable '{variableId}' was not found as an authored tree variable.");
                return;
            }

            Undo.RecordObject(tree, undoName);
            tree.variables.RemoveAt(index);
            tree.SerializedObject.Update();
            EditorUtility.SetDirty(tree);
        }

        /// <summary>Moves one authored variable to a new authored-list index.</summary>
        public void ReorderVariable(UUID variableId, int index)
        {
            EnsureUsable();
            int sourceIndex = FindAuthoredVariableIndex(variableId);
            if (sourceIndex < 0)
            {
                Fail($"Variable '{variableId}' was not found as an authored tree variable.");
                return;
            }

            if (index < 0 || index >= tree.variables.Count)
            {
                Fail($"Variable index {index} is outside the authored variable collection.");
                return;
            }

            if (sourceIndex == index)
            {
                Fail("The variable is already at the requested authored-list index.");
                return;
            }

            Undo.RecordObject(tree, undoName);
            VariableData variable = tree.variables[sourceIndex];
            tree.variables.RemoveAt(sourceIndex);
            tree.variables.Insert(index, variable);
            tree.SerializedObject.Update();
            EditorUtility.SetDirty(tree);
        }

        private int FindAuthoredVariableIndex(UUID variableId)
        {
            for (int index = 0; index < tree.variables.Count; index++)
            {
                VariableData variable = tree.variables[index];
                if (variable != null && variable.UUID == variableId && !variable.IsStandardVariable)
                {
                    return index;
                }
            }

            return -1;
        }

        private TResult Apply<TResult>(Func<TResult> operation) where TResult : BehaviourTreeMutationResult
        {
            EnsureUsable();
            TResult result = operation();
            if (result == null || !result.Success)
            {
                Fail(result?.Error ?? "Behaviour-tree edit returned no result.");
            }

            return result;
        }

        private UUID[] SnapshotNodeIdentities()
        {
            return tree.EditorNodes.Select(node => node?.uuid ?? UUID.Empty).ToArray();
        }

        private string[] SnapshotOwnershipTopology()
        {
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(tree.EditorNodes);
            List<string> result = new List<string>();
            foreach (TreeNode node in tree.EditorNodes)
            {
                if (node == null)
                {
                    continue;
                }

                result.Add($"node:{node.uuid}:parent:{node.parent?.UUID ?? UUID.Empty}");
                result.AddRange(topology.GetOutgoing(node).Select(occurrence =>
                    $"edge:{occurrence.Owner.uuid}:{occurrence.Address.FieldName}:{occurrence.Address.Index}:{occurrence.Target.uuid}:{occurrence.Kind}"));
            }

            return result.ToArray();
        }

        private void EnsureTopologyIdentityUnchanged(
            IReadOnlyList<UUID> identities,
            UUID head,
            IReadOnlyList<string> topology)
        {
            UUID[] current = SnapshotNodeIdentities();
            string[] currentTopology = SnapshotOwnershipTopology();
            if (!identities.SequenceEqual(current)
                || tree.headNodeUUID != head
                || !topology.SequenceEqual(currentTopology))
            {
                Fail("Authored-field callbacks cannot change node identity, Head, parent metadata, or structural ownership; use the topology operations on this context.");
            }
        }

        private void EnsureUsable()
        {
            if (!string.IsNullOrEmpty(failure))
            {
                throw new InvalidOperationException(failure);
            }
        }

        private void Fail(string message)
        {
            failure ??= message ?? "Behaviour-tree edit failed.";
            throw new InvalidOperationException(failure);
        }
    }

    /// <summary>Typed authored settings that may be changed by a safe edit transaction.</summary>
    public sealed class BehaviourTreeSettingsEditContext
    {
        private readonly BehaviourTreeData tree;

        internal BehaviourTreeSettingsEditContext(BehaviourTreeData tree)
        {
            this.tree = tree;
            NoActionMaximumDurationLimit = tree.noActionMaximumDurationLimit;
            ActionMaximumDuration = tree.actionMaximumDuration;
            TreeErrorHandle = tree.treeErrorHandle;
            NodeErrorHandle = tree.nodeErrorHandle;
            RandomSource = tree.randomSource;
            ArithmeticMode = tree.arithmeticMode;
            TargetScript = tree.targetScript;
            Prefab = tree.prefab;
            BaseAnimatorController = tree.BaseAnimatorController;
        }

        /// <summary>Whether the action execution time limit is disabled.</summary>
        public bool NoActionMaximumDurationLimit { get; set; }

        /// <summary>Maximum action execution time when the limit is enabled.</summary>
        public float ActionMaximumDuration { get; set; }

        /// <summary>Tree-level exception handling policy.</summary>
        public BehaviourTreeErrorSolution TreeErrorHandle { get; set; }

        /// <summary>Node-level exception handling policy.</summary>
        public NodeErrorSolution NodeErrorHandle { get; set; }

        /// <summary>Authored random-source binding.</summary>
        public RandomSourceBinding RandomSource { get; set; }

        /// <summary>Authored arithmetic mode.</summary>
        public ArithmeticMode ArithmeticMode { get; set; }

        /// <summary>Optional target script used for tree-facing variables and calls.</summary>
        public MonoScript TargetScript { get; set; }

        /// <summary>Optional prefab associated with the behaviour tree.</summary>
        public GameObject Prefab { get; set; }

        /// <summary>Optional authored base animator controller.</summary>
        public RuntimeAnimatorController BaseAnimatorController { get; set; }

        internal void Apply()
        {
            tree.noActionMaximumDurationLimit = NoActionMaximumDurationLimit;
            tree.actionMaximumDuration = ActionMaximumDuration;
            tree.treeErrorHandle = TreeErrorHandle;
            tree.nodeErrorHandle = NodeErrorHandle;
            tree.randomSource = RandomSource;
            tree.arithmeticMode = ArithmeticMode;
            tree.targetScript = TargetScript;
            tree.prefab = Prefab;
            tree.BaseAnimatorController = BaseAnimatorController;
            tree.SerializedObject.Update();
        }
    }

    /// <summary>Executes a complete behaviour-tree edit with one validation and save boundary.</summary>
    public static class BehaviourTreeEditTransaction
    {
        /// <summary>Executes all edits in one Undo group and saves only after final validation succeeds.</summary>
        public static BehaviourTreeEditResult Execute(
            BehaviourTreeData tree,
            string undoName,
            Action<BehaviourTreeEditContext> edit)
        {
            return Execute(tree, undoName, edit, AssetDatabase.SaveAssets);
        }

        /// <summary>Executes a transaction with an injectable save boundary for focused editor tests.</summary>
        internal static BehaviourTreeEditResult Execute(
            BehaviourTreeData tree,
            string undoName,
            Action<BehaviourTreeEditContext> edit,
            System.Action saveAssets)
        {
            if (tree == null)
            {
                return Failure("Behaviour tree is null.");
            }

            if (edit == null)
            {
                return Failure("An edit callback is required.");
            }

            if (saveAssets == null)
            {
                return Failure("A save callback is required.");
            }

            try
            {
                tree.RegenerateTable();
                IReadOnlyList<string> initialStructureErrors = tree.GetStructureValidationErrors();
                if (initialStructureErrors.Count > 0)
                {
                    return Failure("Cannot start a behaviour-tree edit from invalid structural topology: "
                        + string.Join(" | ", initialStructureErrors));
                }
            }
            catch (Exception exception)
            {
                return Failure("Cannot start a behaviour-tree edit because node identity could not be resolved: "
                    + exception.Message);
            }

            string effectiveUndoName = string.IsNullOrWhiteSpace(undoName)
                ? "Edit Aethiumian behaviour tree"
                : undoName.Trim();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(effectiveUndoName);
            BehaviourTreeEditContext context = new BehaviourTreeEditContext(tree, effectiveUndoName);

            try
            {
                edit(context);
                if (!string.IsNullOrEmpty(context.Failure))
                {
                    throw new InvalidOperationException(context.Failure);
                }

                tree.RegenerateTable();
                string[] validationErrors = GetValidationErrors(tree);
                if (validationErrors.Length > 0)
                {
                    return Rollback(tree, undoGroup, "Final behaviour-tree validation failed: " + string.Join(" | ", validationErrors));
                }

                saveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return new BehaviourTreeEditResult
                {
                    Success = true,
                    Saved = true,
                    HeadNodeId = tree.headNodeUUID,
                    Diagnostics = Array.Empty<string>(),
                };
            }
            catch (Exception exception)
            {
                return Rollback(tree, undoGroup, exception.Message);
            }
        }

        internal static string[] GetValidationErrors(BehaviourTreeData tree)
        {
            BehaviourTreeValidationResult validation = BehaviourTreeDomInspector.Validate(tree);
            return validation.Diagnostics
                .Where(diagnostic => diagnostic.Severity == BehaviourTreeDomDiagnosticSeverity.Error)
                .Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        internal static BehaviourTreeEditResult Rollback(BehaviourTreeData tree, int undoGroup, string error)
        {
            string rollbackError = null;
            try
            {
                Undo.RevertAllDownToGroup(undoGroup);
                tree.RegenerateTable();
            }
            catch (Exception exception)
            {
                rollbackError = exception.Message;
            }

            string message = error ?? "Behaviour-tree edit failed.";
            if (!string.IsNullOrEmpty(rollbackError))
            {
                message += $" Rollback also failed: {rollbackError}";
            }

            return Failure(message);
        }

        internal static BehaviourTreeEditResult Failure(string message)
        {
            return new BehaviourTreeEditResult
            {
                Success = false,
                Saved = false,
                Error = message ?? "Behaviour-tree edit failed.",
                Diagnostics = Array.Empty<string>(),
            };
        }
    }

    /// <summary>Executes explicitly unsafe SerializedObject repairs for trusted editor diagnostics.</summary>
    public static class BehaviourTreeUnsafeEditTransaction
    {
        /// <summary>Repairs one tree through its complete SerializedObject while preserving transactional save and rollback.</summary>
        /// <param name="tree">The behaviour-tree asset to repair.</param>
        /// <param name="undoName">The single Undo label for the repair.</param>
        /// <param name="edit">A trusted callback that may edit any serialized tree property.</param>
        /// <returns>A result containing initial diagnostics and the final save outcome.</returns>
        public static BehaviourTreeEditResult Execute(
            BehaviourTreeData tree,
            string undoName,
            Action<SerializedObject> edit)
        {
            return Execute(tree, undoName, edit, AssetDatabase.SaveAssets);
        }

        /// <summary>Executes an unsafe repair with an injectable save boundary for focused editor tests.</summary>
        internal static BehaviourTreeEditResult Execute(
            BehaviourTreeData tree,
            string undoName,
            Action<SerializedObject> edit,
            System.Action saveAssets)
        {
            if (tree == null)
            {
                return BehaviourTreeEditTransaction.Failure("Behaviour tree is null.");
            }

            if (edit == null)
            {
                return BehaviourTreeEditTransaction.Failure("An unsafe edit callback is required.");
            }

            if (saveAssets == null)
            {
                return BehaviourTreeEditTransaction.Failure("A save callback is required.");
            }

            string[] initialDiagnostics = CaptureDiagnostics(tree);
            string effectiveUndoName = string.IsNullOrWhiteSpace(undoName)
                ? "Unsafe repair Aethiumian behaviour tree"
                : undoName.Trim();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(effectiveUndoName);
            Undo.RegisterCompleteObjectUndo(tree, effectiveUndoName);

            try
            {
                SerializedObject serializedTree = new SerializedObject(tree);
                serializedTree.Update();
                edit(serializedTree);
                serializedTree.ApplyModifiedProperties();
                tree.RegenerateTable();

                string[] validationErrors = BehaviourTreeEditTransaction.GetValidationErrors(tree);
                if (validationErrors.Length > 0)
                {
                    return CompleteRollback(
                        tree,
                        undoGroup,
                        "Final behaviour-tree validation failed: " + string.Join(" | ", validationErrors),
                        initialDiagnostics);
                }

                saveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return new BehaviourTreeEditResult
                {
                    Success = true,
                    Saved = true,
                    HeadNodeId = tree.headNodeUUID,
                    Diagnostics = initialDiagnostics,
                };
            }
            catch (Exception exception)
            {
                return CompleteRollback(tree, undoGroup, exception.Message, initialDiagnostics);
            }
        }

        private static string[] CaptureDiagnostics(BehaviourTreeData tree)
        {
            try
            {
                return BehaviourTreeDomInspector.Validate(tree).Diagnostics
                    .Select(diagnostic => $"{diagnostic.Severity}: {diagnostic.Code}: {diagnostic.Message}")
                    .ToArray();
            }
            catch (Exception exception)
            {
                return new[] { $"Initial validation unavailable: {exception.Message}" };
            }
        }

        private static BehaviourTreeEditResult CompleteRollback(
            BehaviourTreeData tree,
            int undoGroup,
            string error,
            IReadOnlyList<string> initialDiagnostics)
        {
            BehaviourTreeEditResult result = BehaviourTreeEditTransaction.Rollback(tree, undoGroup, error);
            result.Diagnostics = initialDiagnostics?.ToArray() ?? Array.Empty<string>();
            return result;
        }
    }
}
