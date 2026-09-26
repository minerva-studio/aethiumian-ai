using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System.Linq;
using UnityEditor;

namespace Aethiumian.AI.Editor.Tests.Graph
{
    /// <summary>Parent metadata coverage for Graph topology simplification.</summary>
    public sealed class BehaviourTreeSimplificationTests : GraphTopologyEditTestBase
    {
        [Test]
        public void SimplifySequence_PruneClearsRemovedBranchParentAndSupportsUndo()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Constant constant = Node<Constant>("True");
            Always retained = Node<Always>("Retained");
            constant.returnValue = true;
            sequence.events = new[] { constant.ToReference(), retained.ToReference() };
            constant.parent = sequence.ToReference();
            retained.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, constant, retained);
            Undo.ClearAll();

            Assert.That(tree.TrySimplifyNode(sequence, out _), Is.True);
            Assert.That(sequence.events, Has.Length.EqualTo(1));
            Assert.That(sequence.events[0].UUID, Is.EqualTo(retained.uuid));
            Assert.That(constant.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(retained.parent.UUID, Is.EqualTo(sequence.uuid));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);

            Undo.PerformUndo();
            Assert.That(sequence.events, Has.Length.EqualTo(2));
            Assert.That(constant.parent.UUID, Is.EqualTo(sequence.uuid));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }

        [Test]
        public void SimplifyDecision_PruneClearsRemovedBranchParentAndSupportsUndo()
        {
            Decision decision = Node<Decision>("Decision");
            Constant constant = Node<Constant>("False");
            Always retained = Node<Always>("Retained");
            constant.returnValue = false;
            decision.events = new[] { constant.ToReference(), retained.ToReference() };
            constant.parent = decision.ToReference();
            retained.parent = decision.ToReference();
            BehaviourTreeData tree = Tree(decision, constant, retained);
            Undo.ClearAll();

            Assert.That(tree.TrySimplifyNode(decision, out _), Is.True);
            Assert.That(decision.events, Has.Length.EqualTo(1));
            Assert.That(decision.events[0].UUID, Is.EqualTo(retained.uuid));
            Assert.That(constant.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(retained.parent.UUID, Is.EqualTo(decision.uuid));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);

            Undo.PerformUndo();
            Assert.That(decision.events, Has.Length.EqualTo(2));
            Assert.That(constant.parent.UUID, Is.EqualTo(decision.uuid));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }

        [Test]
        public void SimplifySequence_FlattenReparentsInnerChildrenAndSupportsUndo()
        {
            Sequence outer = Node<Sequence>("Outer");
            Sequence inner = Node<Sequence>("Inner");
            Always child = Node<Always>("Child");
            outer.events = new[] { inner.ToReference() };
            inner.events = new[] { child.ToReference() };
            inner.parent = outer.ToReference();
            child.parent = inner.ToReference();
            BehaviourTreeData tree = Tree(outer, inner, child);
            Undo.ClearAll();

            Assert.That(tree.TrySimplifyNode(outer, out _), Is.True);
            Assert.That(tree.GetNode(inner.uuid), Is.Null);
            Assert.That(outer.events, Has.Length.EqualTo(1));
            Assert.That(outer.events[0].UUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(outer.uuid));

            Undo.PerformUndo();
            tree.RegenerateTable();
            // Undo deserializes the tree, so restored nodes are new instances; compare by identity UUID.
            Sequence restoredInner = tree.GetNode(inner.uuid) as Sequence;
            Assert.That(restoredInner, Is.Not.Null);
            Assert.That(restoredInner.events.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid }));
            Assert.That(tree.GetNode(child.uuid).parent.UUID, Is.EqualTo(inner.uuid));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }
    }
}
