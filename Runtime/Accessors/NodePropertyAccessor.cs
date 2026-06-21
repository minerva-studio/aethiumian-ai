using Aethiumian.AI.Nodes;

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Provides generated node access and lifecycle operations.
    /// <br/>
    /// The non-generic <see cref="NodePropertyAccessor"/> serves as a base type for storing accessors in a common collection,
    /// while the generic <see cref="NodePropertyAccessor{T}"/> provides type-specific operations for nodes of type <typeparamref name="T"/>.
    /// </summary>
    public abstract class NodePropertyAccessor : NodeAccessor
    {
        public abstract TreeNode Duplicate(TreeNode source, DuplicateMode mode);

        public abstract void Copy(TreeNode dst, TreeNode src, DuplicateMode mode);

        public abstract void FillNull(TreeNode node);
    }

    /// <summary>
    /// Provides generated node access and lifecycle operations for one node type.
    /// <br/>
    /// You should not implement this class directly; source generators will generate concrete implementations based on the node types.
    /// </summary>
    /// <typeparam name="T">The node type handled by this accessor.</typeparam>
    public abstract class NodePropertyAccessor<T> : NodePropertyAccessor where T : TreeNode
    {
        public sealed override TreeNode Duplicate(TreeNode source, DuplicateMode mode)
        {
            return Duplicate((T)source, mode);
        }

        public sealed override void Copy(TreeNode dst, TreeNode src, DuplicateMode mode)
        {
            Copy((T)dst, (T)src, mode);
        }

        public sealed override void FillNull(TreeNode node)
        {
            FillNull((T)node);
        }

        public abstract T Duplicate(T source, DuplicateMode mode);

        public abstract void Copy(T dst, T src, DuplicateMode mode);

        public abstract void FillNull(T node);
    }
}
