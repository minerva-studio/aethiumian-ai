using System;
using Aethiumian.AI.References;
using UnityEngine.Serialization;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Base class for nodes that wrap one child and handle its result.
    /// </summary>
    [Serializable]
    public abstract class Decorator : TreeNode
    {
        [FormerlySerializedAs("subtreeHead")]
        public NodeReference node = new();

        /// <summary>
        /// Schedules the decorated child as the next node and gives up this node's current execution turn.
        /// </summary>
        /// <remarks>
        /// This is a terminal handoff. Callers must return the returned state immediately.
        /// </remarks>
        protected State SetNextExecute(NodeReference child)
        {
            // A failed handoff must not report NONE_RETURN, because the current decorator
            // remains on top of the stack and would be processed again as recursive execution.
            return behaviourTree.ExecuteNext(child, callStack) ? State.NONE_RETURN : State.Error;
        }

        /// <summary>
        /// Executes the decorated child, or returns the decorator-specific fallback when no child is assigned.
        /// </summary>
        public sealed override State Execute()
        {
            if (behaviourTree.GetNode(node) == null)
            {
                return ExecuteWithoutChild();
            }

            return SetNextExecute(node);
        }

        /// <summary>
        /// Returns the result used when the decorator has no valid child.
        /// </summary>
        protected virtual State ExecuteWithoutChild()
        {
            return State.Failed;
        }

        /// <summary>
        /// Initializes this stateless decorator.
        /// </summary>
        public override void Initialize()
        {
        }
    }
}
