using Amlos.AI.References;
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
        /// Schedules <paramref name="child"/> as the next node and gives up this node's current execution turn.
        /// </summary>
        /// <remarks>
        /// This is a terminal handoff. Callers must return the returned state immediately.
        /// </remarks>
        protected State SetNextExecute(NodeReference child)
        {
            behaviourTree.ExecuteNext(child, callStack);
            return State.NONE_RETURN;
        }
    }
}
