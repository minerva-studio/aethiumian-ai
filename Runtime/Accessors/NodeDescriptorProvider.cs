using System;
using System.Collections.Concurrent;

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Stores generated and manually registered node descriptors for all loaded node assemblies.
    /// </summary>
    public static class NodeDescriptorProvider
    {
        private static readonly ConcurrentDictionary<Type, NodeDescriptor> descriptors = new();

        /// <summary>Registers a descriptor. Re-registering the same node type is idempotent.</summary>
        /// <param name="descriptor">The descriptor to register.</param>
        public static void Register(NodeDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            descriptors[descriptor.NodeType] = descriptor;
        }

        /// <summary>Gets a registered descriptor for a node type.</summary>
        /// <param name="nodeType">The node type to resolve.</param>
        /// <returns>The registered descriptor.</returns>
        public static NodeDescriptor Get(Type nodeType)
        {
            if (nodeType == null)
            {
                throw new ArgumentNullException(nameof(nodeType));
            }

            if (descriptors.TryGetValue(nodeType, out NodeDescriptor descriptor))
            {
                return descriptor;
            }

            throw new InvalidOperationException(
                $"No NodeDescriptor is registered for node type '{nodeType.FullName}'. " +
                "Use a source-generated node or register a descriptor explicitly.");
        }

        /// <summary>Tries to resolve a registered descriptor for a node type.</summary>
        /// <param name="nodeType">The node type to resolve.</param>
        /// <param name="descriptor">The registered descriptor, when found.</param>
        /// <returns>True when a descriptor is registered.</returns>
        public static bool TryGet(Type nodeType, out NodeDescriptor descriptor)
        {
            if (nodeType == null)
            {
                throw new ArgumentNullException(nameof(nodeType));
            }

            if (descriptors.TryGetValue(nodeType, out descriptor))
            {
                return true;
            }

            return false;
        }
    }
}
