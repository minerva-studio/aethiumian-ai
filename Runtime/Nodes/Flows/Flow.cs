using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Base class of all Flow Control node
    /// </summary>
    /// <remarks>
    /// Flow nodes that are more likely
    /// </remarks>
    [Serializable]
    public abstract class Flow : ServiceHostNode
    {
        /// <summary>
        /// Schedules <paramref name="child"/> as the next node and gives up this node's current execution turn.
        /// </summary>
        /// <remarks>
        /// This is a terminal handoff. Callers must return the returned state immediately.
        /// </remarks>
        protected State SetNextExecute(NodeReference child)
        {
            // A failed handoff must not report NONE_RETURN, because the current flow node
            // remains on top of the stack and would be processed again as recursive execution.
            return behaviourTree.ExecuteNext(child, callStack) ? State.NONE_RETURN : State.Error;
        }
    }
}
