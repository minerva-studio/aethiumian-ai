using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Aethiumian.AI
{
#if UNITY_EDITOR
    public partial class BehaviourTreeData
    {
        /// <summary>Checks an authored assignment against a fresh topology snapshot.</summary>
        internal bool CanSetReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            UUID candidateUUID,
            bool allowMoveExisting)
        {
            return TryResolveReference(ownerUUID, fieldName, index, out TreeNode owner, out INodeReference current, out bool raw)
                && GetNode(candidateUUID) is TreeNode candidate
                && current?.UUID != candidateUUID
                && IsCompatibleReference(owner, fieldName, candidate)
                && (raw || CanAssign(NodeTopologySnapshot.Create(EditorNodes), owner, candidate, allowMoveExisting, out _))
                && (raw || current == null || current.UUID == UUID.Empty || CanDetach(ownerUUID, fieldName, index, current.UUID));
        }

        /// <summary>Checks insertion into one authored collection against a fresh topology snapshot.</summary>
        internal bool CanInsertReference(UUID ownerUUID, string fieldName, UUID candidateUUID, bool allowMoveExisting)
        {
            return TryResolveCollection(ownerUUID, fieldName, out TreeNode owner, out INodeReferenceCollectionFieldAccessor field)
                && GetNode(candidateUUID) is TreeNode candidate
                && IsCompatibleReference(owner, fieldName, candidate)
                && (IsRaw(field) || CanAssign(
                    NodeTopologySnapshot.Create(EditorNodes),
                    owner,
                    candidate,
                    allowMoveExisting,
                    out _));
        }

        /// <summary>Checks assignment to an empty scalar occurrence.</summary>
        internal bool CanConnectReference(UUID ownerUUID, string fieldName, int index, UUID candidateUUID)
        {
            return TryResolveReference(ownerUUID, fieldName, index, out _, out INodeReference current, out _)
                && (current == null || current.UUID == UUID.Empty)
                && CanSetReference(ownerUUID, fieldName, index, candidateUUID, allowMoveExisting: false);
        }

        /// <summary>Checks replacement of one occupied occurrence.</summary>
        internal bool CanReplaceReference(UUID ownerUUID, string fieldName, int index, UUID candidateUUID)
        {
            return TryResolveReference(ownerUUID, fieldName, index, out _, out INodeReference current, out _)
                && current != null
                && current.UUID != UUID.Empty
                && CanSetReference(ownerUUID, fieldName, index, candidateUUID, allowMoveExisting: false);
        }

        /// <summary>Assigns a target only when the destination remains empty.</summary>
        internal bool TryConnectReference(UUID ownerUUID, string fieldName, int index, UUID candidateUUID, string undoName)
        {
            return CanConnectReference(ownerUUID, fieldName, index, candidateUUID)
                && TrySetReference(ownerUUID, fieldName, index, candidateUUID, false, undoName);
        }

        /// <summary>Replaces a target only when the destination remains occupied.</summary>
        internal bool TryReplaceReference(UUID ownerUUID, string fieldName, int index, UUID candidateUUID, string undoName)
        {
            return CanReplaceReference(ownerUUID, fieldName, index, candidateUUID)
                && TrySetReference(ownerUUID, fieldName, index, candidateUUID, false, undoName);
        }

        /// <summary>Sets or replaces one scalar or collection reference occurrence.</summary>
        internal bool TrySetReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            UUID candidateUUID,
            bool allowMoveExisting,
            string undoName)
        {
            if (!TryResolveReference(ownerUUID, fieldName, index, out TreeNode owner, out INodeReference current, out bool raw)
                || GetNode(candidateUUID) is not TreeNode candidate
                || current?.UUID == candidateUUID
                || !IsCompatibleReference(owner, fieldName, candidate))
            {
                return false;
            }

            NodeReferenceOccurrence previousOccurrence = default;
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            if (!raw && (!CanAssign(topology, owner, candidate, allowMoveExisting, out previousOccurrence)
                || current != null && current.UUID != UUID.Empty && !CanDetach(ownerUUID, fieldName, index, current.UUID)))
            {
                return false;
            }

            UUID displacedUUID = current?.UUID ?? UUID.Empty;
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                int destinationIndex = index;
                if (previousOccurrence.Target != null)
                {
                    if (previousOccurrence.Owner.uuid == ownerUUID
                        && previousOccurrence.FieldName == fieldName
                        && previousOccurrence.Index >= 0
                        && previousOccurrence.Index < destinationIndex)
                    {
                        destinationIndex--;
                    }

                    RemoveOccurrence(previousOccurrence);
                }

                if (!TryResolveReference(ownerUUID, fieldName, destinationIndex, out owner, out current, out raw))
                {
                    throw new InvalidOperationException("The destination reference changed during the transaction.");
                }

                SetReference(owner, fieldName, destinationIndex, candidate);
                if (!raw)
                {
                    candidate.parent = new NodeReference(owner.uuid);
                    ClearParentWhenDetached(displacedUUID, candidateUUID);
                }

                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Inserts one node into an authored reference collection.</summary>
        internal bool TryInsertReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            UUID candidateUUID,
            bool allowMoveExisting,
            string undoName)
        {
            if (!TryResolveCollection(ownerUUID, fieldName, out TreeNode owner, out INodeReferenceCollectionFieldAccessor field)
                || GetNode(candidateUUID) is not TreeNode candidate
                || !IsCompatibleReference(owner, fieldName, candidate))
            {
                return false;
            }

            bool raw = IsRaw(field);
            NodeReferenceOccurrence previousOccurrence = default;
            if (!raw && !CanAssign(
                    NodeTopologySnapshot.Create(EditorNodes),
                    owner,
                    candidate,
                    allowMoveExisting,
                    out previousOccurrence))
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                IList collection = field.Get(owner);
                int insertionIndex = Math.Clamp(index < 0 ? collection?.Count ?? 0 : index, 0, collection?.Count ?? 0);
                if (previousOccurrence.Target != null)
                {
                    if (previousOccurrence.Owner.uuid == ownerUUID
                        && previousOccurrence.FieldName == fieldName
                        && previousOccurrence.Index < insertionIndex)
                    {
                        insertionIndex--;
                    }

                    RemoveOccurrence(previousOccurrence);
                    collection = field.Get(owner);
                }

                InsertCollectionEntry(owner, field, insertionIndex, candidate);
                if (!raw)
                {
                    candidate.parent = new NodeReference(owner.uuid);
                }

                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Clears one scalar reference or removes one collection occurrence.</summary>
        /// <param name="expectedTargetUUID">Optional target identity captured by a graph edge.</param>
        internal bool TryDisconnectReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            string undoName,
            UUID expectedTargetUUID = default)
        {
            if (!TryResolveReference(ownerUUID, fieldName, index, out TreeNode owner, out INodeReference reference, out bool raw)
                || reference == null
                || reference.UUID == UUID.Empty
                || expectedTargetUUID != UUID.Empty && reference.UUID != expectedTargetUUID
                || !raw && !CanDetach(ownerUUID, fieldName, index, reference.UUID))
            {
                return false;
            }

            UUID detachedUUID = reference.UUID;
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                if (index >= 0)
                {
                    RemoveCollectionEntry(owner, fieldName, index);
                }
                else
                {
                    SetReference(owner, fieldName, -1, null);
                }

                if (!raw)
                {
                    ClearParentWhenDetached(detachedUUID);
                }

                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Disconnects the unique authored occurrence that owns a target.</summary>
        internal bool TryDetachTarget(UUID targetUUID, string undoName)
        {
            TreeNode target = GetNode(targetUUID);
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(target);
            if (target == null || incoming.Count != 1 || topology.HasInvalidParentMetadata(target))
            {
                return false;
            }

            NodeReferenceOccurrence occurrence = incoming[0];
            return TryDisconnectReference(occurrence.Owner.uuid, occurrence.FieldName, occurrence.Index, undoName);
        }

        /// <summary>Moves one complete collection entry while preserving its metadata.</summary>
        internal bool TryReorderReference(UUID ownerUUID, string fieldName, int sourceIndex, int destinationIndex, string undoName)
        {
            if (!TryResolveCollection(ownerUUID, fieldName, out TreeNode owner, out INodeReferenceCollectionFieldAccessor field)
                || field.Get(owner) is not IList collection
                || sourceIndex < 0
                || sourceIndex >= collection.Count)
            {
                return false;
            }

            int targetIndex = Math.Clamp(destinationIndex, 0, collection.Count - 1);
            if (sourceIndex == targetIndex)
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                MoveCollectionEntry(owner, field, sourceIndex, targetIndex);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Checks whether a chained Sequence or Loop replacement can skip forward.</summary>
        internal bool CanRedirectReferenceChain(UUID ownerUUID, string fieldName, int sourceIndex, UUID targetUUID)
        {
            if (GetNode(ownerUUID) is not TreeNode owner
                || owner is not (Sequence or Loop)
                || fieldName != "events"
                || sourceIndex < 0
                || !TryResolveCollection(ownerUUID, fieldName, out _, out INodeReferenceCollectionFieldAccessor field)
                || field.Get(owner) is not IList collection)
            {
                return false;
            }

            for (int index = sourceIndex + 1; index < collection.Count; index++)
            {
                if (collection[index] is INodeReference reference && reference.UUID == targetUUID)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Atomically removes the occurrences skipped by a forward chain redirect.</summary>
        internal bool TryRedirectReferenceChain(UUID ownerUUID, string fieldName, int sourceIndex, UUID targetUUID, string undoName)
        {
            if (!CanRedirectReferenceChain(ownerUUID, fieldName, sourceIndex, targetUUID)
                || !TryResolveCollection(ownerUUID, fieldName, out TreeNode owner, out INodeReferenceCollectionFieldAccessor field)
                || field.Get(owner) is not IList collection)
            {
                return false;
            }

            int targetIndex = -1;
            for (int index = sourceIndex + 1; index < collection.Count; index++)
            {
                if (collection[index] is INodeReference reference && reference.UUID == targetUUID)
                {
                    targetIndex = index;
                    break;
                }
            }

            UUID[] detached = collection.Cast<object>()
                .Skip(sourceIndex)
                .Take(targetIndex - sourceIndex)
                .OfType<INodeReference>()
                .Select(reference => reference.UUID)
                .Where(uuid => uuid != UUID.Empty)
                .ToArray();
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                for (int index = targetIndex - 1; index >= sourceIndex; index--)
                {
                    RemoveCollectionEntry(owner, fieldName, index);
                }

                foreach (UUID uuid in detached)
                {
                    ClearParentWhenDetached(uuid);
                }

                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Checks whether a node can become the tree Head.</summary>
        internal bool CanSetHead(UUID candidateUUID, bool allowMoveExisting)
        {
            TreeNode candidate = GetNode(candidateUUID);
            return candidate is not Service
                && candidate != null
                && candidateUUID != headNodeUUID
                && CanAssign(NodeTopologySnapshot.Create(EditorNodes), null, candidate, allowMoveExisting, out _);
        }

        /// <summary>Sets the tree Head without moving an already-owned node.</summary>
        internal bool TrySetHead(UUID candidateUUID, string undoName)
        {
            return TryChangeHead(candidateUUID, false, undoName);
        }

        /// <summary>Moves a uniquely-owned node to the tree Head.</summary>
        internal bool TryMoveToHead(UUID candidateUUID, string undoName)
        {
            return TryChangeHead(candidateUUID, true, undoName);
        }

        /// <summary>Adds detached nodes without assigning an authored owner.</summary>
        internal bool TryAddNodes(
            IReadOnlyList<TreeNode> addedNodes,
            string undoName,
            IReadOnlyDictionary<UUID, Vector2> graphPositions = null)
        {
            if (!CanAddNodes(addedNodes))
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                nodes.AddRange(addedNodes);
                MergeGraphPositions(graphPositions);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Adds detached nodes and assigns their root to one reference occurrence.</summary>
        internal bool TryAddAndSetReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            IReadOnlyList<TreeNode> addedNodes,
            UUID rootUUID,
            string undoName,
            IReadOnlyDictionary<UUID, Vector2> graphPositions = null)
        {
            if (!CanAddAndAssign(ownerUUID, fieldName, index, addedNodes, rootUUID, false, out TreeNode owner, out TreeNode root)
                || !TryResolveReference(ownerUUID, fieldName, index, out _, out INodeReference destination, out bool raw))
            {
                return false;
            }

            UUID displacedUUID = destination?.UUID ?? UUID.Empty;
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                nodes.AddRange(addedNodes);
                SetReference(owner, fieldName, index, root);
                if (!raw)
                {
                    root.parent = new NodeReference(owner.uuid);
                    ClearParentWhenDetached(displacedUUID, rootUUID);
                }
                MergeGraphPositions(graphPositions);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Adds detached nodes and inserts their root into one reference collection.</summary>
        internal bool TryAddAndInsertReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            IReadOnlyList<TreeNode> addedNodes,
            UUID rootUUID,
            string undoName,
            IReadOnlyDictionary<UUID, Vector2> graphPositions = null)
        {
            if (!CanAddAndAssign(ownerUUID, fieldName, -1, addedNodes, rootUUID, true, out TreeNode owner, out TreeNode root)
                || !TryResolveCollection(ownerUUID, fieldName, out _, out INodeReferenceCollectionFieldAccessor field))
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                nodes.AddRange(addedNodes);
                InsertCollectionEntry(owner, field, index, root);
                if (!IsRaw(field))
                {
                    root.parent = new NodeReference(owner.uuid);
                }
                MergeGraphPositions(graphPositions);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Adds detached nodes and assigns their root as the tree Head.</summary>
        internal bool TryAddAndSetHead(
            IReadOnlyList<TreeNode> addedNodes,
            UUID rootUUID,
            string undoName,
            IReadOnlyDictionary<UUID, Vector2> graphPositions = null)
        {
            TreeNode root = addedNodes?.FirstOrDefault(node => node?.uuid == rootUUID);
            if (root is Service || !CanAddNodes(addedNodes)
                || NodeTopologySnapshot.Create(EditorNodes.Concat(addedNodes)).GetIncoming(root).Count != 0)
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                nodes.AddRange(addedNodes);
                headNodeUUID = rootUUID;
                root.parent = NodeReference.Empty;
                MergeGraphPositions(graphPositions);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Deletes nodes and clears every authored or Raw incoming reference to them.</summary>
        internal bool TryDeleteNodes(ISet<UUID> removedUUIDs, string undoName, bool recordUndo = true)
        {
            if (removedUUIDs == null || removedUUIDs.Count == 0 || !nodes.Any(node => node != null && removedUUIDs.Contains(node.uuid)))
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, recordUndo);
            try
            {
                foreach (TreeNode owner in nodes.Where(node => node != null && !removedUUIDs.Contains(node.uuid)).ToArray())
                {
                    ClearReferencesTo(owner, removedUUIDs);
                    if (owner.parent != null && removedUUIDs.Contains(owner.parent.UUID))
                    {
                        owner.parent = NodeReference.Empty;
                    }
                }

                nodes.RemoveAll(node => node == null || removedUUIDs.Contains(node.uuid));
                if (removedUUIDs.Contains(headNodeUUID))
                {
                    headNodeUUID = UUID.Empty;
                }

                graphLayout?.RemoveNodes(removedUUIDs);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Repairs parent metadata only when authored ownership is unambiguous.</summary>
        private void ReconcileUnambiguousParents()
        {
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            foreach (TreeNode node in nodes.Where(node => node != null))
            {
                IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(node);
                if (incoming.Count == 1)
                {
                    node.parent = new NodeReference(incoming[0].Owner.uuid);
                }
            }
        }

        private bool TryChangeHead(UUID candidateUUID, bool allowMoveExisting, string undoName)
        {
            if (candidateUUID == UUID.Empty)
            {
                if (headNodeUUID == UUID.Empty)
                {
                    return false;
                }

                int clearUndoGroup = BeginTransaction(undoName, true);
                try
                {
                    headNodeUUID = UUID.Empty;
                    CompleteTransaction(clearUndoGroup);
                    return true;
                }
                catch (Exception exception)
                {
                    RollbackTransaction(clearUndoGroup, exception);
                    return false;
                }
            }

            TreeNode candidate = GetNode(candidateUUID);
            NodeReferenceOccurrence previousOccurrence = default;
            if (candidate is Service || candidate == null || candidateUUID == headNodeUUID
                || !CanAssign(NodeTopologySnapshot.Create(EditorNodes), null, candidate, allowMoveExisting, out previousOccurrence))
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                if (previousOccurrence.Target != null)
                {
                    RemoveOccurrence(previousOccurrence);
                }

                headNodeUUID = candidateUUID;
                candidate.parent = NodeReference.Empty;
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        private bool CanAddAndAssign(
            UUID ownerUUID,
            string fieldName,
            int index,
            IReadOnlyList<TreeNode> addedNodes,
            UUID rootUUID,
            bool collection,
            out TreeNode owner,
            out TreeNode root)
        {
            owner = GetNode(ownerUUID);
            root = addedNodes?.FirstOrDefault(node => node?.uuid == rootUUID);
            if (owner == null || root == null || !CanAddNodes(addedNodes) || !IsCompatibleReference(owner, fieldName, root))
            {
                return false;
            }

            bool raw;
            if (collection)
            {
                if (!TryResolveCollection(ownerUUID, fieldName, out _, out INodeReferenceCollectionFieldAccessor collectionField))
                {
                    return false;
                }

                raw = IsRaw(collectionField);
            }
            else if (!TryResolveReference(ownerUUID, fieldName, index, out _, out INodeReference destination, out raw)
                || !raw && destination != null && destination.UUID != UUID.Empty && !CanDetach(ownerUUID, fieldName, index, destination.UUID))
            {
                return false;
            }

            NodeTopologySnapshot combined = NodeTopologySnapshot.Create(EditorNodes.Concat(addedNodes));
            return combined.GetValidationErrors().Count == 0
                && (raw || CanAssign(combined, owner, root, false, out _));
        }

        private bool CanAddNodes(IReadOnlyList<TreeNode> addedNodes)
        {
            return addedNodes != null
                && addedNodes.Count > 0
                && addedNodes.All(node => node != null && GetNode(node.uuid) == null)
                && addedNodes.Select(node => node.uuid).Distinct().Count() == addedNodes.Count
                && NodeTopologySnapshot.Create(EditorNodes.Concat(addedNodes)).GetValidationErrors().Count == 0;
        }

        private static bool CanAssign(
            NodeTopologySnapshot topology,
            TreeNode owner,
            TreeNode candidate,
            bool allowMoveExisting,
            out NodeReferenceOccurrence previousOccurrence)
        {
            previousOccurrence = default;
            if (topology == null || candidate == null
                || owner != null && (owner == candidate || topology.WouldCreateCycle(owner, candidate))
                || topology.HasInvalidParentMetadata(candidate))
            {
                return false;
            }

            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(candidate);
            if (incoming.Count > 1 || incoming.Count == 1 && owner != null && incoming[0].Owner == owner)
            {
                return false;
            }

            if (incoming.Count == 1)
            {
                if (!allowMoveExisting)
                {
                    return false;
                }

                previousOccurrence = incoming[0];
            }

            return true;
        }

        private bool CanDetach(UUID ownerUUID, string fieldName, int index, UUID targetUUID)
        {
            TreeNode target = GetNode(targetUUID);
            IReadOnlyList<NodeReferenceOccurrence> incoming = NodeTopologySnapshot.Create(EditorNodes).GetIncoming(target);
            return target != null
                && incoming.Count == 1
                && incoming[0].Owner.uuid == ownerUUID
                && incoming[0].FieldName == fieldName
                && incoming[0].Index == index
                && (target.parent?.UUID ?? UUID.Empty) == ownerUUID;
        }

        private bool TryResolveReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            out TreeNode owner,
            out INodeReference reference,
            out bool raw)
        {
            owner = GetNode(ownerUUID);
            reference = null;
            raw = false;
            if (owner == null || fieldName == nameof(TreeNode.parent))
            {
                return false;
            }

            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            INodeReferenceFieldAccessor single = accessor.NodeReferences.FirstOrDefault(candidate => candidate.Name == fieldName);
            if (single != null)
            {
                if (index >= 0)
                {
                    return false;
                }

                reference = single.Get(owner);
                raw = single.FieldType == typeof(RawNodeReference);
                return true;
            }

            INodeReferenceCollectionFieldAccessor collection = accessor.NodeReferenceCollections.FirstOrDefault(candidate => candidate.Name == fieldName);
            IList entries = collection?.Get(owner);
            if (collection == null || entries == null || index < 0 || index >= entries.Count)
            {
                return false;
            }

            reference = entries[index] as INodeReference;
            raw = IsRaw(collection);
            return reference != null;
        }

        private bool TryResolveCollection(
            UUID ownerUUID,
            string fieldName,
            out TreeNode owner,
            out INodeReferenceCollectionFieldAccessor field)
        {
            owner = GetNode(ownerUUID);
            field = owner == null || fieldName == nameof(TreeNode.parent)
                ? null
                : NodeAccessorProvider.GetAccessor(owner.GetType()).NodeReferenceCollections
                    .FirstOrDefault(candidate => candidate.Name == fieldName);
            return field != null;
        }

        private static bool IsCompatibleReference(TreeNode owner, string fieldName, TreeNode candidate)
        {
            return fieldName != nameof(ServiceHostNode.services)
                ? candidate is not Service
                : candidate is Service && owner?.CanEditServices() == true;
        }

        private static bool IsRaw(INodeReferenceCollectionFieldAccessor field)
        {
            return field?.ElementType == typeof(RawNodeReference);
        }

        private static INodeReference CreateReference(Type type, TreeNode target)
        {
            INodeReference reference = (INodeReference)Activator.CreateInstance(type);
            reference.UUID = target?.uuid ?? UUID.Empty;
            reference.Node = null;
            return reference;
        }

        private static object CreateCollectionEntry(Type type, TreeNode target)
        {
            if (type == typeof(Probability.EventWeight))
            {
                return new Probability.EventWeight { reference = new NodeReference(target.uuid), weight = 1 };
            }

            if (type == typeof(PseudoProbability.EventWeight))
            {
                return new PseudoProbability.EventWeight { reference = new NodeReference(target.uuid), weight = 1 };
            }

            return CreateReference(type, target);
        }

        private static void SetReference(TreeNode owner, string fieldName, int index, TreeNode target)
        {
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            if (index < 0)
            {
                INodeReferenceFieldAccessor field = accessor.NodeReferences.Single(candidate => candidate.Name == fieldName);
                field.Set(owner, CreateReference(field.FieldType, target));
                return;
            }

            INodeReferenceCollectionFieldAccessor collection = accessor.NodeReferenceCollections.Single(candidate => candidate.Name == fieldName);
            IList entries = collection.Get(owner);
            if (entries[index] is INodeReference reference)
            {
                reference.UUID = target?.uuid ?? UUID.Empty;
                reference.Node = null;
            }
            else
            {
                entries[index] = CreateCollectionEntry(collection.ElementType, target);
            }
        }

        private static void InsertCollectionEntry(
            TreeNode owner,
            INodeReferenceCollectionFieldAccessor field,
            int index,
            TreeNode target)
        {
            IList collection = field.Get(owner);
            int count = collection?.Count ?? 0;
            int targetIndex = Math.Clamp(index < 0 ? count : index, 0, count);
            object entry = CreateCollectionEntry(field.ElementType, target);
            if (field.CollectionType.IsArray)
            {
                Array source = collection as Array ?? Array.CreateInstance(field.ElementType, 0);
                Array destination = Array.CreateInstance(field.ElementType, source.Length + 1);
                Array.Copy(source, 0, destination, 0, targetIndex);
                destination.SetValue(entry, targetIndex);
                Array.Copy(source, targetIndex, destination, targetIndex + 1, source.Length - targetIndex);
                field.Set(owner, destination);
                return;
            }

            if (collection == null)
            {
                collection = (IList)Activator.CreateInstance(field.CollectionType);
                field.Set(owner, collection);
            }

            collection.Insert(targetIndex, entry);
        }

        private static void RemoveCollectionEntry(TreeNode owner, string fieldName, int index)
        {
            INodeReferenceCollectionFieldAccessor field = NodeAccessorProvider.GetAccessor(owner.GetType())
                .NodeReferenceCollections.Single(candidate => candidate.Name == fieldName);
            IList collection = field.Get(owner);
            if (field.CollectionType.IsArray)
            {
                Array source = (Array)collection;
                Array destination = Array.CreateInstance(field.ElementType, source.Length - 1);
                Array.Copy(source, 0, destination, 0, index);
                Array.Copy(source, index + 1, destination, index, source.Length - index - 1);
                field.Set(owner, destination);
                return;
            }

            collection.RemoveAt(index);
        }

        private static void MoveCollectionEntry(
            TreeNode owner,
            INodeReferenceCollectionFieldAccessor field,
            int sourceIndex,
            int destinationIndex)
        {
            IList collection = field.Get(owner);
            object moved = collection[sourceIndex];
            if (field.CollectionType.IsArray)
            {
                Array source = (Array)collection;
                Array destination = Array.CreateInstance(field.ElementType, source.Length);
                List<object> entries = source.Cast<object>().ToList();
                entries.RemoveAt(sourceIndex);
                entries.Insert(destinationIndex, moved);
                for (int index = 0; index < entries.Count; index++)
                {
                    destination.SetValue(entries[index], index);
                }

                field.Set(owner, destination);
                return;
            }

            collection.RemoveAt(sourceIndex);
            collection.Insert(destinationIndex, moved);
        }

        private void RemoveOccurrence(NodeReferenceOccurrence occurrence)
        {
            if (occurrence.Index < 0)
            {
                SetReference(occurrence.Owner, occurrence.FieldName, -1, null);
            }
            else
            {
                RemoveCollectionEntry(occurrence.Owner, occurrence.FieldName, occurrence.Index);
            }

            ClearParentWhenDetached(occurrence.Target.uuid);
        }

        private void ClearParentWhenDetached(UUID targetUUID, UUID replacementUUID = default)
        {
            if (targetUUID == UUID.Empty || targetUUID == replacementUUID || GetNode(targetUUID) is not TreeNode target)
            {
                return;
            }

            if (NodeTopologySnapshot.Create(EditorNodes).GetIncoming(target).Count == 0)
            {
                target.parent = NodeReference.Empty;
            }
        }

        private static void ClearReferencesTo(TreeNode owner, ISet<UUID> removedUUIDs)
        {
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
            {
                if (field.Name != nameof(TreeNode.parent) && removedUUIDs.Contains(field.Get(owner)?.UUID ?? UUID.Empty))
                {
                    field.Set(owner, CreateReference(field.FieldType, null));
                }
            }

            foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
            {
                IList entries = field.Get(owner);
                if (entries == null)
                {
                    continue;
                }

                for (int index = entries.Count - 1; index >= 0; index--)
                {
                    if (entries[index] is INodeReference reference && removedUUIDs.Contains(reference.UUID))
                    {
                        RemoveCollectionEntry(owner, field.Name, index);
                        entries = field.Get(owner);
                    }
                }
            }
        }

        private void MergeGraphPositions(IReadOnlyDictionary<UUID, Vector2> positions)
        {
            if (positions == null || positions.Count == 0)
            {
                return;
            }

            GraphLayoutData current = GraphLayout;
            IEnumerable<GraphLayoutEntry> merged = (current?.Positions ?? Array.Empty<GraphLayoutEntry>())
                .Where(entry => !positions.ContainsKey(entry.UUID))
                .Concat(positions.Select(pair => new GraphLayoutEntry(pair.Key, pair.Value)));
            GraphLayout = GraphLayoutData.Create(
                merged,
                current?.Services,
                current?.HasEntrancePosition == true ? current.EntrancePosition : null,
                current?.HasExitPosition == true ? current.ExitPosition : null);
        }


        #region Transaction Support

        private int BeginTransaction(string undoName, bool recordUndo)
        {
            if (!recordUndo)
            {
                return -1;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(this, undoName);
            return undoGroup;
        }

        private void CompleteTransaction(int undoGroup)
        {
            // Synchronize the cached SerializedObject so an Inspector apply after this callback
            // cannot restore the pre-transaction reference values over the object-model mutation.
            SerializedObject.Update();
            RegenerateTable();
            EditorUtility.SetDirty(this);
            if (undoGroup >= 0)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private void RollbackTransaction(int undoGroup, Exception exception)
        {
            if (undoGroup >= 0)
            {
                Undo.RevertAllDownToGroup(undoGroup);
            }

            SerializedObject.Update();
            RegenerateTable();
            Debug.LogException(exception, this);
        }

        #endregion
    }
#endif
}
