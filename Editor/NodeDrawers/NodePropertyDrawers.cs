using Amlos.AI.Nodes;
using System;
using UnityEditor;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Shared helpers for AI editor property drawers.
    /// </summary>
    public static class NodePropertyDrawerContext
    {
        private const string NodePathToken = "nodes.Array.data[";

        /// <summary>
        /// Try get the behaviour tree data from a serialized property.
        /// </summary>
        /// <param name="property">Serialized property instance.</param>
        /// <param name="tree">Resolved behaviour tree data.</param>
        /// <returns>True if resolved.</returns>
        public static bool TryGetTree(SerializedProperty property, out BehaviourTreeData tree)
        {
            tree = property.serializedObject.targetObject as BehaviourTreeData ?? AIEditorWindow.Instance?.tree;
            return tree != null;
        }

        /// <summary>
        /// Try resolve the node that owns the property, based on its serialized path.
        /// </summary>
        /// <param name="property">Serialized property instance.</param>
        /// <param name="tree">Behaviour tree data.</param>
        /// <param name="node">Resolved node.</param>
        /// <returns>True if resolved.</returns>
        public static bool TryGetNode(SerializedProperty property, BehaviourTreeData tree, out TreeNode node)
        {
            node = null;
            if (tree == null || string.IsNullOrEmpty(property.propertyPath))
            {
                return false;
            }

            string path = property.propertyPath;
            int start = path.IndexOf(NodePathToken, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += NodePathToken.Length;
            int end = path.IndexOf(']', start);
            if (end < 0)
            {
                return false;
            }

            if (!int.TryParse(path.Substring(start, end - start), out int index))
            {
                return false;
            }

            if (index < 0 || index >= tree.nodes.Count)
            {
                return false;
            }

            node = tree.nodes[index];
            return node != null;
        }
    }
}
