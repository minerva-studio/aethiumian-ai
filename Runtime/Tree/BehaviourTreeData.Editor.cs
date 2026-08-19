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
                && (raw
                    || current == null
                    || current.UUID == UUID.Empty
                    || GetNode(current.UUID) == null
                    || CanDetach(ownerUUID, fieldName, index, current.UUID));
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

        /// <summary>Checks whether an empty Decorator can wrap an existing structural node.</summary>
        internal bool CanWrapDecoratorChild(UUID decoratorUUID, UUID targetUUID)
        {
            if (GetNode(decoratorUUID) is not Decorator decorator
                || decorator.node != null && decorator.node.UUID != UUID.Empty
                || GetNode(targetUUID) is not TreeNode target
                || target is Service
                || targetUUID == decoratorUUID)
            {
                return false;
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> decoratorIncoming = topology.GetIncoming(decorator);
            if (decoratorIncoming.Count > 1
                || decoratorIncoming.Count == 1 && (decorator.parent?.UUID ?? UUID.Empty) != decoratorIncoming[0].Owner.uuid
                || !HasConsistentParentMetadata(decorator, decoratorIncoming)
                || topology.WouldCreateCycleAfterRemovingOccurrence(
                    decorator, target, decoratorIncoming.SingleOrDefault()))
            {
                return false;
            }

            IReadOnlyList<NodeReferenceOccurrence> targetIncoming = topology.GetIncoming(target);
            return targetIncoming.Count <= 1
                && HasConsistentParentMetadata(target, targetIncoming)
                && (targetIncoming.Count == 0
                    || (target.parent?.UUID ?? UUID.Empty) == targetIncoming[0].Owner.uuid);
        }

        /// <summary>Atomically moves an empty Decorator into a target occurrence and wraps that target.</summary>
        internal bool TryWrapDecoratorChild(UUID decoratorUUID, UUID targetUUID, string undoName)
        {
            if (!CanWrapDecoratorChild(decoratorUUID, targetUUID))
            {
                return false;
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            NodeReferenceOccurrence targetOccurrence = topology.GetIncoming(GetNode(targetUUID)).SingleOrDefault();
            NodeReferenceOccurrence decoratorOccurrence = topology.GetIncoming(GetNode(decoratorUUID)).SingleOrDefault();
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                if (decoratorOccurrence.Target != null)
                {
                    RemoveOccurrence(decoratorOccurrence);
                }

                Decorator decorator = (Decorator)GetNode(decoratorUUID);
                TreeNode target = GetNode(targetUUID);
                if (targetOccurrence.Target != null)
                {
                    int index = targetOccurrence.Index;
                    if (decoratorOccurrence.Target != null
                        && decoratorOccurrence.Owner.uuid == targetOccurrence.Owner.uuid
                        && decoratorOccurrence.Index >= 0
                        && decoratorOccurrence.Index < index)
                    {
                        index--;
                    }

                    SetReference(targetOccurrence.Owner, targetOccurrence.FieldName, index, decorator);
                    decorator.parent = new NodeReference(targetOccurrence.Owner.uuid);
                }
                else if (targetUUID == headNodeUUID)
                {
                    headNodeUUID = decorator.uuid;
                    decorator.parent = NodeReference.Empty;
                }
                else decorator.parent = NodeReference.Empty;
                SetReference(decorator, nameof(Decorator.node), -1, target);
                target.parent = new NodeReference(decorator.uuid);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Checks whether an empty Decorator is still owned by Head or one structural occurrence.</summary>
        internal bool CanDetachEmptyDecoratorToFree(UUID decoratorUUID)
        {
            if (GetNode(decoratorUUID) is not Decorator decorator
                || decorator.node != null && decorator.node.UUID != UUID.Empty)
            {
                return false;
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(decorator);
            return incoming.Count <= 1
                && HasConsistentParentMetadata(decorator, incoming)
                && (incoming.Count == 1 || headNodeUUID == decoratorUUID);
        }

        /// <summary>Gets whether an empty Decorator has no Head or structural occurrence ownership.</summary>
        internal bool IsFreeEmptyDecorator(UUID decoratorUUID)
        {
            if (GetNode(decoratorUUID) is not Decorator decorator
                || decorator.node != null && decorator.node.UUID != UUID.Empty
                || headNodeUUID == decoratorUUID)
            {
                return false;
            }

            return NodeTopologySnapshot.Create(EditorNodes).GetIncoming(decorator).Count == 0;
        }

        /// <summary>Atomically removes an empty Decorator from its structural occurrence or Head.</summary>
        internal bool TryDetachEmptyDecoratorToFree(UUID decoratorUUID, string undoName)
        {
            if (!CanDetachEmptyDecoratorToFree(decoratorUUID))
            {
                return false;
            }

            Decorator decorator = (Decorator)GetNode(decoratorUUID);
            NodeReferenceOccurrence occurrence = NodeTopologySnapshot.Create(EditorNodes)
                .GetIncoming(decorator)
                .SingleOrDefault();
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                if (occurrence.Target != null)
                {
                    RemoveOccurrence(occurrence);
                }
                else
                {
                    headNodeUUID = UUID.Empty;
                    decorator.parent = NodeReference.Empty;
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

        /// <summary>Checks whether an occupied Decorator can be extracted to its parent occurrence.</summary>
        internal bool CanExtractDecoratorToFree(UUID decoratorUUID)
        {
            if (GetNode(decoratorUUID) is not Decorator decorator
                || decorator.node?.UUID == UUID.Empty
                || GetNode(decorator.node.UUID) is not TreeNode child
                || child is Service)
            {
                return false;
            }
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            return topology.GetIncoming(decorator).Count <= 1
                && topology.GetIncoming(child).Count == 1
                && HasConsistentParentMetadata(decorator, topology.GetIncoming(decorator))
                && (child.parent?.UUID ?? UUID.Empty) == decorator.uuid;
        }

        /// <summary>Extracts an occupied Decorator and restores its child at the Decorator occurrence.</summary>
        internal bool TryExtractDecoratorToFree(UUID decoratorUUID, string undoName)
        {
            if (!CanExtractDecoratorToFree(decoratorUUID)) return false;
            Decorator decorator = (Decorator)GetNode(decoratorUUID);
            TreeNode child = GetNode(decorator.node.UUID);
            NodeReferenceOccurrence occurrence = NodeTopologySnapshot.Create(EditorNodes).GetIncoming(decorator).SingleOrDefault();
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                decorator.node = NodeReference.Empty;
                if (occurrence.Target == null && decoratorUUID == headNodeUUID)
                {
                    headNodeUUID = child.uuid;
                    child.parent = NodeReference.Empty;
                }
                else if (occurrence.Target != null)
                {
                    SetReference(occurrence.Owner, occurrence.FieldName, occurrence.Index, child);
                    child.parent = new NodeReference(occurrence.Owner.uuid);
                }
                else child.parent = NodeReference.Empty;
                decorator.parent = NodeReference.Empty;
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception) { RollbackTransaction(undoGroup, exception); return false; }
        }

        /// <summary>Extracts a Decorator and atomically wraps another structural target.</summary>
        internal bool CanExtractDecoratorAndWrapTarget(UUID decoratorUUID, UUID targetUUID)
        {
            if (!CanExtractDecoratorToFree(decoratorUUID) || decoratorUUID == targetUUID
                || GetNode(decoratorUUID) is not Decorator decorator
                || GetNode(targetUUID) is not TreeNode target || target is Service
                || decorator.node?.UUID == targetUUID) return false;
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(target);
            return incoming.Count <= 1 && HasConsistentParentMetadata(target, incoming)
                && (incoming.Count == 0 || (target.parent?.UUID ?? UUID.Empty) == incoming[0].Owner.uuid);
        }

        /// <summary>Performs extraction and wrapping in one tree transaction.</summary>
        internal bool TryExtractDecoratorAndWrapTarget(UUID decoratorUUID, UUID targetUUID, string undoName)
        {
            if (!CanExtractDecoratorAndWrapTarget(decoratorUUID, targetUUID)) return false;
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            Decorator decorator = (Decorator)GetNode(decoratorUUID);
            TreeNode child = GetNode(decorator.node.UUID);
            NodeReferenceOccurrence decoratorOccurrence = topology.GetIncoming(decorator).SingleOrDefault();
            NodeReferenceOccurrence targetOccurrence = topology.GetIncoming(GetNode(targetUUID)).SingleOrDefault();
            bool decoratorWasHead = decoratorUUID == headNodeUUID;
            bool targetWasHead = targetUUID == headNodeUUID;
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                decorator.node = NodeReference.Empty;
                if (decoratorOccurrence.Target != null)
                {
                    SetReference(decoratorOccurrence.Owner, decoratorOccurrence.FieldName, decoratorOccurrence.Index, child);
                    child.parent = new NodeReference(decoratorOccurrence.Owner.uuid);
                }
                else if (decoratorWasHead)
                {
                    headNodeUUID = child.uuid;
                    child.parent = NodeReference.Empty;
                }
                else child.parent = NodeReference.Empty;

                if (targetOccurrence.Target != null)
                {
                    SetReference(targetOccurrence.Owner, targetOccurrence.FieldName, targetOccurrence.Index, decorator);
                    decorator.parent = new NodeReference(targetOccurrence.Owner.uuid);
                }
                else if (targetWasHead) { headNodeUUID = decorator.uuid; decorator.parent = NodeReference.Empty; }
                else decorator.parent = NodeReference.Empty;
                decorator.node = new NodeReference(GetNode(targetUUID).uuid);
                GetNode(targetUUID).parent = new NodeReference(decorator.uuid);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception) { RollbackTransaction(undoGroup, exception); return false; }
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

        /// <summary>Checks whether replacing one occupied structural occurrence can bypass nodes to a reachable descendant.</summary>
        internal bool CanRedirectReferenceChain(UUID ownerUUID, string fieldName, int sourceIndex, UUID targetUUID)
        {
            if (TryGetOrderedChainTargetIndex(ownerUUID, fieldName, sourceIndex, targetUUID, out _))
            {
                return true;
            }

            return TryGetStructuralPromotion(
                ownerUUID,
                fieldName,
                sourceIndex,
                targetUUID,
                out _,
                out _,
                out _,
                out _);
        }

        /// <summary>Atomically bypasses ordered or structural nodes while keeping skipped nodes authored but unreachable.</summary>
        internal bool TryRedirectReferenceChain(UUID ownerUUID, string fieldName, int sourceIndex, UUID targetUUID, string undoName)
        {
            if (TryGetOrderedChainTargetIndex(ownerUUID, fieldName, sourceIndex, targetUUID, out int targetIndex)
                && TryResolveCollection(ownerUUID, fieldName, out TreeNode orderedOwner, out INodeReferenceCollectionFieldAccessor orderedField)
                && orderedField.Get(orderedOwner) is IList orderedCollection)
            {
                UUID[] detached = orderedCollection.Cast<object>()
                    .Skip(sourceIndex)
                    .Take(targetIndex - sourceIndex)
                    .OfType<INodeReference>()
                    .Select(reference => reference.UUID)
                    .Where(uuid => uuid != UUID.Empty)
                    .ToArray();
                int orderedUndoGroup = BeginTransaction(undoName, true);
                try
                {
                    for (int index = targetIndex - 1; index >= sourceIndex; index--)
                    {
                        RemoveCollectionEntry(orderedOwner, fieldName, index);
                    }

                    foreach (UUID uuid in detached)
                    {
                        ClearParentWhenDetached(uuid);
                    }

                    CompleteTransaction(orderedUndoGroup);
                    return true;
                }
                catch (Exception exception)
                {
                    RollbackTransaction(orderedUndoGroup, exception);
                    return false;
                }
            }

            if (!TryGetStructuralPromotion(
                    ownerUUID,
                    fieldName,
                    sourceIndex,
                    targetUUID,
                    out TreeNode owner,
                    out TreeNode current,
                    out TreeNode candidate,
                    out NodeReferenceOccurrence predecessor))
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                SetReference(owner, fieldName, sourceIndex, candidate);
                candidate.parent = new NodeReference(owner.uuid);
                RemoveOccurrence(predecessor);
                ClearParentWhenDetached(current.uuid, candidate.uuid);

                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Finds a later entry in the ordered Sequence or Loop event collection.</summary>
        private bool TryGetOrderedChainTargetIndex(UUID ownerUUID, string fieldName, int sourceIndex, UUID targetUUID, out int targetIndex)
        {
            targetIndex = -1;
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
                    targetIndex = index;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Validates promotion of a uniquely reachable structural descendant into one occupied port.</summary>
        private bool TryGetStructuralPromotion(
            UUID ownerUUID,
            string fieldName,
            int sourceIndex,
            UUID targetUUID,
            out TreeNode owner,
            out TreeNode current,
            out TreeNode candidate,
            out NodeReferenceOccurrence predecessor)
        {
            owner = null;
            current = null;
            candidate = null;
            predecessor = default;
            if (!TryResolveReference(ownerUUID, fieldName, sourceIndex, out owner, out INodeReference reference, out bool raw)
                || raw
                || reference == null
                || reference.UUID == UUID.Empty
                || GetNode(reference.UUID) is not TreeNode resolvedCurrent
                || GetNode(targetUUID) is not TreeNode resolvedCandidate
                || resolvedCandidate is Service
                || !IsCompatibleReference(owner, fieldName, resolvedCandidate)
                || !CanDetach(ownerUUID, fieldName, sourceIndex, resolvedCurrent.uuid))
            {
                return false;
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            if (topology.GetValidationErrors().Count > 0
                || !TryFindUniqueStructuralPath(topology, resolvedCurrent, resolvedCandidate, out List<NodeReferenceOccurrence> path)
                || path.Count == 0)
            {
                return false;
            }

            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(resolvedCandidate);
            predecessor = path[^1];
            if (incoming.Count != 1 || incoming[0].Owner != predecessor.Owner
                || incoming[0].FieldName != predecessor.FieldName || incoming[0].Index != predecessor.Index)
            {
                return false;
            }

            current = resolvedCurrent;
            candidate = resolvedCandidate;
            return true;
        }

        /// <summary>Finds exactly one non-Service, non-Raw ownership path between two nodes.</summary>
        private static bool TryFindUniqueStructuralPath(
            NodeTopologySnapshot topology,
            TreeNode source,
            TreeNode target,
            out List<NodeReferenceOccurrence> result)
        {
            result = null;
            List<NodeReferenceOccurrence> path = new();
            HashSet<UUID> active = new();
            int pathCount = 0;
            FindStructuralPaths(topology, source, target, path, active, ref result, ref pathCount);
            return pathCount == 1;
        }

        /// <summary>Enumerates structural paths until a second path proves the promotion ambiguous.</summary>
        private static void FindStructuralPaths(
            NodeTopologySnapshot topology,
            TreeNode current,
            TreeNode target,
            List<NodeReferenceOccurrence> path,
            ISet<UUID> active,
            ref List<NodeReferenceOccurrence> result,
            ref int pathCount)
        {
            if (pathCount > 1 || current == null || !active.Add(current.uuid))
            {
                return;
            }

            if (current == target)
            {
                pathCount++;
                if (pathCount == 1)
                {
                    result = new List<NodeReferenceOccurrence>(path);
                }

                active.Remove(current.uuid);
                return;
            }

            foreach (NodeReferenceOccurrence occurrence in topology.GetOutgoing(current))
            {
                if (occurrence.Kind != NodeOwnershipKind.Structural)
                {
                    continue;
                }

                path.Add(occurrence);
                FindStructuralPaths(topology, occurrence.Target, target, path, active, ref result, ref pathCount);
                path.RemoveAt(path.Count - 1);
            }

            active.Remove(current.uuid);
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
            IReadOnlyDictionary<UUID, Vector2> graphPositions = null,
            bool recordUndo = true)
        {
            if (!CanAddNodes(addedNodes))
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, recordUndo);
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

        /// <summary>Adds an empty Decorator and atomically wraps an occupied structural reference.</summary>
        internal bool TryAddAndWrapReference(UUID ownerUUID, string fieldName, int index,
            IReadOnlyList<TreeNode> addedNodes, UUID rootUUID, string undoName,
            IReadOnlyDictionary<UUID, Vector2> graphPositions = null)
        {
            if (!TryResolveReference(ownerUUID, fieldName, index, out TreeNode owner, out INodeReference destination, out bool raw)
                || raw || destination?.UUID == UUID.Empty || !CanAddNodes(addedNodes)
                || addedNodes.FirstOrDefault(node => node?.uuid == rootUUID) is not Decorator decorator
                || decorator.node?.UUID != UUID.Empty)
            {
                return false;
            }
            TreeNode target = GetNode(destination.UUID);
            if (target == null || !IsCompatibleReference(owner, fieldName, decorator)
                || !IsCompatibleReference(decorator, nameof(Decorator.node), target))
            {
                return false;
            }
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            if (topology.GetIncoming(target).Count != 1 || topology.HasInvalidParentMetadata(target)) return false;
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                nodes.AddRange(addedNodes);
                SetReference(owner, fieldName, index, decorator);
                decorator.parent = new NodeReference(owner.uuid);
                decorator.node = new NodeReference(target.uuid);
                target.parent = new NodeReference(decorator.uuid);
                MergeGraphPositions(graphPositions);
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception) { RollbackTransaction(undoGroup, exception); return false; }
        }

        /// <summary>Deletes selected decorators by bypassing them to their first surviving child.</summary>
        /// <remarks>Non-decorator selections retain the normal delete semantics. This keeps a decorator
        /// stack an editable single-child wrapper chain rather than detaching its surviving child.</remarks>
        internal bool TryDeleteNodesWithDecoratorUnwrap(ISet<UUID> removedUUIDs, string undoName)
        {
            if (removedUUIDs == null || removedUUIDs.Count == 0)
            {
                return false;
            }

            HashSet<UUID> decorators = nodes.Where(node => node is Decorator && removedUUIDs.Contains(node.uuid))
                .Select(node => node.uuid).ToHashSet();
            if (decorators.Count == 0)
            {
                return TryDeleteNodes(removedUUIDs, undoName);
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                UUID ResolveSurvivor(UUID uuid)
                {
                    HashSet<UUID> visited = new();
                    while (decorators.Contains(uuid) && visited.Add(uuid))
                    {
                        uuid = (GetNode(uuid) as Decorator)?.node?.UUID ?? UUID.Empty;
                    }

                    return uuid == UUID.Empty || removedUUIDs.Contains(uuid) ? UUID.Empty : uuid;
                }

                foreach (TreeNode owner in nodes.Where(node => node != null && !removedUUIDs.Contains(node.uuid)).ToArray())
                {
                    NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
                    foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
                    {
                        INodeReference reference = field.Get(owner);
                        if (field.Name == nameof(TreeNode.parent) || reference == null || !decorators.Contains(reference.UUID))
                            continue;
                        SetReference(owner, field.Name, -1, GetNode(ResolveSurvivor(reference.UUID)));
                    }

                    foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
                    {
                        IList entries = field.Get(owner);
                        if (entries == null) continue;
                        for (int index = 0; index < entries.Count; index++)
                        {
                            if (entries[index] is INodeReference reference && decorators.Contains(reference.UUID))
                            {
                                SetReference(owner, field.Name, index, GetNode(ResolveSurvivor(reference.UUID)));
                            }
                        }
                    }
                }

                if (decorators.Contains(headNodeUUID))
                {
                    headNodeUUID = ResolveSurvivor(headNodeUUID);
                }

                foreach (TreeNode owner in nodes.Where(node => node != null && !removedUUIDs.Contains(node.uuid)).ToArray())
                {
                    ClearReferencesTo(owner, removedUUIDs);
                }

                nodes.RemoveAll(node => node == null || removedUUIDs.Contains(node.uuid));
                graphLayout?.RemoveNodes(removedUUIDs);
                ReconcileUnambiguousParents();
                if (GetNode(headNodeUUID) is TreeNode head) head.parent = NodeReference.Empty;
                CompleteTransaction(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Atomically rewires one verified decorator chain into the requested wrapper order.</summary>
        internal bool TryReorderDecoratorStack(IReadOnlyList<UUID> orderedDecorators, string undoName)
        {
            if (orderedDecorators == null || orderedDecorators.Count < 2
                || orderedDecorators.Distinct().Count() != orderedDecorators.Count
                || orderedDecorators.Any(uuid => GetNode(uuid) is not Decorator))
            {
                return false;
            }

            HashSet<UUID> requested = orderedDecorators.ToHashSet();
            Decorator outer = nodes.OfType<Decorator>().FirstOrDefault(decorator => requested.Contains(decorator.uuid)
                && !requested.Contains(decorator.parent?.UUID ?? UUID.Empty));
            if (outer == null)
            {
                return false;
            }
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(outer);
            NodeReferenceOccurrence external = incoming.FirstOrDefault(occurrence => occurrence.Kind == NodeOwnershipKind.Structural);
            if (incoming.Count(occurrence => occurrence.Kind == NodeOwnershipKind.Structural) > 1
                || external.Target == null && headNodeUUID != outer.uuid)
            {
                return false;
            }

            UUID childUUID = outer.node?.UUID ?? UUID.Empty;
            for (int index = 1; index < orderedDecorators.Count; index++)
            {
                if (GetNode(childUUID) is not Decorator child || !requested.Contains(child.uuid)) return false;
                childUUID = child.node?.UUID ?? UUID.Empty;
            }
            if (GetNode(childUUID) is Decorator || requested.Contains(childUUID)) return false;

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                TreeNode first = GetNode(orderedDecorators[0]);
                if (headNodeUUID == outer.uuid)
                {
                    headNodeUUID = first.uuid;
                }
                else
                {
                    SetReference(external.Owner, external.FieldName, external.Index, first);
                }

                for (int index = 0; index < orderedDecorators.Count; index++)
                {
                    Decorator decorator = (Decorator)GetNode(orderedDecorators[index]);
                    SetReference(decorator, nameof(Decorator.node), -1,
                        index + 1 < orderedDecorators.Count ? GetNode(orderedDecorators[index + 1]) : GetNode(childUUID));
                }

                ReconcileUnambiguousParents();
                if (GetNode(headNodeUUID) is TreeNode head) head.parent = NodeReference.Empty;
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
                || !raw
                    && destination != null
                    && destination.UUID != UUID.Empty
                    && GetNode(destination.UUID) != null
                    && !CanDetach(ownerUUID, fieldName, index, destination.UUID))
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

        /// <summary>Checks parent metadata for both owned and genuinely free nodes.</summary>
        private static bool HasConsistentParentMetadata(
            TreeNode node,
            IReadOnlyList<NodeReferenceOccurrence> incoming)
        {
            if (node == null || incoming == null || incoming.Count > 1) return false;
            UUID declaredParent = node.parent?.UUID ?? UUID.Empty;
            return incoming.Count == 0
                ? declaredParent == UUID.Empty
                : declaredParent == incoming[0].Owner.uuid;
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
                current?.HasExitPosition == true ? current.ExitPosition : null,
                current?.Groups);
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
