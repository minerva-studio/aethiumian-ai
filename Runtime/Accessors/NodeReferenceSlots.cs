using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aethiumian.AI.Accessors
{
    /// <summary>Describes one editable node-reference member.</summary>
    public interface INodeReferenceSlot
    {
        /// <summary>Gets the direct member name represented by this slot.</summary>
        string Name { get; }
        /// <summary>Determines whether the slot currently points at a node.</summary>
        bool Contains(TreeNode node);
        /// <summary>Clears the authored and runtime reference.</summary>
        void Clear();
    }

    /// <summary>Describes one scalar node-reference member.</summary>
    public interface INodeReferenceSingleSlot : INodeReferenceSlot
    {
        /// <summary>Gets the authored reference object, if present.</summary>
        INodeReference GetReference();
        /// <summary>Sets the referenced node while preserving the reference type.</summary>
        void Set(TreeNode treeNode);
    }

    /// <summary>Describes one editable node-reference collection.</summary>
    public interface INodeReferenceListSlot : INodeReferenceSlot
    {
        /// <summary>Gets the current collection size.</summary>
        int Count { get; }
        /// <summary>Gets an indexed reference.</summary>
        INodeReference GetReference(int index);
        /// <summary>Adds a node reference.</summary>
        bool Add(TreeNode treeNode);
        /// <summary>Inserts a node reference.</summary>
        void Insert(int index, TreeNode treeNode);
        /// <summary>Finds a node by authored UUID.</summary>
        int IndexOf(TreeNode treeNode);
        /// <summary>Removes a node reference.</summary>
        bool Remove(TreeNode treeNode);
    }

    /// <summary>Provides a generated scalar reference slot without reflection over node fields.</summary>
    public sealed class DelegateNodeReferenceSingleSlot : INodeReferenceSingleSlot
    {
        private readonly TreeNode owner;
        private readonly Type referenceType;
        private readonly Func<TreeNode, INodeReference> getter;
        private readonly Action<TreeNode, INodeReference> setter;

        /// <summary>Initializes a delegate-backed scalar reference slot.</summary>
        public DelegateNodeReferenceSingleSlot(TreeNode owner, string name, Type referenceType,
            Func<TreeNode, INodeReference> getter, Action<TreeNode, INodeReference> setter)
        {
            this.owner = owner;
            Name = name;
            this.referenceType = referenceType;
            this.getter = getter;
            this.setter = setter;
        }

        /// <inheritdoc />
        public string Name { get; }
        /// <inheritdoc />
        public INodeReference GetReference() => owner == null ? null : getter(owner);
        /// <inheritdoc />
        public bool Contains(TreeNode node) => node != null && GetReference()?.UUID == node.uuid;

        /// <inheritdoc />
        public void Clear()
        {
            INodeReference reference = GetReference();
            if (reference != null) reference.Clear();
            else setter(owner, CreateReference(null));
        }

        /// <inheritdoc />
        public void Set(TreeNode treeNode)
        {
            INodeReference reference = GetReference();
            if (reference == null) setter(owner, CreateReference(treeNode));
            else reference.Set(treeNode);
        }

        private INodeReference CreateReference(TreeNode treeNode)
        {
            INodeReference reference = (INodeReference)Activator.CreateInstance(referenceType);
            reference.Set(treeNode);
            return reference;
        }
    }

    /// <summary>Helpers for the current reference structure provider.</summary>
    public static class NodeReferenceSlotExtensions
    {
        /// <summary>Gets editable slots excluding parent and service ownership slots.</summary>
        public static List<INodeReferenceSlot> ToReferenceSlots(this TreeNode treeNode)
        {
            if (treeNode == null) return new List<INodeReferenceSlot>();
            return NodeReferenceStructureProvider.GetSlots(treeNode)
                .Where(slot => slot.Name != nameof(treeNode.parent)
                    && slot.Name != nameof(ServiceHostNode.services)).ToList();
        }

        /// <summary>Gets the first editable collection slot on a node.</summary>
        public static INodeReferenceListSlot GetListSlot(this TreeNode treeNode)
        {
            return treeNode == null ? null : NodeReferenceStructureProvider.GetListSlots(treeNode).FirstOrDefault();
        }
    }
}
