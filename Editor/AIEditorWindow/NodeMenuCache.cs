using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
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
                List<Type> derivedTypes = new();
                foreach (var type in TypeCache.GetTypesDerivedFrom(baseType))
                {
                    if (IsCreatableNodeType(type))
                    {
                        derivedTypes.Add(type);
                    }
                }

                cachedTypes = derivedTypes;
                derivedTypesCache[baseType] = cachedTypes;
            }

            return cachedTypes;
        }

        /// <summary>
        /// Tests whether a type can be created through the AI editor node menu.
        /// </summary>
        /// <param name="type">The node type to test.</param>
        /// <returns>True if the editor should offer the node type for creation.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        internal static bool IsCreatableNodeType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (!type.IsSubclassOf(typeof(TreeNode)))
            {
                return false;
            }

            if (type.IsAbstract || type.IsGenericTypeDefinition)
            {
                return false;
            }

            if (!IsPublicNodeType(type))
            {
                return false;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                return false;
            }

            if (Attribute.IsDefined(type, typeof(DoNotReleaseAttribute)))
            {
                return false;
            }

            return !IsTestAssembly(type.Assembly);
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
        /// Gets the normalized authoring-menu path for one creatable node type.
        /// </summary>
        /// <param name="type">The node type to query.</param>
        /// <returns>The normalized path, or an empty string when the type belongs at the menu root.</returns>
        internal string GetMenuPath(Type type)
        {
            return type == null ? string.Empty : NodeMenuPathAttribute.NormalizePath(NodeMenuPathAttribute.GetEntry(type));
        }

        /// <summary>Builds the graph creation catalogue for the requested Service context.</summary>
        /// <param name="typeFilter">Context filter applied to every catalogue entry.</param>
        /// <returns>A newly built, read-only-by-convention graph creation folder tree.</returns>
        internal NodeCreationMenuFolder BuildCreationMenu(Func<Type, bool> typeFilter)
        {
            if (typeFilter == null)
            {
                throw new ArgumentNullException(nameof(typeFilter));
            }

            NodeCreationMenuFolder root = new("Nodes");
            HashSet<Type> categorized = new();

            AddTypes(root.GetOrAddChild("Common"), AIEditorSetting.GetOrCreateSettings().GetCommonNodeTypes(), typeFilter, categorized);

            NodeCreationMenuFolder logics = root.GetOrAddChild("Logics");
            AddDerivedTypes(logics.GetOrAddChild("Composites"), typeof(Flow), typeFilter, categorized, excludeServices: true);
            AddDerivedTypes(logics.GetOrAddChild("Determine"), typeof(DetermineBase), typeFilter, categorized);
            AddDerivedTypes(logics.GetOrAddChild("Arithmetic"), typeof(Arithmetic), typeFilter, categorized);

            AddDerivedTypes(root.GetOrAddChild("Calls"), typeof(Call), typeFilter, categorized);
            AddDerivedTypes(root.GetOrAddChild("Actions"), typeof(Nodes.Action), typeFilter, categorized);
            AddTypes(root.GetOrAddChild("Unity"), UnityNodeTypes, typeFilter, categorized);
            AddDerivedTypes(root.GetOrAddChild("Services"), typeof(Service), typeFilter, categorized);

            NodeCreationMenuFolder menuPaths = new("Menu Paths");
            foreach (Type type in allNodeTypes.Where(typeFilter))
            {
                string path = GetMenuPath(type);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                AddPathType(menuPaths, path, type);
                categorized.Add(type);
            }

            if (menuPaths.HasEntries)
            {
                root.Children.Add(menuPaths);
            }

            AddTypes(root.GetOrAddChild("Other"), allNodeTypes, typeFilter, categorized, onlyUncategorized: true);
            root.RemoveEmptyFolders();
            return root;
        }

        private static readonly Type[] UnityNodeTypes =
        {
            typeof(FunctionAction),
            typeof(FunctionCall),
            typeof(CallStatic),
            typeof(CallGameObject),
            typeof(GetComponentValue),
            typeof(SetComponentValue),
            typeof(GetObjectValue),
            typeof(SetObjectValue),
            typeof(GetComponent),
        };

        private void AddDerivedTypes(
            NodeCreationMenuFolder folder,
            Type baseType,
            Func<Type, bool> typeFilter,
            HashSet<Type> categorized,
            bool excludeServices = false)
        {
            AddTypes(folder, GetDerivedTypes(baseType), typeFilter, categorized, excludeServices: excludeServices);
        }

        private static void AddTypes(
            NodeCreationMenuFolder folder,
            IEnumerable<Type> types,
            Func<Type, bool> typeFilter,
            HashSet<Type> categorized,
            bool onlyUncategorized = false,
            bool excludeServices = false)
        {
            foreach (Type type in types)
            {
                if (!typeFilter(type) || (excludeServices && typeof(Service).IsAssignableFrom(type)))
                {
                    continue;
                }

                if (onlyUncategorized && categorized.Contains(type))
                {
                    continue;
                }

                folder.Types.Add(type);
                categorized.Add(type);
            }
        }

        private static void AddPathType(NodeCreationMenuFolder root, string path, Type type)
        {
            NodeCreationMenuFolder folder = root;
            foreach (string segment in path.Split('/').Where(segment => !string.IsNullOrWhiteSpace(segment)))
            {
                folder = folder.GetOrAddChild(segment);
            }

            if (!folder.Types.Contains(type))
            {
                folder.Types.Add(type);
            }
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
                if (!IsCreatableNodeType(type))
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
                    menuPathRoot.Types.Add(type);
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

        private static bool IsPublicNodeType(Type type)
        {
            return type.IsNested ? type.IsNestedPublic : type.IsPublic;
        }

        private static bool IsTestAssembly(System.Reflection.Assembly assembly)
        {
            if (assembly == null)
            {
                return false;
            }

            string assemblyName = assembly.GetName().Name ?? string.Empty;
            // Unity test assemblies commonly use these names and should never leak into authoring menus.
            return assemblyName.EndsWith(".Tests", StringComparison.Ordinal)
                || assemblyName.EndsWith(".Test", StringComparison.Ordinal)
                || assemblyName.IndexOf("Tests", StringComparison.Ordinal) >= 0;
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

    /// <summary>Represents one graph creation folder assembled from semantic and custom menu sources.</summary>
    internal sealed class NodeCreationMenuFolder
    {
        internal NodeCreationMenuFolder(string name)
        {
            Name = name ?? string.Empty;
            Types = new List<Type>();
            Children = new List<NodeCreationMenuFolder>();
        }

        internal string Name { get; }
        internal List<Type> Types { get; }
        internal List<NodeCreationMenuFolder> Children { get; }
        internal bool HasEntries => Types.Count > 0 || Children.Any(child => child.HasEntries);

        internal NodeCreationMenuFolder GetOrAddChild(string name)
        {
            NodeCreationMenuFolder child = Children.FirstOrDefault(folder => string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase));
            if (child == null)
            {
                child = new NodeCreationMenuFolder(name);
                Children.Add(child);
            }

            return child;
        }

        internal void RemoveEmptyFolders()
        {
            for (int index = Children.Count - 1; index >= 0; index--)
            {
                Children[index].RemoveEmptyFolders();
                if (!Children[index].HasEntries)
                {
                    Children.RemoveAt(index);
                }
            }
        }
    }
}
