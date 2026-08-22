using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.NodeDrawers
{
    /// <summary>Verifies node documentation URL routing used by drawer actions.</summary>
    public sealed class NodeDocumentationTests
    {
        [Test]
        /// <summary>Checks the English detail-page route for a built-in flow node.</summary>
        public void PackageNode_UsesEnglishDetailPage()
        {
            Assert.That(
                NodeDocumentation.GetUrl(typeof(Sequence), SystemLanguage.English),
                Is.EqualTo("https://minerva-studio.github.io/aethiumian-ai/reference/flow/sequence/"));
        }

        [Test]
        /// <summary>Checks the Chinese detail-page route for a built-in flow node.</summary>
        public void PackageNode_UsesChineseDetailPage()
        {
            Assert.That(
                NodeDocumentation.GetUrl(typeof(Sequence), SystemLanguage.Chinese),
                Is.EqualTo("https://minerva-studio.github.io/aethiumian-ai/zh/reference/flow/sequence/"));
        }

        [Test]
        /// <summary>Checks localized fallback routes for a node without package documentation.</summary>
        public void ExternalNode_UsesLocalizedReferenceIndex()
        {
            Assert.That(
                NodeDocumentation.GetUrl(typeof(NodeDocumentationTests), SystemLanguage.English),
                Is.EqualTo("https://minerva-studio.github.io/aethiumian-ai/reference/"));
            Assert.That(
                NodeDocumentation.GetUrl(typeof(NodeDocumentationTests), SystemLanguage.Chinese),
                Is.EqualTo("https://minerva-studio.github.io/aethiumian-ai/zh/reference/"));
        }
    }
}
