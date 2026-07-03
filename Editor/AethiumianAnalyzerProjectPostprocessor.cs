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
        private const string ExtensionDirectoryPrefix = "minerva-studio.aethiumian-ai-vscode-";
        private const string AnalyzerFileName = "Aethiumian.AI.CodeAnalysis.dll";
        private const string AnalyzerIncludeMarker = "<Analyzer Include=";
        private const string AttributeSuffix = "Attribute";
        private const string AttributeTypeName = nameof(GenerateForAethiumianAIAttribute);
        private const string ItemGroupCloseTag = "</ItemGroup>";
        private const string ProjectCloseTag = "</Project>";
        private static readonly string AttributeShortName = AttributeTypeName.EndsWith(AttributeSuffix, StringComparison.Ordinal)
            ? AttributeTypeName.Substring(0, AttributeTypeName.Length - AttributeSuffix.Length)
            : AttributeTypeName;

        private static string OnGeneratedCSProject(string path, string content)
        {
            if (!ShouldInjectAnalyzer(path, content))
            {
                return content;
            }

            if (content.IndexOf(AnalyzerFileName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return content;
            }

            if (!TryFindAnalyzerPath(out string analyzerPath))
            {
                return content;
            }

            return InsertAnalyzerReference(content, analyzerPath);
        }

        internal static bool ShouldInjectAnalyzer(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            foreach (string sourcePath in EnumerateCompileSourcePaths(path, content))
            {
                if (SourceContainsGenerateAttribute(sourcePath))
                {
                    return true;
                }
            }

            // Missing opt-in marker defaults to no injection to keep unrelated assemblies cheap to load.
            return false;
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
            foreach (string extensionDirectory in SortExtensionDirectories(EnumerateExtensionDirectories()))
            {
                string candidate = Path.Combine(extensionDirectory, "tools", "roslyn", AnalyzerFileName);
                if (!File.Exists(candidate))
                {
                    continue;
                }

                analyzerPath = Path.GetFullPath(candidate);
                return true;
            }

            analyzerPath = string.Empty;
            return false;
        }

        private static IEnumerable<string> EnumerateExtensionDirectories()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
            {
                yield break;
            }

            foreach (string extensionRoot in EnumerateExtensionRoots(userProfile))
            {
                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(extensionRoot, ExtensionDirectoryPrefix + "*", SearchOption.TopDirectoryOnly);
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

        private static IEnumerable<string> EnumerateCompileSourcePaths(string projectPath, string content)
        {
            XDocument projectDocument;
            try
            {
                projectDocument = XDocument.Parse(content);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is System.Xml.XmlException)
            {
                return Enumerable.Empty<string>();
            }

            string projectDirectory;
            try
            {
                projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is NotSupportedException)
            {
                return Enumerable.Empty<string>();
            }

            if (string.IsNullOrEmpty(projectDirectory))
            {
                return Enumerable.Empty<string>();
            }

            return projectDocument
                .Descendants()
                .Where(element => element.Name.LocalName == "Compile")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(includePath => !string.IsNullOrWhiteSpace(includePath))
                .Select(includePath => Path.IsPathRooted(includePath) ? includePath : Path.Combine(projectDirectory, includePath));
        }

        private static bool SourceContainsGenerateAttribute(string sourcePath)
        {
            string source;
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return false;
                }

                // Text scan runs before compilation, so the marker can opt an assembly into analyzer injection.
                source = File.ReadAllText(sourcePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                return false;
            }

            if (source.IndexOf(AttributeShortName, StringComparison.Ordinal) < 0)
            {
                return false;
            }

            string attributePattern = $"{Regex.Escape(AttributeShortName)}(?:{Regex.Escape(AttributeSuffix)})?";

            return Regex.IsMatch(
                source,
                $@"\[\s*(?:assembly\s*:\s*)?(?:global::)?(?:[A-Za-z_][A-Za-z0-9_]*\.)*{attributePattern}(?:\s|\(|\]|,)",
                RegexOptions.CultureInvariant);
        }

        private static Version GetExtensionVersion(string directory)
        {
            string directoryName = Path.GetFileName(directory);
            if (directoryName == null || !directoryName.StartsWith(ExtensionDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new Version(0, 0);
            }

            string suffix = directoryName.Substring(ExtensionDirectoryPrefix.Length);
            Match match = Regex.Match(suffix, @"^\d+(?:\.\d+){0,3}");
            if (!match.Success || !Version.TryParse(match.Value, out Version version))
            {
                return new Version(0, 0);
            }

            return version;
        }

        private static string InsertAnalyzerReference(string content, string analyzerPath)
        {
            string newline = content.Contains("\r\n") ? "\r\n" : "\n";
            string analyzerItem = $"    <Analyzer Include=\"{EscapeXmlAttribute(analyzerPath)}\" />" + newline;

            // Unity invokes OnGeneratedCSProject by reflection after generating the whole project file.
            // Inserting into Unity's analyzer ItemGroup is for readability and IDE parity; MSBuild would merge separate ItemGroups too.
            if (TryFindAnalyzerItemGroupInsertIndex(content, out int analyzerItemGroupInsertIndex))
            {
                return content.Insert(analyzerItemGroupInsertIndex, analyzerItem);
            }

            int projectCloseIndex = content.LastIndexOf(ProjectCloseTag, StringComparison.OrdinalIgnoreCase);
            if (projectCloseIndex < 0)
            {
                return content;
            }

            string analyzerItemGroup =
                "  <ItemGroup>" + newline +
                analyzerItem +
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
