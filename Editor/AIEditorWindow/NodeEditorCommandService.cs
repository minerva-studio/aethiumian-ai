using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Owns editor-only node queries and mutations that are shared by the Graph and Nodes pages.
    /// Page selection, layout, refresh, and deletion policy remain with the page owners.
    /// </summary>
    internal sealed class NodeEditorCommandService
    {
        private BehaviourTreeData tree;

        /// <summary>Initializes the command service with the shared editor clipboard.</summary>
        /// <param name="clipboard">The clipboard shared by the editor window.</param>
        internal NodeEditorCommandService(Clipboard clipboard)
        {
            Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        }

        /// <summary>Gets the clipboard used by this command service.</summary>
        internal Clipboard Clipboard { get; }

        /// <summary>Gets the currently bound behaviour tree.</summary>
        internal BehaviourTreeData Tree => tree;

        /// <summary>Rebinds the service after the editor window changes its active tree.</summary>
        /// <param name="value">The active behaviour tree.</param>
        internal void Rebind(BehaviourTreeData value) => tree = value;

        /// <summary>Gets whether the clipboard contains a compatible structural subtree.</summary>
        internal bool CanPasteStructure => tree != null
            && Clipboard.HasSingleRootContent
            && Clipboard.Root is not Service;

        /// <summary>Gets whether the clipboard can replace the target's editable value fields.</summary>
        /// <param name="node">The target node.</param>
        internal bool CanPasteValue(TreeNode node) => node != null
            && tree?.GetNode(node.uuid) == node
            && Clipboard.HasSingleRootContent
            && Clipboard.TypeMatch(node);

        /// <summary>Gets authored single-reference slots that may receive a structural paste.</summary>
        /// <param name="node">The owner node.</param>
        internal IReadOnlyList<INodeReferenceSingleSlot> GetPasteSingleTargets(TreeNode node) => node == null
            ? Array.Empty<INodeReferenceSingleSlot>()
            : node.ToReferenceSlots().OfType<INodeReferenceSingleSlot>().ToArray();

        /// <summary>Gets authored list-reference slots that may receive a structural paste.</summary>
        /// <param name="node">The owner node.</param>
        internal IReadOnlyList<INodeReferenceListSlot> GetPasteListTargets(TreeNode node) => node == null
            ? Array.Empty<INodeReferenceListSlot>()
            : node.ToReferenceSlots().OfType<INodeReferenceListSlot>().ToArray();

        /// <summary>Finds the exact list occurrence used to insert beside an existing node.</summary>
        /// <param name="node">The node beside which the paste will occur.</param>
        /// <param name="parent">The authored parent containing the list.</param>
        /// <param name="slot">The authored list slot.</param>
        /// <param name="index">The node's current index.</param>
        internal bool TryGetSiblingPasteTarget(
            TreeNode node,
            out TreeNode parent,
            out INodeReferenceListSlot slot,
            out int index)
        {
            if (!CanPasteStructure)
            {
                parent = null;
                slot = null;
                index = -1;
                return false;
            }

            return TryGetSiblingOccurrence(node, out parent, out slot, out index);
        }

        /// <summary>Gets whether a node can be duplicated at its authored occurrence.</summary>
        /// <param name="node">The node to duplicate.</param>
        internal bool CanDuplicateNode(TreeNode node)
        {
            if (node == null || tree?.GetNode(node.uuid) != node)
            {
                return false;
            }

            TreeNode parent = tree.GetParent(node);
            return node is Service
                ? parent != null && parent.CanEditServices()
                : TryGetSiblingOccurrence(node, out _, out _, out _);
        }

        /// <summary>Copies an authored node or subtree into the editor clipboard.</summary>
        /// <param name="node">The node to copy.</param>
        /// <param name="includeSubtree">Whether descendants should be included.</param>
        internal void Copy(TreeNode node, bool includeSubtree)
        {
            if (node == null || tree?.GetNode(node.uuid) != node)
            {
                return;
            }

            Clipboard.Clear();
            if (includeSubtree)
            {
                Clipboard.Write(node, tree);
            }
            else
            {
                Clipboard.WriteSingle(node, tree);
            }
        }

        /// <summary>Pastes clipboard value fields while retaining the target node identity.</summary>
        /// <param name="node">The target node.</param>
        internal bool PasteValue(TreeNode node)
        {
            if (!CanPasteValue(node))
            {
                return false;
            }

            Clipboard.PasteValue(tree, node);
            return true;
        }

        /// <summary>Duplicates a node at its authored occurrence.</summary>
        /// <param name="node">The node to duplicate.</param>
        /// <param name="graphPosition">Optional position for the duplicated root.</param>
        /// <returns>The duplicated root, or <c>null</c> when the command is rejected.</returns>
        internal TreeNode Duplicate(TreeNode node, UnityEngine.Vector2? graphPosition = null)
        {
            if (!CanDuplicateNode(node))
            {
                return null;
            }

            Clipboard source = new();
            source.Write(node, tree);
            List<TreeNode> content = source.Content;
            foreach (TreeNode item in content)
            {
                item.name = tree.GenerateNewNodeName(item.name);
            }

            TreeNode root = content[0];
            TreeNode parent = tree.GetParent(node);
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(tree.EditorNodes);
            NodeReferenceOccurrence occurrence = topology.GetIncoming(node).SingleOrDefault();
            if (parent == null || occurrence.Owner != parent || occurrence.Index < 0)
            {
                return null;
            }

            IReadOnlyDictionary<UUID, UnityEngine.Vector2> positions = graphPosition.HasValue
                ? new Dictionary<UUID, UnityEngine.Vector2> { [root.uuid] = graphPosition.Value }
                : null;
            return tree.TryAddAndInsertReference(
                parent.uuid,
                occurrence.FieldName,
                occurrence.Index + 1,
                content,
                root.uuid,
                $"Duplicate {node.name}",
                positions)
                ? root
                : null;
        }

        /// <summary>Pastes a subtree into a single-reference slot.</summary>
        /// <param name="owner">The owner node.</param>
        /// <param name="slot">The destination slot.</param>
        /// <param name="graphPosition">Optional position for the pasted root.</param>
        /// <returns>The newly added root, or <c>null</c> when rejected.</returns>
        internal TreeNode PasteTo(TreeNode owner, INodeReferenceSingleSlot slot, UnityEngine.Vector2? graphPosition = null)
        {
            if (!CanPasteStructure || owner == null || slot == null)
            {
                return null;
            }

            HashSet<UUID> existing = tree.EditorNodes.Select(item => item.uuid).ToHashSet();
            if (!Clipboard.PasteTo(tree, owner, slot, graphPosition))
            {
                return null;
            }

            return tree.EditorNodes.FirstOrDefault(item => !existing.Contains(item.uuid));
        }

        /// <summary>Pastes a subtree into a list-reference slot.</summary>
        /// <param name="owner">The owner node.</param>
        /// <param name="slot">The destination slot.</param>
        /// <param name="index">The insertion index.</param>
        /// <param name="graphPosition">Optional position for the pasted root.</param>
        /// <returns>The newly added root, or <c>null</c> when rejected.</returns>
        internal TreeNode PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index, UnityEngine.Vector2? graphPosition = null)
        {
            if (!CanPasteStructure || owner == null || slot == null)
            {
                return null;
            }

            HashSet<UUID> existing = tree.EditorNodes.Select(item => item.uuid).ToHashSet();
            if (!Clipboard.PasteAt(tree, owner, slot, index, graphPosition))
            {
                return null;
            }

            return tree.EditorNodes.FirstOrDefault(item => !existing.Contains(item.uuid));
        }

        /// <summary>Pastes a service subtree into a service host.</summary>
        /// <param name="host">The host that receives the service.</param>
        /// <param name="index">The insertion index, or <c>-1</c> for the end.</param>
        /// <returns>The newly added service root, or <c>null</c> when rejected.</returns>
        internal TreeNode PasteServiceAt(TreeNode host, int index = -1)
        {
            if (tree == null
                || host == null
                || !host.CanEditServices()
                || !ServiceHostNodeUtility.TryAsServiceHost(host, out IServiceHostNode serviceHost)
                || !Clipboard.HasContent
                || !Clipboard.TypeMatch(typeof(Service)))
            {
                return null;
            }

            List<TreeNode> content = Clipboard.Content;
            if (content == null || content.Count == 0 || content[0] is not Service rootService)
            {
                return null;
            }

            foreach (TreeNode item in content)
            {
                item.name = tree.GenerateNewNodeName(item.name);
            }

            return tree.TryAddAndInsertReference(
                serviceHost.Node.uuid,
                nameof(ServiceHostNode.services),
                index,
                content,
                rootService.uuid,
                $"Paste service {rootService.name} under {serviceHost.Node.name}")
                ? rootService
                : null;
        }

        /// <summary>Resolves and validates a dropdown choice without attaching it to the tree.</summary>
        /// <param name="choice">The mutation-free dropdown choice.</param>
        /// <param name="context">The destination catalogue context.</param>
        /// <param name="root">The resolved root node.</param>
        /// <param name="addedNodes">Newly created or pasted nodes, if any.</param>
        /// <returns><c>true</c> when the choice is valid.</returns>
        internal bool TryResolveChoice(
            NodeSelectionChoice choice,
            NodeSelectionContext context,
            out TreeNode root,
            out IReadOnlyList<TreeNode> addedNodes)
        {
            root = null;
            addedNodes = null;
            if (tree == null)
            {
                return false;
            }

            switch (choice.Kind)
            {
                case NodeSelectionChoiceKind.ExistingNode:
                    root = tree.GetNode(choice.ExistingNodeUUID);
                    break;
                case NodeSelectionChoiceKind.CreateType:
                    if (choice.CreateType != null && NodeMenuCache.IsCreatableNodeType(choice.CreateType))
                    {
                        root = NodeFactory.Create(choice.CreateType);
                        root.name = tree.GenerateNewNodeName(NodeMenuCache.Shared.GetDisplayName(choice.CreateType));
                        addedNodes = new[] { root };
                    }
                    break;
                case NodeSelectionChoiceKind.PasteRoot:
                    List<TreeNode> pasted = Clipboard.Content;
                    if (pasted == null || pasted.Count == 0)
                    {
                        return false;
                    }

                    foreach (TreeNode pastedNode in pasted)
                    {
                        pastedNode.name = tree.GenerateNewNodeName(pastedNode.name);
                    }

                    root = pasted[0];
                    addedNodes = pasted;
                    break;
            }

            return root != null
                && (context == NodeSelectionContext.Services ? root is Service : root is not Service);
        }

        /// <summary>Commits a dropdown choice to a reference collection.</summary>
        /// <param name="choice">The mutation-free dropdown choice.</param>
        /// <param name="context">The destination catalogue context.</param>
        /// <param name="ownerUUID">The owner node UUID.</param>
        /// <param name="fieldName">The serialized collection field name.</param>
        /// <param name="index">The insertion index.</param>
        /// <param name="undoName">The Undo transaction name.</param>
        /// <returns><c>true</c> when the transaction commits.</returns>
        internal bool CommitChoiceToCollection(
            NodeSelectionChoice choice,
            NodeSelectionContext context,
            UUID ownerUUID,
            string fieldName,
            int index,
            string undoName,
            out TreeNode root)
        {
            if (!TryResolveChoice(choice, context, out root, out IReadOnlyList<TreeNode> addedNodes))
            {
                return false;
            }

            if (addedNodes != null)
            {
                return tree.TryAddAndInsertReference(ownerUUID, fieldName, index, addedNodes, root.uuid, undoName);
            }

            if (!tree.CanInsertReference(ownerUUID, fieldName, root.uuid, allowMoveExisting: true))
            {
                return false;
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(tree.EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(root);
            TreeNode parent = incoming.Count == 1 ? incoming[0].Owner : null;
            TreeNode owner = tree.GetNode(ownerUUID);
            if (parent != null && parent != owner
                && !EditorUtility.DisplayDialog(
                    "Node has a parent already",
                    $"This Node is connecting to {parent.name}, move under {owner.name} ?",
                    "OK",
                    "Cancel"))
            {
                return false;
            }

            return tree.TryInsertReference(ownerUUID, fieldName, index, root.uuid, true, undoName);
        }

        /// <summary>Commits a dropdown choice to one exact reference occurrence.</summary>
        /// <param name="choice">The mutation-free dropdown choice.</param>
        /// <param name="context">The destination catalogue context.</param>
        /// <param name="ownerUUID">The owner node UUID.</param>
        /// <param name="fieldName">The serialized field name.</param>
        /// <param name="index">The exact occurrence index.</param>
        /// <param name="expectedTargetUUID">The target UUID captured when the picker opened.</param>
        /// <param name="undoName">The Undo transaction name.</param>
        /// <param name="rawReference">Whether the destination is a raw reference without topology ownership.</param>
        /// <returns><c>true</c> when the transaction commits.</returns>
        internal bool CommitChoiceToReference(
            NodeSelectionChoice choice,
            NodeSelectionContext context,
            UUID ownerUUID,
            string fieldName,
            int index,
            UUID expectedTargetUUID,
            string undoName,
            out TreeNode root,
            bool rawReference = false)
        {
            root = null;
            TreeNode owner = tree?.GetNode(ownerUUID);
            if (owner == null)
            {
                return false;
            }

            TreeNode currentTarget = NodeTopologySnapshot.Create(tree.EditorNodes)
                .GetOutgoing(owner)
                .FirstOrDefault(occurrence => occurrence.FieldName == fieldName && occurrence.Index == index)
                .Target;
            if (!rawReference && expectedTargetUUID != UUID.Empty
                && (currentTarget == null || currentTarget.uuid != expectedTargetUUID))
            {
                return false;
            }

            if (!TryResolveChoice(choice, context, out root, out IReadOnlyList<TreeNode> addedNodes))
            {
                return false;
            }

            if (addedNodes != null)
            {
                return tree.TryAddAndSetReference(ownerUUID, fieldName, index, addedNodes, root.uuid, undoName);
            }

            if (!rawReference && !tree.CanSetReference(ownerUUID, fieldName, index, root.uuid, allowMoveExisting: true))
            {
                return false;
            }

            if (!rawReference)
            {
                NodeReferenceOccurrence incoming = NodeTopologySnapshot.Create(tree.EditorNodes)
                    .GetIncoming(root)
                    .FirstOrDefault();
                if (incoming.Owner != null && incoming.Owner != owner
                    && !EditorUtility.DisplayDialog(
                        "Node has a parent already",
                        $"This Node is connecting to {incoming.Owner.name}, move under {owner.name} ?",
                        "OK",
                        "Cancel"))
                {
                    return false;
                }
            }

            return tree.TrySetReference(ownerUUID, fieldName, index, root.uuid, true, undoName);
        }

        /// <summary>Clears one exact reference occurrence after verifying its captured target.</summary>
        /// <param name="ownerUUID">The owner node UUID.</param>
        /// <param name="fieldName">The serialized field name.</param>
        /// <param name="index">The exact occurrence index.</param>
        /// <param name="expectedTargetUUID">The target UUID captured when the picker opened.</param>
        /// <param name="undoName">The Undo transaction name.</param>
        /// <param name="rawReference">Whether the destination is a raw reference without topology ownership.</param>
        /// <returns><c>true</c> when the clear transaction commits.</returns>
        internal bool ClearReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            UUID expectedTargetUUID,
            string undoName,
            bool rawReference = false)
        {
            TreeNode owner = tree?.GetNode(ownerUUID);
            if (owner == null)
            {
                return false;
            }

            if (!rawReference)
            {
                NodeReferenceOccurrence current = NodeTopologySnapshot.Create(tree.EditorNodes)
                    .GetOutgoing(owner)
                    .FirstOrDefault(occurrence => occurrence.FieldName == fieldName
                        && occurrence.Index == index
                        && occurrence.Target?.uuid == expectedTargetUUID);
                if (current.Target == null)
                {
                    return false;
                }
            }

            return tree.TryDisconnectReference(ownerUUID, fieldName, index, undoName);
        }

        /// <summary>Finds a node's actual list owner without consulting clipboard state.</summary>
        private bool TryGetSiblingOccurrence(
            TreeNode node,
            out TreeNode parent,
            out INodeReferenceListSlot slot,
            out int index)
        {
            parent = node == null ? null : tree?.GetParent(node);
            slot = null;
            index = -1;
            if (parent == null)
            {
                return false;
            }

            foreach (INodeReferenceListSlot candidate in parent.ToReferenceSlots().OfType<INodeReferenceListSlot>())
            {
                int candidateIndex = candidate.IndexOf(node);
                if (candidateIndex < 0)
                {
                    continue;
                }

                slot = candidate;
                index = candidateIndex;
                return true;
            }

            return false;
        }
    }
}
