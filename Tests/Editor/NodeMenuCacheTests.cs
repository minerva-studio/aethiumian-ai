using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System;
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
