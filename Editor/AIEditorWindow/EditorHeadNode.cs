using Amlos.AI.Nodes;
using System;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// A node that only use as a placeholder for AIE
    /// </summary>
    [DoNotRelease]
    internal class EditorHeadNode : TreeNode
    {
        public EditorHeadNode()
        {
            name = "HEAD";
        }

        public override State Execute()
        {
            throw new NotImplementedException();
        }

        public override void Initialize()
        {
            throw new NotImplementedException();
        }
    }
}
