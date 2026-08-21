using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Editor.Tests.Accessors
{
    /// <summary>Verifies the runtime-facing node descriptor contract.</summary>
    public sealed class NodeDescriptorRuntimeTests
    {
        [Test]
        public void Provider_ResolvesConcreteRuntimeNode()
        {
            NodeDescriptor descriptor = NodeDescriptorProvider.Get(typeof(Sequence));
            Assert.That(descriptor.NodeType, Is.EqualTo(typeof(Sequence)));
        }

        [Test]
        public void Provider_CreatesConcreteRuntimeNodeThroughFactory()
        {
            TreeNode node = NodeFactory.Create(typeof(Sequence));

            Assert.That(node, Is.TypeOf<Sequence>());
            Assert.That(node.uuid, Is.Not.EqualTo(UUID.Empty));
            Assert.That(node.parent, Is.Not.Null);
        }

        [Test]
        public void Provider_ReflectionFallbackCachesAndDuplicatesPrivateEditorTestNode()
        {
            Assert.That(NodeDescriptorProvider.TryGet(typeof(PrivateReflectionNode), out NodeDescriptor first), Is.True);
            Assert.That(NodeDescriptorProvider.TryGet(typeof(PrivateReflectionNode), out NodeDescriptor second), Is.True);
            Assert.That(second, Is.SameAs(first));

            PrivateReflectionNode source = new()
            {
                child = new NodeReference(UUID.NewUUID()),
                children = new List<NodeReference> { new(UUID.NewUUID()) },
            };
            PrivateReflectionNode duplicate = (PrivateReflectionNode)NodeFactory.Duplicate(source);

            Assert.That(duplicate, Is.Not.SameAs(source));
            Assert.That(duplicate.child, Is.Not.SameAs(source.child));
            Assert.That(duplicate.child.UUID, Is.EqualTo(source.child.UUID));
            Assert.That(duplicate.children, Is.Not.SameAs(source.children));
            Assert.That(duplicate.children[0], Is.Not.SameAs(source.children[0]));
            Assert.That(duplicate.children[0].UUID, Is.EqualTo(source.children[0].UUID));

            Assert.That(
                NodeReferenceStructureProvider.TryInsertReference(duplicate, nameof(PrivateReflectionNode.children), 1, null),
                Is.True);
            Assert.That(duplicate.children, Has.Count.EqualTo(2));
        }

        [Test]
        public void Provider_ReflectionFallbackResolvesPrivateExecutionTestNodes()
        {
            string[] typeNames =
            {
                "Aethiumian.AI.Editor.Tests.Execution.BehaviourTreeServiceStackTests+YieldingNode",
                "Aethiumian.AI.Editor.Tests.Execution.BehaviourTreeServiceStackTests+InlineReturnProbe",
                "Aethiumian.AI.Editor.Tests.Execution.DecoratorTests+ResultNode",
                "Aethiumian.AI.Editor.Tests.Graph.GraphCanvasPerformanceBaselineTests+SyntheticNode",
            };

            foreach (string typeName in typeNames)
            {
                Type nodeType = typeof(NodeDescriptorRuntimeTests).Assembly.GetType(typeName);
                Assert.That(nodeType, Is.Not.Null, typeName);
                Assert.That(NodeDescriptorProvider.TryGet(nodeType, out _), Is.True, typeName);
            }
        }

        [Test]
        public void Copy_PreservesDestinationIdentityAndReferences()
        {
            Sequence source = new()
            {
                name = "source",
                uuid = UUID.NewUUID(),
                parent = new NodeReference(UUID.NewUUID()),
                events = new[] { new NodeReference(UUID.NewUUID()) },
            };
            Sequence destination = new()
            {
                name = "destination",
                uuid = UUID.NewUUID(),
                parent = new NodeReference(UUID.NewUUID()),
                events = new[] { new NodeReference(UUID.NewUUID()), new NodeReference(UUID.NewUUID()) },
            };
            UUID destinationUUID = destination.uuid;
            UUID destinationParentUUID = destination.parent.UUID;
            UUID firstDestinationChildUUID = destination.events[0].UUID;
            UUID secondDestinationChildUUID = destination.events[1].UUID;

            NodeFactory.Copy(destination, source);

            Assert.That(destination.uuid, Is.EqualTo(destinationUUID));
            Assert.That(destination.name, Is.EqualTo("destination"));
            Assert.That(destination.parent.UUID, Is.EqualTo(destinationParentUUID));
            Assert.That(destination.events, Has.Length.EqualTo(2));
            Assert.That(destination.events[0].UUID, Is.EqualTo(firstDestinationChildUUID));
            Assert.That(destination.events[1].UUID, Is.EqualTo(secondDestinationChildUUID));
        }

        [Test]
        public void VisitMembers_ReportsScalarAndCollectionReferences()
        {
            Sequence node = new()
            {
                parent = new NodeReference(UUID.NewUUID()),
                events = new[] { new NodeReference(UUID.NewUUID()), new NodeReference(UUID.NewUUID()) },
            };
            ReferenceVisitor visitor = new();

            NodeDescriptorProvider.Get(typeof(Sequence)).VisitMembers(node, visitor);

            Assert.That(visitor.Paths, Does.Contain(nameof(TreeNode.parent)));
            Assert.That(visitor.Paths, Does.Contain("events[0]"));
            Assert.That(visitor.Paths, Does.Contain("events[1]"));
        }

        [Test]
        public void BuiltInCollectionStructure_ProvidesIndexedMutation()
        {
            Sequence node = new() { uuid = UUID.NewUUID(), events = Array.Empty<NodeReference>() };
            Assert.That(NodeReferenceStructureProvider.TryInsertReference(node, nameof(Sequence.events), 0, null), Is.True);
            Assert.That(NodeReferenceStructureProvider.TrySetReference(node, "events[0]", node), Is.True);
            Assert.That(node.events[0].UUID, Is.EqualTo(node.uuid));
            Assert.That(NodeReferenceStructureProvider.TryRemoveReference(node, nameof(Sequence.events), 0), Is.True);
            Assert.That(node.events, Is.Empty);
        }

        [Test]
        public void ReferenceRemapVisitor_RemapNormalAndPreserveExternalRawReference()
        {
            UUID internalUUID = UUID.NewUUID();
            UUID remappedUUID = UUID.NewUUID();
            UUID externalRawUUID = UUID.NewUUID();
            NodeReference normal = new(internalUUID);
            RawNodeReference raw = new() { UUID = externalRawUUID };
            Dictionary<UUID, UUID> translation = new() { [internalUUID] = remappedUUID };
            NodeReferenceRemapVisitor visitor = new(translation);

            visitor.VisitNodeReference("child", normal);
            visitor.VisitNodeReference("raw", raw);

            Assert.That(normal.UUID, Is.EqualTo(remappedUUID));
            Assert.That(normal.Node, Is.Null);
            Assert.That(raw.UUID, Is.EqualTo(externalRawUUID));
            Assert.That(raw.Node, Is.Null);
        }

        [Test]
        public void ReferenceRemapVisitor_RejectsExternalNormalReference()
        {
            NodeReference reference = new(UUID.NewUUID());
            NodeReferenceRemapVisitor visitor = new(new Dictionary<UUID, UUID>());

            Assert.Throws<InvalidOperationException>(() => visitor.VisitNodeReference("child", reference));
        }

        private sealed class ReferenceVisitor : NodeMemberVisitor
        {
            public List<string> Paths { get; } = new();

            protected override void OnNodeReference(string path, INodeReference reference)
            {
                Paths.Add(path);
            }

            protected override void OnVariableBinding(string path, Aethiumian.AI.Variables.IVariableBinding binding)
            {
            }
        }

        private sealed class PrivateReflectionNode : TreeNode
        {
            public NodeReference child;
            public List<NodeReference> children = new();

            public override void Initialize()
            {
            }

            public override State Execute()
            {
                return State.Success;
            }
        }
    }
}
