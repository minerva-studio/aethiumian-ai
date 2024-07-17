using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// A type of nodes that calls certain methods
    /// </summary>
    [Serializable]
    public abstract class Call : TreeNode
    {
        public override void Initialize()
        {
        }
    }
}