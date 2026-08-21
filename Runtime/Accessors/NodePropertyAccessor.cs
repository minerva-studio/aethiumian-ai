using Aethiumian.AI.Nodes;
using System;

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Describes construction, copying, null filling, and semantic member traversal for one node type.
    /// </summary>
    public abstract class NodeDescriptor
    {
        /// <summary>Gets the node type described by this descriptor.</summary>
        public abstract Type NodeType { get; }

        /// <summary>Creates a new node instance using the described node's public parameterless constructor.</summary>
        internal abstract TreeNode CreateInstance();

        /// <summary>Duplicates a node using the selected copy strategy.</summary>
        internal abstract TreeNode Duplicate(TreeNode source, DuplicateMode mode);

        /// <summary>Copies authored state from one node into another node of the same type.</summary>
        public abstract void Copy(TreeNode destination, TreeNode source, DuplicateMode mode);

        /// <summary>Fills supported null fields on a node.</summary>
        public abstract void FillNull(TreeNode node);

        /// <summary>Visits all runtime-bound semantic members on a node.</summary>
        public abstract void VisitMembers(TreeNode node, NodeMemberVisitor visitor);

    }

    /// <summary>
    /// Base implementation for generated descriptors of one concrete node type.
    /// </summary>
    /// <typeparam name="T">The concrete node type handled by this descriptor.</typeparam>
    public abstract class NodeDescriptor<T> : NodeDescriptor
        where T : TreeNode, new()
    {
        /// <inheritdoc />
        public sealed override Type NodeType => typeof(T);

        /// <inheritdoc />
        internal sealed override TreeNode CreateInstance()
        {
            return new T();
        }

        /// <inheritdoc />
        internal sealed override TreeNode Duplicate(TreeNode source, DuplicateMode mode)
        {
            var destination = new T();
            Copy(destination, (T)source, mode);
            return destination;
        }

        /// <inheritdoc />
        public sealed override void Copy(TreeNode destination, TreeNode source, DuplicateMode mode)
        {
            Copy((T)destination, (T)source, mode);
        }

        /// <inheritdoc />
        public sealed override void FillNull(TreeNode node)
        {
            FillNull((T)node);
        }

        /// <inheritdoc />
        public sealed override void VisitMembers(TreeNode node, NodeMemberVisitor visitor)
        {
            VisitMembers((T)node, visitor);
        }

        /// <summary>Copies fields for the concrete node type.</summary>
        protected abstract void Copy(T destination, T source, DuplicateMode mode);

        /// <summary>Fills supported null fields for the concrete node type.</summary>
        protected abstract void FillNull(T node);

        /// <summary>Visits runtime-bound members for the concrete node type.</summary>
        protected abstract void VisitMembers(T node, NodeMemberVisitor visitor);

    }
}
