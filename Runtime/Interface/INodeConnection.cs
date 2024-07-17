using Amlos.AI.Nodes;
using Minerva.Module;

namespace Amlos.AI
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
