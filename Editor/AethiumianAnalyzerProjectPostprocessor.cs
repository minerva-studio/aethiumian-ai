using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    internal sealed class AethiumianAnalyzerProjectPostprocessor : AssetPostprocessor
    {
        private static readonly string[] ExtensionDirectoryPrefixes =
        {
            "minerva-game-studio.aethiumian-ai-vscode-",
            "minerva-studio.aethiumian-ai-vscode-"
        };
        private static readonly string[] AnalyzerFileNames =
        {
            "Aethiumian.AI.CodeAnalysis.dll",
            "Aethiumian.AI.CodeFixes.dll"
        };
        private const string AnalyzerIncludeMarker = "<Analyzer Include=";
        private const string RuntimeProjectFileName = "Aethiumian.AI.csproj";
        private const string RuntimeSourcePathPrefix = "Packages/Aethiumian.AI/Runtime/";
        private const string ItemGroupCloseTag = "</ItemGroup>";
        private const string ProjectCloseTag = "</Project>";

        private static string OnGeneratedCSProject(string path, string content)
        {
            if (!ShouldInjectAnalyzer(path, content))
            {
                return content;
            }

            if (!TryFindAnalyzerPaths(out IReadOnlyList<string> analyzerPaths))
            {
                return content;
            }

            string[] missingPaths = analyzerPaths
                .Where(path => AnalyzerFileNames.Any(fileName =>
                    path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) &&
                    content.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) >= 0) == false)
                .ToArray();
            return missingPaths.Length == 0
                ? content
                : InsertAnalyzerReferences(content, missingPaths);
        }

        internal static bool ShouldInjectAnalyzer(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            if (IsRuntimeProject(path))
            {
                return true;
            }

            // Analyzer scope follows the runtime project relation; GenerateForAethiumianAI is generator-only.
            XDocument projectDocument = TryParseProject(content);
            if (projectDocument == null)
            {
                return false;
            }

            return projectDocument
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "Compile" &&
                    IsRuntimeSourcePath(element.Attribute("Include")?.Value)) ||
                projectDocument
                .Descendants()
                .Any(element =>
                    element.Name.LocalName == "ProjectReference" &&
                    IsRuntimeProjectReference(element.Attribute("Include")?.Value));
        }

        [MenuItem("Window/Aethiumian AI/Analyzer/Log VS Code Analyzer Path")]
        private static void LogAnalyzerPath()
        {
            if (TryFindAnalyzerPath(out string analyzerPath))
            {
                Debug.Log($"Aethiumian AI analyzer will be injected from: {analyzerPath}");
                return;
            }

            Debug.LogWarning("Aethiumian AI analyzer DLL was not found. Install the Aethiumian AI VS Code extension, then regenerate project files.");
        }

        private static bool TryFindAnalyzerPath(out string analyzerPath)
        {
            if (TryFindAnalyzerPaths(out IReadOnlyList<string> analyzerPaths) && analyzerPaths.Count > 0)
            {
                analyzerPath = analyzerPaths[0];
                return true;
            }

            analyzerPath = string.Empty;
            return false;
        }

        /// <summary>Resolves the newest complete analyzer installation for the current user.</summary>
        private static bool TryFindAnalyzerPaths(out IReadOnlyList<string> analyzerPaths)
        {
            IReadOnlyList<string> paths = FindAnalyzerPaths(EnumerateExtensionRootsForCurrentUser());
            if (paths.Count > 0)
            {
                analyzerPaths = paths;
                return true;
            }

            analyzerPaths = Array.Empty<string>();
            return false;
        }

        /// <summary>Finds the newest complete analyzer installation below the supplied extension roots.</summary>
        internal static IReadOnlyList<string> FindAnalyzerPaths(IEnumerable<string> extensionRoots)
        {
            foreach (string extensionDirectory in SortExtensionDirectories(EnumerateExtensionDirectories(extensionRoots)))
            {
                string[] candidates = AnalyzerFileNames
                    .Select(fileName => Path.Combine(extensionDirectory, "tools", "roslyn", fileName))
                    .Where(File.Exists)
                    .Select(Path.GetFullPath)
                    .ToArray();
                if (candidates.Length == AnalyzerFileNames.Length)
                {
                    return candidates;
                }
            }

            return Array.Empty<string>();
        }

        /// <summary>Enumerates installed Aethiumian AI extension directories below the supplied roots.</summary>
        internal static IEnumerable<string> EnumerateExtensionDirectories(IEnumerable<string> extensionRoots)
        {
            if (extensionRoots == null)
            {
                yield break;
            }

            foreach (string extensionRoot in extensionRoots)
            {
                foreach (string prefix in ExtensionDirectoryPrefixes)
                {
                    string[] directories;
                    try
                    {
                        directories = Directory.GetDirectories(extensionRoot, prefix + "*", SearchOption.TopDirectoryOnly);
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
                    {
                        continue;
                    }

                    foreach (string directory in directories)
                    {
                        yield return directory;
                    }
                }
            }
        }

        /// <summary>Enumerates VS Code extension roots for the current user.</summary>
        private static IEnumerable<string> EnumerateExtensionRootsForCurrentUser()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(userProfile)
                ? Enumerable.Empty<string>()
                : EnumerateExtensionRoots(userProfile);
        }

        private static IEnumerable<string> EnumerateExtensionRoots(string userProfile)
        {
            yield return Path.Combine(userProfile, ".vscode", "extensions");
            yield return Path.Combine(userProfile, ".vscode-insiders", "extensions");
        }

        private static IEnumerable<string> SortExtensionDirectories(IEnumerable<string> directories)
        {
            return directories
                .OrderByDescending(GetExtensionVersion)
                .ThenByDescending(directory => Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Determines whether the generated project is the Aethiumian runtime project itself.</summary>
        private static bool IsRuntimeProject(string projectPath)
        {
            try
            {
                return string.Equals(
                    Path.GetFileName(Path.GetFullPath(projectPath)),
                    RuntimeProjectFileName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException)
            {
                return false;
            }
        }

        /// <summary>Parses generated MSBuild content without allowing malformed XML to escape the postprocessor.</summary>
        private static XDocument TryParseProject(string content)
        {
            try
            {
                return XDocument.Parse(content);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is System.Xml.XmlException)
            {
                return null;
            }
        }

        /// <summary>Determines whether a Compile item belongs to the Aethiumian runtime package.</summary>
        private static bool IsRuntimeSourcePath(string includePath)
        {
            if (string.IsNullOrWhiteSpace(includePath))
            {
                return false;
            }

            string normalized = includePath.Replace('\\', '/');
            return normalized.StartsWith(RuntimeSourcePathPrefix, StringComparison.OrdinalIgnoreCase) ||
                normalized.IndexOf('/' + RuntimeSourcePathPrefix, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Determines whether a ProjectReference targets the Aethiumian runtime project.</summary>
        private static bool IsRuntimeProjectReference(string includePath)
        {
            if (string.IsNullOrWhiteSpace(includePath))
            {
                return false;
            }

            string normalized = includePath.Replace('\\', '/').TrimEnd('/');
            return string.Equals(
                Path.GetFileName(normalized),
                RuntimeProjectFileName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static Version GetExtensionVersion(string directory)
        {
            string directoryName = Path.GetFileName(directory);
            string matchedPrefix = ExtensionDirectoryPrefixes.FirstOrDefault(prefix =>
                directoryName != null && directoryName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (matchedPrefix == null)
            {
                return new Version(0, 0);
            }

            string suffix = directoryName.Substring(matchedPrefix.Length);
            Match match = Regex.Match(suffix, @"^\d+(?:\.\d+){0,3}");
            if (!match.Success || !Version.TryParse(match.Value, out Version version))
            {
                return new Version(0, 0);
            }

            return version;
        }

        /// <summary>Inserts all complete Aethiumian analyzer assemblies into generated project content.</summary>
        internal static string InsertAnalyzerReferences(string content, IReadOnlyList<string> analyzerPaths)
        {
            string newline = content.Contains("\r\n") ? "\r\n" : "\n";
            string analyzerItems = string.Join(
                string.Empty,
                analyzerPaths.Select(path =>
                    $"    <Analyzer Include=\"{EscapeXmlAttribute(path)}\" />" + newline));

            // Unity invokes OnGeneratedCSProject by reflection after generating the whole project file.
            // Inserting into Unity's analyzer ItemGroup is for readability and IDE parity; MSBuild would merge separate ItemGroups too.
            if (TryFindAnalyzerItemGroupInsertIndex(content, out int analyzerItemGroupInsertIndex))
            {
                return content.Insert(analyzerItemGroupInsertIndex, analyzerItems);
            }

            int projectCloseIndex = content.LastIndexOf(ProjectCloseTag, StringComparison.OrdinalIgnoreCase);
            if (projectCloseIndex < 0)
            {
                return content;
            }

            string analyzerItemGroup =
                "  <ItemGroup>" + newline +
                analyzerItems +
                "  </ItemGroup>" + newline;

            string prefix = content.Substring(0, projectCloseIndex);
            if (prefix.Length > 0 && !prefix.EndsWith("\n", StringComparison.Ordinal))
            {
                analyzerItemGroup = newline + analyzerItemGroup;
            }

            return content.Insert(projectCloseIndex, analyzerItemGroup);
        }

        private static bool TryFindAnalyzerItemGroupInsertIndex(string content, out int insertIndex)
        {
            int analyzerIndex = content.IndexOf(AnalyzerIncludeMarker, StringComparison.OrdinalIgnoreCase);
            if (analyzerIndex < 0)
            {
                insertIndex = -1;
                return false;
            }

            int closeTagIndex = content.IndexOf(ItemGroupCloseTag, analyzerIndex, StringComparison.OrdinalIgnoreCase);
            if (closeTagIndex < 0)
            {
                insertIndex = -1;
                return false;
            }

            int closeLineStartIndex = content.LastIndexOf('\n', closeTagIndex);
            insertIndex = closeLineStartIndex < 0 ? 0 : closeLineStartIndex + 1;
            return true;
        }

        private static string EscapeXmlAttribute(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
