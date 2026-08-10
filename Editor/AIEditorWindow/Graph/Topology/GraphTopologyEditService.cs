using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>Identifies one authored node-reference occurrence without relying on presentation relations.</summary>
    internal readonly struct GraphReferenceAddress : IEquatable<GraphReferenceAddress>
    {
        internal GraphReferenceAddress(UUID ownerUUID, string fieldName, int index = -1)
        {
            OwnerUUID = ownerUUID;
            FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
            Index = index;
        }

        internal UUID OwnerUUID { get; }
        internal string FieldName { get; }
        internal int Index { get; }

        public bool Equals(GraphReferenceAddress other)
        {
            return OwnerUUID == other.OwnerUUID && FieldName == other.FieldName && Index == other.Index;
        }

        public override bool Equals(object obj) => obj is GraphReferenceAddress other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(OwnerUUID, FieldName, Index);
    }

    /// <summary>Describes the serialized storage shape of an authored node reference.</summary>
    internal enum GraphReferenceSlotKind
    {
        Single,
        Collection,
        Service,
        Raw,
        ProbabilityWeighted,
        PseudoProbabilityWeighted,
    }

    /// <summary>Resolved authoritative metadata for one serialized authored reference field.</summary>
    internal readonly struct GraphReferenceSlotDescriptor
    {
        internal GraphReferenceSlotDescriptor(GraphReferenceSlotKind kind, SerializedProperty field)
        {
            Kind = kind;
            Field = field;
        }

        internal GraphReferenceSlotKind Kind { get; }
        internal SerializedProperty Field { get; }
        internal bool IsCollection => Kind is GraphReferenceSlotKind.Collection
            or GraphReferenceSlotKind.Service
            or GraphReferenceSlotKind.ProbabilityWeighted
            or GraphReferenceSlotKind.PseudoProbabilityWeighted;
        internal bool IsStructural => Kind is GraphReferenceSlotKind.Single
            or GraphReferenceSlotKind.Collection
            or GraphReferenceSlotKind.ProbabilityWeighted
            or GraphReferenceSlotKind.PseudoProbabilityWeighted;
    }

    /// <summary>Reports the outcome of one atomic topology edit command.</summary>
    internal readonly struct GraphTopologyEditResult
    {
        private GraphTopologyEditResult(bool succeeded, string error, IReadOnlyCollection<UUID> affectedUUIDs)
        {
            Succeeded = succeeded;
            Error = error;
            AffectedUUIDs = affectedUUIDs;
        }

        internal bool Succeeded { get; }
        internal string Error { get; }
        internal IReadOnlyCollection<UUID> AffectedUUIDs { get; }

        internal static GraphTopologyEditResult Success(params UUID[] affectedUUIDs)
        {
            return new GraphTopologyEditResult(true, null, affectedUUIDs.Where(uuid => uuid != UUID.Empty).Distinct().ToArray());
        }

        internal static GraphTopologyEditResult Failure(string error)
        {
            return new GraphTopologyEditResult(false, error, Array.Empty<UUID>());
        }
    }

    /// <summary>
    /// Owns editor-only mutations of authored node references.
    /// Canvas interactions must call this service rather than writing a node or layout directly.
    /// </summary>
    internal sealed class GraphTopologyEditService
    {
        private readonly BehaviourTreeData tree;

        internal GraphTopologyEditService(BehaviourTreeData tree)
        {
            this.tree = tree ?? throw new ArgumentNullException(nameof(tree));
        }

        /// <summary>Assigns a target to an empty single reference or appends it to a collection.</summary>
        internal GraphTopologyEditResult Connect(GraphReferenceAddress address, UUID targetUUID)
        {
            if (!TryGetTarget(address, targetUUID, out TreeNode owner, out TreeNode target, out GraphReferenceSlotDescriptor descriptor, out string error))
            {
                return GraphTopologyEditResult.Failure(error);
            }

            if (descriptor.IsCollection)
            {
                return InsertResolved(address, int.MaxValue, owner, target, descriptor);
            }

            SerializedProperty reference = GetReferenceProperty(descriptor.Field, descriptor.Kind);
            if (ReadTargetUUID(reference) != UUID.Empty)
            {
                return GraphTopologyEditResult.Failure("The single reference is occupied. Use Replace instead.");
            }

            if (descriptor.IsStructural && WouldCreateCycle(owner, target))
            {
                return GraphTopologyEditResult.Failure("The structural connection would create a cycle.");
            }

            if (descriptor.IsStructural && HasStructuralIncoming(target))
            {
                return GraphTopologyEditResult.Failure("The target already has a structural parent. Duplicate the node or use a Subtree action instead.");
            }

            return Mutate($"Connect {address.FieldName}", () => WriteReference(reference, descriptor.Kind, targetUUID), owner.uuid, targetUUID);
        }

        /// <summary>Clears one single reference or removes one collection occurrence.</summary>
        internal GraphTopologyEditResult Disconnect(GraphReferenceAddress address)
        {
            if (!TryResolve(address, out TreeNode owner, out GraphReferenceSlotDescriptor descriptor, out string error))
            {
                return GraphTopologyEditResult.Failure(error);
            }

            if (descriptor.IsCollection)
            {
                if (address.Index < 0 || address.Index >= descriptor.Field.arraySize)
                {
                    return GraphTopologyEditResult.Failure("The collection occurrence does not exist.");
                }

                UUID removed = ReadTargetUUID(GetReferenceProperty(descriptor.Field.GetArrayElementAtIndex(address.Index), descriptor.Kind));
                return Mutate($"Remove {address.FieldName}", () => descriptor.Field.DeleteArrayElementAtIndex(address.Index), owner.uuid, removed);
            }

            UUID previous = ReadTargetUUID(GetReferenceProperty(descriptor.Field, descriptor.Kind));
            if (previous == UUID.Empty)
            {
                return GraphTopologyEditResult.Failure("The single reference is already empty.");
            }

            return Mutate($"Disconnect {address.FieldName}", () => WriteReference(GetReferenceProperty(descriptor.Field, descriptor.Kind), descriptor.Kind, UUID.Empty), owner.uuid, previous);
        }

        /// <summary>Replaces an occupied reference without changing weighted-entry metadata.</summary>
        internal GraphTopologyEditResult Replace(GraphReferenceAddress address, UUID targetUUID)
        {
            if (!TryGetTarget(address, targetUUID, out TreeNode owner, out TreeNode target, out GraphReferenceSlotDescriptor descriptor, out string error))
            {
                return GraphTopologyEditResult.Failure(error);
            }

            if (descriptor.IsCollection && (address.Index < 0 || address.Index >= descriptor.Field.arraySize))
            {
                return GraphTopologyEditResult.Failure("Replace requires an existing collection occurrence.");
            }

            SerializedProperty reference = descriptor.IsCollection
                ? GetReferenceProperty(descriptor.Field.GetArrayElementAtIndex(address.Index), descriptor.Kind)
                : GetReferenceProperty(descriptor.Field, descriptor.Kind);
            UUID previous = ReadTargetUUID(reference);
            if (descriptor.IsStructural && WouldCreateCycle(owner, target))
            {
                return GraphTopologyEditResult.Failure("The structural connection would create a cycle.");
            }

            if (descriptor.IsStructural && HasStructuralIncoming(target))
            {
                return GraphTopologyEditResult.Failure("The target already has a structural parent. Duplicate the node or use a Subtree action instead.");
            }

            if (previous == targetUUID)
            {
                return GraphTopologyEditResult.Failure("The reference already points to this target.");
            }

            return Mutate($"Replace {address.FieldName}", () => WriteReference(reference, descriptor.Kind, targetUUID), owner.uuid, previous, targetUUID);
        }

        /// <summary>Inserts a collection occurrence at a deterministic index.</summary>
        internal GraphTopologyEditResult Insert(GraphReferenceAddress address, int index, UUID targetUUID)
        {
            if (!TryGetTarget(address, targetUUID, out TreeNode owner, out TreeNode target, out GraphReferenceSlotDescriptor descriptor, out string error))
            {
                return GraphTopologyEditResult.Failure(error);
            }

            return descriptor.IsCollection
                ? InsertResolved(address, index, owner, target, descriptor)
                : GraphTopologyEditResult.Failure("Insert requires a collection reference address.");
        }

        /// <summary>Removes the addressed collection occurrence.</summary>
        internal GraphTopologyEditResult Remove(GraphReferenceAddress address) => Disconnect(address);

        /// <summary>Moves a complete collection entry, preserving weighted data with its reference.</summary>
        internal GraphTopologyEditResult Reorder(GraphReferenceAddress address, int destinationIndex)
        {
            if (!TryResolve(address, out TreeNode owner, out GraphReferenceSlotDescriptor descriptor, out string error))
            {
                return GraphTopologyEditResult.Failure(error);
            }

            if (!descriptor.IsCollection || address.Index < 0 || address.Index >= descriptor.Field.arraySize)
            {
                return GraphTopologyEditResult.Failure("The collection occurrence does not exist.");
            }

            int targetIndex = Math.Clamp(destinationIndex, 0, descriptor.Field.arraySize - 1);
            if (targetIndex == address.Index)
            {
                return GraphTopologyEditResult.Failure("The collection occurrence is already at that index.");
            }

            UUID moved = ReadTargetUUID(GetReferenceProperty(descriptor.Field.GetArrayElementAtIndex(address.Index), descriptor.Kind));
            return Mutate($"Reorder {address.FieldName}", () => descriptor.Field.MoveArrayElement(address.Index, targetIndex), owner.uuid, moved);
        }

        private GraphTopologyEditResult InsertResolved(
            GraphReferenceAddress address,
            int index,
            TreeNode owner,
            TreeNode target,
            GraphReferenceSlotDescriptor descriptor)
        {
            if (descriptor.IsStructural && WouldCreateCycle(owner, target))
            {
                return GraphTopologyEditResult.Failure("The structural connection would create a cycle.");
            }

            if (descriptor.IsStructural && HasStructuralIncoming(target))
            {
                return GraphTopologyEditResult.Failure("The target already has a structural parent. Duplicate the node or use a Subtree action instead.");
            }

            int insertIndex = Math.Clamp(index, 0, descriptor.Field.arraySize);
            return Mutate($"Insert {address.FieldName}", () =>
            {
                descriptor.Field.InsertArrayElementAtIndex(insertIndex);
                SerializedProperty entry = descriptor.Field.GetArrayElementAtIndex(insertIndex);
                InitializeCollectionEntry(entry, descriptor.Kind, target.uuid);
            }, owner.uuid, target.uuid);
        }

        private bool TryGetTarget(GraphReferenceAddress address, UUID targetUUID, out TreeNode owner, out TreeNode target, out GraphReferenceSlotDescriptor descriptor, out string error)
        {
            target = tree.GetNode(targetUUID);
            if (target == null)
            {
                owner = null;
                descriptor = default;
                error = "The target node does not exist in this tree.";
                return false;
            }

            if (!TryResolve(address, out owner, out descriptor, out error))
            {
                return false;
            }

            if (descriptor.Kind == GraphReferenceSlotKind.Service && target is not Service)
            {
                error = "A Service slot can only reference a Service node.";
                return false;
            }

            return true;
        }

        private bool TryResolve(GraphReferenceAddress address, out TreeNode owner, out GraphReferenceSlotDescriptor descriptor, out string error)
        {
            owner = tree.GetNode(address.OwnerUUID);
            if (owner == null)
            {
                descriptor = default;
                error = "The reference owner does not exist in this tree.";
                return false;
            }

            if (address.FieldName == nameof(TreeNode.parent))
            {
                descriptor = default;
                error = "The reference address is not an authored node-reference field.";
                return false;
            }

            tree.SerializedObject.Update();
            SerializedProperty field = tree.GetNodeProperty(owner)?.FindPropertyRelative(address.FieldName);
            if (field == null)
            {
                descriptor = default;
                error = "The serialized reference field could not be resolved.";
                return false;
            }

            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            INodeReferenceFieldAccessor single = accessor.NodeReferences.FirstOrDefault(candidate => candidate.Name == address.FieldName);
            if (single != null)
            {
                descriptor = new GraphReferenceSlotDescriptor(
                    single.FieldType == typeof(RawNodeReference) ? GraphReferenceSlotKind.Raw : GraphReferenceSlotKind.Single,
                    field);
                error = null;
                return true;
            }

            INodeReferenceCollectionFieldAccessor collection = accessor.NodeReferenceCollections.FirstOrDefault(candidate => candidate.Name == address.FieldName);
            if (collection == null)
            {
                descriptor = default;
                error = "The reference address is not an authored node-reference field.";
                return false;
            }

            GraphReferenceSlotKind kind = collection.Name == nameof(ServiceHostNode.services)
                ? GraphReferenceSlotKind.Service
                : collection.ElementType == typeof(Probability.EventWeight)
                    ? GraphReferenceSlotKind.ProbabilityWeighted
                    : collection.ElementType == typeof(PseudoProbability.EventWeight)
                        ? GraphReferenceSlotKind.PseudoProbabilityWeighted
                        : collection.ElementType == typeof(RawNodeReference)
                            ? GraphReferenceSlotKind.Raw
                            : GraphReferenceSlotKind.Collection;
            descriptor = new GraphReferenceSlotDescriptor(kind, field);
            error = null;
            return true;
        }

        private GraphTopologyEditResult Mutate(string undoName, System.Action mutation, params UUID[] affected)
        {
            Undo.RecordObject(tree, undoName);
            mutation();
            tree.SerializedObject.ApplyModifiedProperties();
            tree.SerializedObject.Update();
            ReconcileParents();
            tree.SerializedObject.ApplyModifiedProperties();
            tree.SerializedObject.Update();
            tree.Relink();
            EditorUtility.SetDirty(tree);
            return GraphTopologyEditResult.Success(affected);
        }

        private void ReconcileParents()
        {
            foreach (TreeNode child in tree.EditorNodes)
            {
                TreeNode preferred = FindSingleIncomingOwner(child);
                SerializedProperty parent = tree.GetNodeProperty(child)?.FindPropertyRelative(nameof(TreeNode.parent));
                if (parent == null)
                {
                    continue;
                }

                WriteReference(parent, GraphReferenceSlotKind.Single, preferred?.uuid ?? UUID.Empty);
            }
        }

        private TreeNode FindSingleIncomingOwner(TreeNode child)
        {
            List<TreeNode> incoming = new();
            foreach (TreeNode candidate in tree.EditorNodes)
            {
                if (candidate == child || !ReferencesStructurally(candidate, child))
                {
                    continue;
                }

                incoming.Add(candidate);
            }

            return incoming.Count switch
            {
                0 => null,
                1 => incoming[0],
                _ => tree.GetNode(child.parent),
            };
        }

        /// <summary>Returns whether the target already participates as a structural child.</summary>
        private bool HasStructuralIncoming(TreeNode target)
        {
            foreach (TreeNode candidate in tree.EditorNodes)
            {
                if (candidate != target && ReferencesStructurally(candidate, target))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReferencesStructurally(TreeNode owner, TreeNode target)
        {
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
            {
                if (field.Name != nameof(TreeNode.parent) && field.Get(owner)?.UUID == target.uuid && !field.Get(owner).IsRawReference)
                {
                    return true;
                }
            }

            foreach (INodeReferenceCollectionFieldAccessor collection in accessor.NodeReferenceCollections)
            {
                System.Collections.IList entries = collection.Get(owner);
                if (entries == null)
                {
                    continue;
                }

                foreach (object entry in entries)
                {
                    if (entry is INodeReference reference && reference.UUID == target.uuid && !reference.IsRawReference)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool WouldCreateCycle(TreeNode owner, TreeNode target)
        {
            if (owner == target)
            {
                return true;
            }

            HashSet<UUID> visited = new();
            Stack<TreeNode> stack = new();
            stack.Push(target);
            while (stack.Count > 0)
            {
                TreeNode current = stack.Pop();
                if (!visited.Add(current.uuid))
                {
                    continue;
                }

                if (current == owner)
                {
                    return true;
                }

                NodeAccessor accessor = NodeAccessorProvider.GetAccessor(current.GetType());
                foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
                {
                    INodeReference reference = field.Get(current);
                    TreeNode next = field.Name == nameof(TreeNode.parent) || reference?.IsRawReference == true ? null : tree.GetNode(reference.UUID);
                    if (next != null)
                    {
                        stack.Push(next);
                    }
                }

                foreach (INodeReferenceCollectionFieldAccessor collection in accessor.NodeReferenceCollections)
                {
                    System.Collections.IList entries = collection.Get(current);
                    if (entries == null)
                    {
                        continue;
                    }

                    foreach (object entry in entries)
                    {
                        if (entry is INodeReference reference && !reference.IsRawReference)
                        {
                            TreeNode next = tree.GetNode(reference.UUID);
                            if (next != null)
                            {
                                stack.Push(next);
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static SerializedProperty GetReferenceProperty(SerializedProperty property, GraphReferenceSlotKind kind)
        {
            return kind is GraphReferenceSlotKind.ProbabilityWeighted or GraphReferenceSlotKind.PseudoProbabilityWeighted
                ? property.FindPropertyRelative("reference")
                : property;
        }

        private static void InitializeCollectionEntry(SerializedProperty entry, GraphReferenceSlotKind kind, UUID targetUUID)
        {
            switch (kind)
            {
                case GraphReferenceSlotKind.ProbabilityWeighted:
                    entry.boxedValue = new Probability.EventWeight { reference = new NodeReference(targetUUID), weight = 1 };
                    break;
                case GraphReferenceSlotKind.PseudoProbabilityWeighted:
                    entry.boxedValue = new PseudoProbability.EventWeight { reference = new NodeReference(targetUUID), weight = 1 };
                    break;
                default:
                    WriteReference(entry, kind, targetUUID);
                    break;
            }
        }

        private static UUID ReadTargetUUID(SerializedProperty reference)
        {
            return reference?.FindPropertyRelative(NodeReference.uuidPropertyName)?.boxedValue is UUID uuid ? uuid : UUID.Empty;
        }

        private static void WriteReference(SerializedProperty reference, GraphReferenceSlotKind kind, UUID targetUUID)
        {
            if (reference == null)
            {
                return;
            }

            reference.boxedValue = kind == GraphReferenceSlotKind.Raw
                ? new RawNodeReference { UUID = targetUUID }
                : new NodeReference(targetUUID);
        }
    }
}
