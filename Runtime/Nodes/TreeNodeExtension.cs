using Amlos.AI.Nodes;
using Amlos.AI.References;

namespace Amlos.AI.Accessors
{
    public static class TreeNodeExtension
    {
        /// <summary>
        /// Get a node reference object
        /// </summary>
        /// <returns></returns>
        public static NodeReference ToReference(this TreeNode node)
        {
            return new NodeReference() { UUID = node.UUID, Node = node };
        }

        public static RawNodeReference ToRawReference(this TreeNode node)
        {
            return new RawNodeReference() { UUID = node.UUID, Node = node };
        }
    }
}
