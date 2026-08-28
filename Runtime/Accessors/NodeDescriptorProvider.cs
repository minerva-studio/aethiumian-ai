using System;
using System.Collections.Concurrent;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Stores generated and manually registered node descriptors for all loaded node assemblies.
    /// </summary>
    public static class NodeDescriptorProvider
    {
        private static readonly ConcurrentDictionary<Type, NodeDescriptor> descriptors = new();

#if UNITY_EDITOR
        private static readonly ConcurrentDictionary<Type, Lazy<NodeDescriptor>> reflectionDescriptors = new();
        private static readonly ConcurrentDictionary<Type, byte> reportedReflectionFallbacks = new();
#endif

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

#if UNITY_EDITOR
            if (TryGetReflectionDescriptor(nodeType, out descriptor))
            {
                return descriptor;
            }
#endif

            throw new InvalidOperationException(
                $"No NodeDescriptor is registered for node type '{nodeType.FullName}'. " +
                $"Assembly '{nodeType.Assembly.GetName().Name}' must enable source generation or register a descriptor explicitly.");
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

#if UNITY_EDITOR
            return TryGetReflectionDescriptor(nodeType, out descriptor);
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        /// <summary>Resolves an editor-only reflection descriptor for a node outside the runtime assembly.</summary>
        private static bool TryGetReflectionDescriptor(Type nodeType, out NodeDescriptor descriptor)
        {
            descriptor = null;
            if (!ReflectionNodeDescriptor.IsEligible(nodeType))
            {
                return false;
            }

            string assemblyName = nodeType.Assembly.GetName().Name;
            if (assemblyName == "Aethiumian.AI")
            {
                return false;
            }

            bool internalEditorOrTest = IsInternalEditorOrTestAssembly(nodeType);
            if (!internalEditorOrTest)
            {
                if (reportedReflectionFallbacks.TryAdd(nodeType, 0))
                {
                    Debug.LogError(
                        $"Node type '{nodeType.FullName}' from assembly '{assemblyName}' " +
                        "has no generated or registered NodeDescriptor. " +
                        "The Unity Editor is using a temporary reflection fallback; Player/runtime will throw. " +
                        "Enable Aethiumian.AI source generation or register a descriptor explicitly.");
                }
            }

            Lazy<NodeDescriptor> lazy = reflectionDescriptors.GetOrAdd(
                nodeType,
                static type => new Lazy<NodeDescriptor>(
                    () => ReflectionNodeDescriptor.Create(type),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                descriptor = lazy.Value;
                descriptors.TryAdd(nodeType, descriptor);
                NodeReferenceStructureProvider.Register(
                    nodeType,
                    ((ReflectionNodeDescriptor)descriptor).ReferenceStructure);
                return true;
            }
            catch
            {
                reflectionDescriptors.TryRemove(nodeType, out _);
                throw;
            }
        }

        /// <summary>Determines whether a node belongs to an editor or test assembly allowed to use reflection.</summary>
        private static bool IsInternalEditorOrTestAssembly(Type nodeType)
        {
            string assemblyName = nodeType.Assembly.GetName().Name;
            return assemblyName == "Aethiumian.AI.Editor" ||
                assemblyName.EndsWith(".Tests", StringComparison.Ordinal);
        }
#endif
    }
}
