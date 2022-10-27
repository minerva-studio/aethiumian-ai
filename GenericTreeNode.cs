using Amlos.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// A generic tree node, used for storing all data in <see cref="BehaviourTreeData"/>
    /// </summary>
    [Serializable]
    [Obsolete("Generic Tree Node is abandoned due to using SerializeReference")]
    public class GenericTreeNode : TreeNodeBase
    {

        [Label] public string type;
        [TextArea(10, 20)] public string serializedData;

        private TreeNode shadowTreeNode;

        public TreeNode ShadowTreeNode => GetTreeNode();

        /// <summary>
        /// create a generic node by using a normal tree node
        /// </summary>
        /// <param name="treeNode"></param>
        /// <exception cref="ArgumentException">if cannot determine the node type</exception>
        public GenericTreeNode(TreeNode treeNode)
        {
            if (treeNode.GetType().IsAbstract)
            {
                throw new ArgumentException($"cannot determine node type of node {treeNode.name}, check inheritance.");
            }
            name = treeNode.name;
            uuid = treeNode.uuid;
            //parentUUID = treeNode.parentUUID;
            serializedData = JsonUtility.ToJson(treeNode);
            type = treeNode.GetType().Name;
            shadowTreeNode = treeNode;
        }


        /// <summary>
        /// get the real tree node representing this node
        /// </summary>
        /// <returns></returns>
        private TreeNode GetTreeNode()
        {
            //if there is a instance, return the instance
            return shadowTreeNode ??= Reshadow();
        }

        public TreeNode Reshadow()
        {
            TreeNode shadowTreeNode;
            //Debug.Log("start reshadow " + name);
            var nodeType = Type.GetType(typeof(TreeNode).AssemblyQualifiedName.Replace(nameof(TreeNode), type));

            if (nodeType != null)
            {
                //check migrate
                MigrateAttribute attribute = Attribute.GetCustomAttribute(nodeType, typeof(MigrateAttribute)) as MigrateAttribute;
                if (attribute != null)
                {
                    nodeType = attribute.newType;
                    type = attribute.newType.Name;
                }
            }

            try
            {
                shadowTreeNode = (TreeNode)JsonUtility.FromJson(serializedData, nodeType);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                var placeholder = JsonUtility.FromJson<PlaceholderNode>(serializedData);
                placeholder.values = serializedData;
                placeholder.originalType = type;
                shadowTreeNode = placeholder;
            }

#if UNITY_EDITOR
            GenericTreeNodeManager.References[shadowTreeNode] = this;
#endif
            return shadowTreeNode;
        }

        public void ClearShadow()
        {
            shadowTreeNode = null;
        }

    }

#if UNITY_EDITOR

    /// <summary>
    /// manager of generic node
    /// </summary>
    [Obsolete("Generic Tree Node Manager is abandoned due to using SerializeReference")]
    public class GenericTreeNodeManager
    {
        /// <summary>
        /// reference table of tree node to generic node
        /// </summary>
        private static Dictionary<TreeNode, GenericTreeNode> references;
        public static Dictionary<TreeNode, GenericTreeNode> References
        {
            get
            {
                if (references is null)
                {
                    references = new Dictionary<TreeNode, GenericTreeNode>();
                }
                return references;
            }
        }

        /// <summary>
        /// save changes made to this node
        /// </summary>
        /// <param name="treeNode"></param>
        /// <returns>true if the node has a generic form</returns>
        public static bool SaveChange(TreeNode treeNode)
        {
            if (!References.TryGetValue(treeNode, out GenericTreeNode actual))
            {
                //Debug.Log("Node cannot be saved: no shadowing node found");
                return false;
            }


            actual.name = treeNode.name;
            //actual.parentUUID = treeNode.parentUUID;
            if (treeNode is not PlaceholderNode p)
            {
                actual.serializedData = JsonUtility.ToJson(treeNode);
                actual.type = treeNode.GetType().Name;
            }
            //for placeholder node, set node type to its original missing type
            else
            {
                actual.type = p.originalType;
            }
            //Debug.Log("Node saved"); 
            return true;
        }

        /// <summary>
        /// create a generic node
        /// </summary>
        /// <param name="treeNode"></param>
        /// <returns></returns>
        public static GenericTreeNode ToGenericNode(TreeNode treeNode)
        {
            if (References.TryGetValue(treeNode, out GenericTreeNode actual))
            {
                SaveChange(treeNode);
                return actual;
            }

            GenericTreeNode genericTreeNode = new GenericTreeNode(treeNode);
            References[treeNode] = genericTreeNode;
            return genericTreeNode;
        }


        public static void Clear()
        {
            var values = references.Values;
            foreach (var item in values)
            {
                item.ClearShadow();
            }
            references.Clear();
            //reshadow
            foreach (var item in values)
            {
                _ = item.ShadowTreeNode;
            }
        }

    }

#endif
}