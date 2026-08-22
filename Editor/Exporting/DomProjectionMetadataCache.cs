using Aethiumian.AI.Nodes;
using Aethiumian.AI.Attributes;
using Aethiumian.AI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Caches reflection metadata used while projecting a behaviour tree.</summary>
    internal sealed class DomProjectionMetadataCache
    {
        private readonly Dictionary<Type, DomFieldMetadata[]> fields = new Dictionary<Type, DomFieldMetadata[]>();
        private readonly Dictionary<Type, DomTypeIdentity> identities = new Dictionary<Type, DomTypeIdentity>();
        private readonly Dictionary<string, int> shortNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        internal DomProjectionMetadataCache(IEnumerable<TreeNode> nodes)
        {
            foreach (Type type in nodes
                .Where(node => node != null)
                .Select(node => node.GetType())
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                RegisterType(type);
            }
        }

        /// <summary>Returns stable serialized-field metadata for a reflected type.</summary>
        internal IReadOnlyList<DomFieldMetadata> GetFields(Type type)
        {
            if (!fields.TryGetValue(type, out DomFieldMetadata[] result))
            {
                result = BuildFields(type);
                fields[type] = result;
            }

            return result;
        }

        /// <summary>Returns the semantic short/full identity for a loaded type.</summary>
        internal DomTypeIdentity GetTypeIdentity(Type type)
        {
            if (!identities.TryGetValue(type, out DomTypeIdentity identity))
            {
                RegisterType(type);
                identity = identities[type];
            }

            return identity;
        }

        /// <summary>Resolves and returns the semantic identity for a serialized type reference.</summary>
        internal DomTypeIdentity GetTypeIdentity(string fullName, string assemblyName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return new DomTypeIdentity(string.Empty, string.Empty, false);
            }

            Type resolved = ResolveType(fullName, assemblyName);
            if (resolved != null)
            {
                return GetTypeIdentity(resolved);
            }

            string shortName = GetShortName(fullName);
            bool includeClrType = ShouldIncludeClrType(assemblyName, fullName);
            return new DomTypeIdentity(shortName, fullName, includeClrType);
        }

        private void RegisterType(Type type)
        {
            if (type == null)
            {
                return;
            }

            string fullName = type.FullName ?? type.Name;
            string shortName = GetShortName(fullName);
            if (!shortNameCounts.ContainsKey(shortName))
            {
                shortNameCounts[shortName] = 0;
            }

            shortNameCounts[shortName]++;
            bool includeClrType = ShouldIncludeClrType(type);
            identities[type] = new DomTypeIdentity(shortName, fullName, includeClrType);

            if (shortNameCounts[shortName] > 1)
            {
                foreach (Type registeredType in identities.Keys
                    .Where(candidate => GetShortName(candidate.FullName ?? candidate.Name) == shortName)
                    .ToArray())
                {
                    DomTypeIdentity previous = identities[registeredType];
                    identities[registeredType] = new DomTypeIdentity(previous.ShortName, previous.FullName, true);
                }
            }
        }

        private static DomFieldMetadata[] BuildFields(Type type)
        {
            List<Type> hierarchy = new List<Type>();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                hierarchy.Add(current);
            }

            hierarchy.Reverse();
            List<DomFieldMetadata> result = new List<DomFieldMetadata>();
            foreach (Type current in hierarchy)
            {
                foreach (FieldInfo field in UnitySerialization.GetUnitySerializedFields(current)
                    .Where(candidate => candidate.DeclaringType == current)
                    .OrderBy(candidate => candidate.MetadataToken))
                {
                    result.Add(new DomFieldMetadata(
                        field,
                        field.GetCustomAttribute<AIInspectorIgnoreAttribute>() != null,
                        field.Name == nameof(ServiceHostNode.services)));
                }
            }

            return result.ToArray();
        }

        private static Type ResolveType(string fullName, string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return Type.GetType(fullName, false);
            }

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(candidate => string.Equals(candidate.GetName().Name, assemblyName, StringComparison.Ordinal));
            return assembly?.GetType(fullName, false) ?? Type.GetType(fullName + ", " + assemblyName, false);
        }

        private static bool ShouldIncludeClrType(Type type)
        {
            return type != null && ShouldIncludeClrType(type.Assembly.GetName().Name, type.FullName ?? type.Name)
                && type.Assembly != typeof(TreeNode).Assembly;
        }

        private static bool ShouldIncludeClrType(string assemblyName, string fullName)
        {
            if (string.Equals(assemblyName, typeof(TreeNode).Assembly.GetName().Name, StringComparison.Ordinal)
                || string.IsNullOrEmpty(fullName))
            {
                return false;
            }

            return !fullName.StartsWith("System.", StringComparison.Ordinal)
                && !fullName.StartsWith("UnityEngine.", StringComparison.Ordinal)
                && !fullName.StartsWith("UnityEditor.", StringComparison.Ordinal);
        }

        private static string GetShortName(string fullName)
        {
            int separator = Math.Max(fullName.LastIndexOf('.'), fullName.LastIndexOf('+'));
            return separator < 0 ? fullName : fullName.Substring(separator + 1);
        }
    }

    /// <summary>Cached serialized-field metadata for one reflected type.</summary>
    internal sealed class DomFieldMetadata
    {
        internal DomFieldMetadata(FieldInfo field, bool isIgnored, bool isService)
        {
            Field = field;
            IsIgnored = isIgnored;
            IsService = isService;
        }

        internal FieldInfo Field { get; }
        internal bool IsIgnored { get; }
        internal bool IsService { get; }
    }

    /// <summary>Stable short/full type identity used by the semantic DOM.</summary>
    internal readonly struct DomTypeIdentity
    {
        internal DomTypeIdentity(string shortName, string fullName, bool includeClrType)
        {
            ShortName = shortName;
            FullName = fullName;
            IncludeClrType = includeClrType;
        }

        internal string ShortName { get; }
        internal string FullName { get; }
        internal bool IncludeClrType { get; }
    }
}
