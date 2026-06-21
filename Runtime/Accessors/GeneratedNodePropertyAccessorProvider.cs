using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace Aethiumian.AI.Accessors
{
    /// <summary>
    /// Resolves generated node property accessors from the node assembly.
    /// </summary>
    public static class GeneratedNodePropertyAccessorProvider
    {
        private const string RegistryTypeName = "Aethiumian.AI.Accessors.GeneratedNodePropertyAccessorRegistry";
        private const string TryGetMethodName = "TryGet";

        private static readonly ConcurrentDictionary<Type, AccessorLookup> Accessors = new();

        public static bool TryGet(Type nodeType, out NodePropertyAccessor accessor)
        {
            if (nodeType == null)
            {
                throw new ArgumentNullException(nameof(nodeType));
            }

            AccessorLookup lookup = Accessors.GetOrAdd(nodeType, ResolveAccessor);
            accessor = lookup.Accessor;
            return lookup.Found;
        }

        private static AccessorLookup ResolveAccessor(Type nodeType)
        {
            Type registryType = nodeType.Assembly.GetType(RegistryTypeName);
            if (registryType == null)
            {
                return AccessorLookup.Missing;
            }

            MethodInfo tryGet = registryType.GetMethod(
                TryGetMethodName,
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type), typeof(NodePropertyAccessor).MakeByRefType() },
                null);
            if (tryGet == null)
            {
                return AccessorLookup.Missing;
            }

            object[] args = { nodeType, null };
            if (tryGet.Invoke(null, args) is true && args[1] is NodePropertyAccessor accessor)
            {
                return new AccessorLookup(true, accessor);
            }

            return AccessorLookup.Missing;
        }

        private readonly struct AccessorLookup
        {
            public static readonly AccessorLookup Missing = new(false, null);

            public AccessorLookup(bool found, NodePropertyAccessor accessor)
            {
                Found = found;
                Accessor = accessor;
            }

            public bool Found { get; }
            public NodePropertyAccessor Accessor { get; }
        }
    }
}
