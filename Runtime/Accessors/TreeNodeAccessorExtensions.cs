using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System.Collections.Generic;

namespace Aethiumian.AI.Accessors
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
            foreach (CollectedReference collected in Collect(treeNode).ReferencesWithPaths)
            {
                INodeReference item = collected.Reference;
                if (item == null || collected.Path == nameof(TreeNode.parent)) continue;
                if (item.IsRawReference || item.UUID != uuid) continue;
                return ToNodeReference(item);
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
            foreach (CollectedReference collected in Collect(treeNode).ReferencesWithPaths)
            {
                INodeReference item = collected.Reference;
                if (item == null) continue;
                if (collected.Path == nameof(TreeNode.parent) || item.IsRawReference) continue;
                NodeReference reference = ToNodeReference(item);
                if (reference != null) list.Add(reference);
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
            foreach (CollectedReference item in Collect(treeNode).ReferencesWithPaths)
            {
                if (item.Reference == null || item.Path == nameof(TreeNode.parent)) continue;
                if (!includeRawReference && item.Reference.IsRawReference) continue;
                list.Add(item.Reference);
            }
            return list;
        }

        private static ReferenceCollector Collect(TreeNode treeNode)
        {
            ReferenceCollector collector = new();
            if (treeNode != null)
            {
                NodeDescriptorProvider.Get(treeNode.GetType()).VisitMembers(treeNode, collector);
            }

            return collector;
        }

        private static NodeReference ToNodeReference(INodeReference reference)
        {
            if (reference is NodeReference nodeReference) return nodeReference;
            if (reference is Probability.EventWeight probability) return probability.reference;
            if (reference is PseudoProbability.EventWeight pseudoProbability) return pseudoProbability.reference;
            return null;
        }

        private sealed class ReferenceCollector : NodeMemberVisitor
        {
            public List<INodeReference> References { get; } = new();
            public List<CollectedReference> ReferencesWithPaths { get; } = new();

            protected override void OnNodeReference(string path, INodeReference reference)
            {
                References.Add(reference);
                ReferencesWithPaths.Add(new CollectedReference(path, reference));
            }

            protected override void OnVariableBinding(string path, IVariableBinding binding)
            {
            }
        }

        private readonly struct CollectedReference
        {
            public CollectedReference(string path, INodeReference reference)
            {
                Path = path;
                Reference = reference;
            }

            public string Path { get; }
            public INodeReference Reference { get; }
        }
    }
}
