using System;

namespace Amlos.AI
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
        /// </summary>
        /// <param name="return"></param>
        public sealed override void Stop()
        {
            base.Stop();
        }
    }
}
