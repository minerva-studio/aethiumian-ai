using Aethiumian.AI.Nodes;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Provides cached accessors for node types resolved at runtime.
    /// </summary>
    public static class NodeAccessorProvider
    {
        private static readonly ConcurrentDictionary<Type, NodeAccessor> Accessors = new();

        /// <summary>
        /// Gets the cached accessor for the provided node type.
        /// </summary>
        /// <param name="nodeType">The node type to access.</param>
        /// <returns>A cached <see cref="NodeAccessor"/> instance.</returns>
        /// <remarks>Exceptions: throws <see cref="ArgumentNullException"/> when <paramref name="nodeType"/> is null.</remarks>
        public static NodeAccessor GetAccessor(Type nodeType)
        {
            if (nodeType == null)
            {
                throw new ArgumentNullException(nameof(nodeType));
            }

            if (!Accessors.TryGetValue(nodeType, out NodeAccessor accessor))
            {
                accessor = CreateAccessor(nodeType);
                Accessors[nodeType] = accessor;
            }

            return accessor;
        }

        /// <summary>
        /// Creates a new accessor for the provided node type.
        /// </summary>
        /// <param name="nodeType">The node type to access.</param>
        /// <returns>A constructed <see cref="NodeAccessor"/> instance.</returns>
        /// <remarks>Exceptions: throws <see cref="ArgumentException"/> when the type is not a <see cref="TreeNode"/>.</remarks>
        private static NodeAccessor CreateAccessor(Type nodeType)
        {
            if (!typeof(TreeNode).IsAssignableFrom(nodeType))
            {
                throw new ArgumentException("Node accessor types must derive from TreeNode.", nameof(nodeType));
            }

            if (GeneratedNodePropertyAccessorProvider.TryGet(nodeType, out NodePropertyAccessor generatedAccessor))
            {
                return generatedAccessor;
            }

            Type accessorType = typeof(NodeAccessor<>).MakeGenericType(nodeType);
            MethodInfo factory = accessorType.GetMethod(nameof(NodeAccessor<TreeNode>.Create), BindingFlags.Public | BindingFlags.Static);
            return (NodeAccessor)factory.Invoke(null, null);
        }
    }
}
