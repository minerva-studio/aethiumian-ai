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
    /// <summary>Graph Editor topology tests for GraphCollectionEditTests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphCollectionEditTests : GraphTopologyEditTestBase
    {
private static void AssertProbabilityAnchors(
            TreeNode node,
            IReadOnlyList<GraphPortDescriptor> ports,
            GraphPresentation presentation,
            GraphEdgeLayerElement painted,
            GraphEdgeLayerElement unmodified)
        {
            GraphPresentationItem owner = presentation.Find(node.uuid);
            GraphPortDescriptor port = ports.Single(candidate => candidate.Address.OwnerUUID == node.uuid
                && candidate.Address.FieldName == "events");
            GraphPresentationRelation authored = presentation.Relations.Single(relation =>
                relation.Role == GraphPresentationRelationRole.AuthoredReference
                && relation.Kind == GraphPresentationRelationKind.ProbabilityBranch
                && relation.Source.Item == owner);
            GraphPresentationRelation[] completion = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete).ToArray();
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Source == owner.FlowComplete && relation.Target.Item?.Node?.Node is TestNode);

            Assert.That(painted.GetSourceAnchor(authored), Is.EqualTo(painted.GetSourceAnchor(port)));
            Assert.That(completion, Is.Not.Empty);
            Assert.That(completion.All(relation => painted.GetSourceAnchor(relation) == unmodified.GetSourceAnchor(relation)), Is.True);
            Assert.That(completion.All(relation => painted.GetSourceAnchor(relation) != painted.GetSourceAnchor(port)), Is.True);
            Assert.That(continuation.Source, Is.EqualTo(owner.FlowComplete));
            Assert.That(painted.GetSourceAnchor(continuation), Is.EqualTo(unmodified.GetSourceAnchor(continuation)));
        }

        [Test]
        public void DecisionAppendPort_AppendsThenIndexCommandReorders()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode inserted = Node<TestNode>("Inserted");
            TestNode last = Node<TestNode>("Last");
            decision.events = new[] { first.ToReference(), last.ToReference() };
            first.parent = decision.ToReference();
            last.parent = decision.ToReference();
            BehaviourTreeData tree = Tree(decision, first, inserted, last);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor append = BuildPorts(module.Topology).Single(port =>
                port.Address.OwnerUUID == decision.uuid
                && port.Address.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionAppend);

            Assert.That(append.Address.Index, Is.EqualTo(-1));
            Assert.That(module.Assign(append, inserted.uuid), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, last.uuid, inserted.uuid }));
            Assert.That(inserted.parent.UUID, Is.EqualTo(decision.uuid));
            Assert.That(module.ReorderCollection(decision.uuid, nameof(Decision.events), 2, 1), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, inserted.uuid, last.uuid }));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, last.uuid, inserted.uuid }));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID), Is.EqualTo(new[] { first.uuid, last.uuid }));
        }
        [Test]
        public void DecisionStandardPorts_PrependAndReplaceByCollectionAddress()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode last = Node<TestNode>("Last");
            TestNode prepended = Node<TestNode>("Prepended");
            TestNode replacement = Node<TestNode>("Replacement");
            decision.events = new[] { first.ToReference(), last.ToReference() };
            first.parent = decision.ToReference();
            last.parent = decision.ToReference();
            BehaviourTreeData tree = Tree(decision, first, last, prepended, replacement);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor prepend = BuildPorts(module.Topology).Single(port =>
                port.Address.OwnerUUID == decision.uuid
                && port.Address.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionPrepend);

            Assert.That(module.Assign(prepend, prepended.uuid), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { prepended.uuid, first.uuid, last.uuid }));
            GraphPortDescriptor replace = BuildPorts(module.Topology).Single(port =>
                port.Address.OwnerUUID == decision.uuid
                && port.Address.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionOption
                && port.Address.Index == 1);
            Assert.That(module.Assign(replace, replacement.uuid), Is.True);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { prepended.uuid, replacement.uuid, last.uuid }));

            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { prepended.uuid, first.uuid, last.uuid }));
            Undo.PerformUndo();
            tree.SerializedObject.Update();
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { first.uuid, last.uuid }));
        }
        [Test]
        public void DecisionStandardPorts_CreateAtPrependAndReplaceMissingSlot()
        {
            Decision decision = Node<Decision>("Decision");
            UUID missing = UUID.NewUUID();
            decision.events = new[] { new NodeReference(missing) };
            BehaviourTreeData tree = Tree(decision);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPortDescriptor prepend = BuildPorts(module.Topology).Single(port =>
                port.Address.OwnerUUID == decision.uuid && port.AnchorKind == GraphPortAnchorKind.DecisionPrepend);

            Assert.That(module.CreateNode(typeof(Sequence), new Vector2(20f, 30f), prepend), Is.True);
            TreeNode createdFirst = tree.EditorNodes.Single(node => node != decision);
            Assert.That(decision.events.Select(reference => reference.UUID),
                Is.EqualTo(new[] { createdFirst.uuid, missing }));
            GraphPortDescriptor missingPort = BuildPorts(module.Topology).Single(port =>
                port.Address.OwnerUUID == decision.uuid
                && port.AnchorKind == GraphPortAnchorKind.DecisionOption
                && port.Address.Index == 1);
            Assert.That(module.CreateNode(typeof(Sequence), new Vector2(40f, 50f), missingPort), Is.True);
            Assert.That(decision.events[1].UUID, Is.Not.EqualTo(missing));
            Assert.That(tree.GraphLayout.TryGetPosition(decision.events[1].UUID, out Vector2 replacementPosition), Is.True);
            Assert.That(replacementPosition, Is.EqualTo(new Vector2(40f, 50f)));
        }
        [Test]
        public void DecisionIndexCommand_ReordersEmptyAndMissingReferences()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode valid = Node<TestNode>("Valid");
            UUID missing = UUID.NewUUID();
            decision.events = new[]
            {
                new NodeReference(),
                new NodeReference(missing),
                valid.ToReference(),
            };
            BehaviourTreeData tree = Tree(decision, valid);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            Assert.That(module.ReorderCollection(decision.uuid, nameof(Decision.events), 0, 2), Is.True);
            Assert.That(decision.events.Select(reference => reference?.UUID ?? UUID.Empty),
                Is.EqualTo(new[] { missing, valid.uuid, UUID.Empty }));
        }
        [TestCase(0f, 0)]
        [TestCase(47f, 0)]
        [TestCase(49f, 1)]
        [TestCase(143f, 1)]
        [TestCase(145f, 2)]
        [TestCase(400f, 4)]
        public void DecisionOrderBoundary_UsesNearestMemberGap(float localX, int expectedBoundary)
        {
            Assert.That(GraphDecisionOrderStripElement.GetInsertionBoundary(localX, 4), Is.EqualTo(expectedBoundary));
        }
        [TestCase(1, 0, 0, true)]
        [TestCase(1, 1, 1, false)]
        [TestCase(1, 2, 1, false)]
        [TestCase(1, 3, 2, true)]
        [TestCase(1, 4, 3, true)]
        [TestCase(3, 0, 0, true)]
        [TestCase(3, 2, 2, true)]
        [TestCase(0, 2, 1, true)]
        [TestCase(0, 4, 3, true)]
        public void DecisionOrderBoundary_ConvertsToFinalCollectionIndex(
            int sourceIndex,
            int boundaryIndex,
            int expectedDestination,
            bool expectedMove)
        {
            bool canMove = GraphDecisionOrderStripElement.TryGetDestinationIndex(
                sourceIndex,
                boundaryIndex,
                4,
                out int destinationIndex);

            Assert.That(canMove, Is.EqualTo(expectedMove));
            Assert.That(destinationIndex, Is.EqualTo(expectedDestination));
        }
        [Test]
        public void GraphEdges_ProbabilityFamilyKeepsDerivedCompletionAnchors()
        {
            Sequence probabilityOuter = Node<Sequence>("Probability Outer");
            Probability probability = Node<Probability>("Probability");
            TestNode probabilityCandidate = Node<TestNode>("Probability Candidate");
            TestNode probabilityAfter = Node<TestNode>("Probability After");
            probabilityOuter.events = new[] { probability.ToReference(), probabilityAfter.ToReference() };
            probability.events = new[] { new Probability.EventWeight { weight = 1, reference = probabilityCandidate.ToReference() } };

            Sequence pseudoOuter = Node<Sequence>("Pseudo Outer");
            PseudoProbability pseudo = Node<PseudoProbability>("Pseudo Probability");
            TestNode pseudoCandidate = Node<TestNode>("Pseudo Candidate");
            TestNode pseudoAfter = Node<TestNode>("Pseudo After");
            pseudoOuter.events = new[] { pseudo.ToReference(), pseudoAfter.ToReference() };
            pseudo.events = new[] { new PseudoProbability.EventWeight { weight = 1, reference = pseudoCandidate.ToReference() } };

            BehaviourTreeData tree = Tree(
                probabilityOuter, probability, probabilityCandidate, probabilityAfter,
                pseudoOuter, pseudo, pseudoCandidate, pseudoAfter);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement painted = new(new GraphCanvasAppearance());
            painted.SetPresentation(presentation, ports);
            GraphEdgeLayerElement unmodified = new(new GraphCanvasAppearance());
            unmodified.SetPresentation(presentation, Array.Empty<GraphPortDescriptor>());

            AssertProbabilityAnchors(probability, ports, presentation, painted, unmodified);
            AssertProbabilityAnchors(pseudo, ports, presentation, painted, unmodified);
        }
        [Test]
        public void GraphEdges_SelectRenderedAuthoredOccurrenceWithoutDirtyingTree()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            host.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(host, child);
            EditorUtility.ClearDirty(tree);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPresentationRelation relation = presentation.Relations.Single(candidate => candidate.AuthoredEdge != null);
            Vector2 from = edgeLayer.GetSourceAnchor(relation);
            Vector2 to = GraphPortLayerElement.GetTargetPosition(presentation.Find(child.uuid));

            Assert.That(edgeLayer.SelectAt((from + to) * 0.5f, 8f), Is.True);
            Assert.That(edgeLayer.SelectedRelation, Is.SameAs(relation));
            edgeLayer.ClearEdgeSelection();
            Assert.That(edgeLayer.SelectedRelation, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }
        [Test]
        public void GraphEdges_DisconnectUsesExactOccurrenceAndRebuildsOnce()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            host.children = new[] { first.ToReference(), second.ToReference() };
            first.parent = host.ToReference();
            second.parent = host.ToReference();
            BehaviourTreeData tree = Tree(host, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Dictionary<UUID, Vector2> positions = module.Topology.Nodes.ToDictionary(node => node.UUID, node => node.Position);
            GraphEdgeDescriptor selected = module.Topology.Edges.Single(edge => edge.Source.UUID == host.uuid
                && edge.Reference.Address.FieldName == nameof(TestHost.children)
                && edge.Reference.Address.Index == 0);
            EditorUtility.ClearDirty(tree);

            Assert.That(module.Disconnect(selected), Is.True);
            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid }));
            Assert.That(module.Topology.Edges.Count(edge => edge.Source.UUID == host.uuid
                && edge.Reference.Address.FieldName == nameof(TestHost.children)), Is.EqualTo(1));
            AssertGraphPositions(module.Topology, positions);
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }
        [Test]
        public void TopologyEdit_WeightedReplaceAndReorderPreserveEntryMetadata()
        {
            Probability probability = Node<Probability>("Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode replacement = Node<TestNode>("Replacement");
            probability.events = new[]
            {
                new Probability.EventWeight { reference = first.ToReference(), weight = 7 },
                new Probability.EventWeight { reference = second.ToReference(), weight = 19 },
            };
            first.parent = probability.ToReference();
            second.parent = probability.ToReference();
            BehaviourTreeData tree = Tree(probability, first, second, replacement);
            bool replaced = tree.TryReplaceReference(Address(probability.uuid, nameof(Probability.events), 0), replacement.uuid, "Replace weighted event");
            bool reordered = tree.TryReorderReference(Address(probability.uuid, nameof(Probability.events), 1), 0, "Reorder weighted event");

            Assert.That(replaced, Is.True);
            Assert.That(reordered, Is.True);
            Assert.That(probability.events.Select(entry => entry.reference.UUID), Is.EqualTo(new[] { second.uuid, replacement.uuid }));
            Assert.That(probability.events.Select(entry => entry.weight), Is.EqualTo(new[] { 19, 7 }));
            Assert.That(replacement.parent?.UUID, Is.EqualTo(probability.uuid));
        }
        [Test]
        public void TopologyEdit_InsertWeightedEntriesUsesDefaultWeightOne()
        {
            Probability probability = Node<Probability>("Probability");
            PseudoProbability pseudoProbability = Node<PseudoProbability>("Pseudo Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(probability, pseudoProbability, first, second);
            bool probabilityResult = tree.TryInsertReference(Address(probability.uuid, nameof(Probability.events), 0), first.uuid, false, "Insert event");
            bool pseudoResult = tree.TryInsertReference(Address(pseudoProbability.uuid, nameof(PseudoProbability.events), 0), second.uuid, false, "Insert event");

            Assert.That(probabilityResult, Is.True);
            Assert.That(pseudoResult, Is.True);
            Assert.That(probability.events, Has.Length.EqualTo(1));
            Assert.That(probability.events[0].reference.UUID, Is.EqualTo(first.uuid));
            Assert.That(probability.events[0].weight, Is.EqualTo(1));
            Assert.That(pseudoProbability.events, Has.Length.EqualTo(1));
            Assert.That(pseudoProbability.events[0].reference.UUID, Is.EqualTo(second.uuid));
            Assert.That(pseudoProbability.events[0].weight.IsConstant, Is.True);
            Assert.That((int)pseudoProbability.events[0].weight, Is.EqualTo(1));
        }
        [Test]
        public void TopologyEdit_PseudoProbabilityEditsPreserveVariableWeightMetadata()
        {
            PseudoProbability probability = Node<PseudoProbability>("Pseudo Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode replacement = Node<TestNode>("Replacement");
            VariableData dynamicWeight = new("Dynamic Weight", VariableType.Int);
            VariableField<int> dynamicField = new();
            dynamicField.SetReference(dynamicWeight);
            probability.events = new[]
            {
                new PseudoProbability.EventWeight { reference = first.ToReference(), weight = dynamicField },
                new PseudoProbability.EventWeight { reference = second.ToReference(), weight = 9 },
            };
            first.parent = probability.ToReference();
            second.parent = probability.ToReference();
            BehaviourTreeData tree = Tree(probability, first, second, replacement);
            tree.variables.Add(dynamicWeight);
            bool replaced = tree.TryReplaceReference(Address(probability.uuid, nameof(PseudoProbability.events), 0), replacement.uuid, "Replace event");
            bool reordered = tree.TryReorderReference(Address(probability.uuid, nameof(PseudoProbability.events), 0), 1, "Reorder event");

            Assert.That(replaced, Is.True);
            Assert.That(reordered, Is.True);
            Assert.That(probability.events.Select(entry => entry.reference.UUID), Is.EqualTo(new[] { second.uuid, replacement.uuid }));
            Assert.That(probability.events[1].weight.IsConstant, Is.False);
            Assert.That(probability.events[1].weight.UUID, Is.EqualTo(dynamicWeight.UUID));
        }
    }
}
