using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Shared helpers for AI editor property drawers.
    /// </summary>
    public static class NodePropertyDrawerUtility
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
            tree = property.serializedObject.targetObject as BehaviourTreeData;
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

        /// <summary>
        /// Try resolve the uuid property from a serialized node reference or weighted node reference entry.
        /// </summary>
        /// <param name="element">Serialized reference element.</param>
        /// <param name="uuidProperty">Resolved uuid property.</param>
        /// <returns>True if resolved.</returns>
        public static bool TryGetReferenceUuidProperty(SerializedProperty element, out SerializedProperty uuidProperty)
        {
            uuidProperty = null;
            if (element == null)
            {
                return false;
            }

            uuidProperty = element.FindPropertyRelative(NodeReference.uuidPropertyName);
            if (uuidProperty != null)
            {
                return true;
            }

            SerializedProperty referenceProperty = element.FindPropertyRelative(nameof(Probability.EventWeight.reference));
            uuidProperty = referenceProperty?.FindPropertyRelative(NodeReference.uuidPropertyName);
            return uuidProperty != null;
        }

        /// <summary>
        /// Try resolve the referenced tree node from a serialized reference element.
        /// </summary>
        /// <param name="element">Serialized reference element.</param>
        /// <param name="node">Resolved tree node.</param>
        /// <returns>True if a referenced node is found.</returns>
        public static bool TryResolveReferencedNode(SerializedProperty element, out TreeNode node)
        {
            node = null;
            if (element?.serializedObject.targetObject is not BehaviourTreeData tree ||
                !TryGetReferenceUuidProperty(element, out SerializedProperty uuidProperty))
            {
                return false;
            }

            if (uuidProperty.boxedValue is not UUID uuid || uuid == UUID.Empty)
            {
                return false;
            }

            node = tree.GetNode(uuid);
            return node != null;
        }

    }
}
