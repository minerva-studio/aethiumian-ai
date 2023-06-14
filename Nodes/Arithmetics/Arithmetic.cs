using System;

namespace Amlos.AI.Nodes
{
    [AllowServiceCall]
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
        public sealed override void Stop()
        {
            base.Stop();
        }
    }
}
