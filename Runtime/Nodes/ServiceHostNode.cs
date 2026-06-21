#nullable enable
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Base class for nodes that can host service branches.
    /// </summary>
    [Serializable]
    public abstract class ServiceHostNode : TreeNode, IServiceHostNode
    {
        /// <summary>
        /// Services attached to this host node.
        /// </summary>
        [AIInspectorIgnore]
        public List<NodeReference>? services;

        public TreeNode Node => this;
        public List<NodeReference>? Services => services;

        /// <summary>
        /// Ensure the serialized service list exists before editor or runtime mutation.
        /// </summary>
        /// <returns>The writable service reference list.</returns>
        public List<NodeReference> EnsureServices()
        {
            services ??= new List<NodeReference>();
            return services;
        }

        /// <summary>
        /// Add a service node under this host node.
        /// </summary>
        /// <param name="service">The service node to attach.</param>
        public void AddService(Service service)
        {
            if (service == null)
            {
                return;
            }

            EnsureServices().Add(service);
            service.parent = new NodeReference(uuid);
        }
    }

    /// <summary>
    /// Centralizes the contract that service hosts are also tree nodes.
    /// </summary>
    public static class ServiceHostNodeUtility
    {
        /// <summary>
        /// Gets service references from a node without allocating a new list.
        /// </summary>
        /// <param name="node">The node that may host services.</param>
        /// <returns>The existing service list, or null when the node is not a host.</returns>
        public static List<NodeReference>? GetServices(this TreeNode? node)
        {
            return TryAsServiceHost(node, out var host) ? host.Services : null;
        }

        /// <summary>
        /// Casts a tree node to a service host and verifies the host contract in editor/debug builds.
        /// </summary>
        /// <param name="node">The node that may implement <see cref="IServiceHostNode"/>.</param>
        /// <param name="host">The service host when the cast succeeds.</param>
        /// <returns>True when the node can host services.</returns>
        public static bool TryAsServiceHost(this TreeNode? node, [NotNullWhen(true)] out IServiceHostNode? host)
        {
            if (node is IServiceHostNode serviceHost)
            {
                AssertHostIsNode(serviceHost);
                host = serviceHost;
                return true;
            }

            host = null;
            return false;
        }

        /// <summary>
        /// Guards the project convention that every service host implementation is the node itself.
        /// </summary>
        /// <param name="host">The host to validate.</param>
        /// <exception cref="InvalidOperationException">Thrown when a host breaks the node identity contract.</exception>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AssertHostIsNode(IServiceHostNode host)
        {
            if (host == null)
            {
                return;
            }

            if (host is not TreeNode node || !ReferenceEquals(host.Node, node))
            {
                throw new InvalidOperationException($"{host.GetType().FullName} implements IServiceHostNode but does not expose itself as Node.");
            }
        }
    }
}
