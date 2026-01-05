namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Flow node that contains a list of children
    /// </summary>
    public interface IListFlow
    {
        int Count { get; }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Add reference to given tree node
        /// </summary>
        /// <param name="treeNode"></param> 
        void Add(TreeNode treeNode);

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Insert reference to given tree node at position
        /// </summary>
        /// <param name="treeNode"></param> 
        void Insert(int index, TreeNode treeNode);

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get the index of the given node
        /// </summary>
        /// <param name="treeNode"></param> 
        int IndexOf(TreeNode treeNode);

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Remove given node from the list
        /// </summary>
        /// <param name="treeNode"></param>
        void Remove(TreeNode treeNode);
    }
}
