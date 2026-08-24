using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Aethiumian.AI.Editor.Mutations
{
    // Unity owns generated assembly outputs; this editor API remains source-only.
    /// <summary>Provides editor-only, transactional mutations for behaviour-tree assets.</summary>
    public static class BehaviourTreeMutator
    {
        /// <summary>Creates and attaches one default node, then saves the asset.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="request">The type and attachment request.</param>
        /// <returns>A mutation result describing the created node or the validation error.</returns>
        public static BehaviourTreeAddResult AddNode(BehaviourTreeData tree, BehaviourTreeAddRequest request)
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

            BehaviourTreeAddResult result = SaveSuccess<BehaviourTreeAddResult>(tree);
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
        public static BehaviourTreeRemoveResult RemoveNodes(BehaviourTreeData tree, IReadOnlyList<UUID> nodeIds)
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

            BehaviourTreeRemoveResult result = SaveSuccess<BehaviourTreeRemoveResult>(tree);
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
        public static BehaviourTreeRearrangeResult ReorderNode(BehaviourTreeData tree, BehaviourTreeReorderRequest request)
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
                    request.Index)
                : Failure<BehaviourTreeRearrangeResult>($"Unable to reorder node '{node.name}'.");
        }

        /// <summary>Moves a node to a different structural or Service reference slot.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="request">The node and destination slot.</param>
        /// <returns>A mutation result describing the moved node.</returns>
        public static BehaviourTreeRearrangeResult MoveNode(BehaviourTreeData tree, BehaviourTreeMoveRequest request)
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
                ? SaveRearrangement(tree, node, sourceOccurrence, targetParent, request.Field, request.Index)
                : Failure<BehaviourTreeRearrangeResult>($"Unable to move node '{node.name}' to '{targetParent.name}.{request.Field}'.");
        }

        /// <summary>Detaches a uniquely-owned node while keeping it in the authored node list.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="nodeId">The authored UUID to detach.</param>
        /// <returns>A mutation result describing the detached node.</returns>
        public static BehaviourTreeRearrangeResult DetachNode(BehaviourTreeData tree, UUID nodeId)
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
                ? SaveRearrangement(tree, node, occurrence, null, null, -1)
                : Failure<BehaviourTreeRearrangeResult>($"Unable to detach node '{node.name}'.");
        }

        /// <summary>Moves an existing node to the tree Head.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="nodeId">The authored UUID to make Head.</param>
        /// <returns>A mutation result describing the new Head.</returns>
        public static BehaviourTreeRearrangeResult SetHead(BehaviourTreeData tree, UUID nodeId)
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
                ? SaveRearrangement(tree, node, sourceOccurrence, null, "$head", -1)
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
        private static BehaviourTreeRearrangeResult SaveRearrangement(BehaviourTreeData tree, TreeNode node, NodeReferenceOccurrence source, TreeNode targetParent, string targetField, int targetIndex)
        {
            BehaviourTreeRearrangeResult result = SaveSuccess<BehaviourTreeRearrangeResult>(tree);
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

        private static T SaveSuccess<T>(BehaviourTreeData tree)
            where T : BehaviourTreeMutationResult, new()
        {
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
}
