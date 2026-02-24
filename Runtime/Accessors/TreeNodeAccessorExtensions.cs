using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using System.Collections.Generic;

namespace Amlos.AI.Accessors
{
    public static class TreeNodeAccessorExtensions
    {
        /// <summary>
        /// Find a node reference to uuid in this node, return null if not found. 
        /// <br/>
        /// Note that this method will ignore raw reference and parent reference, so it will only find real child reference.
        /// <br/>
        /// If you want to find raw reference, please use GetChildrenReference and check IsRawReference property.
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public static NodeReference FindReference(this TreeNode treeNode, UUID uuid)
        {
            var accessor = NodeAccessorProvider.GetAccessor(treeNode.GetType());
            foreach (var item in accessor.GetNodeReferences(treeNode))
            {
                if (item == null) continue;
                if ((object)item == treeNode.parent) continue;
                if (item.UUID != uuid) continue;
                if (item is NodeReference r)
                {
                    return r;
                }
                else if (item is Probability.EventWeight ew)
                {
                    return ew.reference;
                }
                else if (item is PseudoProbability.EventWeight pgew)
                {
                    return pgew.reference;
                }
            }
            return null;
        }

        /// <summary>
        /// get children of this node (NodeReference)
        /// </summary>
        /// <returns></returns>
        public static List<NodeReference> GetChildrenReference(this TreeNode treeNode)
        {
            List<NodeReference> list = new();
            var accessor = NodeAccessorProvider.GetAccessor(treeNode.GetType());
            foreach (var item in accessor.GetNodeReferences(treeNode))
            {
                if (item == null) continue;
                if ((object)item == treeNode.parent) continue;

                if (item is NodeReference r)
                {
                    list.Add(r);
                }
                else if (item is Probability.EventWeight ew)
                {
                    list.Add(ew.reference);
                }
                else if (item is PseudoProbability.EventWeight pgew)
                {
                    list.Add(pgew.reference);
                }
            }
            return list;
        }

        /// <summary>
        /// get children of this node (NodeReference)
        /// </summary>
        /// <param name="includeRawReference">whether include raw reference in the child (note that raw reference is not child) </param>
        /// <returns></returns>
        public static List<INodeReference> GetChildrenReference(this TreeNode treeNode, bool includeRawReference = false)
        {
            List<INodeReference> list = new();
            var accessor = NodeAccessorProvider.GetAccessor(treeNode.GetType());
            foreach (var item in accessor.GetNodeReferences(treeNode))
            {
                if (item == null) continue;
                if ((object)item == treeNode.parent) continue;
                if (!includeRawReference && item.IsRawReference) continue;
                list.Add(item);
            }
            return list;
        }
    }
}
