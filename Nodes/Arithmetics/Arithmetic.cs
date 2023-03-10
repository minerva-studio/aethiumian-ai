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
        /// <br></br>
        /// Cannot override
        /// </summary>
        /// <param name="return"></param>
        public sealed override void End(bool @return)
        {
            base.End(@return);
        }

        /// <summary>
        /// <inheritdoc/>
        /// <br></br>
        /// Cannot override
        /// <br></br>
        /// It is very unlikely for this method to be called
        /// </summary>
        public sealed override void Stop()
        {
            base.Stop();
        }
    }
}
