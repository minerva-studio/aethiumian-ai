using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Base class of all Flow Control node
    /// </summary>
    /// <remarks>
    /// Flow nodes that are more likely
    /// </remarks>
    [Serializable]
    public abstract class Flow : TreeNode
    {
        /// <summary>
        /// Set <paramref name="child"/> as the current stage to the tree
        /// </summary>
        public State SetNextExecute(TreeNode child)
        {
            behaviourTree.ExecuteNext(child);
            return State.NONE_RETURN;
        }
    }
}