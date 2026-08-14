using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// Focused data-level coverage for NodeReference selection transactions.
    /// </summary>
    public sealed class NodeReferenceSelectionSessionTests : GraphEditorTestFixture
    {
        [SetUp]
        public void SetUpUndo()
        {
            Undo.ClearAll();
            AIEditorWindow.SharedClipboard.Clear();
        }

        [Test]
        public void ExistingSelectionReparentsAndUndoRestoresTheOldReference()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode oldChild = Node<TestNode>("Old");
            TestNode replacement = Node<TestNode>("Replacement");
            owner.child = new NodeReference(oldChild.uuid);
            oldChild.parent = new NodeReference(owner.uuid);
            BehaviourTreeData tree = Tree(owner, oldChild, replacement);
            tree.RegenerateTable();

            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(replacement.uuid)), Is.True);
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            TestNode updatedOwner = (TestNode)tree.GetNode(owner.uuid);
            TestNode updatedOldChild = (TestNode)tree.GetNode(oldChild.uuid);
            TestNode updatedReplacement = (TestNode)tree.GetNode(replacement.uuid);
            Assert.That(updatedOwner.child.UUID, Is.EqualTo(replacement.uuid));
            Assert.That(updatedOldChild.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(updatedReplacement.parent.UUID, Is.EqualTo(owner.uuid));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            updatedOwner = (TestNode)tree.GetNode(owner.uuid);
            updatedOldChild = (TestNode)tree.GetNode(oldChild.uuid);
            updatedReplacement = (TestNode)tree.GetNode(replacement.uuid);
            Assert.That(updatedOwner.child.UUID, Is.EqualTo(oldChild.uuid));
            Assert.That(updatedOldChild.parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(updatedReplacement.parent.UUID, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void CreateSelectionAddsAndAssignsOneNodeInOneTransaction()
        {
            TestNode owner = Node<TestNode>("Owner");
            BehaviourTreeData tree = Tree(owner);
            tree.RegenerateTable();
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Create(typeof(Sequence))), Is.True);
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.EditorNodes, Has.Count.EqualTo(2));
            Assert.That(owner.child.UUID, Is.Not.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(owner.child.UUID).parent.UUID, Is.EqualTo(owner.uuid));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.EditorNodes, Has.Count.EqualTo(1));
            Assert.That(((TestNode)tree.GetNode(owner.uuid)).child.UUID, Is.EqualTo(UUID.Empty));
        }

        /// <summary>
        /// Verifies that an Inspector apply after the selection callback cannot restore stale reference data.
        /// </summary>
        [Test]
        public void ExistingSelectionSynchronizesCachedSerializedObject()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode candidate = Node<TestNode>("Candidate");
            BehaviourTreeData tree = Tree(owner, candidate);
            tree.RegenerateTable();
            tree.SerializedObject.Update();
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(candidate.uuid)), Is.True);
            tree.SerializedObject.ApplyModifiedProperties();
            tree.RegenerateTable();

            Assert.That(((TestNode)tree.GetNode(owner.uuid)).child.UUID, Is.EqualTo(candidate.uuid));
            Assert.That(tree.GetNode(candidate.uuid).parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }

        [Test]
        public void PasteSelectionClonesClipboardAndAssignsItsRoot()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode source = Node<TestNode>("Source");
            BehaviourTreeData tree = Tree(owner, source);
            tree.RegenerateTable();
            AIEditorWindow.SharedClipboard.Write(source, tree);
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Paste()), Is.True);
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            TreeNode pasted = tree.GetNode(owner.child.UUID);
            Assert.That(pasted, Is.Not.Null);
            Assert.That(pasted.uuid, Is.Not.EqualTo(source.uuid));
            Assert.That(pasted.parent.UUID, Is.EqualTo(owner.uuid));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.EditorNodes, Has.Count.EqualTo(2));
            Assert.That(((TestNode)tree.GetNode(owner.uuid)).child.UUID, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void RawSelectionChangesOnlyUuidAndClearIsUndoable()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode current = Node<TestNode>("Current");
            TestNode replacement = Node<TestNode>("Replacement");
            owner.raw = new RawNodeReference { UUID = current.uuid };
            current.parent = new NodeReference(owner.uuid);
            BehaviourTreeData tree = Tree(owner, current, replacement);
            tree.RegenerateTable();
            NodeReferenceSelectionSession rawSession = CreateSession(tree, owner, nameof(TestNode.raw), rawReference: true);

            Assert.That(rawSession.ApplyChoice(NodeSelectionChoice.Existing(replacement.uuid)), Is.True);
            Assert.That(owner.raw.UUID, Is.EqualTo(replacement.uuid));
            Assert.That(current.parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(replacement.parent.UUID, Is.EqualTo(UUID.Empty));

            NodeReferenceSelectionSession clearSession = CreateSession(tree, owner, nameof(TestNode.raw), rawReference: true);
            Assert.That(clearSession.Clear(), Is.True);
            Assert.That(owner.raw.UUID, Is.EqualTo(UUID.Empty));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(owner.raw.UUID, Is.EqualTo(replacement.uuid));
        }

        /// <summary>Verifies that a structural reference cannot point back to its owner.</summary>
        [Test]
        public void ExistingSelectionRejectsOwnerWithoutDirtyingTheTree()
        {
            TestNode owner = Node<TestNode>("Owner");
            BehaviourTreeData tree = Tree(owner);
            tree.RegenerateTable();
            EditorUtility.ClearDirty(tree);
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));
            int undoGroup = Undo.GetCurrentGroup();

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(owner.uuid)), Is.False);
            Assert.That(owner.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
        }

        /// <summary>Verifies that assigning an ancestor below its descendant is rejected.</summary>
        [Test]
        public void ExistingSelectionRejectsStructuralCycle()
        {
            TestNode ancestor = Node<TestNode>("Ancestor");
            TestNode owner = Node<TestNode>("Owner");
            ancestor.child = new NodeReference(owner.uuid);
            owner.parent = new NodeReference(ancestor.uuid);
            BehaviourTreeData tree = Tree(ancestor, owner);
            tree.RegenerateTable();
            EditorUtility.ClearDirty(tree);
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(ancestor.uuid)), Is.False);
            Assert.That(owner.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(owner.parent.UUID, Is.EqualTo(ancestor.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies that a candidate with multiple structural owners cannot be moved by selection.</summary>
        [Test]
        public void ExistingSelectionRejectsMultipleStructuralIncomingReferences()
        {
            TestNode firstOwner = Node<TestNode>("First Owner");
            TestNode secondOwner = Node<TestNode>("Second Owner");
            TestNode destination = Node<TestNode>("Destination");
            TestNode candidate = Node<TestNode>("Candidate");
            firstOwner.child = new NodeReference(candidate.uuid);
            secondOwner.child = new NodeReference(candidate.uuid);
            candidate.parent = new NodeReference(firstOwner.uuid);
            BehaviourTreeData tree = Tree(firstOwner, secondOwner, destination, candidate);
            tree.RegenerateTable();
            EditorUtility.ClearDirty(tree);
            NodeReferenceSelectionSession session = CreateSession(tree, destination, nameof(TestNode.child));
            int undoGroup = Undo.GetCurrentGroup();

            Assert.That(session.CanSelectExistingNode(candidate), Is.False);
            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(candidate.uuid)), Is.False);
            Assert.That(destination.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(firstOwner.child.UUID, Is.EqualTo(candidate.uuid));
            Assert.That(secondOwner.child.UUID, Is.EqualTo(candidate.uuid));
            Assert.That(candidate.parent.UUID, Is.EqualTo(firstOwner.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
            Assert.That(tree.GetStructureValidationErrors(), Is.Not.Empty);
        }

        /// <summary>Verifies that selecting a node already owned by the destination cannot create a duplicate edge.</summary>
        [Test]
        public void ExistingSelectionRejectsDuplicateReferenceFromDestinationOwner()
        {
            TestHost owner = Node<TestHost>("Owner");
            TestNode candidate = Node<TestNode>("Candidate");
            owner.children = new[]
            {
                new NodeReference(candidate.uuid),
                NodeReference.Empty,
            };
            candidate.parent = new NodeReference(owner.uuid);
            BehaviourTreeData tree = Tree(owner, candidate);
            tree.RegenerateTable();
            EditorUtility.ClearDirty(tree);
            SerializedProperty list = tree.GetNodeProperty(owner).FindPropertyRelative(nameof(TestHost.children));
            NodeReferenceSelectionSession session = new(
                tree,
                owner.uuid,
                list.GetArrayElementAtIndex(1).propertyPath,
                false,
                AIEditorWindow.SharedClipboard,
                null);
            int undoGroup = Undo.GetCurrentGroup();

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(candidate.uuid)), Is.False);
            Assert.That(owner.children.Select(reference => reference.UUID), Is.EqualTo(new[] { candidate.uuid, UUID.Empty }));
            Assert.That(candidate.parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
            Assert.That(tree.GetStructureValidationErrors(), Is.Empty);
        }

        /// <summary>Verifies that a mismatched parent field cannot be used to move a structurally referenced node.</summary>
        [Test]
        public void ExistingSelectionRejectsInconsistentStructuralParentMetadata()
        {
            TestNode source = Node<TestNode>("Source");
            TestNode destination = Node<TestNode>("Destination");
            TestNode declaredParent = Node<TestNode>("Declared Parent");
            TestNode candidate = Node<TestNode>("Candidate");
            source.child = new NodeReference(candidate.uuid);
            candidate.parent = new NodeReference(declaredParent.uuid);
            BehaviourTreeData tree = Tree(source, destination, declaredParent, candidate);
            tree.RegenerateTable();
            EditorUtility.ClearDirty(tree);
            NodeReferenceSelectionSession session = CreateSession(tree, destination, nameof(TestNode.child));
            int undoGroup = Undo.GetCurrentGroup();

            Assert.That(session.CanSelectExistingNode(candidate), Is.False);
            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(candidate.uuid)), Is.False);
            Assert.That(destination.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(source.child.UUID, Is.EqualTo(candidate.uuid));
            Assert.That(candidate.parent.UUID, Is.EqualTo(declaredParent.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
            Assert.That(tree.GetStructureValidationErrors(), Is.Not.Empty);
        }

        /// <summary>Verifies that raw references intentionally bypass structural cycle checks.</summary>
        [Test]
        public void RawSelectionAllowsOwnerWithoutChangingParent()
        {
            TestNode owner = Node<TestNode>("Owner");
            BehaviourTreeData tree = Tree(owner);
            tree.RegenerateTable();
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.raw), rawReference: true);

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(owner.uuid)), Is.True);
            Assert.That(owner.raw.UUID, Is.EqualTo(owner.uuid));
            Assert.That(owner.parent.UUID, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void InvalidPropertyDoesNotDirtyOrAddNodes()
        {
            TestNode owner = Node<TestNode>("Owner");
            BehaviourTreeData tree = Tree(owner);
            tree.RegenerateTable();
            EditorUtility.ClearDirty(tree);
            NodeReferenceSelectionSession session = new(
                tree,
                owner.uuid,
                "nodes.Array.data[999].child",
                false,
                AIEditorWindow.SharedClipboard,
                null);

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Create(typeof(Sequence))), Is.False);
            Assert.That(tree.EditorNodes, Has.Count.EqualTo(1));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies that a reordered node list still resolves the original owner field.</summary>
        [Test]
        public void ReorderedNodeListStillResolvesTheOriginalOwnerField()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode other = Node<TestNode>("Other");
            TestNode replacement = Node<TestNode>("Replacement");
            BehaviourTreeData tree = Tree(owner, other, replacement);
            tree.RegenerateTable();
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));

            TreeNode movedOwner = tree.nodes[0];
            tree.nodes.RemoveAt(0);
            tree.nodes.Add(movedOwner);
            tree.RegenerateTable();

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(replacement.uuid)), Is.True);
            Assert.That(((TestNode)tree.GetNode(owner.uuid)).child.UUID, Is.EqualTo(replacement.uuid));
        }

        /// <summary>Verifies that a weighted nested reference participates in the same transaction.</summary>
        [Test]
        public void WeightedEntrySelectionReparentsAndUndoRestoresTheReference()
        {
            Probability owner = Node<Probability>("Probability");
            TestNode oldChild = Node<TestNode>("Old");
            TestNode replacement = Node<TestNode>("Replacement");
            owner.events = new[]
            {
                new Probability.EventWeight
                {
                    reference = new NodeReference(oldChild.uuid),
                    weight = 1,
                },
            };
            oldChild.parent = new NodeReference(owner.uuid);
            BehaviourTreeData tree = Tree(owner, oldChild, replacement);
            tree.RegenerateTable();
            SerializedProperty referenceProperty = tree.GetNodeProperty(owner)
                .FindPropertyRelative(nameof(Probability.events))
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative(nameof(Probability.EventWeight.reference));
            NodeReferenceSelectionSession session = new(
                tree,
                owner.uuid,
                referenceProperty.propertyPath,
                false,
                AIEditorWindow.SharedClipboard,
                null);

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(replacement.uuid)), Is.True);
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Probability updatedOwner = (Probability)tree.GetNode(owner.uuid);
            Assert.That(updatedOwner.events, Has.Length.EqualTo(1));
            Assert.That(updatedOwner.events[0].reference.UUID, Is.EqualTo(replacement.uuid));
            Assert.That(updatedOwner.events[0].weight, Is.EqualTo(1));
            Assert.That(tree.GetNode(oldChild.uuid).parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(replacement.uuid).parent.UUID, Is.EqualTo(owner.uuid));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            updatedOwner = (Probability)tree.GetNode(owner.uuid);
            Assert.That(updatedOwner.events, Has.Length.EqualTo(1));
            Assert.That(updatedOwner.events[0].reference.UUID, Is.EqualTo(oldChild.uuid));
            Assert.That(updatedOwner.events[0].weight, Is.EqualTo(1));
            Assert.That(tree.GetNode(oldChild.uuid).parent.UUID, Is.EqualTo(owner.uuid));
            Assert.That(tree.GetNode(replacement.uuid).parent.UUID, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies that deleting the owner cancels the delayed selection safely.</summary>
        [Test]
        public void DeletedOwnerCancelsWithoutDirtyingOrCreatingNodes()
        {
            TestNode owner = Node<TestNode>("Owner");
            BehaviourTreeData tree = Tree(owner);
            tree.RegenerateTable();
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));

            tree.nodes.Remove(owner);
            tree.RegenerateTable();
            EditorUtility.ClearDirty(tree);

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Create(typeof(Sequence))), Is.False);
            Assert.That(tree.EditorNodes, Is.Empty);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies that a field replaced with null cancels without creating an Undo entry.</summary>
        [Test]
        public void MissingReferenceFieldCancelsWithoutUndo()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode replacement = Node<TestNode>("Replacement");
            BehaviourTreeData tree = Tree(owner, replacement);
            tree.RegenerateTable();
            NodeReferenceSelectionSession session = CreateSession(tree, owner, nameof(TestNode.child));
            owner.child = null;
            EditorUtility.ClearDirty(tree);

            Assert.That(session.ApplyChoice(NodeSelectionChoice.Existing(replacement.uuid)), Is.False);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        private static NodeReferenceSelectionSession CreateSession(BehaviourTreeData tree, TreeNode owner, string field, bool rawReference = false)
        {
            SerializedProperty property = tree.GetNodeProperty(owner).FindPropertyRelative(field);
            return new NodeReferenceSelectionSession(
                tree,
                owner.uuid,
                property.propertyPath,
                rawReference,
                AIEditorWindow.SharedClipboard,
                null);
        }
    }
}
