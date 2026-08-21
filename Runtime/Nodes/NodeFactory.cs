using Aethiumian.AI.Accessors;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// A factory class use to create new node in AI Editor
    /// </summary>
    public static class NodeFactory
    {
        /// <summary>
        /// Create a node by type
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static TreeNode Create(Type nodeType)
        {
            if (!nodeType.IsSubclassOf(typeof(TreeNode))) throw new ArgumentException($"Type {nodeType} is not a valid type of node");
            if (nodeType.IsAbstract) throw new ArgumentException($"Type {nodeType} is an abstract node type");

            TreeNode node = NodeDescriptorProvider.Get(nodeType).CreateInstance();
            node.uuid = UUID.NewUUID();
            FillNull(node);
            node.parent ??= NodeReference.Empty;
            return node;
        }

        /// <summary>
        /// Fill all empty field in the node with value (if supported)
        /// </summary>
        /// <param name="node"></param>
        public static void FillNull(TreeNode node)
        {
            NodeDescriptorProvider.Get(node.GetType()).FillNull(node);
        }


        /// <summary>
        /// Get a copy of the object via serialization. (result in same uuid and name)
        /// </summary>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>A deep copy of the object.</returns>
        public static TreeNode Duplicate(TreeNode source) => Duplicate(source, DuplicateMode.Duplicate);

        public static TreeNode Instantiate(TreeNode source) => Duplicate(source, DuplicateMode.Instantiate);

        private static TreeNode Duplicate(TreeNode source, DuplicateMode mode)
        {
            return NodeDescriptorProvider.Get(source.GetType()).Duplicate(source, mode);
        }

        /// <summary>
        /// Create deep clone of the tree node, this will assign the new node with different uuid
        /// </summary>
        /// <param name="treeNode"></param>
        /// <returns></returns>
        public static TreeNode DuplicateNode(TreeNode treeNode)
        {
            var cloned = Duplicate(treeNode);
            cloned.uuid = UUID.NewUUID();
            return cloned;
        }

#if UNITY_EDITOR 

        /// <summary>
        /// Clone the entire subtree
        /// </summary>
        /// <returns> List of node cloned and linked, root clone will be the first in the list </returns>
        public static List<TreeNode> DuplicateSubtree(TreeNode root, BehaviourTreeData data)
        {
            Dictionary<UUID, UUID> translationTable = new Dictionary<UUID, UUID>();
            List<TreeNode> result = new();

            BuildTableSubTree(translationTable, result, root, data);
            ApplyTranslation(translationTable, result);

            return result;
        }

        /// <summary>
        /// Apply the translation of the uuid
        /// </summary>
        /// <param name="translationTable"></param>
        /// <param name="result"></param>
        private static void ApplyTranslation(Dictionary<UUID, UUID> translationTable, List<TreeNode> result)
        {
            foreach (var node in result)
            {
                NodeDescriptorProvider.Get(node.GetType())
                    .VisitMembers(node, new NodeReferenceRemapVisitor(translationTable));
            }
        }

        /// <summary>
        /// Clone the subtree and build the translation table of the cloned tree
        /// </summary>
        /// <returns></returns>
        private static void BuildTableSubTree(Dictionary<UUID, UUID> translationTable, List<TreeNode> result, TreeNode root, BehaviourTreeData data)
        {
            var cloned = DuplicateNode(root);
            translationTable[root.uuid] = cloned.uuid;
            result.Add(cloned);
            var childrens = root.GetChildrenReference();

            foreach (var childRef in childrens)
            {
                TreeNode child = data.GetNode(childRef);
                if (child != null)
                    BuildTableSubTree(translationTable, result, child, data);
            }
        }

        /// <summary>
        /// Reassign the uuid of the given subtree
        /// </summary>
        /// <param name="contents"></param>
        public static void ReassignUUID(List<TreeNode> contents)
        {
            Dictionary<UUID, UUID> translationTable = new();

            foreach (var node in contents)
            {
                UUID uuid = node.uuid;
                UUID newUUID = UUID.NewUUID();

                translationTable[uuid] = newUUID;
                node.uuid = newUUID;
            }

            ApplyTranslation(translationTable, contents);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EditorError(string str)
        {
            EditorGUILayout.HelpBox(str, MessageType.Error);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EditorWarning(string str)
        {
            EditorGUILayout.HelpBox(str, MessageType.Warning);
        }

#endif

        /// <summary>
        /// Copy data from src to dst while preserving the destination identity and node references.
        /// Generated accessors copy authored values deeply; destination name, UUID, parent, and
        /// reference identities remain owned by the destination node.
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="src"></param>
        public static void Copy(TreeNode dst, TreeNode src)
        {
            if (dst.GetType() != src.GetType())
            {
                throw new ArgumentException("Cannot copy between different node runtime types.", nameof(src));
            }

            NodeDescriptor generatedDescriptor = NodeDescriptorProvider.Get(src.GetType());
            string name = dst.name;
            UUID uuid = dst.uuid;
            NodeReference parent = global::Aethiumian.AI.Accessors.Duplicate.Value(dst.parent);
            DestinationReferenceSnapshotVisitor destinationReferences = new(dst);
            generatedDescriptor.VisitMembers(dst, destinationReferences);
            List<(INodeReferenceSingleSlot Slot, UUID UUID)> references = new();
            List<(IIndexedNodeReferenceListSlot Slot, List<UUID> UUIDs)> collections = new();
            foreach (INodeReferenceSlot slot in NodeReferenceStructureProvider.GetSlots(dst))
            {
                if (slot is INodeReferenceSingleSlot single)
                {
                    references.Add((single, single.GetReference()?.UUID ?? UUID.Empty));
                }
                else if (slot is IIndexedNodeReferenceListSlot list)
                {
                    List<UUID> uuids = new(list.Count);
                    for (int index = 0; index < list.Count; index++)
                    {
                        uuids.Add(list.GetReference(index)?.UUID ?? UUID.Empty);
                    }

                    collections.Add((list, uuids));
                }
            }

            generatedDescriptor.Copy(dst, src, DuplicateMode.Duplicate);
            dst.name = name;
            dst.uuid = uuid;
            dst.parent = parent;
            foreach ((INodeReferenceSingleSlot slot, UUID referenceUUID) in references)
            {
                slot.Set(null);
                INodeReference reference = slot.GetReference();
                if (reference != null)
                {
                    reference.UUID = referenceUUID;
                    reference.Node = null;
                }
            }

            foreach ((IIndexedNodeReferenceListSlot slot, List<UUID> uuids) in collections)
            {
                while (slot.Count > uuids.Count)
                {
                    slot.RemoveAt(slot.Count - 1);
                }

                while (slot.Count < uuids.Count)
                {
                    slot.Insert(slot.Count, null);
                }

                for (int index = 0; index < uuids.Count; index++)
                {
                    INodeReference reference = slot.GetReference(index);
                    if (reference != null)
                    {
                        reference.UUID = uuids[index];
                        reference.Node = null;
                    }
                }
            }

            destinationReferences.Restore();
        }

    }
}
