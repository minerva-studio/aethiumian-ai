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
        public Type RootType => RootBuffered?.GetType();


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
        public bool HasContent
        {
            get
            {
                treeNodes.RemoveAll(x => x == null);
                return treeNodes.Count > 0;
            }
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
        /// write clipboard entry (without given node's subtree)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="tree"></param>
        public void WriteSingle(TreeNode node, BehaviourTreeData tree)
        {
            this.tree = tree;

            if (node != null)
            {
                uuid = node.uuid;
                TreeNode treeNode = NodeFactory.DeepClone(node);
                // clear node child references
                foreach (var item in treeNode.GetChildrenReference())
                {
                    item.UUID = UUID.Empty;
                }
                treeNodes = new List<TreeNode>() { treeNode };
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
                TreeNode clone = NodeFactory.Clone(item);
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
            return NodeFactory.Clone(RootBuffered);
        }

        /// <summary>
        /// Check root has the same type as the given node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool TypeMatch(TreeNode node)
        {
            return HasContent && RootType == node.GetType();
        }

        /// <summary>
        /// Check root has the same type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool TypeMatch(Type type)
        {
            Type rootType = RootType;
            if (rootType == null) return false;
            return rootType.IsSubclassOf(type) || rootType == type;
        }







        /// <summary>
        /// Paste clipboard content to given reference
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="nodeReference"></param>
        public void PasteTo(BehaviourTreeData tree, TreeNode parent, INodeReference nodeReference)
        {
            if (RootBuffered is Service)
            {
                EditorUtility.DisplayDialog("Pasting service node", "Cannot paste service to main tree as normal node", "OK");
                return;
            }

            List<TreeNode> content = Content;
            TreeNode root = content[0];
            foreach (var item in content)
            {
                item.name = tree.GenerateNewNodeName(item.name);
            }

            Undo.RecordObject(tree, $"Paste clipboard content under {parent.name}");
            tree.AddRange(content, false);         // Undo require be first
            nodeReference.Set(root);
            root.parent.UUID = parent.uuid;


            //// node is a service call, need to remove services
            //RemoveServicesIfServiceStack(tree, parent, content);
        }

        ///// <summary>
        ///// Paste clipboard content to append the list flow
        ///// </summary>
        ///// <param name="lf"></param>
        //public void PasteAsLast(BehaviourTreeData tree, IListFlow lf) => PasteAt(tree, lf, lf.Count);

        ///// <summary>
        ///// Paste clipboard content to append the list flow (but at first)
        ///// </summary>
        ///// <param name="lf"></param>
        //public void PasteAsFirst(BehaviourTreeData tree, IListFlow lf) => PasteAt(tree, lf, 0);

        ///// <summary>
        ///// Paste clipboard content to given index of the flow
        ///// </summary>
        ///// <param name="tree"></param>
        ///// <param name="lf"></param>
        ///// <param name="index"></param>
        //public void PasteAt(BehaviourTreeData tree, IListFlow lf, int index)
        //{
        //    //  a service node cannot append
        //    if (RootBuffered is Service)
        //    {
        //        EditorUtility.DisplayDialog("Pasting service node", "Cannot paste service to main tree as normal node", "OK");
        //        return;
        //    }

        //    List<TreeNode> content = Content;
        //    TreeNode root = content[0];

        //    Undo.RecordObject(tree, $"Insert clipboard content to {lf.GetType().Name} index {index}");
        //    tree.AddRange(content, false);
        //    lf.Insert(index, root);
        //    // node is a service call, need to remove services
        //    RemoveServicesIfServiceStack(tree, lf as TreeNode, content);
        //}

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
            if (!HasContent)
            {
                EditorUtility.DisplayDialog("Empty Clipboard", $"Nothing is in clipboard", "OK");
                return;
            }
            if (!TypeMatch(node))
            {
                EditorUtility.DisplayDialog("Type mismatch", $"Pasting to  \"{node.GetType().Name}\" from type \"{RootType?.Name}\" is not allowed", "OK");
                return;
            }

            Undo.RecordObject(tree, $"Paste value to {node.name}");
            NodeFactory.Copy(node, Root);
        }




        public void PasteAsLast(BehaviourTreeData tree, TreeNode owner, INodeReferenceListSlot slot) => PasteAt(tree, owner, slot, slot?.Count ?? 0);

        public void PasteAsFirst(BehaviourTreeData tree, TreeNode owner, INodeReferenceListSlot slot) => PasteAt(tree, owner, slot, 0);

        public void PasteAt(BehaviourTreeData tree, TreeNode owner, INodeReferenceListSlot slot, int index)
        {
            if (tree == null)
            {
                EditorUtility.DisplayDialog("Null Tree", "Pasting to null tree is not allowed", "OK");
                return;
            }

            if (owner == null)
            {
                EditorUtility.DisplayDialog("Null Destination", "Pasting to null node is not allowed", "OK");
                return;
            }

            if (slot == null)
            {
                EditorUtility.DisplayDialog("Null Destination", "Pasting to null slot is not allowed", "OK");
                return;
            }

            if (RootBuffered is Service)
            {
                EditorUtility.DisplayDialog("Pasting service node", "Cannot paste service to main tree as normal node", "OK");
                return;
            }

            List<TreeNode> content = Content;
            TreeNode root = content[0];

            foreach (var item in content)
            {
                item.name = tree.GenerateNewNodeName(item.name);
            }

            Undo.RecordObject(tree, $"Insert clipboard content to {owner.name}.{slot.Name} index {index}");
            tree.AddRange(content, false);

            int clampedIndex = Mathf.Clamp(index, 0, slot.Count);
            slot.Insert(clampedIndex, root);

            root.parent.UUID = owner.uuid;

            //RemoveServicesIfServiceStack(tree, owner, content);
        }


        [Obsolete("This method is obsoleted since now support service in serivce")]
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

    }
}
