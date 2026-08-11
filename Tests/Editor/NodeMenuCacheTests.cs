using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aethiumian.AI.Tests
{
    public sealed class NodeMenuCacheTests
    {
        [Test]
        public void IsCreatableNodeType_PublicRuntimeNode_ReturnsTrue()
        {
            Assert.True(NodeMenuCache.IsCreatableNodeType(typeof(Sequence)));
        }

        [Test]
        public void IsCreatableNodeType_AbstractNode_ReturnsFalse()
        {
            Assert.False(NodeMenuCache.IsCreatableNodeType(typeof(Flow)));
        }

        [Test]
        public void IsCreatableNodeType_DoNotReleaseNode_ReturnsFalse()
        {
            Assert.False(NodeMenuCache.IsCreatableNodeType(typeof(ComponentCall)));
        }

        [Test]
        public void IsCreatableNodeType_PrivateNestedNode_ReturnsFalse()
        {
            // Fetch the nested type through reflection so the test exercises the same Type path as TypeCache.
            Type privateNodeType = typeof(NodeMenuCacheTests)
                .GetNestedType(nameof(PrivateProbeNode), BindingFlags.NonPublic);

            Assert.False(NodeMenuCache.IsCreatableNodeType(privateNodeType));
        }

        [Test]
        public void MenuPathRoot_ContainsUncategorizedNodesAndCategorizedFolders()
        {
            NodeMenuCache cache = NodeMenuCache.Shared;

            Assert.That(cache.MenuPathRoot.Types, Does.Contain(typeof(Sequence)));
            Assert.That(cache.MenuPathRoot.Children["External"].Types, Does.Contain(typeof(FunctionCall)));
        }

        [Test]
        public void BuildCreationMenu_RestoresLegacyFoldersAndAllowsDuplicateEntries()
        {
            NodeCreationMenuFolder root = NodeMenuCache.Shared.BuildCreationMenu(type => !typeof(Service).IsAssignableFrom(type));

            string[] names = root.Children.Select(folder => folder.Name).ToArray();
            Assert.That(names, Does.Contain("Common"));
            Assert.That(names, Does.Contain("Logics"));
            Assert.That(names, Does.Contain("Calls"));
            Assert.That(names, Does.Contain("Actions"));
            Assert.That(names, Does.Contain("Unity"));
            Assert.That(names, Does.Contain("Menu Paths"));
            Assert.That(names, Does.Contain("Other"));
            Assert.That(FindFolder(root, "Logics", "Composites").Types, Does.Contain(typeof(Sequence)));
            Assert.That(FindFolder(root, "Calls").Types, Does.Contain(typeof(FunctionCall)));
            Assert.That(FindFolder(root, "Unity").Types, Does.Contain(typeof(FunctionCall)));
            Assert.That(FindFolder(root, "Menu Paths", "External").Types, Does.Contain(typeof(FunctionCall)));
            Assert.That(FindFolder(root, "Common").Types, Does.Contain(typeof(Sequence)));
        }

        [Test]
        public void BuildCreationMenu_ServiceContextOnlyContainsServices()
        {
            NodeCreationMenuFolder root = NodeMenuCache.Shared.BuildCreationMenu(typeof(Service).IsAssignableFrom);

            Assert.That(root.Children.Select(folder => folder.Name), Is.EqualTo(new[] { "Services" }));
            Assert.That(root.Children[0].Types, Is.Not.Empty);
        }

        private static NodeCreationMenuFolder FindFolder(NodeCreationMenuFolder root, params string[] path)
        {
            NodeCreationMenuFolder current = root;
            foreach (string segment in path)
            {
                current = current.Children.Single(folder => folder.Name == segment);
            }

            return current;
        }

        private sealed class PrivateProbeNode : TreeNode
        {
            public override State Execute()
            {
                return State.Success;
            }

            public override void Initialize()
            {
                throw new NotImplementedException();
            }

        }
    }
}
