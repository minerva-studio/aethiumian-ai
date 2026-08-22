using Aethiumian.AI.Nodes;
using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>Builds and opens the package documentation page for a node type.</summary>
    internal static class NodeDocumentation
    {
        private const string DocumentationRoot = "https://minerva-studio.github.io/aethiumian-ai/";
        private static readonly Regex SlugSplitRegex = new(
            @"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Za-z])(?=[0-9])|(?<=[0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])",
            RegexOptions.Compiled);

        /// <summary>Opens the localized detail page or reference index for a node type.</summary>
        /// <param name="nodeType">The node type whose documentation should be opened.</param>
        internal static void Open(Type nodeType)
        {
            Application.OpenURL(GetUrl(nodeType, Application.systemLanguage));
        }

        /// <summary>Builds a deterministic localized documentation URL for a node type.</summary>
        /// <param name="nodeType">The node type whose documentation should be resolved.</param>
        /// <param name="language">The language used to select the documentation locale.</param>
        /// <returns>A detail URL for package nodes, or a localized reference index URL otherwise.</returns>
        internal static string GetUrl(Type nodeType, SystemLanguage language)
        {
            string localePrefix = IsChinese(language) ? "zh/" : string.Empty;
            string referenceRoot = $"{DocumentationRoot}{localePrefix}reference/";
            if (!IsPackageNode(nodeType))
            {
                return referenceRoot;
            }

            string category = GetCategory(nodeType);
            return string.IsNullOrEmpty(category)
                ? referenceRoot
                : $"{referenceRoot}{category}/{ToKebabCase(nodeType.Name)}/";
        }

        /// <summary>Determines whether a node type belongs to this package assembly.</summary>
        private static bool IsPackageNode(Type nodeType)
        {
            return nodeType != null && nodeType.Assembly == typeof(TreeNode).Assembly;
        }

        /// <summary>Determines whether a Unity system language should use the Chinese site.</summary>
        private static bool IsChinese(SystemLanguage language)
        {
            return language.ToString().StartsWith("Chinese", StringComparison.Ordinal);
        }

        /// <summary>Maps a package node type to its documentation category folder.</summary>
        private static string GetCategory(Type nodeType)
        {
            if (typeof(Service).IsAssignableFrom(nodeType)) return "service";
            if (typeof(Decorator).IsAssignableFrom(nodeType)) return "decorator";
            if (typeof(Flow).IsAssignableFrom(nodeType)) return "flow";
            if (typeof(DetermineBase).IsAssignableFrom(nodeType)) return "determines";
            if (typeof(Arithmetic).IsAssignableFrom(nodeType)) return "arithmetic";
            if (typeof(Aethiumian.AI.Nodes.Action).IsAssignableFrom(nodeType)) return "actions";
            if (typeof(Call).IsAssignableFrom(nodeType)) return "calls";
            return string.Empty;
        }

        /// <summary>Converts a PascalCase node type name to the documentation slug format.</summary>
        private static string ToKebabCase(string typeName)
        {
            string[] segments = SlugSplitRegex.Split(typeName);
            return string.Join("-", Array.FindAll(segments, segment => !string.IsNullOrWhiteSpace(segment)))
                .ToLowerInvariant();
        }
    }
}
