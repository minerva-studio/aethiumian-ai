using Amlos.AI.Nodes;
using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Amlos.AI.Accessors
{
    /// <summary>
    /// Provides cached accessors for a given node type.
    /// </summary>
    /// <typeparam name="T">The node type to access.</typeparam>
    public static class NodeAccessorProvider<T> where T : TreeNode
    {
        /// <summary>
        /// Gets the cached accessor for the node type.
        /// </summary>
        /// <returns>A <see cref="NodeAccessor{T}"/> instance for the node type.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public static NodeAccessor<T> Accessor => accessor ??= NodeAccessor<T>.Create();

        /// <summary>
        /// Stores the cached accessor instance for the node type.
        /// </summary>
        /// <returns>The cached <see cref="NodeAccessor{T}"/> instance.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public static NodeAccessor<T> accessor;
    }

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

            Type accessorType = typeof(NodeAccessor<>).MakeGenericType(nodeType);
            MethodInfo factory = accessorType.GetMethod(nameof(NodeAccessor<TreeNode>.Create), BindingFlags.Public | BindingFlags.Static);
            return (NodeAccessor)factory.Invoke(null, null);
        }
    }
}
