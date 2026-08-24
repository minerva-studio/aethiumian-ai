using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Aethiumian.AI.Visual;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor.Tests.Graph
{
    /// <summary>Graph Editor topology tests for GraphTopologyReferenceEditTests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphTopologyReferenceEditTests : GraphTopologyEditTestBase
    {
        [Test]
        public void TopologyEdit_ConnectAndDisconnectCollectionOccurrenceReconcilesParent()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);
            bool connected = tree.TryInsertReference(Address(head.uuid, nameof(TestHost.children), 0), child.uuid, false, "Connect children");

            Assert.That(connected, Is.True);
            Assert.That(head.children.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid }));
            Assert.That(child.parent?.UUID, Is.EqualTo(head.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);

            bool disconnected = tree.TryDisconnectReference(Address(head.uuid, nameof(TestHost.children), 0), "Disconnect children");

            Assert.That(disconnected, Is.True);
            Assert.That(head.children, Is.Empty);
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }
        [Test]
        public void TopologyEdit_SequenceDisconnectsExactOccurrenceAndRebuildsGraph()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            first.parent = sequence.ToReference();
            second.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, first, second);

            Assert.That(tree.TryDisconnectReference(
                Address(sequence.uuid, nameof(Sequence.events), 0),
                "Remove Sequence child",
                second.uuid), Is.False);
            Assert.That(sequence.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, second.uuid }));

            Assert.That(tree.TryDisconnectReference(
                Address(sequence.uuid, nameof(Sequence.events), 0),
                "Remove Sequence child",
                first.uuid), Is.True);

            GraphTopology rebuilt = GraphTopologyBuilder.Build(tree);
            Assert.That(sequence.events.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid }));
            Assert.That(first.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(rebuilt.Edges.Any(edge => edge.Source.UUID == sequence.uuid
                && edge.Target.UUID == first.uuid), Is.False);
            Assert.That(rebuilt.Edges.Any(edge => edge.Source.UUID == sequence.uuid
                && edge.Target.UUID == second.uuid
                && edge.Reference.Address.Index == 0), Is.True);
        }

        /// <summary>Verifies that null, empty, and dangling collection occurrences share one graph mutation.</summary>
        [Test]
        public void TopologyEdit_TryRemoveReferenceHandlesEmptyAndDanglingCollectionOccurrences()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestHost host = Node<TestHost>("Host");
            UUID missingService = UUID.NewUUID();
            sequence.events = new NodeReference[] { null, new NodeReference(UUID.Empty) };
            host.services = new List<NodeReference> { new(missingService) };
            BehaviourTreeData tree = Tree(sequence, host);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            GraphEdgeDescriptor nullOccurrence = module.Topology.Edges.Single(edge => edge.Source.UUID == sequence.uuid
                && edge.Reference.Address.FieldName == nameof(Sequence.events)
                && edge.Reference.Address.Index == 0);
            Assert.That(nullOccurrence.Reference.IsNull, Is.True);
            Assert.That(nullOccurrence.ReferenceState, Is.EqualTo(GraphReferenceState.Empty));
            Assert.That(module.Disconnect(nullOccurrence), Is.True);
            Assert.That(sequence.events, Has.Length.EqualTo(1));

            GraphEdgeDescriptor empty = module.Topology.Edges.Single(edge => edge.Source.UUID == sequence.uuid
                && edge.Reference.Address.FieldName == nameof(Sequence.events));
            Assert.That(empty.ReferenceState, Is.EqualTo(GraphReferenceState.Empty));
            Assert.That(module.Disconnect(empty), Is.True);
            Assert.That(sequence.events, Is.Empty);

            GraphEdgeDescriptor dangling = module.Topology.Edges.Single(edge => edge.Source.UUID == host.uuid
                && edge.Reference.Address.FieldName == nameof(ServiceHostNode.services));
            Assert.That(dangling.ReferenceState, Is.EqualTo(GraphReferenceState.Missing));
            Assert.That(module.Disconnect(dangling), Is.True);
            Assert.That(host.services, Is.Empty);
        }

        /// <summary>Verifies that a dangling scalar can be cleared while an empty scalar remains non-removable.</summary>
        [Test]
        public void TopologyEdit_TryRemoveReferenceHandlesDanglingScalarAndRejectsEmptyScalar()
        {
            TestNode danglingOwner = Node<TestNode>("Dangling owner");
            UUID missing = UUID.NewUUID();
            danglingOwner.child = new NodeReference(missing);
            TestNode emptyOwner = Node<TestNode>("Empty owner");
            emptyOwner.child = new NodeReference(UUID.Empty);
            BehaviourTreeData tree = Tree(danglingOwner, emptyOwner);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            GraphEdgeDescriptor dangling = module.Topology.Edges.Single(edge => edge.Source.UUID == danglingOwner.uuid);
            GraphEdgeDescriptor empty = module.Topology.Edges.Single(edge => edge.Source.UUID == emptyOwner.uuid);
            Assert.That(module.Disconnect(dangling), Is.True);
            Assert.That(danglingOwner.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(module.Disconnect(empty), Is.False);
            Assert.That(emptyOwner.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies that removing an invalid collection occurrence participates in Undo and Redo.</summary>
        [Test]
        public void TopologyEdit_TryRemoveReferenceSupportsUndoRedoForEmptyCollection()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            sequence.events = new[] { new NodeReference(UUID.Empty) };
            BehaviourTreeData tree = Tree(sequence);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphEdgeDescriptor edge = module.Topology.Edges.Single();

            Assert.That(module.Disconnect(edge), Is.True);
            Assert.That(sequence.events, Is.Empty);
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(sequence.events, Has.Length.EqualTo(1));
            Assert.That(sequence.events[0].UUID, Is.EqualTo(UUID.Empty));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(sequence.events, Is.Empty);
        }

        /// <summary>Verifies that unified removal keeps Raw references outside structural ownership checks.</summary>
        [Test]
        public void TopologyEdit_TryRemoveReferencePreservesRawOwnershipRules()
        {
            TestNode owner = Node<TestNode>("Raw owner");
            TestNode target = Node<TestNode>("Raw target");
            owner.raw = target.ToRawReference();
            BehaviourTreeData tree = Tree(owner, target);
            GraphEdgeDescriptor edge = GraphTopologyBuilder.Build(tree, includeRawReferences: true).Edges
                .Single(candidate => candidate.Kind == GraphEdgeKind.Raw);

            Assert.That(tree.TryRemoveReference(edge.Reference, "Remove Raw reference"), Is.True);
            Assert.That(owner.raw?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(target.uuid), Is.SameAs(target));
        }

        /// <summary>Verifies that the authored occurrence snapshot rejects a changed value or invalid index.</summary>
        [Test]
        public void TopologyEdit_TryRemoveReferenceRejectsStaleValueAndIndex()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(sequence, first, second);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            AuthoredReferenceSnapshot staleValue = topology.Edges.Single(edge => edge.Reference.Address.Index == 0).Reference;
            AuthoredReferenceSnapshot staleIndex = topology.Edges.Single(edge => edge.Reference.Address.Index == 1).Reference;

            sequence.events[0] = second.ToReference();
            sequence.events = new[] { sequence.events[0] };

            Assert.That(tree.TryRemoveReference(staleValue, "Remove stale value"), Is.False);
            Assert.That(tree.TryRemoveReference(staleIndex, "Remove stale index"), Is.False);
            Assert.That(sequence.events, Has.Length.EqualTo(1));
            Assert.That(sequence.events[0].UUID, Is.EqualTo(second.uuid));
        }

        /// <summary>Verifies that stale empty and dangling snapshots cannot remove a replaced occurrence.</summary>
        [Test]
        public void TopologyEdit_TryRemoveReferenceRejectsStaleEmptyAndDanglingValue()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            UUID firstMissing = UUID.NewUUID();
            UUID secondMissing = UUID.NewUUID();
            sequence.events = new[]
            {
                NodeReference.Empty,
                new NodeReference(firstMissing),
            };
            BehaviourTreeData tree = Tree(sequence);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            AuthoredReferenceSnapshot staleEmpty = topology.Edges.Single(edge => edge.Reference.Address.Index == 0).Reference;
            AuthoredReferenceSnapshot staleDangling = topology.Edges.Single(edge => edge.Reference.Address.Index == 1).Reference;

            sequence.events[0] = new NodeReference(secondMissing);
            sequence.events[1] = new NodeReference(secondMissing);

            Assert.That(tree.TryRemoveReference(staleEmpty, "Remove stale empty reference"), Is.False);
            Assert.That(tree.TryRemoveReference(staleDangling, "Remove stale dangling reference"), Is.False);
            Assert.That(sequence.events, Has.Length.EqualTo(2));
            Assert.That(sequence.events.All(reference => reference.UUID == secondMissing), Is.True);
        }

        /// <summary>Verifies that every Condition scalar authored edge disconnects without deleting its target.</summary>
        [TestCase(nameof(Condition.condition))]
        [TestCase(nameof(Condition.trueNode))]
        [TestCase(nameof(Condition.falseNode))]
        public void TopologyEdit_DisconnectsConditionScalarReference(string fieldName)
        {
            Condition owner = Node<Condition>("Condition");
            TestNode target = Node<TestNode>("Target");
            SetScalarReference(owner, fieldName, target);
            target.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphEdgeDescriptor edge = module.Topology.Edges.Single(candidate => candidate.Source.UUID == owner.uuid
                && candidate.Reference.Address.FieldName == fieldName);

            Assert.That(module.Disconnect(edge), Is.True);
            Assert.That(GetScalarReference(owner, fieldName)?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(target.uuid), Is.SameAs(target));
            Assert.That(target.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }
        /// <summary>Verifies scalar Loop and Raw references use the authored disconnect contract.</summary>
        [Test]
        public void TopologyEdit_DisconnectsLoopScalarAndRawWithoutOwningRawTarget()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode scalarTarget = Node<TestNode>("Loop condition");
            TestNode rawOwner = Node<TestNode>("Raw owner");
            TestNode rawTarget = Node<TestNode>("Raw target");
            loop.condition = scalarTarget.ToReference();
            rawOwner.raw = rawTarget.ToRawReference();
            scalarTarget.parent = loop.ToReference();
            BehaviourTreeData tree = Tree(loop, scalarTarget, rawOwner, rawTarget);

            Assert.That(tree.TryDisconnectReference(Address(loop.uuid, nameof(Loop.condition), -1), "Disconnect Loop condition",
                scalarTarget.uuid), Is.True);
            Assert.That(loop.condition?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(scalarTarget.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.TryDisconnectReference(Address(rawOwner.uuid, nameof(TestNode.raw), -1), "Disconnect Loop raw",
                rawTarget.uuid), Is.True);
            Assert.That(rawOwner.raw?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(rawTarget.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(rawTarget.uuid), Is.SameAs(rawTarget));
        }
        /// <summary>Verifies scalar graph disconnect rejects a stale edge target and supports Undo/Redo.</summary>
        [Test]
        public void GraphEdges_ScalarDisconnectChecksTargetAndSupportsUndoRedo()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            owner.child = first.ToReference();
            first.parent = owner.ToReference();
            BehaviourTreeData tree = Tree(owner, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphEdgeDescriptor current = module.Topology.Edges.Single(edge => edge.Reference.Address.FieldName == nameof(TestNode.child));
            AuthoredReferenceSnapshot staleReference = new(
                current.Reference.Address,
                second.uuid,
                current.Reference.IsNull);
            GraphEdgeDescriptor stale = new(current.Source, current.Target, staleReference, current.Kind, current.Label,
                current.OccurrenceId);

            Assert.That(module.Disconnect(stale), Is.False);
            Assert.That(owner.child.UUID, Is.EqualTo(first.uuid));
            Assert.That(module.Disconnect(current), Is.True);
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(first.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(first.uuid));
            Assert.That(first.parent?.UUID ?? UUID.Empty, Is.EqualTo(owner.uuid));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(tree.GetNode(first.uuid), Is.SameAs(first));
        }
        /// <summary>Verifies that a rejected Clipboard destination reports failure without changing the tree.</summary>
        [Test]
        public void ClipboardPaste_RejectsInvalidDestinationWithoutMutation()
        {
            TestNode owner = Node<TestNode>("Owner");
            TestNode source = Node<TestNode>("Source");
            TestNode foreignOwner = Node<TestNode>("Foreign Owner");
            BehaviourTreeData tree = Tree(owner, source);
            Clipboard clipboard = new();
            clipboard.Write(source, tree);
            INodeReferenceSingleSlot slot = owner.ToReferenceSlots()
                .OfType<INodeReferenceSingleSlot>()
                .Single(candidate => candidate.Name == nameof(TestNode.child));
            int nodeCount = tree.EditorNodes.Count;
            UUID childUUID = owner.child?.UUID ?? UUID.Empty;
            EditorUtility.ClearDirty(tree);

            Assert.That(clipboard.PasteTo(tree, foreignOwner, slot), Is.False);
            Assert.That(tree.EditorNodes, Has.Count.EqualTo(nodeCount));
            Assert.That(owner.child?.UUID ?? UUID.Empty, Is.EqualTo(childUUID));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }
        [Test]
        public void TopologyEdit_DeleteClearsIncomingReferencesAndKeepsChildren()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost target = Node<TestHost>("Target");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { target.ToReference(), target.ToReference() };
            target.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, target, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Assert.That(module.TryAnalyzeDelete(target.uuid, out GraphNodeDeleteImpact impact), Is.True);
            Assert.That(impact.StructuralIncoming, Is.EqualTo(2));
            Assert.That(impact.DirectStructuralChildCount, Is.EqualTo(1));
            Assert.That(tree.TryDeleteNodes(new HashSet<UUID> { target.uuid }, "Delete target"), Is.True);
            Assert.That(tree.GetNode(target.uuid), Is.Null);
            Assert.That(head.children, Is.Empty);
            Assert.That(tree.GetNode(child.uuid), Is.SameAs(child));
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }
        [Test]
        public void Entrance_AssignmentChangesOnlyHeadAndSupportsUndoRedo()
        {
            TestNode firstHead = Node<TestNode>("First Head");
            TestNode replacement = Node<TestNode>("Replacement");
            TestService service = Node<TestService>("Service");
            BehaviourTreeData tree = Tree(firstHead, replacement, service);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            IReadOnlyList<TreeNode> beforeNodes = tree.EditorNodes.ToArray();
            UUID replacementParent = replacement.parent?.UUID ?? UUID.Empty;

            Assert.That(module.CanAssignEntrance(service.uuid), Is.False);
            Assert.That(module.AssignEntrance(replacement.uuid), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            Assert.That(tree.EditorNodes, Is.EqualTo(beforeNodes));
            Assert.That(replacement.parent?.UUID ?? UUID.Empty, Is.EqualTo(replacementParent));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(firstHead.uuid));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            Assert.That(module.DisconnectEntrance(), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(UUID.Empty));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(replacement.uuid));
            Undo.PerformRedo();
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            Assert.That(tree.headNodeUUID, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies that moving Entrance reparents a uniquely-owned target atomically.</summary>
        [Test]
        public void Entrance_AssignmentReparentsExistingTargetAndSupportsUndoRedo()
        {
            TestNode oldHead = Node<TestNode>("Old Head");
            TestNode target = Node<TestNode>("Target");
            oldHead.child = target.ToReference();
            target.parent = oldHead.ToReference();
            BehaviourTreeData tree = Tree(oldHead, target);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Undo.ClearAll();

            Assert.That(module.CanAssignEntrance(target.uuid), Is.True);
            Assert.That(module.AssignEntrance(target.uuid), Is.True);
            Assert.That(tree.headNodeUUID, Is.EqualTo(target.uuid));
            Assert.That(oldHead.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(target.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(tree.headNodeUUID, Is.EqualTo(oldHead.uuid));
            Assert.That(oldHead.child.UUID, Is.EqualTo(target.uuid));
            Assert.That(target.parent.UUID, Is.EqualTo(oldHead.uuid));

            Undo.PerformRedo();
            tree.SerializedObject.Update();
            Assert.That(tree.headNodeUUID, Is.EqualTo(target.uuid));
            Assert.That(oldHead.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(target.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        [Test]
        public void Entrance_CreateNodeAssignsHeadAndRejectsServices()
        {
            TestNode existing = Node<TestNode>("Existing");
            BehaviourTreeData tree = Tree(existing);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            Assert.That(module.CreateEntranceNode(typeof(Sequence), new Vector2(17f, 29f)), Is.True);
            TreeNode created = tree.EditorNodes.Single(node => node is Sequence);
            Assert.That(tree.headNodeUUID, Is.EqualTo(created.uuid));
            Assert.That(module.CreateEntranceNode(typeof(Branch), new Vector2(3f, 5f)), Is.False);
        }
        [Test]
        public void ConnectionDrag_SourcePortHitTestingUsesNearestAnchor()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            host.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(host, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPortLayerElement portLayer = new();
            portLayer.SetPorts(topology, presentation, edgeLayer, ports);
            GraphPortDescriptor occupied = ports.Single(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(TestHost.children)
                && port.Address.Index == 0);
            Vector2 anchor = portLayer.GetSourcePosition(occupied);

            Assert.That(portLayer.FindSourcePort(anchor + new Vector2(3f, 0f), 4f), Is.SameAs(occupied));
            Assert.That(portLayer.FindSourcePort(anchor + new Vector2(5f, 0f), 4f), Is.Null);
        }
    }
}
