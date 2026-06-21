#nullable enable
using Aethiumian.AI.References;
using System.Collections.Generic;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Provides services hosted by a node that can stay in an execution stack.
    /// </summary>
    public interface IServiceHostNode
    {
        TreeNode Node { get; }
        List<NodeReference>? Services { get; }
        List<NodeReference> EnsureServices();
        void AddService(Service service);
    }
}
