using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// base class of all Flow Control node
    /// </summary>
    [Serializable]
    public abstract class Flow : TreeNode
    {
        /// <summary>
        /// set the node as the current stage to the tree
        /// </summary>
        public State SetNextExecute(TreeNode child)
        {
            //Debug.Log("Add " + name + " to progess stack");
            behaviourTree.ExecuteNext(child);
            return State.NONE_RETURN;
        }
    }
}