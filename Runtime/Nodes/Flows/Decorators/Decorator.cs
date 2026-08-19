using System;
using Aethiumian.AI.References;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Base class for flow nodes that execute one child and handle its result.
    /// </summary>
    [Serializable]
    public abstract class Decorator : Flow
    {
        public NodeReference node = new();

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
