using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor.Tests.Window
{
    public sealed class NodeMenuCacheTests
    {
        [TestCase("PascalCase", "Pascal Case")]
        [TestCase("Raycast2D", "Raycast 2D")]
        [TestCase("Vector3Int", "Vector 3 Int")]
        [TestCase("HTTP2Request", "HTTP 2 Request")]
        [TestCase("", "")]
        [TestCase("X", "X")]
        public void ToTitleCase_UsesReadableWordBoundaries(string input, string expected)
        {
            Assert.That(input.ToTitleCase(), Is.EqualTo(expected));
        }

        [Test]
        public void GetDisplayName_PrefersAliasAndParsesUnaliasedTypes()
        {
            NodeMenuCache cache = NodeMenuCache.Shared;

            Assert.That(cache.GetDisplayName(typeof(CallStatic)), Is.EqualTo("Static Call"));
            Assert.That(cache.GetDisplayName(typeof(Raycast2D)), Is.EqualTo("Raycast 2D"));
        }

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
        public void IsMenuVisibleNodeType_ObsoletePlaceholder_ReturnsFalse()
        {
            Assert.True(NodeMenuCache.IsCreatableNodeType(typeof(PlaceholderNode)));
            Assert.False(NodeMenuCache.IsMenuVisibleNodeType(typeof(PlaceholderNode)));
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
        public void BuildCreationMenu_UsesCanonicalUniquePaths()
        {
            NodeCreationMenuFolder root = NodeMenuCache.Shared.BuildCreationMenu(NodeCreationMenuContext.Nodes);

            Assert.That(root.Name, Is.EqualTo("Nodes"));
            string[] names = root.Children.Select(folder => folder.Name).ToArray();
            Assert.That(names, Does.Contain("Control Flow"));
            Assert.That(names, Does.Contain("Conditions"));
            Assert.That(names, Does.Contain("Calculations"));
            Assert.That(names, Does.Contain("Calls"));
            Assert.That(names, Does.Contain("Actions"));
            Assert.That(FindFolder(root, "External").Types, Does.Contain(typeof(FunctionCall)));
            Assert.That(root.Children.SelectMany(FlattenTypes).Count(type => type == typeof(FunctionCall)), Is.EqualTo(1));
            Assert.That(FlattenTypes(root).Any(type => typeof(Service).IsAssignableFrom(type)), Is.False);
        }

        [Test]
        public void BuildCreationMenu_ServiceContextOnlyContainsServices()
        {
            NodeCreationMenuFolder root = NodeMenuCache.Shared.BuildCreationMenu(NodeCreationMenuContext.Services);

            Assert.That(root.Name, Is.EqualTo("Services"));
            Assert.That(root.Children, Is.Empty);
            Assert.That(root.Types, Is.Not.Empty);
            Assert.That(root.Types.All(type => typeof(Service).IsAssignableFrom(type)), Is.True);
            Assert.That(root.Types.Count, Is.EqualTo(NodeMenuCache.Shared.AllNodeTypes.Count(type => typeof(Service).IsAssignableFrom(type))));
        }

        [Test]
        public void BuildCreationMenu_ServicePaletteUsesServicesRoot()
        {
            GraphNodeCreationPalette palette = new(NodeCreationMenuContext.Services, _ => { }, () => { });
            Label title = palette.Q<Label>("ai-editor-graph-node-creation-title");

            Assert.That(title.text, Is.EqualTo("Services"));
            Assert.That(palette.Q<Button>("ai-editor-graph-node-creation-back").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
            Assert.That(palette.Query<VisualElement>(className: "ai-editor-graph-node-creation-row")
                .ToList().Any(row => row.Q<Label>(className: "ai-editor-graph-node-creation-row-detail").text == "Browse category"),
                Is.False);
            ListView results = palette.Q<ListView>("ai-editor-graph-node-creation-results");
            Assert.That(results.itemsSource, Is.Not.Null);
            Assert.That(results.itemsSource, Is.Not.Empty);
        }

        [Test]
        public void VisibleCreationTypes_HaveMenuDescriptions()
        {
            IEnumerable<Type> visibleTypes = NodeMenuCache.Shared.AllNodeTypes
                .Where(NodeMenuCache.IsMenuVisibleNodeType);

            Assert.That(visibleTypes, Is.Not.Empty);
            Assert.That(visibleTypes.All(type => !string.IsNullOrWhiteSpace(NodeMenuCache.Shared.GetTooltip(type))), Is.True);
        }

        private static IEnumerable<Type> FlattenTypes(NodeCreationMenuFolder folder)
        {
            return folder.Types.Concat(folder.Children.SelectMany(FlattenTypes));
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
