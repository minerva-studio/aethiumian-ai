using Amlos.AI.Nodes;
using Minerva.Module;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Caches node menu data to reduce editor allocation and reflection costs.
    /// </summary>
    internal sealed class NodeMenuCache
    {
        private static NodeMenuCache shared;

        private readonly List<Type> allNodeTypes = new();
        private readonly Dictionary<Type, IReadOnlyList<Type>> derivedTypesCache = new();
        private readonly Dictionary<Type, string> displayNameCache = new();
        private readonly Dictionary<Type, GUIContent> contentCache = new();
        private readonly NodeMenuPathFolder menuPathRoot = new(string.Empty);

        /// <summary>
        /// Gets the shared menu cache instance.
        /// </summary>
        /// <returns>The shared cache instance.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public static NodeMenuCache Shared => shared ??= new NodeMenuCache();

        /// <summary>
        /// Initialize cached node data.
        /// </summary>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private NodeMenuCache()
        {
            BuildAllNodeTypes();
            BuildMenuPathCache();
        }

        /// <summary>
        /// Gets all non-abstract, released node types.
        /// </summary>
        /// <returns>A cached list of node types.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public IReadOnlyList<Type> AllNodeTypes => allNodeTypes;

        /// <summary>
        /// Gets the root folder for menu path entries.
        /// </summary>
        /// <returns>The root folder for menu paths.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public NodeMenuPathFolder MenuPathRoot => menuPathRoot;

        /// <summary>
        /// Get cached derived node types for a base type.
        /// </summary>
        /// <param name="baseType">The base type to query.</param>
        /// <returns>A cached list of derived types.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public IReadOnlyList<Type> GetDerivedTypes(Type baseType)
        {
            if (baseType == null)
            {
                return Array.Empty<Type>();
            }

            if (!derivedTypesCache.TryGetValue(baseType, out var cachedTypes))
            {
                cachedTypes = new List<Type>(TypeCache.GetTypesDerivedFrom(baseType));
                derivedTypesCache[baseType] = cachedTypes;
            }

            return cachedTypes;
        }

        /// <summary>
        /// Get cached GUI content for a node type.
        /// </summary>
        /// <param name="type">The node type to display.</param>
        /// <returns>A cached GUIContent instance.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public GUIContent GetContent(Type type)
        {
            if (type == null)
            {
                return GUIContent.none;
            }

            if (!contentCache.TryGetValue(type, out var content))
            {
                CacheDisplayData(type);
                content = contentCache.TryGetValue(type, out var cached) ? cached : GUIContent.none;
            }

            return content;
        }

        /// <summary>
        /// Get the cached display name for a node type.
        /// </summary>
        /// <param name="type">The node type to resolve.</param>
        /// <returns>The cached display name.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public string GetDisplayName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (!displayNameCache.TryGetValue(type, out var displayName))
            {
                CacheDisplayData(type);
                return displayNameCache.TryGetValue(type, out var cached) ? cached : string.Empty;
            }

            return displayName;
        }

        /// <summary>
        /// Get the cached tooltip text for a node type.
        /// </summary>
        /// <param name="type">The node type to resolve.</param>
        /// <returns>The tooltip text.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public string GetTooltip(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            return GetContent(type).tooltip ?? string.Empty;
        }

        /// <summary>
        /// Populate the cache with all valid node types.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void BuildAllNodeTypes()
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom<TreeNode>())
            {
                if (type.IsAbstract)
                {
                    continue;
                }

                if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute)))
                {
                    continue;
                }

                allNodeTypes.Add(type);
                CacheDisplayData(type);
            }
        }

        /// <summary>
        /// Build the menu path hierarchy from attribute data.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void BuildMenuPathCache()
        {
            foreach (var type in allNodeTypes)
            {
                string path = NodeMenuPathAttribute.GetEntry(type);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                AddToMenuPathCache(type, path);
            }
        }

        /// <summary>
        /// Add a node type to the menu path hierarchy.
        /// </summary>
        /// <param name="type">The node type to register.</param>
        /// <param name="menuPath">The menu path to register.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void AddToMenuPathCache(Type type, string menuPath)
        {
            string normalized = NodeMenuPathAttribute.NormalizePath(menuPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            var folder = menuPathRoot;
            var segments = normalized.Split('/');
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                folder = folder.GetOrAddChild(segment);
            }

            folder.Types.Add(type);
        }

        /// <summary>
        /// Cache display name and tooltip data for a node type.
        /// </summary>
        /// <param name="type">The node type to cache.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void CacheDisplayData(Type type)
        {
            if (type == null)
            {
                return;
            }

            string alias = AliasAttribute.GetEntry(type);
            string displayName = string.IsNullOrWhiteSpace(alias) ? type.Name.ToTitleCase() : alias;

            displayNameCache[type] = displayName;
            contentCache[type] = new GUIContent(displayName, NodeTipAttribute.GetEntry(type));
        }
    }

    /// <summary>
    /// Represents a folder in the node menu path hierarchy.
    /// </summary>
    internal sealed class NodeMenuPathFolder
    {
        private readonly SortedDictionary<string, NodeMenuPathFolder> children;

        /// <summary>
        /// Initialize a new menu path folder.
        /// </summary>
        /// <param name="name">The folder name.</param>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public NodeMenuPathFolder(string name)
        {
            Name = name ?? string.Empty;
            children = new SortedDictionary<string, NodeMenuPathFolder>(StringComparer.OrdinalIgnoreCase);
            Types = new List<Type>();
        }

        /// <summary>
        /// Gets the folder name.
        /// </summary>
        /// <returns>The folder name.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public string Name { get; }

        /// <summary>
        /// Gets child folders by name.
        /// </summary>
        /// <returns>A read-only view of child folders.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public IReadOnlyDictionary<string, NodeMenuPathFolder> Children => children;

        /// <summary>
        /// Gets the node types assigned to this folder.
        /// </summary>
        /// <returns>The list of node types.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public List<Type> Types { get; }

        /// <summary>
        /// Get or create a child folder.
        /// </summary>
        /// <param name="name">The child folder name.</param>
        /// <returns>The child folder instance.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public NodeMenuPathFolder GetOrAddChild(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return this;
            }

            if (!children.TryGetValue(name, out var folder))
            {
                folder = new NodeMenuPathFolder(name);
                children.Add(name, folder);
            }

            return folder;
        }
    }
}
