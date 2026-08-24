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
        /// <summary>Replaces one upgradeable node while preserving its identity and hosted services.</summary>
        internal bool TryUpgradeNode(TreeNode node, out TreeNode upgradedNode)
        {
            upgradedNode = null;
            if (node == null || !node.CanUpgrade())
            {
                return false;
            }

            try
            {
                upgradedNode = node.Upgrade();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                upgradedNode = null;
                return false;
            }

            int index = upgradedNode == null ? -1 : nodes.IndexOf(node);
            if (index < 0)
            {
                upgradedNode = null;
                return false;
            }

            Undo.RecordObject(this, $"Upgrade node {node.name}");
            upgradedNode.UUID = node.UUID;
            upgradedNode.name = node.name;
            upgradedNode.parent = node.parent;
            if (ServiceHostNodeUtility.TryAsServiceHost(node, out var oldHost)
                && ServiceHostNodeUtility.TryAsServiceHost(upgradedNode, out var upgradedHost)
                && oldHost.Services != null
                && oldHost.Services.Count > 0)
            {
                var upgradedServices = upgradedHost.EnsureServices();
                if (upgradedServices.Count == 0)
                {
                    upgradedServices.AddRange(oldHost.Services);
                }
            }

            nodes[index] = upgradedNode;
            RegenerateTable();
            EditorUtility.SetDirty(this);
            return true;
        }

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
            return TryResolveCollection(ownerUUID, fieldName, out TreeNode owner, out INodeReferenceListSlot field)
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
                || topology.WouldCreateCycleAfterRemovingOccurrence(
                    decorator, target, decoratorIncoming.SingleOrDefault()))
            {
                return false;
            }

            IReadOnlyList<NodeReferenceOccurrence> targetIncoming = topology.GetIncoming(target);
            // Parent is derived metadata. A free decorator must remain attachable even when a
            // previous structural edit left stale cached metadata behind; the transaction below
            // rewrites both parent values from the authored occurrence graph.
            return targetIncoming.Count <= 1;
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
            bool decoratorWasHead = decoratorUUID == headNodeUUID;
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                if (decoratorOccurrence.Target != null)
                {
                    RemoveOccurrence(decoratorOccurrence);
                }
                else if (decoratorWasHead)
                {
                    headNodeUUID = UUID.Empty;
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
                }

                // Removing an occurrence always makes this empty decorator a free node.
                // Keep parent metadata consistent so later wrap/connect validation can accept it.
                decorator.parent = NodeReference.Empty;

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

        /// <summary>Checks whether one contiguous decorator block can be moved together to wrap a target.</summary>
        internal bool CanExtractDecoratorBlockAndWrapTarget(IReadOnlyList<UUID> decoratorUUIDs, UUID targetUUID)
        {
            if (decoratorUUIDs == null || decoratorUUIDs.Count < 2
                || decoratorUUIDs.Distinct().Count() != decoratorUUIDs.Count
                || decoratorUUIDs.Any(uuid => GetNode(uuid) is not Decorator)
                || decoratorUUIDs.Contains(targetUUID)
                || GetNode(targetUUID) is not TreeNode target || target is Service)
            {
                return false;
            }

            List<Decorator> block = decoratorUUIDs.Select(uuid => (Decorator)GetNode(uuid)).ToList();
            for (int index = 0; index < block.Count - 1; index++)
            {
                if (block[index].node?.UUID != block[index + 1].uuid)
                {
                    return false;
                }
            }

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> sourceIncoming = topology.GetIncoming(block[0]);
            IReadOnlyList<NodeReferenceOccurrence> targetIncoming = topology.GetIncoming(target);
            UUID restoredChildUUID = block[^1].node?.UUID ?? UUID.Empty;
            return sourceIncoming.Count <= 1
                && targetIncoming.Count <= 1
                && HasConsistentParentMetadata(block[0], sourceIncoming)
                && HasConsistentParentMetadata(target, targetIncoming)
                && restoredChildUUID != targetUUID
                && !topology.WouldCreateCycleAfterRemovingOccurrence(
                    block[0], target, sourceIncoming.SingleOrDefault());
        }

        /// <summary>Atomically moves a contiguous decorator block so its inner wrapper owns the target.</summary>
        internal bool TryExtractDecoratorBlockAndWrapTarget(
            IReadOnlyList<UUID> decoratorUUIDs,
            UUID targetUUID,
            string undoName)
        {
            if (!CanExtractDecoratorBlockAndWrapTarget(decoratorUUIDs, targetUUID))
            {
                return false;
            }

            List<Decorator> block = decoratorUUIDs.Select(uuid => (Decorator)GetNode(uuid)).ToList();
            Decorator outer = block[0];
            Decorator inner = block[^1];
            TreeNode restoredChild = GetNode(inner.node?.UUID ?? UUID.Empty);
            TreeNode target = GetNode(targetUUID);
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            NodeReferenceOccurrence sourceOccurrence = topology.GetIncoming(outer).SingleOrDefault();
            NodeReferenceOccurrence targetOccurrence = topology.GetIncoming(target).SingleOrDefault();
            bool sourceWasHead = outer.uuid == headNodeUUID;
            bool targetWasHead = target.uuid == headNodeUUID;
            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                if (sourceOccurrence.Target != null)
                {
                    if (restoredChild != null)
                    {
                        SetReference(sourceOccurrence.Owner, sourceOccurrence.FieldName, sourceOccurrence.Index, restoredChild);
                        restoredChild.parent = new NodeReference(sourceOccurrence.Owner.uuid);
                    }
                    else
                    {
                        RemoveOccurrence(sourceOccurrence);
                        if (targetOccurrence.Target != null
                            && targetOccurrence.Owner.uuid == sourceOccurrence.Owner.uuid
                            && targetOccurrence.FieldName == sourceOccurrence.FieldName
                            && targetOccurrence.Index > sourceOccurrence.Index)
                        {
                            targetOccurrence = new NodeReferenceOccurrence(
                                targetOccurrence.Owner,
                                targetOccurrence.Target,
                                targetOccurrence.FieldName,
                                targetOccurrence.Index - 1,
                                targetOccurrence.Kind);
                        }
                    }
                }
                else if (sourceWasHead)
                {
                    headNodeUUID = restoredChild?.uuid ?? UUID.Empty;
                    if (restoredChild != null)
                    {
                        restoredChild.parent = NodeReference.Empty;
                    }
                }
                else if (restoredChild != null)
                {
                    // A free decorator block has no structural occurrence to replace.
                    // Its former child becomes free before the block wraps the new target.
                    restoredChild.parent = NodeReference.Empty;
                }

                if (targetOccurrence.Target != null)
                {
                    SetReference(targetOccurrence.Owner, targetOccurrence.FieldName, targetOccurrence.Index, outer);
                    outer.parent = new NodeReference(targetOccurrence.Owner.uuid);
                }
                else if (targetWasHead)
                {
                    headNodeUUID = outer.uuid;
                    outer.parent = NodeReference.Empty;
                }
                else
                {
                    outer.parent = NodeReference.Empty;
                }

                for (int index = 0; index < block.Count - 1; index++)
                {
                    SetReference(block[index], nameof(Decorator.node), -1, block[index + 1]);
                }

                SetReference(inner, nameof(Decorator.node), -1, target);
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
            if (!TryResolveCollection(ownerUUID, fieldName, out TreeNode owner, out INodeReferenceListSlot field)
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
                int insertionIndex = Math.Clamp(index < 0 ? field.Count : index, 0, field.Count);
                if (previousOccurrence.Target != null)
                {
                    if (previousOccurrence.Owner.uuid == ownerUUID
                        && previousOccurrence.FieldName == fieldName
                        && previousOccurrence.Index < insertionIndex)
                    {
                        insertionIndex--;
                    }

                    RemoveOccurrence(previousOccurrence);
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
        /// <param name="expectEmptyReference">Requires the current occurrence to still be empty before removal.</param>
        internal bool TryDisconnectReference(
            UUID ownerUUID,
            string fieldName,
            int index,
            string undoName,
            UUID expectedTargetUUID = default,
            bool expectEmptyReference = false)
        {
            if (!TryResolveReference(ownerUUID, fieldName, index, out TreeNode owner, out INodeReference reference, out bool raw))
            {
                return false;
            }

            bool isEmptyReference = reference == null || reference.UUID == UUID.Empty;
            if (expectEmptyReference != isEmptyReference
                || expectedTargetUUID != UUID.Empty && (isEmptyReference || reference.UUID != expectedTargetUUID)
                || index < 0 && isEmptyReference)
            {
                return false;
            }

            UUID detachedUUID = isEmptyReference ? UUID.Empty : reference.UUID;
            TreeNode detachedTarget = detachedUUID == UUID.Empty ? null : GetNode(detachedUUID);
            if (!raw && !isEmptyReference && detachedTarget != null
                && !CanDetach(ownerUUID, fieldName, index, detachedUUID))
            {
                return false;
            }

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

                if (!raw && detachedTarget != null)
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
            if (!TryResolveCollection(ownerUUID, fieldName, out TreeNode owner, out INodeReferenceListSlot field)
                || sourceIndex < 0
                || sourceIndex >= field.Count)
            {
                return false;
            }

            int targetIndex = Math.Clamp(destinationIndex, 0, field.Count - 1);
            if (sourceIndex == targetIndex)
            {
                return false;
            }

            int undoGroup = BeginTransaction(undoName, true);
            try
            {
                if (field is not IIndexedNodeReferenceListSlot indexed)
                {
                    return false;
                }

                indexed.Move(sourceIndex, targetIndex);
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
                && TryResolveCollection(ownerUUID, fieldName, out TreeNode orderedOwner, out INodeReferenceListSlot orderedField))
            {
                UUID[] detached = Enumerable.Range(sourceIndex, targetIndex - sourceIndex)
                    .Select(orderedField.GetReference)
                    .Select(reference => reference?.UUID ?? UUID.Empty)
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
                || !TryResolveCollection(ownerUUID, fieldName, out _, out INodeReferenceListSlot field))
            {
                return false;
            }

            for (int index = sourceIndex + 1; index < field.Count; index++)
            {
                if (field.GetReference(index)?.UUID == targetUUID)
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
                || !TryResolveCollection(ownerUUID, fieldName, out _, out INodeReferenceListSlot field))
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
                    foreach (CollectedReference collected in CollectReferences(owner))
                    {
                        INodeReference reference = collected.Reference;
                        if (collected.Path == nameof(TreeNode.parent) || reference == null || !decorators.Contains(reference.UUID))
                            continue;
                        SetReference(owner, collected.Path, GetNode(ResolveSurvivor(reference.UUID)));
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

            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(EditorNodes);
            HashSet<UUID> requested = orderedDecorators.ToHashSet();
            List<Decorator> outers = orderedDecorators
                .Select(uuid => (Decorator)GetNode(uuid))
                .Where(decorator => !topology.GetIncoming(decorator)
                    .Any(occurrence => requested.Contains(occurrence.Owner.uuid)))
                .ToList();
            if (outers.Count != 1)
            {
                return false;
            }

            // Parent metadata is a cache. Reorder must derive the stack root from authored
            // references so an earlier detach cannot prevent an otherwise valid reorder.
            Decorator outer = outers[0];
            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(outer);
            NodeReferenceOccurrence external = incoming.SingleOrDefault();
            bool isFreeStack = incoming.Count == 0 && headNodeUUID != outer.uuid;
            if (incoming.Count > 1)
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
                else if (!isFreeStack)
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
                if (!TryResolveCollection(ownerUUID, fieldName, out _, out INodeReferenceListSlot collectionField))
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

            string path = index < 0 ? fieldName : fieldName + "[" + index + "]";
            if (!NodeReferenceStructureProvider.TryGetReference(owner, path, out reference))
            {
                return false;
            }

            raw = reference?.IsRawReference == true;
            return true;
        }

        private bool TryResolveCollection(
            UUID ownerUUID,
            string fieldName,
            out TreeNode owner,
            out INodeReferenceListSlot field)
        {
            owner = GetNode(ownerUUID);
            field = owner == null || fieldName == nameof(TreeNode.parent)
                ? null
                : NodeReferenceStructureProvider.GetListSlots(owner)
                    .FirstOrDefault(candidate => candidate.Name == fieldName);
            return field != null;
        }

        private static bool IsCompatibleReference(TreeNode owner, string fieldName, TreeNode candidate)
        {
            return fieldName != nameof(ServiceHostNode.services)
                ? candidate is not Service
                : candidate is Service && owner?.CanEditServices() == true;
        }

        private static bool IsRaw(INodeReferenceListSlot field)
        {
            return field?.Count > 0 && field.GetReference(0)?.IsRawReference == true;
        }

        private static void SetReference(TreeNode owner, string fieldName, int index, TreeNode target)
        {
            string path = index < 0 ? fieldName : fieldName + "[" + index + "]";
            SetReference(owner, path, target);
        }

        private static void SetReference(TreeNode owner, string path, TreeNode target)
        {
            if (!NodeReferenceStructureProvider.TrySetReference(owner, path, target))
            {
                throw new InvalidOperationException($"Reference path '{path}' is not writable on '{owner?.GetType().FullName}'.");
            }
        }

        private static void InsertCollectionEntry(
            TreeNode owner,
            INodeReferenceListSlot field,
            int index,
            TreeNode target)
        {
            field.Insert(index < 0 ? field.Count : index, target);
        }

        private static void RemoveCollectionEntry(TreeNode owner, string fieldName, int index)
        {
            INodeReferenceListSlot field = NodeReferenceStructureProvider.GetListSlots(owner)
                .Single(candidate => candidate.Name == fieldName);
            if (field is IIndexedNodeReferenceListSlot indexed)
            {
                indexed.RemoveAt(index);
                return;
            }

            throw new InvalidOperationException($"Reference collection '{fieldName}' is not indexed-writable.");
        }

        private static void MoveCollectionEntry(
            TreeNode owner,
            INodeReferenceListSlot field,
            int sourceIndex,
            int destinationIndex)
        {
            if (field is IIndexedNodeReferenceListSlot indexed)
            {
                indexed.Move(sourceIndex, destinationIndex);
                return;
            }

            throw new InvalidOperationException($"Reference collection '{field.Name}' is not indexed-writable.");
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
            foreach (CollectedReference collected in CollectReferences(owner).OrderByDescending(item => item.Path))
            {
                if (collected.Path == nameof(TreeNode.parent)
                    || !removedUUIDs.Contains(collected.Reference?.UUID ?? UUID.Empty))
                {
                    continue;
                }

                int bracket = collected.Path.IndexOf('[');
                if (bracket < 0)
                {
                    SetReference(owner, collected.Path, null);
                    continue;
                }

                if (!collected.Path.EndsWith("]", StringComparison.Ordinal)
                    || !int.TryParse(collected.Path.Substring(bracket + 1, collected.Path.Length - bracket - 2), out int index))
                {
                    continue;
                }

                RemoveCollectionEntry(owner, collected.Path.Substring(0, bracket), index);
            }
        }

        private static List<CollectedReference> CollectReferences(TreeNode owner)
        {
            ReferenceCollector collector = new();
            if (owner != null)
            {
                NodeDescriptorProvider.Get(owner.GetType()).VisitMembers(owner, collector);
            }

            return collector.References;
        }

        private sealed class ReferenceCollector : NodeMemberVisitor
        {
            public List<CollectedReference> References { get; } = new();

            protected override void OnNodeReference(string path, INodeReference reference)
            {
                References.Add(new CollectedReference(path, reference));
            }

            protected override void OnVariableBinding(string path, Aethiumian.AI.Variables.IVariableBinding binding)
            {
            }
        }

        private readonly struct CollectedReference
        {
            public CollectedReference(string path, INodeReference reference)
            {
                Path = path;
                Reference = reference;
            }

            public string Path { get; }
            public INodeReference Reference { get; }
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
