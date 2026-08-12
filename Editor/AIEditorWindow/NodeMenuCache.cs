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

        private static readonly Dictionary<string, string> commonTooltips = new(StringComparer.Ordinal)
        {
            ["Always"] = "Always succeeds without evaluating its child.",
            ["Condition"] = "Evaluates a condition and runs the success or failure branch.",
            ["Constant"] = "Returns a fixed success or failure result.",
            ["Decision"] = "Evaluates child branches in order and selects the first successful one.",
            ["ForEach"] = "Runs a body once for each item in an enumerable value.",
            ["Inverter"] = "Inverts the success or failure result of its child.",
            ["Loop"] = "Repeats a body using a count or a condition-controlled loop.",
            ["Parallel"] = "Runs child branches concurrently and waits for all or any branch.",
            ["Probability"] = "Selects one child branch using fixed weights.",
            ["PseudoProbability"] = "Selects a child using variable-driven weights without random sampling.",
            ["Restart"] = "Restarts execution of the current behaviour tree.",
            ["ResultChanged"] = "Succeeds when the child result changes from its previous result.",
            ["Rollback"] = "Redirects execution to a referenced node in the current branch.",
            ["Sequence"] = "Runs child nodes in order until one fails.",
            ["Wait"] = "Waits for a configured duration or number of frames.",
            ["Yield"] = "Pauses execution for one frame before continuing.",
            ["FunctionCall"] = "Calls a selected function once.",
            ["FunctionAction"] = "Invokes a selected function as an action.",
            ["ObjectCall"] = "Calls a method on a referenced object.",
            ["CallGameObject"] = "Calls a method on a component attached to a GameObject.",
            ["CallStatic"] = "Calls a static method with optional arguments.",
            ["GetComponent"] = "Gets a component from the current GameObject.",
            ["GetComponentValue"] = "Reads a value from a component on the current GameObject.",
            ["SetComponentValue"] = "Writes a value to a component on the current GameObject.",
            ["GetObjectValue"] = "Reads a value from a referenced object.",
            ["SetObjectValue"] = "Writes a value to a referenced object.",
            ["Instantiate"] = "Instantiates a prefab or object in the scene.",
            ["Raycast"] = "Performs a 3D physics raycast and returns whether it hit.",
            ["Raycast2D"] = "Performs a 2D physics raycast and returns whether it hit.",
            ["ScriptCall"] = "Calls a method on the configured script once.",
            ["Equals"] = "Compares two values for equality.",
            ["IsNull"] = "Checks whether a value or object reference is null.",
            ["IsTypeOf"] = "Checks whether a value has the configured type.",
            ["IsComponent"] = "Checks whether a reference points to a Component.",
            ["IsGameObject"] = "Checks whether a reference points to a GameObject.",
            ["IsComponentOrGameObject"] = "Checks whether a reference is a Component or GameObject.",
            ["IsInScreen"] = "Checks whether a world position is inside the main camera view.",
            ["IsInVision"] = "Checks whether a target is visible to the current entity.",
            ["IsPlayingAnimation"] = "Checks whether an Animator is playing a named state.",
            ["DistanceTo"] = "Reads the distance between the entity and a target.",
            ["Position"] = "Reads the current position of a GameObject.",
            ["MovingDirection"] = "Reads the current movement direction of the entity.",
            ["Add"] = "Adds two numeric or vector values.",
            ["Subtract"] = "Subtracts one numeric or vector value from another.",
            ["Multiply"] = "Multiplies two numeric values or vectors.",
            ["Divide"] = "Divides one numeric value or vector by another.",
            ["Absolute"] = "Calculates the absolute value of a numeric value or vector.",
            ["Normalize"] = "Normalizes a vector value.",
            ["Magnitude"] = "Calculates the magnitude of a vector.",
            ["CreateVector2"] = "Constructs a Vector2 from its components.",
            ["CreateVector3"] = "Constructs a Vector3 from its components.",
            ["VectorComponent"] = "Reads one component from a vector.",
            ["TypeOf"] = "Reads the runtime type of a value.",
            ["TypeObject"] = "Stores a type object in a variable.",
            ["Copy"] = "Copies a value from one variable to another.",
            ["Boolean"] = "Reads a boolean value from a variable.",
            ["Branch"] = "Starts a branch as a service of the current execution stack.",
            ["Break"] = "Stops the current service branch when its condition succeeds.",
            ["Interrupt"] = "Interrupts the host node when its condition succeeds.",
            ["Timeout"] = "Stops the host node after a timeout and returns the configured result.",
            ["Timer"] = "Updates a variable with elapsed time while the host runs.",
            ["Update"] = "Repeats a service subtree while the host node is active.",
        };
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
                    if (IsMenuVisibleNodeType(type))
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

        /// <summary>Tests whether a creatable node should be exposed in editor creation menus.</summary>
        internal static bool IsMenuVisibleNodeType(Type type)
        {
            return IsCreatableNodeType(type) && !Attribute.IsDefined(type, typeof(ObsoleteAttribute));
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

        /// <summary>Gets creatable node types for one explicit creation context.</summary>
        /// <param name="context">The creation context to query.</param>
        /// <returns>All visible creatable types belonging to the context.</returns>
        internal IReadOnlyList<Type> GetCreationTypes(NodeCreationMenuContext context)
        {
            bool services = context == NodeCreationMenuContext.Services;
            return allNodeTypes.Where(type => typeof(Service).IsAssignableFrom(type) == services).ToArray();
        }

        /// <summary>Builds the canonical graph creation catalogue for the requested context.</summary>
        /// <param name="context">The explicit creation context.</param>
        /// <returns>A newly built, read-only-by-convention graph creation folder tree.</returns>
        internal NodeCreationMenuFolder BuildCreationMenu(NodeCreationMenuContext context)
        {
            NodeCreationMenuFolder root = new(context == NodeCreationMenuContext.Services ? "Services" : "Nodes");
            foreach (Type type in GetCreationTypes(context))
            {
                if (context == NodeCreationMenuContext.Services)
                {
                    root.Types.Add(type);
                    continue;
                }

                string path = GetMenuPath(type);
                if (!string.IsNullOrEmpty(path))
                {
                    AddPathType(root, path, type);
                }
                else
                {
                    string semantic = GetSemanticFolder(type);
                    AddPathType(root, semantic, type);
                }
            }

            root.RemoveEmptyFolders();
            return root;
        }

        private static string GetSemanticFolder(Type type)
        {
            if (typeof(Service).IsAssignableFrom(type)) return "Services";
            if (typeof(Flow).IsAssignableFrom(type)) return "Control Flow";
            if (typeof(DetermineBase).IsAssignableFrom(type)) return "Conditions";
            if (typeof(Arithmetic).IsAssignableFrom(type)) return "Calculations";
            if (typeof(Nodes.Action).IsAssignableFrom(type)) return "Actions";
            if (typeof(Call).IsAssignableFrom(type)) return "Calls";
            return "Other";
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
                if (!IsMenuVisibleNodeType(type))
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
            contentCache[type] = new GUIContent(displayName, ResolveTooltip(type));
        }

        /// <summary>Resolves an authored node tip or a concise editor fallback for an undocumented node.</summary>
        /// <param name="type">The node type whose menu description is requested.</param>
        /// <returns>A non-empty description for every visible concrete node.</returns>
        private static string ResolveTooltip(Type type)
        {
            string authored = NodeTipAttribute.GetEntry(type);
            if (!string.IsNullOrWhiteSpace(authored))
            {
                return authored;
            }

            if (commonTooltips.TryGetValue(type.Name, out string specific))
            {
                return specific;
            }

            if (typeof(Service).IsAssignableFrom(type)) return "Runs as a service while its host node is active.";
            if (typeof(Flow).IsAssignableFrom(type)) return "Controls how child nodes are executed.";
            if (typeof(DetermineBase).IsAssignableFrom(type)) return "Evaluates a condition and returns success or failure.";
            if (typeof(Arithmetic).IsAssignableFrom(type)) return "Calculates a value from inputs and writes the result.";
            if (typeof(Call).IsAssignableFrom(type)) return "Performs an operation and returns success or failure.";
            if (typeof(Aethiumian.AI.Nodes.Action).IsAssignableFrom(type)) return "Performs an action and returns its execution result.";
            return "Executes this behaviour-tree node.";
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

    /// <summary>Identifies the node creation catalogue requested by an editor entry point.</summary>
    internal enum NodeCreationMenuContext
    {
        Nodes,
        Services,
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
