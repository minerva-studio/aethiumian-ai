using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Minerva.Module;
using System;

namespace Aethiumian.AI.References
{
    /// <summary>
    /// Common interface of All type of Node Ref, see <see cref="RawNodeReference"/> and <see cref="NodeReference"/>
    /// </summary>
    public interface INodeReference : ICloneable, IDuplicable
    {
        /// <summary>
        /// is the current reference raw reference?
        /// </summary>
        bool IsRawReference { get; }
        bool HasEditorReference { get; }
        bool HasReference { get; }
        TreeNode Node { get; set; }
        UUID UUID { get; set; }
    }

    public static class INodeReferenceExtensions
    {
        public static void Set<T>(this T r, TreeNode treeNode)
            where T : INodeReference
        {
            r.Node = treeNode;
            r.UUID = treeNode?.uuid ?? UUID.Empty;
        }

        public static void Clear<T>(this T r)
            where T : INodeReference
        {
            r.Node = null;
            r.UUID = UUID.Empty;
        }
    }
}
