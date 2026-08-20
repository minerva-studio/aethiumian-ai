using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.PackageManager;

namespace Aethiumian.AI.Editor.Tests.Documentation
{
    /// <summary>
    /// Verifies that the public node reference pages in English and Chinese match the
    /// package runtime-visible node inventory from <see cref="NodeMenuCache"/>.
    /// </summary>
    [TestFixture]
    public sealed class DocumentationReferenceCoverageTests
    {
        private static readonly Regex HeadingRegex =
            new(@"^(?<level>#{1,6})\s+(?<title>.+?)\s*$", RegexOptions.Compiled);

        private static readonly Regex QuotedTitleRegex =
            new(@"^`(?<name>[^`]+)`$", RegexOptions.Compiled);

        private static readonly Regex SlugSplitRegex =
            new(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Za-z])(?=[0-9])|(?<=[0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

        private static readonly Dictionary<ReferenceCategory, string> CategoryFolders = new()
        {
            { ReferenceCategory.Action, "actions" },
            { ReferenceCategory.Arithmetic, "arithmetic" },
            { ReferenceCategory.Call, "calls" },
            { ReferenceCategory.Determine, "determines" },
            { ReferenceCategory.Decorator, "decorator" },
            { ReferenceCategory.Flow, "flow" },
            { ReferenceCategory.Service, "service" },
        };

        private static readonly string[] RequiredEnglishHeadings =
        {
            "Purpose",
            "Key inputs / outputs",
            "Success / Failure semantics",
            "Source code",
        };

        private static readonly Regex[] RequiredChineseSectionPatterns =
        {
            new(@"^用途$", RegexOptions.Compiled),
            new(@"^关键(输入|参数).*(输出|参数).*$", RegexOptions.Compiled),
            new(@"^成功.*(或|/|、).*失败.*(语义|含义).*$", RegexOptions.Compiled),
            new(@"^源码.*$", RegexOptions.Compiled),
        };

        /// <summary>
        /// Verifies the structure of one English or Chinese reference detail page.
        /// </summary>
        [TestCaseSource(nameof(ReferencePageCases))]
        public void DocumentationReferencePage_HasRequiredStructure(ReferencePageCase page)
        {
            Assert.That(File.Exists(page.IndexPath), Is.True, $"Missing index page: {page.IndexPath}");
            ReadNodeTypeTitle(page.IndexPath, page.Category, page.Slug);
            AssertRequiredSections(page);
        }

        /// <summary>
        /// Verifies that one category has matching English and Chinese paths and node names.
        /// </summary>
        [TestCaseSource(nameof(BilingualCategoryCases))]
        public void DocumentationReferenceBilingualPathsAndNames_Match(ReferenceCategory category)
        {
            string packageRoot = ResolvePackageRoot();
            var englishNodePages = LoadReferenceNodePages(
                Path.Combine(packageRoot, "Documentation~", "en", "reference"));
            var chineseNodePages = LoadReferenceNodePages(
                Path.Combine(packageRoot, "Documentation~", "zh", "reference"));

            AssertLanguageNodePathsAndNamesMatch(englishNodePages, chineseNodePages, category);
        }

        /// <summary>
        /// Verifies that one category's runtime nodes have matching English and Chinese pages.
        /// </summary>
        [TestCaseSource(nameof(RuntimeCoverageCategoryCases))]
        public void DocumentationReferenceRuntimeCoverage_MatchesLanguages(ReferenceCategory category)
        {
            string packageRoot = ResolvePackageRoot();
            var englishNodePages = LoadReferenceNodePages(
                Path.Combine(packageRoot, "Documentation~", "en", "reference"));
            var chineseNodePages = LoadReferenceNodePages(
                Path.Combine(packageRoot, "Documentation~", "zh", "reference"));
            var runtimeNodePages = ReadRuntimeCategoryNodes();

            var enByPath = englishNodePages[category];
            var zhByPath = chineseNodePages[category];
            var runtimeByPath = runtimeNodePages[category];

            AssertThatKeySetsMatch(runtimeByPath.Keys, enByPath.Keys, $"Runtime node set mismatch in '{category}' (runtime vs en).");
            AssertThatKeySetsMatch(runtimeByPath.Keys, zhByPath.Keys, $"Runtime node set mismatch in '{category}' (runtime vs zh).");

            foreach (KeyValuePair<string, string> pair in runtimeByPath)
            {
                Assert.That(enByPath[pair.Key], Is.EqualTo(pair.Value),
                    $"English node name mismatch for '{pair.Key}'.");
                Assert.That(zhByPath[pair.Key], Is.EqualTo(pair.Value),
                    $"Chinese node name mismatch for '{pair.Key}'.");
            }
        }

        /// <summary>
        /// Generates one named test case for each English and Chinese detail page.
        /// </summary>
        public static IEnumerable<TestCaseData> ReferencePageCases()
        {
            string packageRoot = ResolvePackageRoot();
            foreach ((string language, bool isEnglish) in new[] { ("en", true), ("zh", false) })
            {
                string referenceRoot = Path.Combine(packageRoot, "Documentation~", language, "reference");
                foreach (ReferenceCategory category in Enum.GetValues(typeof(ReferenceCategory)).Cast<ReferenceCategory>())
                {
                    string categoryFolder = CategoryFolders[category];
                    string categoryPath = Path.Combine(referenceRoot, categoryFolder);
                    foreach (string nodeDir in Directory.GetDirectories(categoryPath, "*", SearchOption.TopDirectoryOnly)
                        .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                    {
                        string slug = Path.GetFileName(nodeDir);
                        string relativePath = Path.Combine(language, "reference", categoryFolder, slug, "index.md")
                            .Replace('\\', '/');
                        var page = new ReferencePageCase(
                            language,
                            isEnglish,
                            category,
                            slug,
                            relativePath,
                            Path.Combine(nodeDir, "index.md"));
                        yield return new TestCaseData(page)
                            .SetName($"DocumentationReferencePage_HasRequiredStructure({relativePath})");
                    }
                }
            }
        }

        /// <summary>
        /// Generates one named test case for each reference category.
        /// </summary>
        public static IEnumerable<TestCaseData> BilingualCategoryCases()
        {
            foreach (ReferenceCategory category in Enum.GetValues(typeof(ReferenceCategory)).Cast<ReferenceCategory>())
            {
                yield return new TestCaseData(category).SetName($"BilingualReferenceCategory({category})");
            }
        }

        /// <summary>
        /// Generates named category cases for runtime coverage checks.
        /// </summary>
        public static IEnumerable<TestCaseData> RuntimeCoverageCategoryCases()
        {
            foreach (ReferenceCategory category in Enum.GetValues(typeof(ReferenceCategory)).Cast<ReferenceCategory>())
            {
                yield return new TestCaseData(category).SetName($"RuntimeCoverageReferenceCategory({category})");
            }
        }

        /// <summary>
        /// Loads reference details by scanning category/*/index.md without validating page structure.
        /// </summary>
        private static Dictionary<ReferenceCategory, Dictionary<string, string>> LoadReferenceNodePages(
            string languageRoot)
        {
            Assert.That(Directory.Exists(languageRoot), Is.True, $"Reference folder missing: {languageRoot}");

            var result =
                new Dictionary<ReferenceCategory, Dictionary<string, string>>();

            foreach (ReferenceCategory category in Enum.GetValues(typeof(ReferenceCategory)).Cast<ReferenceCategory>())
            {
                string categoryFolder = CategoryFolders[category];
                string categoryPath = Path.Combine(languageRoot, categoryFolder);

                Assert.That(Directory.Exists(categoryPath), Is.True, $"Category folder missing: {categoryPath}");

                var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
                string[] nodeDirs = Directory.GetDirectories(categoryPath, "*", SearchOption.TopDirectoryOnly);

                Assert.That(nodeDirs, Is.Not.Empty, $"No node detail directories under '{categoryPath}'.");

                foreach (string nodeDir in nodeDirs.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
                {
                    string slug = Path.GetFileName(nodeDir);
                    string indexPath = Path.Combine(nodeDir, "index.md");

                    string nodeType = ReadNodeTypeTitleForMapping(indexPath);

                    string relativePath = Path.Combine(categoryFolder, slug, "index.md").Replace('\\', '/');
                    bool added = byPath.TryAdd(relativePath, nodeType);
                    Assert.That(added, Is.True, $"Duplicate relative path in documentation: {relativePath}");
                }

                result[category] = byPath;
            }

            return result;
        }

        /// <summary>
        /// Verifies EN/ZH dictionaries have identical relative paths and node type names.
        /// </summary>
        private static void AssertLanguageNodePathsAndNamesMatch(
            Dictionary<ReferenceCategory, Dictionary<string, string>> en,
            Dictionary<ReferenceCategory, Dictionary<string, string>> zh,
            ReferenceCategory category)
        {
            AssertThatKeySetsMatch(en[category].Keys, zh[category].Keys, $"Path mismatch in '{category}' between EN and ZH.");

            foreach (KeyValuePair<string, string> pair in en[category])
            {
                Assert.That(zh[category][pair.Key], Is.EqualTo(pair.Value),
                    $"Node name mismatch in '{category}' for '{pair.Key}'.");
            }
        }

        /// <summary>
        /// Reads runtime category mapping from package node cache, enforcing path/slug contract.
        /// </summary>
        private static Dictionary<ReferenceCategory, Dictionary<string, string>> ReadRuntimeCategoryNodes()
        {
            var packageAssembly = typeof(TreeNode).Assembly;
            Type[] allVisibleNodeTypes = NodeMenuCache.Shared.AllNodeTypes.ToArray();
            Assert.That(allVisibleNodeTypes, Is.Not.Empty, "NodeMenuCache.Shared.AllNodeTypes should not be empty.");

            Type[] packageNodeTypes = allVisibleNodeTypes.Where(type => type.Assembly == packageAssembly).ToArray();
            Assert.That(
                packageNodeTypes,
                Is.Not.Empty,
                "NodeMenuCache.Shared.AllNodeTypes should expose package-assembly nodes.");

            AssertThatKeySetsMatch(
                packageNodeTypes.Where(IsHostProjectType).Select(type => type.FullName ?? type.Name),
                Array.Empty<string>(),
                "Host project types should not be present in NodeMenuCache.Shared.AllNodeTypes.");

            var byCategory =
                new Dictionary<ReferenceCategory, Dictionary<string, string>>();

            foreach (ReferenceCategory category in Enum.GetValues(typeof(ReferenceCategory)).Cast<ReferenceCategory>())
            {
                byCategory[category] = new Dictionary<string, string>(StringComparer.Ordinal);
            }

            foreach (Type type in packageNodeTypes)
            {
                ReferenceCategory category = GetCategory(type);
                string slug = ToKebabCase(type.Name);
                string categoryFolder = CategoryFolders[category];
                string relativePath = Path.Combine(categoryFolder, slug, "index.md").Replace('\\', '/');
                bool added = byCategory[category].TryAdd(relativePath, type.Name);

                Assert.That(
                    added,
                    Is.True,
                    $"Slug collision or duplicated runtime node path: '{relativePath}' from '{type.FullName}'.");
            }

            return byCategory;
        }

        /// <summary>
        /// Maps runtime node classes into one category.
        /// </summary>
        private static ReferenceCategory GetCategory(Type type)
        {
            // Mutually exclusive order is intentionally fixed by documentation policy.
            if (typeof(Service).IsAssignableFrom(type))
            {
                return ReferenceCategory.Service;
            }

            if (typeof(Decorator).IsAssignableFrom(type))
            {
                return ReferenceCategory.Decorator;
            }

            if (typeof(Flow).IsAssignableFrom(type))
            {
                return ReferenceCategory.Flow;
            }

            if (typeof(DetermineBase).IsAssignableFrom(type))
            {
                return ReferenceCategory.Determine;
            }

            if (typeof(Arithmetic).IsAssignableFrom(type))
            {
                return ReferenceCategory.Arithmetic;
            }

            if (typeof(Aethiumian.AI.Nodes.Action).IsAssignableFrom(type))
            {
                return ReferenceCategory.Action;
            }

            if (typeof(Call).IsAssignableFrom(type))
            {
                return ReferenceCategory.Call;
            }

            Assert.Fail(
                $"Type '{type.FullName}' is not assignable to service/decorator/flow/determine/arithmetic/action/call categories.");
            return default;
        }

        /// <summary>
        /// Reads the strict H1 title and validates required section headings for each detail page.
        /// </summary>
        private static string ReadNodeTypeTitle(string indexPath, ReferenceCategory category, string slug)
        {
            string[] lines = File.ReadAllLines(indexPath);
            foreach (string rawLine in lines)
            {
                Match headingMatch = HeadingRegex.Match(rawLine);
                if (!headingMatch.Success)
                {
                    continue;
                }

                if (headingMatch.Groups["level"].Value.Length != 1)
                {
                    continue;
                }

                string title = headingMatch.Groups["title"].Value.Trim();
                Match quotedMatch = QuotedTitleRegex.Match(title);

                Assert.That(
                    quotedMatch.Success,
                    Is.True,
                    $"Page '{indexPath}' in category '{category}' has invalid H1 '{title}'. Expected '# `TypeName`'.");

                string nodeName = quotedMatch.Groups["name"].Value.Trim();
                Assert.That(
                    nodeName,
                    Is.Not.Empty,
                    $"Empty node type name in H1 of page '{indexPath}' in category '{category}'.");

                string expectedSlug = ToKebabCase(nodeName);
                Assert.That(
                    expectedSlug,
                    Is.EqualTo(slug),
                    $"H1 type name '{nodeName}' does not match folder slug '{slug}' (expected '{expectedSlug}') in '{indexPath}'.");

                return nodeName;
            }

            Assert.Fail($"Page '{indexPath}' in category '{category}' does not have any H1 heading.");
            return string.Empty;
        }

        /// <summary>
        /// Reads a page title for path comparisons without making page-structure assertions.
        /// </summary>
        private static string ReadNodeTypeTitleForMapping(string indexPath)
        {
            foreach (string rawLine in File.ReadAllLines(indexPath))
            {
                Match headingMatch = HeadingRegex.Match(rawLine);
                if (!headingMatch.Success || headingMatch.Groups["level"].Value.Length != 1)
                {
                    continue;
                }

                Match quotedMatch = QuotedTitleRegex.Match(headingMatch.Groups["title"].Value.Trim());
                return quotedMatch.Success ? quotedMatch.Groups["name"].Value.Trim() : string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Ensures all required section headings exist for detail pages.
        /// </summary>
        private static void AssertRequiredSections(ReferencePageCase page)
        {
            string[] lines = File.ReadAllLines(page.IndexPath);
            var headings = lines
                .Select(line => HeadingRegex.Match(line))
                .Where(match => match.Success)
                .Where(match => match.Groups["level"].Value.Length >= 2)
                .Select(match => match.Groups["title"].Value.Trim())
                .ToArray();

            if (page.IsEnglish)
            {
                foreach (string required in RequiredEnglishHeadings)
                {
                    Assert.That(
                        headings.Any(heading => heading.Equals(required, StringComparison.OrdinalIgnoreCase)),
                        Is.True,
                        $"English page '{page.RelativePath}' missing required heading '{required}'.");
                }
            }
            else
            {
                for (int i = 0; i < RequiredChineseSectionPatterns.Length; i++)
                {
                    Regex pattern = RequiredChineseSectionPatterns[i];
                    Assert.That(
                        headings.Any(heading => pattern.IsMatch(heading)),
                        Is.True,
                        $"Chinese page '{page.RelativePath}' missing required section {i + 1}.");
                }
            }
        }

        /// <summary>
        /// Normalizes PascalCase type names into kebab-case with continuous digits as one token.
        /// </summary>
        private static string ToKebabCase(string typeName)
        {
            Assert.That(typeName, Is.Not.Null.And.Not.Empty, "Type name for slug conversion must not be empty.");
            string[] segments = SlugSplitRegex
                .Split(typeName)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToArray();

            return string.Join("-", segments.Select(part => part.ToLowerInvariant()));
        }

        /// <summary>
        /// Compares two sets by content and reports missing/extra values in assertion output.
        /// </summary>
        private static void AssertThatKeySetsMatch(IEnumerable<string> expected, IEnumerable<string> actual, string context)
        {
            var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            var actualSet = new HashSet<string>(actual, StringComparer.Ordinal);

            string[] missing = expectedSet.Except(actualSet).OrderBy(value => value).ToArray();
            string[] extra = actualSet.Except(expectedSet).OrderBy(value => value).ToArray();

            Assert.That(
                missing,
                Is.Empty,
                $"{context} Missing: {string.Join(", ", missing)}");
            Assert.That(
                extra,
                Is.Empty,
                $"{context} Extra: {string.Join(", ", extra)}");
        }

        /// <summary>
        /// Resolves the package root from runtime assembly metadata.
        /// </summary>
        private static string ResolvePackageRoot()
        {
            PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(TreeNode).Assembly);
            Assert.That(packageInfo, Is.Not.Null, "Failed to resolve package info for runtime TreeNode assembly.");
            Assert.That(
                packageInfo.resolvedPath,
                Is.Not.Null.And.Not.Empty,
                "PackageInfo.resolvedPath should be a valid path.");
            return packageInfo.resolvedPath;
        }

        /// <summary>
        /// Rejects host project types accidentally entering package runtime node cache.
        /// </summary>
        private static bool IsHostProjectType(Type type)
        {
            string fullName = type.FullName ?? string.Empty;
            return fullName.StartsWith("Amlos.", StringComparison.Ordinal)
                   || fullName.IndexOf(".Amlos.", StringComparison.Ordinal) >= 0
                   || fullName.IndexOf("Amlos:", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Identifies one localized reference detail page for a named structure test case.
        /// </summary>
        public sealed class ReferencePageCase
        {
            public ReferencePageCase(
                string language,
                bool isEnglish,
                ReferenceCategory category,
                string slug,
                string relativePath,
                string indexPath)
            {
                Language = language;
                IsEnglish = isEnglish;
                Category = category;
                Slug = slug;
                RelativePath = relativePath;
                IndexPath = indexPath;
            }

            public string Language { get; }

            public bool IsEnglish { get; }

            public ReferenceCategory Category { get; }

            public string Slug { get; }

            public string RelativePath { get; }

            public string IndexPath { get; }
        }

        public enum ReferenceCategory
        {
            Action,
            Arithmetic,
            Call,
            Determine,
            Decorator,
            Flow,
            Service,
        }
    }
}
