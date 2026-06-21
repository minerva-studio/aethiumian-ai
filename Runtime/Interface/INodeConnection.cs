using Aethiumian.AI.Nodes;
using Minerva.Module;

namespace Aethiumian.AI
{
    /// <summary>
    /// A node connection link common interface
    /// </summary>
    public interface INodeConnection
    {
        UUID UUID { get; set; }
        TreeNode Node { get; set; }
    }
}
