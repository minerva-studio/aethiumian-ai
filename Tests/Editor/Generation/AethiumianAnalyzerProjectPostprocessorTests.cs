using Aethiumian.AI.Editor;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace Aethiumian.AI.Editor.Tests.Generation
{
    public sealed class AethiumianAnalyzerProjectPostprocessorTests
    {
        private string tempDirectory;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "AethiumianAnalyzerProjectPostprocessorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Test]
        public void ShouldInjectAnalyzer_ForRuntimeProjectItself()
        {
            string projectPath = Path.Combine(tempDirectory, "Aethiumian.AI.csproj");

            Assert.That(
                AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, "<Project />"),
                Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenProjectCompilesAethiumianRuntime()
        {
            string projectPath = Path.Combine(tempDirectory, "RuntimeConsumer.csproj");
            string projectContent =
                "<Project><ItemGroup>" +
                "<Compile Include=\"Packages\\Aethiumian.AI\\Runtime\\Nodes\\Flows\\Loop.cs\" />" +
                "</ItemGroup></Project>";

            Assert.That(
                AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, projectContent),
                Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenProjectReferencesRuntimeProject()
        {
            string projectPath = Path.Combine(tempDirectory, "Amlos.Gameplay.Impl.csproj");
            string projectContent =
                "<Project><ItemGroup>" +
                "<ProjectReference Include=\"Aethiumian.AI.csproj\" />" +
                "</ItemGroup></Project>";

            Assert.That(
                AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, projectContent),
                Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenOnlyGeneratorMarkerIsPresent_ReturnsFalse()
        {
            string projectPath = Path.Combine(tempDirectory, "Unrelated.csproj");
            string projectContent =
                "<Project><ItemGroup>" +
                "<Compile Include=\"GeneratorMarker.cs\" />" +
                "</ItemGroup></Project>";
            WriteSource("GeneratorMarker.cs", "[GenerateForAethiumianAI]\npublic sealed class GeneratorMarker {}\n");

            Assert.That(
                AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, projectContent),
                Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_ForUnrelatedProject_ReturnsFalse()
        {
            string projectPath = Path.Combine(tempDirectory, "Unrelated.csproj");
            string projectContent =
                "<Project><ItemGroup>" +
                "<Compile Include=\"Assets\\Scripts\\Gameplay\\Enemy.cs\" />" +
                "</ItemGroup></Project>";

            Assert.That(
                AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, projectContent),
                Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenMalformedProject_ReturnsFalse()
        {
            string projectPath = Path.Combine(tempDirectory, "Malformed.csproj");

            Assert.That(AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, "<Project><ItemGroup>"), Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenProjectXmlHasNoRelevantItems_ReturnsFalse()
        {
            string projectPath = Path.Combine(tempDirectory, "Unrelated.csproj");

            Assert.That(AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, "<Project><ItemGroup /></Project>"), Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenCompileItemIsMissing_ReturnsFalse()
        {
            string projectPath = Path.Combine(tempDirectory, "NoCompile.csproj");

            Assert.That(AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, "<Project><ItemGroup /></Project>"), Is.False);
        }

        [Test]
        public void InsertAnalyzerReferences_AddsAnalyzerAndCodeFixAssemblies()
        {
            string content = "<Project><ItemGroup></ItemGroup></Project>";

            string result = AethiumianAnalyzerProjectPostprocessor.InsertAnalyzerReferences(
                content,
                new[]
                {
                    @"C:\\Extensions\\tools\\roslyn\\Aethiumian.AI.CodeAnalysis.dll",
                    @"C:\\Extensions\\tools\\roslyn\\Aethiumian.AI.CodeFixes.dll"
                });

            Assert.That(result, Does.Contain("Aethiumian.AI.CodeAnalysis.dll"));
            Assert.That(result, Does.Contain("Aethiumian.AI.CodeFixes.dll"));
            Assert.That(result.Split(new[] { "<Analyzer Include=" }, StringSplitOptions.None), Has.Length.EqualTo(3));
        }

        [Test]
        public void FindAnalyzerPaths_FindsCurrentPublisherInstallation()
        {
            CreateExtensionInstallation("minerva-game-studio.aethiumian-ai-vscode-", "0.3.6", includeCodeFix: true);

            IReadOnlyList<string> paths = AethiumianAnalyzerProjectPostprocessor.FindAnalyzerPaths(new[] { tempDirectory });

            Assert.AreEqual(2, paths.Count);
            Assert.That(paths[0], Does.EndWith("Aethiumian.AI.CodeAnalysis.dll"));
            Assert.That(paths[1], Does.EndWith("Aethiumian.AI.CodeFixes.dll"));
        }

        [Test]
        public void FindAnalyzerPaths_FindsLegacyPublisherInstallation()
        {
            CreateExtensionInstallation("minerva-studio.aethiumian-ai-vscode-", "0.3.5", includeCodeFix: true);

            IReadOnlyList<string> paths = AethiumianAnalyzerProjectPostprocessor.FindAnalyzerPaths(new[] { tempDirectory });

            Assert.AreEqual(2, paths.Count);
        }

        [Test]
        public void FindAnalyzerPaths_IgnoresIncompleteInstallation()
        {
            CreateExtensionInstallation("minerva-game-studio.aethiumian-ai-vscode-", "0.3.6", includeCodeFix: false);

            IReadOnlyList<string> paths = AethiumianAnalyzerProjectPostprocessor.FindAnalyzerPaths(new[] { tempDirectory });

            Assert.AreEqual(0, paths.Count);
        }

        [Test]
        public void FindAnalyzerPaths_SelectsHighestInstalledVersion()
        {
            CreateExtensionInstallation("minerva-game-studio.aethiumian-ai-vscode-", "0.3.5", includeCodeFix: true);
            CreateExtensionInstallation("minerva-game-studio.aethiumian-ai-vscode-", "0.3.6", includeCodeFix: true);

            IReadOnlyList<string> paths = AethiumianAnalyzerProjectPostprocessor.FindAnalyzerPaths(new[] { tempDirectory });

            Assert.AreEqual(2, paths.Count);
            Assert.That(paths[0], Does.Contain("aethiumian-ai-vscode-0.3.6"));
            Assert.That(paths[1], Does.Contain("aethiumian-ai-vscode-0.3.6"));
        }

        private string WriteSource(string fileName, string source)
        {
            string sourcePath = Path.Combine(tempDirectory, fileName);
            File.WriteAllText(sourcePath, source);
            return sourcePath;
        }

        private string CreateExtensionInstallation(string prefix, string version, bool includeCodeFix)
        {
            string extensionDirectory = Path.Combine(tempDirectory, prefix + version);
            string roslynDirectory = Path.Combine(extensionDirectory, "tools", "roslyn");
            Directory.CreateDirectory(roslynDirectory);
            File.WriteAllText(Path.Combine(roslynDirectory, "Aethiumian.AI.CodeAnalysis.dll"), string.Empty);
            if (includeCodeFix)
            {
                File.WriteAllText(Path.Combine(roslynDirectory, "Aethiumian.AI.CodeFixes.dll"), string.Empty);
            }

            return extensionDirectory;
        }
    }
}
