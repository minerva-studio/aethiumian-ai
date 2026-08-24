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
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    /// <summary>Graph Editor GraphTopologyBuilder contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphTopologyBuilderTests : GraphEditorTestFixture
    {
        [Test]
        public void Build_UsesReferenceOrderAndPreservesDuplicateEdges()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            head.children = new[] { first.ToReference(), second.ToReference(), first.ToReference() };
            BehaviourTreeData tree = Tree(head, first, second);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphEdgeDescriptor[] headEdges = topology.Edges
                .Where(edge => edge.Source.Node == head)
                .ToArray();

            Assert.That(topology.Nodes.Select(node => node.DisplayName), Is.EqualTo(new[] { "Head", "First", "Second" }));
            Assert.That(headEdges, Has.Length.EqualTo(3));
            Assert.That(headEdges[0].Label, Is.EqualTo("children [0]"));
            Assert.That(headEdges[2].Target, Is.SameAs(topology.Nodes[1]));
            Assert.That(headEdges.Select(edge => edge.Reference.Address.Index), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(headEdges.All(edge => edge.Reference.Address.OwnerUUID == head.uuid), Is.True);
            Assert.That(headEdges.Select(edge => edge.OccurrenceId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(topology.Nodes.All(node => node.IsReachable), Is.True);
        }

        [Test]
        public void Ports_BuildsOccupiedEmptyAndCollectionAppendSlots()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            TestNode detached = Node<TestNode>("Detached");
            host.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(host, child, detached);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);

            Assert.That(ports.Any(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(TestHost.children)
                && port.Address.Index == 0
                && port.Operation == GraphPortOperation.Replace), Is.True);
            Assert.That(ports.Any(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(TestHost.children)
                && port.Address.Index == -1
                && port.Operation == GraphPortOperation.Insert), Is.True);
            Assert.That(ports.Any(port => port.Address.OwnerUUID == detached.uuid
                && port.Address.FieldName == nameof(TestNode.child)
                && port.Operation == GraphPortOperation.Connect), Is.True);
            Assert.That(ports.All(port => port.Relation?.Role != GraphPresentationRelationRole.DerivedCompletion), Is.True);
            GraphPortDescriptor service = ports.Single(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(ServiceHostNode.services));
            Assert.That(service.AnchorKind, Is.EqualTo(GraphPortAnchorKind.Service));
        }

        [Test]
        public void Ports_RespectRawVisibilityAndRetainMissingAndWeightedOccurrences()
        {
            TestHost host = Node<TestHost>("Host");
            Probability probability = Node<Probability>("Probability");
            TestNode child = Node<TestNode>("Child");
            UUID missing = UUID.NewUUID();
            host.raw = new RawNodeReference { UUID = child.uuid };
            host.children = new[] { new NodeReference(missing) };
            probability.events = new[] { new Probability.EventWeight { reference = child.ToReference(), weight = 3 } };
            BehaviourTreeData tree = Tree(host, probability, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree, includeRawReferences: true);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            IReadOnlyList<GraphPortDescriptor> hidden = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            IReadOnlyList<GraphPortDescriptor> shown = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: true);

            Assert.That(hidden.Any(port => port.Address.OwnerUUID == host.uuid && port.Address.FieldName == nameof(TestHost.raw)), Is.False);
            Assert.That(shown.Any(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(TestHost.raw)
                && port.Operation == GraphPortOperation.Replace
                && port.IsRaw), Is.True);
            Assert.That(shown.Any(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(TestHost.children)
                && port.Address.Index == 0
                && port.Origin?.ReferenceState == GraphReferenceState.Missing), Is.True);
            GraphPortDescriptor probabilityPort = shown.Single(port => port.Address.OwnerUUID == probability.uuid
                && port.Address.FieldName == nameof(Probability.events));
            Assert.That(probabilityPort.PresentationMode, Is.EqualTo(GraphPortPresentationMode.Shared));
            Assert.That(probabilityPort.Operation, Is.EqualTo(GraphPortOperation.Insert));
            Assert.That(probabilityPort.Origins.Count, Is.EqualTo(1));
            Assert.That(shown.Any(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(ServiceHostNode.services)
                && port.Operation == GraphPortOperation.Insert), Is.True);
        }

        [Test]
        public void Ports_UseExplicitOrderedAndSharedCollectionModes()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Decision decision = Node<Decision>("Decision");
            Loop loop = Node<Loop>("Loop");
            Parallel parallel = Node<Parallel>("Parallel");
            Probability probability = Node<Probability>("Probability");
            PseudoProbability pseudoProbability = Node<PseudoProbability>("Pseudo Probability");
            TestHost host = Node<TestHost>("Host");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestService firstService = Node<TestService>("First Service");
            TestService secondService = Node<TestService>("Second Service");

            sequence.events = new[] { first.ToReference(), second.ToReference() };
            decision.events = new[] { first.ToReference(), second.ToReference() };
            loop.events = new[] { first.ToReference(), second.ToReference() };
            parallel.events = new[] { first.ToReference(), second.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { reference = first.ToReference(), weight = 1 },
                new Probability.EventWeight { reference = second.ToReference(), weight = 1 },
            };
            pseudoProbability.events = new[]
            {
                new PseudoProbability.EventWeight { reference = first.ToReference() },
                new PseudoProbability.EventWeight { reference = second.ToReference() },
            };
            host.services = new List<NodeReference> { firstService.ToReference(), secondService.ToReference() };
            BehaviourTreeData tree = Tree(
                sequence, decision, loop, parallel, probability, pseudoProbability, host,
                first, second, firstService, secondService);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);

            AssertOrderedPortCount(ports, sequence.uuid, nameof(Sequence.events), 3);
            AssertOrderedPortCount(ports, decision.uuid, nameof(Decision.events), 4, expectedInsertCount: 2);
            AssertOrderedPortCount(ports, loop.uuid, nameof(Loop.events), 3);
            AssertSharedPort(ports, parallel.uuid, nameof(Parallel.events), 2, GraphPortAnchorKind.Output);
            AssertSharedPort(ports, probability.uuid, nameof(Probability.events), 2, GraphPortAnchorKind.Output);
            AssertSharedPort(ports, pseudoProbability.uuid, nameof(PseudoProbability.events), 2, GraphPortAnchorKind.Output);
            AssertSharedPort(ports, host.uuid, nameof(ServiceHostNode.services), 2, GraphPortAnchorKind.Service);
        }

        [Test]
        public void Ports_FlowCollectionsUseChainedAnchorsAndDecisionRemainsDistributed()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode sequenceFirst = Node<TestNode>("Sequence First");
            TestNode sequenceSecond = Node<TestNode>("Sequence Second");
            sequence.events = new[] { sequenceFirst.ToReference(), sequenceSecond.ToReference() };

            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            TestNode loopFirst = Node<TestNode>("Loop First");
            TestNode loopSecond = Node<TestNode>("Loop Second");
            loop.condition = condition.ToReference();
            loop.events = new[] { loopFirst.ToReference(), loopSecond.ToReference() };

            Sequence emptySequence = Node<Sequence>("Empty Sequence");
            Loop emptyLoop = Node<Loop>("Empty Loop");
            emptyLoop.condition = NodeReference.Empty;
            emptyLoop.events = Array.Empty<NodeReference>();

            Decision decision = Node<Decision>("Decision");
            TestNode decisionFirst = Node<TestNode>("Decision First");
            TestNode decisionSecond = Node<TestNode>("Decision Second");
            decision.events = new[] { decisionFirst.ToReference(), decisionSecond.ToReference() };
            BehaviourTreeData tree = Tree(sequence, sequenceFirst, sequenceSecond, loop, condition, loopFirst, loopSecond,
                emptySequence, emptyLoop, decision, decisionFirst, decisionSecond);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edges = new(new GraphCanvasAppearance());
            edges.SetPresentation(presentation, ports);

            AssertChainedPorts(ports, edges, sequence.uuid, nameof(Sequence.events), sequenceSecond, presentation);
            AssertChainedPorts(ports, edges, loop.uuid, nameof(Loop.events), loopSecond, presentation);

            GraphPortDescriptor emptySequenceAppend = FindPort(ports, emptySequence.uuid, nameof(Sequence.events), -1);
            GraphPresentationItem emptySequenceItem = presentation.Find(emptySequence.uuid);
            Assert.That(edges.GetSourceAnchor(emptySequenceAppend), Is.EqualTo(
                emptySequenceItem.Position + new Vector2(emptySequenceItem.Size.x * 0.5f, emptySequenceItem.Size.y)));

            GraphPortDescriptor emptyLoopAppend = FindPort(ports, emptyLoop.uuid, nameof(Loop.events), -1);
            GraphPresentationRelation emptyLoopBody = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopBody
                && relation.Target.Item == presentation.Find(emptyLoop.uuid).LoopScope.Body[0]);
            Assert.That(edges.GetSourceAnchor(emptyLoopAppend), Is.EqualTo(edges.GetSourceAnchor(emptyLoopBody)));

            GraphPortDescriptor[] decisionPorts = ports.Where(port => port.Address.OwnerUUID == decision.uuid
                && port.Address.FieldName == nameof(Decision.events) && port.IsDecisionOption)
                .OrderBy(port => port.OutputIndex).ToArray();
            Assert.That(decisionPorts.All(port => port.AnchorKind == GraphPortAnchorKind.DecisionOption), Is.True);
            Assert.That(decisionPorts.Select(port => port.Address.Index), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(decisionPorts.Select(edges.GetSourceAnchor).Select(position => position.x), Is.Ordered);
            GraphPortDescriptor decisionPrepend = ports.Single(port => port.Address.OwnerUUID == decision.uuid
                && port.Address.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionPrepend);
            GraphPortDescriptor decisionAppend = ports.Single(port => port.Address.OwnerUUID == decision.uuid
                && port.Address.FieldName == nameof(Decision.events)
                && port.AnchorKind == GraphPortAnchorKind.DecisionAppend);
            Assert.That(decisionPrepend.Address.Index, Is.EqualTo(0));
            Assert.That(decisionAppend.Address.Index, Is.EqualTo(-1));
            Assert.That(edges.GetSourceAnchor(decisionPrepend).x, Is.LessThan(edges.GetSourceAnchor(decisionPorts[0]).x));
            Assert.That(edges.GetSourceAnchor(decisionAppend).x, Is.GreaterThan(edges.GetSourceAnchor(decisionPorts[1]).x));
            Assert.That(edges.GetSourceAnchor(decisionPrepend).y, Is.EqualTo(edges.GetSourceAnchor(decisionPorts[0]).y));
            Assert.That(edges.GetSourceAnchor(decisionAppend).y, Is.EqualTo(edges.GetSourceAnchor(decisionPorts[1]).y));
        }

        [Test]
        public void Ports_ForLoopHidesConditionReferenceButKeepsEventsPort()
        {
            Loop loop = Node<Loop>("For Loop");
            TestNode staleCondition = Node<TestNode>("Stale Condition");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@for;
            loop.condition = staleCondition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(loop, staleCondition, body);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(
                topology, presentation, includeRawReferences: false);

            Assert.That(ports.Any(port => port.Address.OwnerUUID == loop.uuid
                && port.Address.FieldName == nameof(Loop.condition)), Is.False);
            Assert.That(ports.Any(port => port.Address.OwnerUUID == loop.uuid
                && port.Address.FieldName == nameof(Loop.events)), Is.True);
            Assert.That(loop.condition.UUID, Is.EqualTo(staleCondition.uuid));
        }

        [Test]
        public void Ports_VisualShapesFollowOperations()
        {
            Assert.That(GraphPortLayerElement.GetVisualShape(GraphPortOperation.Replace), Is.EqualTo(GraphPortVisualShape.Solid));
            Assert.That(GraphPortLayerElement.GetVisualShape(GraphPortOperation.Connect), Is.EqualTo(GraphPortVisualShape.Ring));
            Assert.That(GraphPortLayerElement.GetVisualShape(GraphPortOperation.Insert), Is.EqualTo(GraphPortVisualShape.RingWithPlus));
        }

        [Test]
        public void Ports_ConditionUsesInternalPredicateAndFixedBranchAnchors()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode whenTrue = Node<TestNode>("True");
            TestNode whenFalse = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = whenTrue.ToReference();
            condition.falseNode = whenFalse.ToReference();
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(condition, predicate, whenTrue, whenFalse));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edges = new(new GraphCanvasAppearance());
            edges.SetPresentation(presentation, ports);

            GraphPortDescriptor check = FindPort(ports, condition.uuid, nameof(Condition.condition), -1);
            GraphPortDescriptor truePort = FindPort(ports, condition.uuid, nameof(Condition.trueNode), -1);
            GraphPortDescriptor falsePort = FindPort(ports, condition.uuid, nameof(Condition.falseNode), -1);
            Assert.That(check.AnchorKind, Is.EqualTo(GraphPortAnchorKind.ConditionPredicate));
            Assert.That(truePort.AnchorKind, Is.EqualTo(GraphPortAnchorKind.ConditionTrue));
            Assert.That(falsePort.AnchorKind, Is.EqualTo(GraphPortAnchorKind.ConditionFalse));
            Assert.That(edges.GetSourceAnchor(truePort).x, Is.LessThan(edges.GetSourceAnchor(falsePort).x));
            Assert.That(presentation.Find(predicate.uuid).Parent, Is.SameAs(presentation.Find(condition.uuid)));
        }

        [Test]
        public void Ports_ConditionServiceUsesHeaderAnchor()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestService service = Node<TestService>("Service");
            condition.condition = predicate.ToReference();
            condition.services = new List<NodeReference> { service.ToReference() };
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(condition, predicate, service));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphEdgeLayerElement edges = new(new GraphCanvasAppearance());
            edges.SetPresentation(presentation, ports);

            GraphPortDescriptor port = ports.Single(candidate => candidate.Address.OwnerUUID == condition.uuid
                && candidate.Address.FieldName == nameof(ServiceHostNode.services));
            GraphPresentationItem owner = presentation.Find(condition.uuid);
            Vector2 expected = owner.Position + new Vector2(owner.Size.x, GraphPresentationMetrics.ConditionHeader * 0.5f);
            Assert.That(edges.GetSourceAnchor(port), Is.EqualTo(expected));
            GraphPresentationRelation relation = presentation.Relations.Single(candidate => candidate.Kind == GraphPresentationRelationKind.Service
                && candidate.Source.Item == owner);
            Assert.That(edges.GetSourceAnchor(relation), Is.EqualTo(expected));
        }

        [Test]
        public void Ports_SharedServiceAnchorsMatchEdgesAndFollowMovedHost()
        {
            TestHost host = Node<TestHost>("Host");
            TestService firstService = Node<TestService>("First Service");
            TestService secondService = Node<TestService>("Second Service");
            host.services = new List<NodeReference> { firstService.ToReference(), secondService.ToReference() };
            BehaviourTreeData tree = Tree(host, firstService, secondService);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
            GraphPortDescriptor servicePort = ports.Single(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(ServiceHostNode.services));
            GraphEdgeLayerElement edgeLayer = new(new GraphCanvasAppearance());
            edgeLayer.SetPresentation(presentation, ports);
            GraphPortLayerElement portLayer = new();
            portLayer.SetPorts(topology, presentation, edgeLayer, ports);

            Vector2 source = edgeLayer.GetSourceAnchor(servicePort);
            GraphPresentationRelation[] serviceRelations = presentation.Relations
                .Where(relation => relation.Kind == GraphPresentationRelationKind.Service && relation.AuthoredEdge != null)
                .ToArray();
            Assert.That(serviceRelations, Has.Length.EqualTo(2));
            Assert.That(serviceRelations.All(relation => Vector2.Distance(edgeLayer.GetSourceAnchor(relation), source) < 0.001f), Is.True);
            Assert.That(GraphPortLayerElement.GetTargetPosition(presentation.Find(firstService.uuid)),
                Is.EqualTo(presentation.Find(firstService.uuid).Position + new Vector2(0f, presentation.Find(firstService.uuid).Size.y * 0.5f)));
            Assert.That(portLayer.GetSourceColor(servicePort), Is.EqualTo(edgeLayer.Appearance.ServiceEdge));

            Vector2 delta = new(37f, 19f);
            presentation.MoveRoot(host.uuid, presentation.Find(host.uuid).Position + delta);
            GraphPresentationLayout.Layout(presentation);
            Assert.That(edgeLayer.GetSourceAnchor(servicePort), Is.EqualTo(source + delta));
        }

        [Test]
        public void StructureValidation_ReportsOnlyAuthoredStructuralOwnershipErrors()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode shared = Node<TestNode>("Shared");
            TestNode rawTarget = Node<TestNode>("Raw Target");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.parent = head.ToReference();
            second.parent = head.ToReference();
            first.child = shared.ToReference();
            second.child = shared.ToReference();
            first.raw = new RawNodeReference { UUID = rawTarget.uuid };
            shared.parent = first.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, shared, rawTarget);

            IReadOnlyList<string> errors = tree.GetStructureValidationErrors();

            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("Shared").And.Contain("First").And.Contain("Second"));
            Assert.That(errors[0], Does.Not.Contain("Raw Target"));
        }

        [Test]
        public void Build_ServicesAreSpecialAndRawReferencesAreOptIn()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            TestService service = Node<TestService>("Service");
            head.children = new[] { child.ToReference() };
            head.services = new List<NodeReference> { service.ToReference() };
            head.raw = new RawNodeReference { UUID = child.uuid };
            BehaviourTreeData tree = Tree(head, child, service);

            GraphTopology hidden = GraphTopologyBuilder.Build(tree);
            Assert.That(hidden.Edges.Any(edge => edge.Kind == GraphEdgeKind.Service), Is.True);
            Assert.That(hidden.Edges.Any(edge => edge.Kind == GraphEdgeKind.Raw), Is.False);

            GraphTopology shown = GraphTopologyBuilder.Build(tree, includeRawReferences: true);
            GraphEdgeDescriptor raw = shown.Edges.Single(edge => edge.Kind == GraphEdgeKind.Raw);
            Assert.That(raw.Target, Is.SameAs(shown.FindNode(child.uuid)));
            Assert.That(raw.Source.IsReachable, Is.True);
            Assert.That(raw.Target.IsReachable, Is.True);
        }

        [Test]
        public void Build_MissingTargetAndCycleDoNotCreateGhostNodes()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            UUID missingUUID = UUID.NewUUID();
            head.children = new[] { child.ToReference(), new NodeReference(missingUUID) };
            child.child = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphEdgeDescriptor missing = topology.Edges.Single(edge => edge.Reference.TargetUUID == missingUUID);
            Assert.That(missing.Target, Is.Null);
            Assert.That(missing.ReferenceState, Is.EqualTo(GraphReferenceState.Missing));
            Assert.That(topology.Nodes.Count, Is.EqualTo(2));
            Assert.That(topology.FindNode(missingUUID), Is.Null);
            Assert.That(topology.Nodes.All(node => node.IsReachable), Is.True);
            Assert.That(topology.Nodes[0].HasWarning, Is.True);
        }

        [Test]
        public void Build_ProbabilityEdgesKeepCollectionOrderAndWeightSummary()
        {
            Probability head = Node<Probability>("Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            head.events = new[]
            {
                new Probability.EventWeight { weight = 3, reference = first.ToReference() },
                new Probability.EventWeight { weight = 7, reference = second.ToReference() },
            };
            BehaviourTreeData tree = Tree(head, first, second);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphEdgeDescriptor[] probabilityEdges = topology.Edges
                .Where(edge => edge.Source.Node == head)
                .ToArray();

            Assert.That(probabilityEdges.Select(edge => edge.Label), Is.EqualTo(new[] { "events [0] (3)", "events [1] (7)" }));
            Assert.That(probabilityEdges.Select(edge => edge.Target.Node), Is.EqualTo(new TreeNode[] { first, second }));
        }

        [Test]
        public void Build_EmptyReferencesAreNotReportedAsMissingTargets()
        {
            TestNode head = Node<TestNode>("Head");
            head.child = NodeReference.Empty;
            head.raw = RawNodeReference.Empty;
            BehaviourTreeData tree = Tree(head);

            GraphTopology topology = GraphTopologyBuilder.Build(tree, includeRawReferences: true);

            Assert.That(topology.Edges, Has.Count.EqualTo(2));
            Assert.That(topology.Edges.All(edge => edge.ReferenceState == GraphReferenceState.Empty), Is.True);
            Assert.That(topology.Nodes[0].HasWarning, Is.False);
        }

        [Test]
        public void Build_NullCollectionOccurrenceRetainsAuthoredAddress()
        {
            TestHost head = Node<TestHost>("Head");
            head.children = new NodeReference[] { null };
            BehaviourTreeData tree = Tree(head);

            GraphEdgeDescriptor edge = GraphTopologyBuilder.Build(tree).Edges.Single();

            Assert.That(edge.Reference.Address.OwnerUUID, Is.EqualTo(head.uuid));
            Assert.That(edge.Reference.Address.FieldName, Is.EqualTo(nameof(TestHost.children)));
            Assert.That(edge.Reference.Address.Index, Is.EqualTo(0));
            Assert.That(edge.Reference.IsNull, Is.True);
            Assert.That(edge.ReferenceState, Is.EqualTo(GraphReferenceState.Empty));
            Assert.That(edge.Reference.HasRemovableValue, Is.True);
        }

        [Test]
        public void Build_ClassifiesControlFlowAsStructuralShapes()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            Parallel parallel = Node<Parallel>("Parallel");
            Loop loop = Node<Loop>("Loop");
            ForEach forEach = Node<ForEach>("ForEach");
            Decision decision = Node<Decision>("Decision");
            Condition condition = Node<Condition>("Condition");
            Probability probability = Node<Probability>("Probability");
            PseudoProbability pseudoProbability = Node<PseudoProbability>("PseudoProbability");
            TestService service = Node<TestService>("Service");
            TestNode action = Node<TestNode>("Action");
            BehaviourTreeData tree = Tree(
                sequence,
                parallel,
                loop,
                forEach,
                decision,
                condition,
                probability,
                pseudoProbability,
                service,
                action);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(topology.FindNode(sequence.uuid).Shape, Is.EqualTo(GraphNodeShape.Flow));
            Assert.That(topology.FindNode(parallel.uuid).Shape, Is.EqualTo(GraphNodeShape.Flow));
            Assert.That(topology.FindNode(loop.uuid).Shape, Is.EqualTo(GraphNodeShape.Flow));
            Assert.That(topology.FindNode(forEach.uuid).Shape, Is.EqualTo(GraphNodeShape.Flow));
            Assert.That(topology.FindNode(decision.uuid).Shape, Is.EqualTo(GraphNodeShape.Branch));
            Assert.That(topology.FindNode(condition.uuid).Shape, Is.EqualTo(GraphNodeShape.Branch));
            Assert.That(topology.FindNode(probability.uuid).Shape, Is.EqualTo(GraphNodeShape.Branch));
            Assert.That(topology.FindNode(pseudoProbability.uuid).Shape, Is.EqualTo(GraphNodeShape.Branch));
            Assert.That(topology.FindNode(service.uuid).Shape, Is.EqualTo(GraphNodeShape.Service));
            Assert.That(topology.FindNode(action.uuid).Shape, Is.EqualTo(GraphNodeShape.Normal));
        }
        private static GraphPortDescriptor FindPort(
            IEnumerable<GraphPortDescriptor> ports, UUID ownerUUID, string fieldName, int index)
        {
            return ports.Single(port => port.Address.OwnerUUID == ownerUUID
                && port.Address.FieldName == fieldName && port.Address.Index == index);
        }

        private static IReadOnlyList<GraphPortDescriptor> BuildPorts(GraphTopology topology)
        {
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            return GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);
        }

        private static void AssertOrderedPortCount(
            IEnumerable<GraphPortDescriptor> ports, UUID ownerUUID, string fieldName, int expectedCount,
            int expectedInsertCount = 1)
        {
            GraphPortDescriptor[] fieldPorts = ports.Where(port => port.Address.OwnerUUID == ownerUUID
                && port.Address.FieldName == fieldName).ToArray();
            Assert.That(fieldPorts, Has.Length.EqualTo(expectedCount));
            Assert.That(fieldPorts.All(port => port.PresentationMode == GraphPortPresentationMode.Ordered), Is.True);
            Assert.That(fieldPorts.Count(port => port.Operation == GraphPortOperation.Insert), Is.EqualTo(expectedInsertCount));
        }

        private static void AssertSharedPort(
            IEnumerable<GraphPortDescriptor> ports, UUID ownerUUID, string fieldName,
            int expectedOrigins, GraphPortAnchorKind anchorKind)
        {
            GraphPortDescriptor port = ports.Single(candidate => candidate.Address.OwnerUUID == ownerUUID
                && candidate.Address.FieldName == fieldName);
            Assert.That(port.PresentationMode, Is.EqualTo(GraphPortPresentationMode.Shared));
            Assert.That(port.Operation, Is.EqualTo(GraphPortOperation.Insert));
            Assert.That(port.Origins, Has.Count.EqualTo(expectedOrigins));
            Assert.That(port.AnchorKind, Is.EqualTo(anchorKind));
        }

        private static void AssertChainedPorts(
            IReadOnlyList<GraphPortDescriptor> ports, GraphEdgeLayerElement edges, UUID ownerUUID,
            string fieldName, TestNode last, GraphPresentation presentation)
        {
            GraphPortDescriptor[] occurrences = ports.Where(port => port.Address.OwnerUUID == ownerUUID
                && port.Address.FieldName == fieldName && port.Address.Index >= 0)
                .OrderBy(port => port.Address.Index).ToArray();
            Assert.That(occurrences, Has.Length.EqualTo(2));
            Assert.That(occurrences.All(port => port.AnchorKind == GraphPortAnchorKind.ChainedOutput), Is.True);
            Assert.That(occurrences.All(port => edges.GetSourceAnchor(port) == edges.GetSourceAnchor(port.Relation)), Is.True);
            GraphPortDescriptor append = FindPort(ports, ownerUUID, fieldName, -1);
            GraphPresentationItem lastItem = presentation.Find(last.uuid);
            Assert.That(append.AnchorKind, Is.EqualTo(GraphPortAnchorKind.ChainedOutput));
            Assert.That(edges.GetSourceAnchor(append), Is.EqualTo(
                lastItem.Position + new Vector2(lastItem.Size.x * 0.5f, lastItem.Size.y)));
        }
    }
}
