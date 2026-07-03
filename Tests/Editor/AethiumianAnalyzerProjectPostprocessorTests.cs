using Aethiumian.AI.Editor;
using NUnit.Framework;
using System;
using System.IO;
using System.Security;

namespace Aethiumian.AI.Tests
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
        public void ShouldInjectAnalyzer_WhenAssemblyAttributeIsPresent()
        {
            string sourcePath = WriteSource("AssemblyOptIn.cs", "using Aethiumian.AI;\n[assembly: GenerateForAethiumianAI]\n");

            Assert.That(ShouldInject(sourcePath), Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenTypeAttributeIsPresent()
        {
            string sourcePath = WriteSource("TypeOptIn.cs", "using Aethiumian.AI;\n[GenerateForAethiumianAI]\npublic sealed class TypeOptIn {}\n");

            Assert.That(ShouldInject(sourcePath), Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenAttributeSuffixIsPresent()
        {
            string sourcePath = WriteSource("AttributeSuffixOptIn.cs", "[GenerateForAethiumianAIAttribute]\npublic struct TypeOptIn {}\n");

            Assert.That(ShouldInject(sourcePath), Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenNamespacedAttributeSuffixIsPresent()
        {
            string sourcePath = WriteSource("NamespacedAttributeSuffixOptIn.cs", "[Aethiumian.AI.GenerateForAethiumianAIAttribute]\npublic struct TypeOptIn {}\n");

            Assert.That(ShouldInject(sourcePath), Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenGlobalNamespacedAttributeIsPresent()
        {
            string sourcePath = WriteSource("GlobalNamespacedOptIn.cs", "[global::Aethiumian.AI.GenerateForAethiumianAI]\npublic struct TypeOptIn {}\n");

            Assert.That(ShouldInject(sourcePath), Is.True);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenMarkerIsMissing_ReturnsFalse()
        {
            string sourcePath = WriteSource("NoOptIn.cs", "public sealed class NoOptIn {}\n");

            Assert.That(ShouldInject(sourcePath), Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenOldMarkerNameIsPresent_ReturnsFalse()
        {
            string sourcePath = WriteSource("OldOptIn.cs", "using Aethiumian.AI;\n[assembly: GenerateAethiumianAI]\n");

            Assert.That(ShouldInject(sourcePath), Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenProjectXmlIsMalformed_ReturnsFalse()
        {
            string projectPath = Path.Combine(tempDirectory, "Malformed.csproj");

            Assert.That(AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, "<Project><ItemGroup>"), Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenCompileItemIsMissing_ReturnsFalse()
        {
            string projectPath = Path.Combine(tempDirectory, "NoCompile.csproj");

            Assert.That(AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, "<Project><ItemGroup /></Project>"), Is.False);
        }

        [Test]
        public void ShouldInjectAnalyzer_WhenSourceFileIsMissing_ReturnsFalse()
        {
            string sourcePath = Path.Combine(tempDirectory, "Missing.cs");

            Assert.That(ShouldInject(sourcePath), Is.False);
        }

        private bool ShouldInject(string sourcePath)
        {
            string projectPath = Path.Combine(tempDirectory, "Test.csproj");
            string projectContent =
                "<Project>" +
                "<ItemGroup>" +
                $"<Compile Include=\"{SecurityElement.Escape(sourcePath)}\" />" +
                "</ItemGroup>" +
                "</Project>";

            return AethiumianAnalyzerProjectPostprocessor.ShouldInjectAnalyzer(projectPath, projectContent);
        }

        private string WriteSource(string fileName, string source)
        {
            string sourcePath = Path.Combine(tempDirectory, fileName);
            File.WriteAllText(sourcePath, source);
            return sourcePath;
        }
    }
}
