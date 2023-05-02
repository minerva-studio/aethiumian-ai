using Amlos.AI.Nodes;
using Minerva.Module;
using System;

namespace Amlos.AI.References
{
    /// <summary>
    /// Common interface of All type of Node Ref, see <see cref="RawNodeReference"/> and <see cref="NodeReference"/>
    /// </summary>
    public interface INodeReference : ICloneable
    {
        bool IsRawReference { get; }
        bool HasEditorReference { get; }
        bool HasReference { get; }
        TreeNode Node { get; set; }
        UUID UUID { get; set; }
    }
}