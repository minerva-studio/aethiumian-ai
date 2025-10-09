using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Arithmetic nodes, nodes that perform arithmetic operations on numbers.
    /// <br/>
    /// Must be instantly executed, cannot be yielded or wait.
    /// </summary>
    [Serializable]
    public abstract class Arithmetic : TreeNode
    {
        public override void Initialize()
        {
        }

        /// <summary>
        /// <inheritdoc/>
        /// <br/>
        /// Cannot override
        /// <br/>
        /// It is very unlikely for this method to be called
        /// </summary>
        protected sealed override void OnStop()
        {
        }
    }
}
