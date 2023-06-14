using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Clipboard used in AI editor
    /// </summary>
    [Serializable]
    public class Clipboard
    {
        /// <summary>
        /// tree ref
        /// </summary>
        public BehaviourTreeData tree;
        /// <summary>
        /// sub tree inside the clipboard
        /// </summary>
        [SerializeReference]
        public List<TreeNode> treeNodes;
        /// <summary>
        /// uuid of the first node
        /// </summary>
        public UUID uuid;

        /// <summary>
        /// subtree size inside the clipboard
        /// </summary>
        public int Count => treeNodes?.Count ?? 0;
        /// <summary>
        /// the main content (root of the subtree)
        /// </summary>
        public TreeNode Root => GetRootCopy();
        /// <summary>
        /// the root buffered (root of the subtree)
        /// </summary>
        private TreeNode RootBuffered => Count > 0 ? treeNodes[0] : null;
        /// <summary>
        /// all contents inside the clipboard
        /// </summary>
        public List<TreeNode> Content => GetContent();
        /// <summary>
        /// root node type
        /// </summary>
        public Type RootType => RootBuffered.GetType();


        public Clipboard()
        {
            Init();
        }

        /// <summary>
        /// init clipboard
        /// </summary>
        private void Init()
        {
            tree = null;
            uuid = UUID.Empty;
            treeNodes ??= new();
            treeNodes.Clear();
        }

        /// <summary>
        /// check whether clipboard has value
        /// </summary>
        /// <returns></returns>
        public bool HasContent()
        {
            return treeNodes.Count > 0;
        }

        /// <summary>
        /// clear clipboard
        /// </summary>
        public void Clear()
        {
            Init();
        }

        /// <summary>
        /// write clipboard entry
        /// </summary>
        /// <param name="node"></param>
        /// <param name="tree"></param>
        public void Write(TreeNode node, BehaviourTreeData tree)
        {
            this.tree = tree;

            if (node != null)
            {
                uuid = node.uuid;
                treeNodes = NodeFactory.DeepCloneSubTree(node, tree);
                // parent of the node is invalid now, set to empty
                treeNodes[0].parent.UUID = UUID.Empty;
            }
        }

        /// <summary>
        /// clone the buffered content inside the clipboard
        /// </summary>
        /// <returns></returns>
        private List<TreeNode> GetContent()
        {
            List<TreeNode> contents = new();
            foreach (var item in treeNodes)
            {
                TreeNode clone = NodeFactory.Clone(item, item.GetType());
                contents.Add(clone);
            }
            NodeFactory.ReassignUUID(contents);
            return contents;
        }

        /// <summary>
        /// clone the buffered content inside the clipboard
        /// </summary>
        /// <returns></returns>
        private TreeNode GetRootCopy()
        {
            return NodeFactory.Clone(RootBuffered, RootType);
        }

        /// <summary>
        /// Check root has the same type as the given node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool TypeMatch(TreeNode node)
        {
            return RootType == node.GetType();
        }

        /// <summary>
        /// Check root has the same type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool TypeMatch(Type type)
        {
            Type rootType = RootType;
            return rootType.IsSubclassOf(type) || rootType == type;
        }







        /// <summary>
        /// Paste clipboard content under given reference
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="nodeReference"></param>
        public void PasteUnder(BehaviourTreeData tree, TreeNode parent, NodeReference nodeReference)
        {
            if (RootBuffered is Service)
            {
                EditorUtility.DisplayDialog("Pasting service node", "Cannot paste service to main tree as normal node", "OK");
                return;
            }

            List<TreeNode> content = Content;
            TreeNode root = content[0];

            nodeReference.UUID = root.uuid;
            root.parent.UUID = parent.uuid;

            tree.AddRange(content);

            // node is a service call, need to remove services
            RemoveServicesIfServiceStack(tree, parent, content);
        }

        /// <summary>
        /// Paste clipboard content to append the list flow
        /// </summary>
        /// <param name="lf"></param>
        public void PasteAppend(BehaviourTreeData tree, IListFlow lf)
        {
            //  a service node cannot apped
            if (RootBuffered is Service)
            {
                EditorUtility.DisplayDialog("Pasting service node", "Cannot paste service to main tree as normal node", "OK");
                return;
            }


            List<TreeNode> content = Content;
            TreeNode root = content[0];

            lf.Add(root);
            tree.AddRange(content);


            // node is a service call, need to remove services
            RemoveServicesIfServiceStack(tree, lf as TreeNode, content);
        }

        private static void RemoveServicesIfServiceStack(BehaviourTreeData tree, TreeNode parent, List<TreeNode> content)
        {
            if (tree.IsServiceCall(parent))
            {
                var names = new List<string>();
                foreach (var item in content)
                {
                    if (item is not Service service) continue;
                    names.Add(item.name);
                    tree.RemoveSubTree(service);
                }
                if (names.Count > 0)
                    EditorUtility.DisplayDialog("Pasting to service", $"Service {string.Join(", ", names)} will not be copied because destination parent node is in a service stack", "ok");
            }
        }

        /// <summary>
        /// Paste clipboard value to given node
        /// </summary>
        /// <param name="node"></param>
        public void PasteValue(TreeNode node)
        {
            if (node == null)
            {
                EditorUtility.DisplayDialog("Null Destination", $"Pasting to null is not allowed", "OK");
                return;
            }
            if (!HasContent())
            {
                EditorUtility.DisplayDialog("Empty Clipboard", $"Nothing is in clipboard", "OK");
                return;
            }
            if (!TypeMatch(node))
            {
                EditorUtility.DisplayDialog("Type mismatch", $"Pasting to  \"{node.GetType().Name}\" from type \"{RootType?.Name}\" is not allowed", "OK");
                return;
            }

            NodeFactory.Copy(node, Root);
        }
    }
}
