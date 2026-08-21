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
    }
}
