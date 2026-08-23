using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Aethiumian.AI.Editor.Exporting
{
    // Unity owns generated assembly outputs; this editor API remains source-only.
    /// <summary>Provides editor-only, transactional mutations for behaviour-tree assets.</summary>
    public static class BehaviourTreeDomMutator
    {
        /// <summary>Creates and attaches one default node, then saves the asset.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="request">The type and attachment request.</param>
        /// <returns>A mutation result describing the created node or the validation error.</returns>
        public static BehaviourTreeDomMutationResult AddNode(
            BehaviourTreeData tree,
            BehaviourTreeDomAddRequest request)
        {
            if (tree == null)
            {
                return Failure("Behaviour tree is null.");
            }

            if (request == null || string.IsNullOrWhiteSpace(request.Type))
            {
                return Failure("A concrete node type is required.");
            }

            if (!TryResolveCreatableType(request.Type, out Type nodeType, out string typeError))
            {
                return Failure(typeError);
            }

            TreeNode node;
            try
            {
                node = NodeFactory.Create(nodeType);
            }
            catch (Exception exception)
            {
                return Failure($"Unable to create node type '{request.Type}': {exception.Message}");
            }

            node.name = string.IsNullOrWhiteSpace(request.Name)
                ? tree.GenerateNewNodeName(node)
                : request.Name;

            if (!TryValidateAttachment(tree, node, request, out TreeNode owner, out INodeReferenceListSlot collection, out string attachmentError))
            {
                return Failure(attachmentError);
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
                        owner.uuid,
                        request.Field,
                        request.Index,
                        new[] { node },
                        node.uuid,
                        undoName);
                }
                else
                {
                    changed = tree.TryAddAndSetReference(
                        owner.uuid,
                        request.Field,
                        -1,
                        new[] { node },
                        node.uuid,
                        undoName);
                }
            }
            catch (Exception exception)
            {
                return Failure($"Unable to attach node '{node.name}': {exception.Message}");
            }

            if (!changed)
            {
                return Failure($"The requested attachment for node type '{nodeType.Name}' is invalid.");
            }

            return SaveSuccess(tree, node, request, owner);
        }

        /// <summary>Deletes selected nodes using the same decorator-unwrapping semantics as the Graph editor.</summary>
        /// <param name="tree">The behaviour-tree asset to mutate.</param>
        /// <param name="nodeIds">The authored node UUIDs selected for deletion.</param>
        /// <returns>A mutation result describing the deletion or the validation error.</returns>
        public static BehaviourTreeDomMutationResult RemoveNodes(
            BehaviourTreeData tree,
            IReadOnlyList<UUID> nodeIds)
        {
            if (tree == null)
            {
                return Failure("Behaviour tree is null.");
            }

            if (nodeIds == null || nodeIds.Count == 0 || nodeIds.Any(uuid => uuid == UUID.Empty))
            {
                return Failure("At least one non-empty node UUID is required.");
            }

            UUID[] distinctIds = nodeIds.Distinct().ToArray();
            List<TreeNode> nodes = new();
            foreach (UUID id in distinctIds)
            {
                TreeNode node = tree.GetNode(id);
                if (node == null)
                {
                    return Failure($"Node '{id}' was not found in the behaviour tree.");
                }

                nodes.Add(node);
            }

            IReadOnlyList<string> topologyErrors = tree.GetStructureValidationErrors();
            if (topologyErrors.Count > 0)
            {
                return Failure("Cannot delete nodes from an invalid structural topology: "
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
                return Failure($"Unable to delete selected nodes: {exception.Message}");
            }

            if (!changed)
            {
                return Failure("The selected nodes could not be deleted.");
            }

            return SaveSuccess(tree, null, null, null, distinctIds);
        }

        private static bool TryValidateAttachment(
            BehaviourTreeData tree,
            TreeNode node,
            BehaviourTreeDomAddRequest request,
            out TreeNode owner,
            out INodeReferenceListSlot collection,
            out string error)
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

        private static BehaviourTreeDomMutationResult SaveSuccess(
            BehaviourTreeData tree,
            TreeNode created,
            BehaviourTreeDomAddRequest request,
            TreeNode owner,
            IReadOnlyList<UUID> removed = null)
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
                    return Failure($"Mutation and rollback both failed. Save error: {exception.Message}; rollback error: {rollbackException.Message}");
                }

                return Failure($"Mutation succeeded in memory but AssetDatabase.SaveAssets failed: {exception.Message}");
            }

            return new BehaviourTreeDomMutationResult
            {
                Success = true,
                Saved = true,
                CreatedNodeId = created?.uuid ?? UUID.Empty,
                CreatedNodeName = created?.name,
                CreatedNodeType = created?.GetType().Name,
                ParentNodeId = owner?.uuid ?? UUID.Empty,
                Field = request?.Field,
                Index = request?.Index ?? -1,
                RemovedNodeIds = removed?.ToArray() ?? Array.Empty<UUID>(),
                HeadNodeId = tree.headNodeUUID,
                Diagnostics = Array.Empty<string>(),
            };
        }

        private static BehaviourTreeDomMutationResult Failure(string message)
        {
            return new BehaviourTreeDomMutationResult
            {
                Success = false,
                Error = message ?? "Behaviour-tree mutation failed.",
            };
        }
    }

    /// <summary>Describes a typed node creation and attachment request.</summary>
    [Serializable]
    public sealed class BehaviourTreeDomAddRequest
    {
        /// <summary>Short node name or full CLR type name.</summary>
        public string Type;

        /// <summary>Optional authored node name.</summary>
        public string Name;

        /// <summary>Parent UUID; omit when creating a new Head.</summary>
        public UUID ParentNode;

        /// <summary>Node-reference field on the parent.</summary>
        public string Field;

        /// <summary>Collection insertion index, or -1 for append/scalar fields.</summary>
        public int Index = -1;
    }

    /// <summary>Reports the result of one editor mutation.</summary>
    [Serializable]
    public sealed class BehaviourTreeDomMutationResult
    {
        /// <summary>Whether the mutation and save completed successfully.</summary>
        public bool Success;

        /// <summary>Whether the changed asset was saved to disk.</summary>
        public bool Saved;

        /// <summary>Failure detail when <see cref="Success"/> is false.</summary>
        public string Error;

        /// <summary>UUID of the newly created node.</summary>
        public UUID CreatedNodeId;

        /// <summary>Name of the newly created node.</summary>
        public string CreatedNodeName;

        /// <summary>Short CLR type name of the newly created node.</summary>
        public string CreatedNodeType;

        /// <summary>UUID of the parent that received the new node.</summary>
        public UUID ParentNodeId;

        /// <summary>Reference field used for the new node.</summary>
        public string Field;

        /// <summary>Collection index used for the new node.</summary>
        public int Index;

        /// <summary>UUIDs selected for removal.</summary>
        public UUID[] RemovedNodeIds;

        /// <summary>Head UUID after the mutation.</summary>
        public UUID HeadNodeId;

        /// <summary>Non-fatal mutation notes; empty when the operation is fully resolved.</summary>
        public string[] Diagnostics;
    }
}
