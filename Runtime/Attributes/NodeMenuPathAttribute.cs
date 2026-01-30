using System;
using System.Collections.Generic;
using UnityEditor;

namespace Amlos.AI
{
    /// <summary>
    /// An attribute that allows defining a custom menu path for adding nodes in the AI editor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class NodeMenuPathAttribute : Attribute
    {
        private static readonly Dictionary<Type, string> menuPaths = new();
        private readonly string menuPath;

        static NodeMenuPathAttribute()
        {
#if UNITY_EDITOR
            foreach (var type in TypeCache.GetTypesWithAttribute<NodeMenuPathAttribute>())
            {
                var attribute = Attribute.GetCustomAttribute(type, typeof(NodeMenuPathAttribute)) as NodeMenuPathAttribute;
                NodeMenuPathAttribute.AddEntry(type, attribute?.MenuPath ?? string.Empty);
            }
#endif
        }

        /// <summary>
        /// Initialize a new menu path attribute.
        /// </summary>
        /// <param name="menuPath">The menu path for the node type.</param>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public NodeMenuPathAttribute(string menuPath)
        {
            this.menuPath = NormalizePath(menuPath);
        }

        /// <summary>
        /// Gets the normalized menu path.
        /// </summary>
        /// <returns>The normalized menu path.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public string MenuPath => menuPath;

        /// <summary>
        /// Add or update a cached menu path entry for a node type.
        /// </summary>
        /// <param name="type">The node type to cache.</param>
        /// <param name="menuPath">The menu path to store.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public static void AddEntry(Type type, string menuPath)
        {
            if (type == null)
            {
                return;
            }

            menuPaths[type] = NormalizePath(menuPath);
        }

        /// <summary>
        /// Get the cached menu path for a node type.
        /// </summary>
        /// <param name="type">The node type to lookup.</param>
        /// <returns>The cached menu path or an empty string.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public static string GetEntry(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            return menuPaths.TryGetValue(type, out string menuPath) ? menuPath : string.Empty;
        }

        /// <summary>
        /// Normalize menu path separators and trimming.
        /// </summary>
        /// <param name="menuPath">The raw menu path.</param>
        /// <returns>The normalized menu path.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public static string NormalizePath(string menuPath)
        {
            if (string.IsNullOrWhiteSpace(menuPath))
            {
                return string.Empty;
            }

            string normalized = menuPath.Replace('\\', '/').Trim();
            return normalized.Trim('/');
        }
    }
}
