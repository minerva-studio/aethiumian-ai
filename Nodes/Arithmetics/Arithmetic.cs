using Minerva.Module;
using System;
using System.Collections.Generic;

namespace Amlos.AI
{
    [AllowServiceCall]
    [Serializable]
    public abstract class Arithmetic : TreeNode
    {
        public override void Initialize()
        {
        }

        public sealed override void End(bool @return)
        {
            base.End(@return);
        }

        public sealed override void Stop()
        {
            base.Stop();
        }
    }
}
