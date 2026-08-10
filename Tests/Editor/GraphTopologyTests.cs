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

namespace Aethiumian.AI.Tests
{
    /// <summary>
    /// EditMode coverage for graph topology and non-dirty layout resolution.
    /// </summary>
    public sealed class GraphTopologyTests
    {
        private readonly List<BehaviourTreeData> trees = new();
        private readonly List<AIEditorWindow> shownWindows = new();
        private readonly List<AIEditorWindow> hiddenWindows = new();

        [TearDown]
        public void TearDown()
        {
            foreach (BehaviourTreeData tree in trees)
            {
                if (tree)
                {
                    UnityEngine.Object.DestroyImmediate(tree);
                }
            }

            trees.Clear();

            foreach (AIEditorWindow window in shownWindows)
            {
                if (window)
                {
                    window.Close();
                }
            }

            shownWindows.Clear();

            foreach (AIEditorWindow window in hiddenWindows)
            {
                if (window)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }

            hiddenWindows.Clear();
        }

        [Test]
        public void Build_UsesReferenceOrderAndPreservesDuplicateEdges()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            head.children = new[] { first.ToReference(), second.ToReference(), first.ToReference() };
            BehaviourTreeData tree = Tree(head, first, second);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(topology.Nodes.Select(node => node.DisplayName), Is.EqualTo(new[] { "Head", "First", "Second" }));
            Assert.That(topology.Edges.Count(edge => edge.Source.Node == head), Is.EqualTo(3));
            Assert.That(topology.Edges[0].Label, Is.EqualTo("children [0]"));
            Assert.That(topology.Edges[2].Target, Is.SameAs(topology.Nodes[1]));
            Assert.That(topology.Nodes.All(node => node.IsReachable), Is.True);
        }

        /// <summary>Verifies a collection command mutates one occurrence and reconciles the structural parent.</summary>
        [Test]
        public void TopologyEdit_ConnectAndDisconnectCollectionOccurrenceReconcilesParent()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);
            GraphTopologyEditService edits = new(tree);
            GraphReferenceAddress address = new(head.uuid, nameof(TestHost.children));

            GraphTopologyEditResult connected = edits.Connect(address, child.uuid);

            Assert.That(connected.Succeeded, Is.True, connected.Error);
            Assert.That(head.children.Select(reference => reference.UUID), Is.EqualTo(new[] { child.uuid }));
            Assert.That(child.parent?.UUID, Is.EqualTo(head.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);

            GraphTopologyEditResult disconnected = edits.Disconnect(new GraphReferenceAddress(
                head.uuid,
                nameof(TestHost.children),
                0));

            Assert.That(disconnected.Succeeded, Is.True, disconnected.Error);
            Assert.That(head.children, Is.Empty);
            Assert.That(child.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies canvas ports derive only authored slots and retain the authoritative reference address.</summary>
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

        /// <summary>Verifies authored ports retain missing and weighted occurrences while respecting Raw visibility.</summary>
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
                && port.Origin?.IsMissingTarget == true), Is.True);
            GraphPortDescriptor probabilityPort = shown.Single(port => port.Address.OwnerUUID == probability.uuid
                && port.Address.FieldName == nameof(Probability.events));
            Assert.That(probabilityPort.PresentationMode, Is.EqualTo(GraphPortPresentationMode.Shared));
            Assert.That(probabilityPort.Operation, Is.EqualTo(GraphPortOperation.Insert));
            Assert.That(probabilityPort.Origins.Count, Is.EqualTo(1));
            Assert.That(shown.Any(port => port.Address.OwnerUUID == host.uuid
                && port.Address.FieldName == nameof(ServiceHostNode.services)
                && port.Operation == GraphPortOperation.Insert), Is.True);
        }

        /// <summary>Verifies ordered collections expose occurrences plus append while shared fields expose one handle.</summary>
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
            AssertOrderedPortCount(ports, decision.uuid, nameof(Decision.events), 3);
            AssertOrderedPortCount(ports, loop.uuid, nameof(Loop.events), 3);
            AssertSharedPort(ports, parallel.uuid, nameof(Parallel.events), 2, GraphPortAnchorKind.Output);
            AssertSharedPort(ports, probability.uuid, nameof(Probability.events), 2, GraphPortAnchorKind.Output);
            AssertSharedPort(ports, pseudoProbability.uuid, nameof(PseudoProbability.events), 2, GraphPortAnchorKind.Output);
            AssertSharedPort(ports, host.uuid, nameof(ServiceHostNode.services), 2, GraphPortAnchorKind.Service);
        }

        /// <summary>Verifies chained Flow collections use their execution relations instead of owner-wide output ordinals.</summary>
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
                && port.Address.FieldName == nameof(Decision.events)).OrderBy(port => port.OutputIndex).ToArray();
            Assert.That(decisionPorts.All(port => port.AnchorKind == GraphPortAnchorKind.DistributedOutput), Is.True);
            Assert.That(decisionPorts.Select(port => port.OutputIndex), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(decisionPorts.Select(edges.GetSourceAnchor).Select(position => position.x), Is.Ordered);
        }

        /// <summary>Verifies port operations retain distinct visual affordances without changing descriptor count.</summary>
        [Test]
        public void Ports_VisualShapesFollowOperations()
        {
            Assert.That(GraphPortLayerElement.GetVisualShape(GraphPortOperation.Replace), Is.EqualTo(GraphPortVisualShape.Solid));
            Assert.That(GraphPortLayerElement.GetVisualShape(GraphPortOperation.Connect), Is.EqualTo(GraphPortVisualShape.Ring));
            Assert.That(GraphPortLayerElement.GetVisualShape(GraphPortOperation.Insert), Is.EqualTo(GraphPortVisualShape.RingWithPlus));
        }

        /// <summary>Verifies Condition fields retain their addresses while using owner-local canvas anchors.</summary>
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

        /// <summary>Verifies a Condition derives compact predicate positions without rewriting authored layout data.</summary>
        [Test]
        public void Presentation_ConditionPredicateLayoutIgnoresStoredInternalSpacing()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode child = Node<TestNode>("Child");
            condition.condition = predicate.ToReference();
            predicate.child = child.ToReference();
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(condition, predicate, child));
            topology.FindNode(condition.uuid).Position = new Vector2(40f, 60f);
            topology.FindNode(predicate.uuid).Position = new Vector2(1200f, 800f);
            topology.FindNode(child.uuid).Position = new Vector2(-900f, 2400f);
            Vector2 predicateStored = topology.FindNode(predicate.uuid).Position;
            Vector2 childStored = topology.FindNode(child.uuid).Position;

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem owner = presentation.Find(condition.uuid);
            GraphPresentationItem predicateItem = presentation.Find(predicate.uuid);
            GraphPresentationItem childItem = presentation.Find(child.uuid);
            Assert.That(topology.FindNode(predicate.uuid).Position, Is.EqualTo(predicateStored));
            Assert.That(topology.FindNode(child.uuid).Position, Is.EqualTo(childStored));
            Assert.That(predicateItem.Position.y, Is.LessThan(childItem.Position.y));
            Assert.That(owner.Size.y, Is.LessThan(250f));
            Assert.That(new Rect(owner.Position, owner.Size).Contains(new Rect(predicateItem.Position, predicateItem.Size).center), Is.True);
            Assert.That(new Rect(owner.Position, owner.Size).Contains(new Rect(childItem.Position, childItem.Size).center), Is.True);
        }

        /// <summary>Verifies a Condition-owned shared Service port remains in the stable header lane.</summary>
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

        /// <summary>Verifies a Condition derives its predicate subtree from the authored slot without absorbing execution branches.</summary>
        [Test]
        public void Presentation_ConditionEmbedsPredicateSubtreeButLeavesBranchesExternal()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode predicateChild = Node<TestNode>("Predicate Child");
            TestNode whenTrue = Node<TestNode>("True");
            condition.condition = predicate.ToReference();
            condition.trueNode = whenTrue.ToReference();
            predicate.child = predicateChild.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, predicateChild, whenTrue);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            Vector2 predicateStoredPosition = topology.FindNode(predicate.uuid).Position;
            Vector2 childStoredPosition = topology.FindNode(predicateChild.uuid).Position;
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphPresentationItem owner = presentation.Find(condition.uuid);
            GraphPresentationItem predicateItem = presentation.Find(predicate.uuid);
            GraphPresentationItem childItem = presentation.Find(predicateChild.uuid);
            GraphConditionScope scope = owner.ConditionScope;

            Assert.That(owner.Slots.Single().Content, Is.SameAs(predicateItem));
            Assert.That(scope.PredicateRoot, Is.SameAs(predicateItem));
            Assert.That(scope.PredicateMembers, Is.EquivalentTo(new[] { predicateItem, childItem }));
            Assert.That(scope.PredicateRoots, Is.EquivalentTo(new[] { predicateItem, childItem }));
            Assert.That(predicateItem.Parent, Is.SameAs(owner));
            Assert.That(childItem.Parent, Is.Null);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, predicateItem)), Is.False);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, childItem)), Is.False);
            Assert.That(presentation.Find(whenTrue.uuid).Parent, Is.Null);
            Assert.That(presentation.Roots.Any(item => item == presentation.Find(whenTrue.uuid)), Is.True);
            Assert.That(topology.FindNode(predicate.uuid).Position, Is.EqualTo(predicateStoredPosition));
            Assert.That(topology.FindNode(predicateChild.uuid).Position, Is.EqualTo(childStoredPosition));

            Vector2 predicatePosition = predicateItem.Position;
            Vector2 childPosition = childItem.Position;
            Vector2 delta = new(32f, 48f);
            topology.FindNode(condition.uuid).Position += delta;
            presentation.MoveRoot(condition.uuid, owner.Position + delta);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(predicateItem.Position, Is.EqualTo(predicatePosition + delta));
            Assert.That(childItem.Position, Is.EqualTo(childPosition + delta));
        }

        /// <summary>Verifies nested Conditions and Services remain inside the outer predicate container.</summary>
        [Test]
        public void Presentation_ConditionContainsNestedConditionAndServiceSubtrees()
        {
            Condition outer = Node<Condition>("Outer");
            TestHost predicate = Node<TestHost>("Predicate Host");
            Condition nested = Node<Condition>("Nested");
            TestNode nestedPredicate = Node<TestNode>("Nested Predicate");
            TestNode nestedTrue = Node<TestNode>("Nested True");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            TestNode outerTrue = Node<TestNode>("Outer True");
            outer.condition = predicate.ToReference();
            outer.trueNode = outerTrue.ToReference();
            predicate.children = new[] { nested.ToReference() };
            predicate.services = new List<NodeReference> { service.ToReference() };
            nested.condition = nestedPredicate.ToReference();
            nested.trueNode = nestedTrue.ToReference();
            service.child = serviceChild.ToReference();
            BehaviourTreeData tree = Tree(
                outer,
                predicate,
                nested,
                nestedPredicate,
                nestedTrue,
                service,
                serviceChild,
                outerTrue);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);

            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem owner = presentation.Find(outer.uuid);
            GraphConditionScope scope = owner.ConditionScope;
            UUID[] expectedMembers =
            {
                predicate.uuid,
                nested.uuid,
                nestedPredicate.uuid,
                nestedTrue.uuid,
                service.uuid,
                serviceChild.uuid,
            };
            Assert.That(scope.PredicateMembers.Select(item => item.TargetUUID), Is.EquivalentTo(expectedMembers));
            Assert.That(scope.PredicateRoots.Any(item => ReferenceEquals(item, presentation.Find(predicate.uuid))), Is.True);
            Assert.That(scope.PredicateRoots.Any(item => ReferenceEquals(item, presentation.Find(nestedPredicate.uuid))), Is.False);
            Assert.That(presentation.Find(nestedPredicate.uuid).Parent, Is.SameAs(presentation.Find(nested.uuid)));
            Assert.That(presentation.Roots.Any(item => expectedMembers.Contains(item.TargetUUID)), Is.False);
            Assert.That(presentation.Roots.Any(item => ReferenceEquals(item, presentation.Find(outerTrue.uuid))), Is.True);

            Rect ownerBounds = new(owner.Position, owner.Size);
            foreach (GraphPresentationItem member in scope.PredicateMembers)
            {
                Rect memberBounds = new(member.Position, member.Size);
                Assert.That(ownerBounds.Overlaps(memberBounds), Is.True, member.Node.DisplayName);
            }
        }

        /// <summary>Verifies compact decorators and leaves use the shared small presentation footprint.</summary>
        [Test]
        public void Presentation_CompactNodesUseSmallFootprintAndKeepOnlyDecoratorPorts()
        {
            Always always = Node<Always>("Always");
            Inverter inverter = Node<Inverter>("Inverter");
            Aethiumian.AI.Nodes.Boolean boolean = Node<Aethiumian.AI.Nodes.Boolean>("Boolean");
            Constant constant = Node<Constant>("Constant");
            TestNode child = Node<TestNode>("Child");
            always.node = child.ToReference();
            inverter.node = child.ToReference();
            GraphTopology topology = GraphTopologyBuilder.Build(Tree(always, inverter, boolean, constant, child));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(topology, presentation, includeRawReferences: false);

            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(always.uuid)), Is.EqualTo(GraphPresentationMetrics.CompactNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(inverter.uuid)), Is.EqualTo(GraphPresentationMetrics.CompactNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(boolean.uuid)), Is.EqualTo(GraphPresentationMetrics.CompactNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(constant.uuid)), Is.EqualTo(GraphPresentationMetrics.CompactNodeSize));
            Assert.That(ports.Any(port => port.Address.OwnerUUID == always.uuid && port.Address.FieldName == nameof(Always.node)), Is.True);
            Assert.That(ports.Any(port => port.Address.OwnerUUID == inverter.uuid && port.Address.FieldName == nameof(Inverter.node)), Is.True);
            Assert.That(ports.Any(port => port.Address.OwnerUUID == boolean.uuid), Is.False);
            Assert.That(ports.Any(port => port.Address.OwnerUUID == constant.uuid), Is.False);
        }

        /// <summary>Verifies a unique decorator chain derives compact badge positions from its real child.</summary>
        [Test]
        public void Presentation_DecoratorStackAttachesBadgesAboveRealChildWithoutRewritingDescriptors()
        {
            Inverter outer = Node<Inverter>("Outer");
            Always inner = Node<Always>("Inner");
            TestNode child = Node<TestNode>("Child");
            outer.node = inner.ToReference();
            inner.node = child.ToReference();
            BehaviourTreeData tree = Tree(outer, inner, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphNodeDescriptor outerNode = topology.FindNode(outer.uuid);
            GraphNodeDescriptor innerNode = topology.FindNode(inner.uuid);
            GraphNodeDescriptor childNode = topology.FindNode(child.uuid);
            outerNode.Position = new Vector2(900f, 700f);
            innerNode.Position = new Vector2(-400f, 300f);
            childNode.Position = new Vector2(120f, 240f);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(outer.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor.TargetUUID, Is.EqualTo(child.uuid));
            Assert.That(stack.Badges.Select(item => item.TargetUUID), Is.EqualTo(new[] { outer.uuid, inner.uuid }));
            Assert.That(presentation.Find(inner.uuid).Position.y + presentation.Find(inner.uuid).Size.y,
                Is.EqualTo(presentation.Find(child.uuid).Position.y).Within(0.01f));
            Assert.That(presentation.Find(outer.uuid).Position.y + presentation.Find(outer.uuid).Size.y,
                Is.EqualTo(presentation.Find(inner.uuid).Position.y).Within(0.01f));
            Assert.That(outerNode.Position, Is.EqualTo(new Vector2(900f, 700f)));
            Assert.That(innerNode.Position, Is.EqualTo(new Vector2(-400f, 300f)));
            Assert.That(childNode.Position, Is.EqualTo(new Vector2(120f, 240f)));
        }

        /// <summary>Verifies a child with another structural owner never becomes a decorator attachment anchor.</summary>
        [Test]
        public void Presentation_DecoratorStackRejectsSharedStructuralChild()
        {
            Inverter inverter = Node<Inverter>("Inverter");
            TestHost otherParent = Node<TestHost>("Other Parent");
            TestNode child = Node<TestNode>("Child");
            inverter.node = child.ToReference();
            otherParent.children = new[] { child.ToReference() };

            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(inverter, otherParent, child)));

            Assert.That(presentation.DecoratorStacks, Is.Empty);
            Assert.That(presentation.FindDecoratorStack(inverter.uuid), Is.Null);
            Assert.That(presentation.FindDecoratorStack(child.uuid), Is.Null);
        }

        /// <summary>Verifies visual-family fallbacks preserve the requested composite identities.</summary>
        [Test]
        public void GraphAppearance_CompositeFamiliesUseDistinctFallbackStrokes()
        {
            GraphCanvasAppearance appearance = new();
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Sequence>("Sequence")), Is.EqualTo(GraphVisualFamily.Sequence));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Loop>("Loop")), Is.EqualTo(GraphVisualFamily.Loop));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Condition>("Condition")), Is.EqualTo(GraphVisualFamily.Condition));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Decision>("Decision")), Is.EqualTo(GraphVisualFamily.Decision));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Probability>("Probability")), Is.EqualTo(GraphVisualFamily.Probability));
            Assert.That(GraphCanvasAppearance.GetFamily(Node<Parallel>("Parallel")), Is.EqualTo(GraphVisualFamily.Parallel));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Condition), Is.Not.EqualTo(appearance.GetFamilyStroke(GraphVisualFamily.Decision)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Loop), Is.EqualTo(new Color(71f / 255f, 209f / 255f, 184f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Condition), Is.EqualTo(new Color(184f / 255f, 122f / 255f, 235f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Decision), Is.EqualTo(new Color(126f / 255f, 138f / 255f, 242f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Probability), Is.EqualTo(new Color(232f / 255f, 111f / 255f, 154f / 255f, 1f)));
            Assert.That(appearance.GetFamilyStroke(GraphVisualFamily.Parallel), Is.EqualTo(new Color(89f / 255f, 168f / 255f, 242f / 255f, 1f)));
            Assert.That(appearance.GetFamilyFill(GraphVisualFamily.Condition, true).a, Is.EqualTo(0.12f));
            Assert.That(appearance.GetFamilyFill(GraphVisualFamily.Condition, false).a, Is.EqualTo(0.08f));
        }

        /// <summary>Verifies shared Service edges and their port use one host source while Service targets remain left-aligned.</summary>
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
                .Where(relation => relation.Kind == GraphPresentationRelationKind.Service && relation.Origin != null)
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

        /// <summary>Verifies Probability-family END relations retain their derived completion anchors.</summary>
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
            GraphPresentationRelation authored = presentation.Relations.Single(relation => relation.Role == GraphPresentationRelationRole.AuthoredReference
                && relation.Kind == GraphPresentationRelationKind.ProbabilityBranch
                && relation.Source.Item == owner);
            GraphPresentationRelation[] completion = presentation.Relations.Where(relation => relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete).ToArray();
            GraphPresentationRelation continuation = presentation.Relations.Single(relation => relation.Source == owner.FlowComplete
                && relation.Target.Item?.Node?.Node is TestNode);

            Assert.That(painted.GetSourceAnchor(authored), Is.EqualTo(painted.GetSourceAnchor(port)));
            Assert.That(completion, Is.Not.Empty);
            Assert.That(completion.All(relation => painted.GetSourceAnchor(relation) == unmodified.GetSourceAnchor(relation)), Is.True);
            Assert.That(completion.All(relation => painted.GetSourceAnchor(relation) != painted.GetSourceAnchor(port)), Is.True);
            Assert.That(continuation.Source, Is.EqualTo(owner.FlowComplete));
            Assert.That(painted.GetSourceAnchor(continuation), Is.EqualTo(unmodified.GetSourceAnchor(continuation)));
        }

        /// <summary>Verifies authored edges can be selected by their rendered curve and cleared without topology changes.</summary>
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
            GraphPresentationRelation relation = presentation.Relations.Single(candidate => candidate.Origin != null);
            Vector2 from = edgeLayer.GetSourceAnchor(relation);
            Vector2 to = GraphPortLayerElement.GetTargetPosition(presentation.Find(child.uuid));

            Assert.That(edgeLayer.SelectAt((from + to) * 0.5f, 8f), Is.True);
            Assert.That(edgeLayer.SelectedRelation, Is.SameAs(relation));
            edgeLayer.ClearEdgeSelection();
            Assert.That(edgeLayer.SelectedRelation, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies the Graph module disconnects the selected occurrence once and rebuilds its snapshot.</summary>
        [Test]
        public void GraphEdges_DisconnectUsesOccurrenceAddressAndRebuildsOnce()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            host.children = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(host, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphEdgeDescriptor selected = module.Topology.Edges.Single(edge => edge.Source.UUID == host.uuid
                && edge.FieldName == nameof(TestHost.children)
                && edge.CollectionIndex == 0);
            EditorUtility.ClearDirty(tree);

            Assert.That(module.Disconnect(selected), Is.True);
            Assert.That(host.children.Select(reference => reference.UUID), Is.EqualTo(new[] { second.uuid }));
            Assert.That(module.Topology.Edges.Count(edge => edge.Source.UUID == host.uuid
                && edge.FieldName == nameof(TestHost.children)), Is.EqualTo(1));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        private static void AssertOrderedPortCount(
            IEnumerable<GraphPortDescriptor> ports,
            UUID ownerUUID,
            string fieldName,
            int expectedCount)
        {
            GraphPortDescriptor[] fieldPorts = ports
                .Where(port => port.Address.OwnerUUID == ownerUUID && port.Address.FieldName == fieldName)
                .ToArray();
            Assert.That(fieldPorts, Has.Length.EqualTo(expectedCount));
            Assert.That(fieldPorts.All(port => port.PresentationMode == GraphPortPresentationMode.Ordered), Is.True);
            Assert.That(fieldPorts.Count(port => port.Operation == GraphPortOperation.Insert), Is.EqualTo(1));
        }

        private static void AssertChainedPorts(
            IReadOnlyList<GraphPortDescriptor> ports,
            GraphEdgeLayerElement edges,
            UUID ownerUUID,
            string fieldName,
            TestNode last,
            GraphPresentation presentation)
        {
            GraphPortDescriptor[] occurrences = ports.Where(port => port.Address.OwnerUUID == ownerUUID
                && port.Address.FieldName == fieldName
                && port.Address.Index >= 0).OrderBy(port => port.Address.Index).ToArray();
            Assert.That(occurrences, Has.Length.EqualTo(2));
            Assert.That(occurrences.All(port => port.AnchorKind == GraphPortAnchorKind.ChainedOutput), Is.True);
            Assert.That(occurrences.All(port => edges.GetSourceAnchor(port) == edges.GetSourceAnchor(port.Relation)), Is.True);

            GraphPortDescriptor append = FindPort(ports, ownerUUID, fieldName, -1);
            GraphPresentationItem lastItem = presentation.Find(last.uuid);
            Assert.That(append.AnchorKind, Is.EqualTo(GraphPortAnchorKind.ChainedOutput));
            Assert.That(edges.GetSourceAnchor(append), Is.EqualTo(
                lastItem.Position + new Vector2(lastItem.Size.x * 0.5f, lastItem.Size.y)));
        }

        private static GraphPortDescriptor FindPort(
            IEnumerable<GraphPortDescriptor> ports,
            UUID ownerUUID,
            string fieldName,
            int index)
        {
            return ports.Single(port => port.Address.OwnerUUID == ownerUUID
                && port.Address.FieldName == fieldName
                && port.Address.Index == index);
        }

        private static void AssertSharedPort(
            IEnumerable<GraphPortDescriptor> ports,
            UUID ownerUUID,
            string fieldName,
            int expectedOrigins,
            GraphPortAnchorKind anchorKind)
        {
            GraphPortDescriptor port = ports.Single(candidate => candidate.Address.OwnerUUID == ownerUUID
                && candidate.Address.FieldName == fieldName);
            Assert.That(port.PresentationMode, Is.EqualTo(GraphPortPresentationMode.Shared));
            Assert.That(port.Operation, Is.EqualTo(GraphPortOperation.Insert));
            Assert.That(port.Origins, Has.Count.EqualTo(expectedOrigins));
            Assert.That(port.AnchorKind, Is.EqualTo(anchorKind));
        }

        /// <summary>Verifies port compatibility queries are read-only and use command-service ownership rules.</summary>
        [Test]
        public void TopologyEdit_CanAssignPortsWithoutDirtyingTree()
        {
            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(host, child);
            EditorUtility.ClearDirty(tree);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult accepted = edits.CanInsert(
                new GraphReferenceAddress(host.uuid, nameof(TestHost.children)), child.uuid);
            GraphTopologyEditResult rejected = edits.CanConnect(
                new GraphReferenceAddress(host.uuid, nameof(ServiceHostNode.services)), child.uuid);

            Assert.That(accepted.Succeeded, Is.True, accepted.Error);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Error, Does.Contain("Service"));
            Assert.That(host.children, Is.Empty);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies compatibility checks reject invalid structural edits without mutating the tree.</summary>
        [Test]
        public void TopologyEdit_CanAssignPortsRejectsStructuralAndCrossTreeViolationsWithoutDirtyingTree()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode child = Node<TestNode>("Child");
            TestNode foreign = Node<TestNode>("Foreign");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.child = child.ToReference();
            child.parent = first.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, child);
            EditorUtility.ClearDirty(tree);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult cycle = edits.CanConnect(
                new GraphReferenceAddress(child.uuid, nameof(TestNode.child)), head.uuid);
            GraphTopologyEditResult secondParent = edits.CanConnect(
                new GraphReferenceAddress(second.uuid, nameof(TestNode.child)), child.uuid);
            GraphTopologyEditResult crossTree = edits.CanConnect(
                new GraphReferenceAddress(second.uuid, nameof(TestNode.child)), foreign.uuid);
            GraphTopologyEditResult occupied = edits.CanConnect(
                new GraphReferenceAddress(first.uuid, nameof(TestNode.child)), second.uuid);
            GraphTopologyEditResult noOp = edits.CanReplace(
                new GraphReferenceAddress(first.uuid, nameof(TestNode.child)), child.uuid);
            GraphTopologyEditResult raw = edits.CanConnect(
                new GraphReferenceAddress(head.uuid, nameof(TestHost.raw)), child.uuid);

            Assert.That(cycle.Succeeded, Is.False);
            Assert.That(secondParent.Succeeded, Is.False);
            Assert.That(crossTree.Succeeded, Is.False);
            Assert.That(occupied.Succeeded, Is.False);
            Assert.That(noOp.Succeeded, Is.False);
            Assert.That(raw.Succeeded, Is.True, raw.Error);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies weighted collection edits preserve entry weights while replacing and moving occurrences.</summary>
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
            BehaviourTreeData tree = Tree(probability, first, second, replacement);
            GraphTopologyEditService edits = new(tree);
            GraphReferenceAddress firstEntry = new(probability.uuid, nameof(Probability.events), 0);

            GraphTopologyEditResult replaced = edits.Replace(firstEntry, replacement.uuid);
            GraphTopologyEditResult reordered = edits.Reorder(new GraphReferenceAddress(
                probability.uuid,
                nameof(Probability.events),
                1), 0);

            Assert.That(replaced.Succeeded, Is.True, replaced.Error);
            Assert.That(reordered.Succeeded, Is.True, reordered.Error);
            Assert.That(probability.events.Select(entry => entry.reference.UUID), Is.EqualTo(new[] { second.uuid, replacement.uuid }));
            Assert.That(probability.events.Select(entry => entry.weight), Is.EqualTo(new[] { 19, 7 }));
            Assert.That(replacement.parent?.UUID, Is.EqualTo(probability.uuid));
        }

        /// <summary>Verifies Service and Raw commands retain their distinct parent ownership rules.</summary>
        [Test]
        public void TopologyEdit_ServiceOwnsParentWhileRawReferenceDoesNot()
        {
            TestHost host = Node<TestHost>("Host");
            TestService service = Node<TestService>("Service");
            TestNode rawTarget = Node<TestNode>("Raw target");
            BehaviourTreeData tree = Tree(host, service, rawTarget);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult serviceResult = edits.Connect(new GraphReferenceAddress(
                host.uuid,
                nameof(ServiceHostNode.services)), service.uuid);
            GraphTopologyEditResult rawResult = edits.Replace(new GraphReferenceAddress(
                host.uuid,
                nameof(TestHost.raw)), rawTarget.uuid);

            Assert.That(serviceResult.Succeeded, Is.True, serviceResult.Error);
            Assert.That(rawResult.Succeeded, Is.True, rawResult.Error);
            Assert.That(host.services.Select(reference => reference.UUID), Is.EqualTo(new[] { service.uuid }));
            Assert.That(service.parent?.UUID, Is.EqualTo(host.uuid));
            Assert.That(host.raw.UUID, Is.EqualTo(rawTarget.uuid));
            Assert.That(rawTarget.parent?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies an edit cannot introduce a new structural cycle into an otherwise valid tree.</summary>
        [Test]
        public void TopologyEdit_RejectsNewStructuralCycle()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, child);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult result = edits.Replace(new GraphReferenceAddress(
                child.uuid,
                nameof(TestNode.child)), head.uuid);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("cycle"));
            Assert.That(child.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
        }

        /// <summary>Verifies authored structural references cannot share one node instance.</summary>
        [Test]
        public void TopologyEdit_RejectsSecondStructuralParent()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.child = child.ToReference();
            child.parent = first.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, child);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult result = edits.Replace(
                new GraphReferenceAddress(second.uuid, nameof(TestNode.child)), child.uuid);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("structural parent"));
            Assert.That(second.child?.UUID ?? UUID.Empty, Is.EqualTo(UUID.Empty));
            Assert.That(child.parent.UUID, Is.EqualTo(first.uuid));
        }

        /// <summary>Verifies weighted inserts initialize both supported entry types with a constant weight of one.</summary>
        [Test]
        public void TopologyEdit_InsertWeightedEntriesUsesDefaultWeightOne()
        {
            Probability probability = Node<Probability>("Probability");
            PseudoProbability pseudoProbability = Node<PseudoProbability>("Pseudo Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(probability, pseudoProbability, first, second);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult probabilityResult = edits.Insert(
                new GraphReferenceAddress(probability.uuid, nameof(Probability.events)), 0, first.uuid);
            GraphTopologyEditResult pseudoResult = edits.Insert(
                new GraphReferenceAddress(pseudoProbability.uuid, nameof(PseudoProbability.events)), 0, second.uuid);

            Assert.That(probabilityResult.Succeeded, Is.True, probabilityResult.Error);
            Assert.That(pseudoResult.Succeeded, Is.True, pseudoResult.Error);
            Assert.That(probability.events, Has.Length.EqualTo(1));
            Assert.That(probability.events[0].reference.UUID, Is.EqualTo(first.uuid));
            Assert.That(probability.events[0].weight, Is.EqualTo(1));
            Assert.That(pseudoProbability.events, Has.Length.EqualTo(1));
            Assert.That(pseudoProbability.events[0].reference.UUID, Is.EqualTo(second.uuid));
            Assert.That(pseudoProbability.events[0].weight.IsConstant, Is.True);
            Assert.That((int)pseudoProbability.events[0].weight, Is.EqualTo(1));
        }

        /// <summary>Verifies replacing and reordering PseudoProbability entries preserve variable-weight metadata.</summary>
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
            BehaviourTreeData tree = Tree(probability, first, second, replacement);
            tree.variables.Add(dynamicWeight);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult replaced = edits.Replace(
                new GraphReferenceAddress(probability.uuid, nameof(PseudoProbability.events), 0), replacement.uuid);
            GraphTopologyEditResult reordered = edits.Reorder(
                new GraphReferenceAddress(probability.uuid, nameof(PseudoProbability.events), 0), 1);

            Assert.That(replaced.Succeeded, Is.True, replaced.Error);
            Assert.That(reordered.Succeeded, Is.True, reordered.Error);
            Assert.That(probability.events.Select(entry => entry.reference.UUID), Is.EqualTo(new[] { second.uuid, replacement.uuid }));
            Assert.That(probability.events[1].weight.IsConstant, Is.False);
            Assert.That(probability.events[1].weight.UUID, Is.EqualTo(dynamicWeight.UUID));
        }

        /// <summary>Verifies rejected occupied and no-op commands leave the tree clean.</summary>
        [Test]
        public void TopologyEdit_RejectedOccupiedAndNoOpCommandsDoNotDirtyTree()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode child = Node<TestNode>("Child");
            head.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            EditorUtility.ClearDirty(tree);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult occupied = edits.Connect(
                new GraphReferenceAddress(head.uuid, nameof(TestNode.child)), child.uuid);
            GraphTopologyEditResult noOp = edits.Replace(
                new GraphReferenceAddress(head.uuid, nameof(TestNode.child)), child.uuid);

            Assert.That(occupied.Succeeded, Is.False);
            Assert.That(noOp.Succeeded, Is.False);
            Assert.That(head.child.UUID, Is.EqualTo(child.uuid));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies Service collections reject ordinary nodes without mutation.</summary>
        [Test]
        public void TopologyEdit_ServiceSlotRejectsNonServiceTarget()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);
            EditorUtility.ClearDirty(tree);

            GraphTopologyEditResult result = new GraphTopologyEditService(tree).Connect(
                new GraphReferenceAddress(head.uuid, nameof(ServiceHostNode.services)), child.uuid);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("Service"));
            Assert.That(head.services, Is.Null.Or.Empty);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies disconnect can repair one edge of an already-authored structural cycle.</summary>
        [Test]
        public void TopologyEdit_DisconnectExistingCycleSucceeds()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost child = Node<TestHost>("Child");
            head.children = new[] { child.ToReference() };
            child.children = new[] { head.ToReference() };
            head.parent = child.ToReference();
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            tree.Relink();

            GraphTopologyEditResult result = new GraphTopologyEditService(tree).Disconnect(
                new GraphReferenceAddress(child.uuid, nameof(TestHost.children), 0));

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(child.children, Is.Empty);
            Assert.That(head.parent.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));
        }

        /// <summary>Verifies reconciliation keeps the existing parent when invalid authored data has multiple incoming owners.</summary>
        [Test]
        public void TopologyEdit_MultipleIncomingOwnersKeepExistingParentFallback()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode shared = Node<TestNode>("Shared");
            TestNode added = Node<TestNode>("Added");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.child = shared.ToReference();
            second.child = shared.ToReference();
            shared.parent = first.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, shared, added);

            GraphTopologyEditResult result = new GraphTopologyEditService(tree).Connect(
                new GraphReferenceAddress(head.uuid, nameof(TestHost.children)), added.uuid);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(shared.parent.UUID, Is.EqualTo(first.uuid));
            Assert.That(added.parent.UUID, Is.EqualTo(head.uuid));
        }

        /// <summary>Verifies topology commands participate in Unity Undo and Redo.</summary>
        [Test]
        public void TopologyEdit_UndoRedoRestoresAuthoredReferenceAndParent()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);
            GraphTopologyEditService edits = new(tree);

            GraphTopologyEditResult result = edits.Connect(
                new GraphReferenceAddress(head.uuid, nameof(TestNode.child)), child.uuid);
            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(head.child.UUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));

            Undo.PerformUndo();
            Assert.That(head.child.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(child.parent.UUID, Is.EqualTo(UUID.Empty));

            Undo.PerformRedo();
            Assert.That(head.child.UUID, Is.EqualTo(child.uuid));
            Assert.That(child.parent.UUID, Is.EqualTo(head.uuid));
        }

        /// <summary>Verifies rebuilding topology immediately observes a completed command mutation.</summary>
        [Test]
        public void TopologyEdit_RebuiltTopologyReflectsCommandMutation()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode child = Node<TestNode>("Child");
            BehaviourTreeData tree = Tree(head, child);

            GraphTopologyEditResult result = new GraphTopologyEditService(tree).Connect(
                new GraphReferenceAddress(head.uuid, nameof(TestNode.child)), child.uuid);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(result.Succeeded, Is.True, result.Error);
            GraphEdgeDescriptor edge = topology.Edges.Single(candidate => candidate.Source.Node == head);
            Assert.That(edge.Target.Node, Is.SameAs(child));
            Assert.That(topology.FindNode(child.uuid).IsReachable, Is.True);
        }

        /// <summary>Verifies validation reports multi-parent and parent mismatch without treating Raw references as ownership.</summary>
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

            GraphEdgeDescriptor missing = topology.Edges.Single(edge => edge.TargetUUID == missingUUID);
            Assert.That(missing.Target, Is.Null);
            Assert.That(missing.IsMissingTarget, Is.True);
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

            Assert.That(topology.Edges.Select(edge => edge.Label), Is.EqualTo(new[] { "events [0] (3)", "events [1] (7)" }));
            Assert.That(topology.Edges.Select(edge => edge.Target.Node), Is.EqualTo(new TreeNode[] { first, second }));
        }

        [Test]
        public void Build_EmptyReferencesAreNotReportedAsMissingTargets()
        {
            TestNode head = Node<TestNode>("Head");
            head.child = NodeReference.Empty;
            head.raw = RawNodeReference.Empty;
            BehaviourTreeData tree = Tree(head);

            GraphTopology topology = GraphTopologyBuilder.Build(tree, includeRawReferences: true);

            Assert.That(topology.Edges, Is.Empty);
            Assert.That(topology.Nodes[0].HasWarning, Is.False);
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

        [Test]
        public void AutoLayout_RawReferencesDoNotAffectPositions()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            head.raw = new RawNodeReference { UUID = child.uuid };
            BehaviourTreeData tree = Tree(head, child);
            GraphTopology hidden = GraphTopologyBuilder.Build(tree);
            GraphTopology shown = GraphTopologyBuilder.Build(tree, includeRawReferences: true);

            GraphLayoutResolver.ApplyAutoLayout(tree, hidden);
            GraphLayoutResolver.ApplyAutoLayout(tree, shown);

            Assert.That(shown.FindNode(head.uuid).Position, Is.EqualTo(hidden.FindNode(head.uuid).Position));
            Assert.That(shown.FindNode(child.uuid).Position, Is.EqualTo(hidden.FindNode(child.uuid).Position));
        }

        [Test]
        public void AutoLayout_UsesDeclarationOrderAndSubtreeWidth()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode grandchild = Node<TestNode>("Grandchild");
            head.children = new[] { first.ToReference(), second.ToReference() };
            first.child = grandchild.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, grandchild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor headNode = topology.FindNode(head.uuid);
            GraphNodeDescriptor firstNode = topology.FindNode(first.uuid);
            GraphNodeDescriptor secondNode = topology.FindNode(second.uuid);
            GraphNodeDescriptor grandchildNode = topology.FindNode(grandchild.uuid);
            Assert.That(firstNode.Position.x, Is.LessThan(secondNode.Position.x));
            Assert.That(firstNode.Position.y, Is.EqualTo(secondNode.Position.y));
            Assert.That(grandchildNode.Position.y, Is.GreaterThan(firstNode.Position.y));
            Assert.That(headNode.Position.y, Is.LessThan(firstNode.Position.y));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        [Test]
        public void AutoLayout_AttachesServiceSubtreeBesideHost()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = serviceChild.ToReference();
            BehaviourTreeData tree = Tree(head, service, serviceChild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            Assert.That(topology.FindNode(service.uuid).Position.x, Is.GreaterThan(topology.FindNode(head.uuid).Position.x));
            Assert.That(topology.FindNode(serviceChild.uuid).Position.y, Is.GreaterThan(topology.FindNode(service.uuid).Position.y));
            Assert.That(topology.FindNode(serviceChild.uuid).IsReachable, Is.True);
        }

        [Test]
        public void Presentation_ServiceOwnsOneScopeWithItsStructuralSubtree()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(head, service, child)));
            GraphPresentationLayout.Layout(presentation);

            GraphServiceScope scope = presentation.ServiceScopes.Single();
            Assert.That(scope.Host.TargetUUID, Is.EqualTo(head.uuid));
            Assert.That(scope.Owner.TargetUUID, Is.EqualTo(service.uuid));
            Assert.That(scope.Members.Select(item => item.TargetUUID), Is.EquivalentTo(new[] { service.uuid, child.uuid }));
            Assert.That(scope.Bounds.Contains(scope.Owner.Position), Is.True);
            Assert.That(scope.Bounds.Contains(presentation.Find(child.uuid).Position), Is.True);
        }

        [Test]
        public void Presentation_SharedServiceUsesFirstHostScopeAndMarksAdditionalHost()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost other = Node<TestHost>("Other Host");
            TestService service = Node<TestService>("Shared Service");
            head.children = new[] { other.ToReference() };
            head.services = new List<NodeReference> { service.ToReference() };
            other.services = new List<NodeReference> { service.ToReference() };
            GraphPresentation presentation = GraphPresentationBuilder.Build(
                GraphTopologyBuilder.Build(Tree(head, other, service)));

            GraphServiceScope scope = presentation.ServiceScopes.Single();
            Assert.That(scope.Host.TargetUUID, Is.EqualTo(head.uuid));
            Assert.That(scope.AdditionalHostCount, Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.Service && relation.TargetUUID == service.uuid), Is.EqualTo(2));
        }

        [Test]
        public void Presentation_MissingServiceCreatesNonPersistentPlaceholder()
        {
            TestHost head = Node<TestHost>("Head");
            UUID missing = UUID.NewUUID();
            head.services = new List<NodeReference> { new(missing) };
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(Tree(head)));
            GraphPresentationLayout.Layout(presentation);

            GraphPresentationItem placeholder = presentation.Roots.Single(item => item.ServicePlaceholder != null);
            GraphPresentationRelation relation = presentation.Relations.Single(item => item.Kind == GraphPresentationRelationKind.Service);
            Assert.That(placeholder.TargetUUID, Is.EqualTo(missing));
            Assert.That(placeholder.IsRoot, Is.True);
            Assert.That(relation.Role, Is.EqualTo(GraphPresentationRelationRole.PlaceholderHint));
            Assert.That(presentation.ServiceScopes, Is.Empty);
        }

        [Test]
        public void AutoLayout_StacksServiceSubtreesWithoutOverlap()
        {
            TestHost head = Node<TestHost>("Head");
            TestService firstService = Node<TestService>("First Service");
            TestService secondService = Node<TestService>("Second Service");
            TestNode child = Node<TestNode>("Child");
            TestNode grandchild = Node<TestNode>("Grandchild");
            head.services = new List<NodeReference> { firstService.ToReference(), secondService.ToReference() };
            firstService.child = child.ToReference();
            child.child = grandchild.ToReference();
            BehaviourTreeData tree = Tree(head, firstService, secondService, child, grandchild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor grandchildNode = topology.FindNode(grandchild.uuid);
            GraphNodeDescriptor secondServiceNode = topology.FindNode(secondService.uuid);
            float grandchildBottom = grandchildNode.Position.y + GraphLayoutResolver.GetNodeSize(grandchildNode).y;
            Assert.That(secondServiceNode.Position.y, Is.GreaterThan(grandchildBottom));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        /// <summary>
        /// Verifies that a Service subtree reserves horizontal space before adjacent main branches are placed.
        /// </summary>
        [Test]
        public void AutoLayout_ServiceEnvelopeDoesNotOverlapAdjacentMainBranch()
        {
            TestHost head = Node<TestHost>("Head");
            TestHost left = Node<TestHost>("Left Host");
            TestNode right = Node<TestNode>("Right Branch");
            TestService service = Node<TestService>("Left Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            TestNode serviceGrandchild = Node<TestNode>("Service Grandchild");
            head.children = new[] { left.ToReference(), right.ToReference() };
            left.services = new List<NodeReference> { service.ToReference() };
            service.child = serviceChild.ToReference();
            serviceChild.child = serviceGrandchild.ToReference();
            BehaviourTreeData tree = Tree(head, left, right, service, serviceChild, serviceGrandchild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor rightNode = topology.FindNode(right.uuid);
            GraphNodeDescriptor serviceChildNode = topology.FindNode(serviceChild.uuid);
            float serviceSubtreeRight = serviceChildNode.Position.x + GraphLayoutResolver.GetNodeSize(serviceChildNode).x;
            Assert.That(rightNode.Position.x, Is.GreaterThan(serviceSubtreeRight));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        /// <summary>
        /// Verifies that wide Probability branches reserve Service lanes owned by different hosts.
        /// </summary>
        [Test]
        public void AutoLayout_ProbabilityBranchesReserveServiceEnvelopes()
        {
            Probability probability = Node<Probability>("Probability");
            TestHost first = Node<TestHost>("First Branch");
            TestHost second = Node<TestHost>("Second Branch");
            TestHost third = Node<TestHost>("Third Branch");
            TestHost fourth = Node<TestHost>("Fourth Branch");
            TestService firstService = Node<TestService>("First Service");
            TestService thirdService = Node<TestService>("Third Service");
            TestNode firstServiceChild = Node<TestNode>("First Service Child");
            TestNode thirdServiceChild = Node<TestNode>("Third Service Child");
            probability.events = new[]
            {
                new Probability.EventWeight { reference = first.ToReference(), weight = 1 },
                new Probability.EventWeight { reference = second.ToReference(), weight = 2 },
                new Probability.EventWeight { reference = third.ToReference(), weight = 3 },
                new Probability.EventWeight { reference = fourth.ToReference(), weight = 4 },
            };
            first.services = new List<NodeReference> { firstService.ToReference() };
            third.services = new List<NodeReference> { thirdService.ToReference() };
            firstService.child = firstServiceChild.ToReference();
            thirdService.child = thirdServiceChild.ToReference();
            BehaviourTreeData tree = Tree(
                probability,
                first,
                second,
                third,
                fourth,
                firstService,
                thirdService,
                firstServiceChild,
                thirdServiceChild);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            Assert.That(topology.FindNode(first.uuid).Position.x, Is.LessThan(topology.FindNode(second.uuid).Position.x));
            Assert.That(topology.FindNode(second.uuid).Position.x, Is.LessThan(topology.FindNode(third.uuid).Position.x));
            Assert.That(topology.FindNode(third.uuid).Position.x, Is.LessThan(topology.FindNode(fourth.uuid).Position.x));
            Assert.That(
                GraphLayoutResolver.FindPresentationOverlaps(GraphPresentationBuilder.Build(topology)),
                Is.Empty);
        }

        [Test]
        public void AutoLayout_MultipleParentsAndCycleTerminateWithOnePositionPerUuid()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode left = Node<TestNode>("Left");
            TestNode right = Node<TestNode>("Right");
            TestNode shared = Node<TestNode>("Shared");
            head.children = new[] { left.ToReference(), right.ToReference() };
            left.child = shared.ToReference();
            right.child = shared.ToReference();
            shared.child = head.ToReference();
            BehaviourTreeData tree = Tree(head, left, right, shared);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.DoesNotThrow(() => GraphLayoutResolver.ApplyAutoLayout(tree, topology));
            Assert.That(topology.Nodes.Select(node => node.Position).All(position => float.IsFinite(position.x) && float.IsFinite(position.y)), Is.True);
            Assert.That(topology.Edges.Count(edge => edge.Target?.Node == shared), Is.EqualTo(2));
        }

        /// <summary>
        /// Verifies that sequence presentation relations determine layout order.
        /// </summary>
        [Test]
        public void AutoLayout_SequenceEventsFormVerticalContinuationChain()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            sequence.events = new[] { first.ToReference(), second.ToReference(), third.ToReference() };
            BehaviourTreeData tree = Tree(sequence, first, second, third);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor sequenceNode = topology.FindNode(sequence.uuid);
            GraphNodeDescriptor firstNode = topology.FindNode(first.uuid);
            GraphNodeDescriptor secondNode = topology.FindNode(second.uuid);
            GraphNodeDescriptor thirdNode = topology.FindNode(third.uuid);
            Assert.That(firstNode.Position.y, Is.GreaterThan(sequenceNode.Position.y));
            Assert.That(secondNode.Position.y, Is.GreaterThan(firstNode.Position.y));
            Assert.That(thirdNode.Position.y, Is.GreaterThan(secondNode.Position.y));
            Assert.That(firstNode.Position.x, Is.EqualTo(secondNode.Position.x));
            Assert.That(secondNode.Position.x, Is.EqualTo(thirdNode.Position.x));
        }

        /// <summary>
        /// Verifies that an outer Sequence continues below an inner Sequence completion marker.
        /// </summary>
        [Test]
        public void AutoLayout_NestedSequenceCompletionPrecedesOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode first = Node<TestNode>("A");
            Sequence inner = Node<Sequence>("Inner");
            TestNode innerFirst = Node<TestNode>("B");
            TestNode innerLast = Node<TestNode>("C");
            TestNode outerLast = Node<TestNode>("D");
            outer.events = new[] { first.ToReference(), inner.ToReference(), outerLast.ToReference() };
            inner.events = new[] { innerFirst.ToReference(), innerLast.ToReference() };
            BehaviourTreeData tree = Tree(outer, first, inner, innerFirst, innerLast, outerLast);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphSequenceScope innerScope = presentation.Find(inner.uuid).SequenceScope;
            Assert.That(topology.FindNode(innerFirst.uuid).Position.y, Is.GreaterThan(topology.FindNode(inner.uuid).Position.y));
            Assert.That(topology.FindNode(innerLast.uuid).Position.y, Is.GreaterThan(topology.FindNode(innerFirst.uuid).Position.y));
            Assert.That(innerScope.CompletionPosition.y, Is.GreaterThan(topology.FindNode(innerLast.uuid).Position.y));
            Assert.That(topology.FindNode(outerLast.uuid).Position.y, Is.GreaterThan(innerScope.CompletionPosition.y));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Count, Is.EqualTo(topology.Nodes.Count));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>
        /// Verifies that the embedded predicate is measured by its owning Condition only.
        /// </summary>
        [Test]
        public void AutoLayout_ConditionUsesCompoundBoundsAndDoesNotPlaceEmbeddedPredicate()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, trueNode, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            Vector2 embeddedSentinel = new(137f, 211f);
            topology.FindNode(predicate.uuid).Position = embeddedSentinel;

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);
            GraphNodeDescriptor conditionNode = topology.FindNode(condition.uuid);
            GraphNodeDescriptor trueDescriptor = topology.FindNode(trueNode.uuid);
            GraphNodeDescriptor falseDescriptor = topology.FindNode(falseNode.uuid);
            Assert.That(topology.FindNode(predicate.uuid).Position, Is.EqualTo(embeddedSentinel));
            Assert.That(trueDescriptor.Position.y, Is.EqualTo(falseDescriptor.Position.y));
            Assert.That(trueDescriptor.Position.y, Is.GreaterThanOrEqualTo(conditionNode.Position.y + conditionItem.Size.y));
            Assert.That(trueDescriptor.Position.x, Is.LessThan(falseDescriptor.Position.x));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>
        /// Verifies that deeply nested Sequence completion markers remain ordered and collision-free.
        /// </summary>
        [Test]
        public void AutoLayout_DeepSequenceCompletionScopesRemainCollisionFree()
        {
            Sequence outer = Node<Sequence>("Outer");
            Sequence middle = Node<Sequence>("Middle");
            Sequence inner = Node<Sequence>("Inner");
            TestNode leaf = Node<TestNode>("Leaf");
            TestNode middleNext = Node<TestNode>("Middle Next");
            TestNode outerNext = Node<TestNode>("Outer Next");
            outer.events = new[] { middle.ToReference(), outerNext.ToReference() };
            middle.events = new[] { inner.ToReference(), middleNext.ToReference() };
            inner.events = new[] { leaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, middle, inner, leaf, middleNext, outerNext);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphSequenceScope innerScope = presentation.Find(inner.uuid).SequenceScope;
            GraphSequenceScope middleScope = presentation.Find(middle.uuid).SequenceScope;
            Assert.That(innerScope.CompletionPosition.y, Is.LessThan(topology.FindNode(middleNext.uuid).Position.y));
            Assert.That(middleScope.CompletionPosition.y, Is.LessThan(topology.FindNode(outerNext.uuid).Position.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>
        /// Verifies that the read-only collision audit reports an intentionally invalid positioned snapshot.
        /// </summary>
        [Test]
        public void CollisionAudit_ReportsOverlappingVisibleCards()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            head.children = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(head, first, second);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                node.Position = Vector2.zero;
            }

            IReadOnlyList<string> overlaps = GraphLayoutResolver.FindPresentationOverlaps(
                GraphPresentationBuilder.Build(topology));

            Assert.That(overlaps, Is.Not.Empty);
            Assert.That(overlaps.Any(overlap => overlap.Contains("First") && overlap.Contains("Second")), Is.True);
        }

        /// <summary>
        /// Verifies that unreachable items cannot expand the graph into one unbounded row.
        /// </summary>
        [Test]
        public void AutoLayout_WrapsUnreachableNodesIntoBoundedRows()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            TestNode fourth = Node<TestNode>("Fourth");
            TestNode fifth = Node<TestNode>("Fifth");
            BehaviourTreeData tree = Tree(head, first, second, third, fourth, fifth);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            Vector2 firstPosition = topology.FindNode(first.uuid).Position;
            Vector2 fifthPosition = topology.FindNode(fifth.uuid).Position;
            Assert.That(fifthPosition.x, Is.EqualTo(firstPosition.x));
            Assert.That(fifthPosition.y, Is.GreaterThan(firstPosition.y));
        }

        [Test]
        public void CommitNodeMove_WritesOneVersionedLayoutAndKeepsImportedPositions()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, child);
            tree.Graph.graphNodes = new List<GraphNode>
            {
                new GraphNode(new Vector2(321f, 654f), 200f, 80f) { uuid = child.uuid },
            };
            UUID staleUUID = UUID.NewUUID();
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(staleUUID, new Vector2(999f, 999f)),
            });
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(window);
            window.Load(tree);
            GraphEditorModule module = new(window);
            module.Attach(CreateDeclaredGraphHost(window));
            GraphNodeDescriptor headDescriptor = module.Topology.FindNode(head.uuid);
            module.MoveNode(headDescriptor, new Vector2(80f, 100f));
            EditorUtility.ClearDirty(tree);

            module.CommitNodeMove();

            Assert.That(tree.GraphLayout, Is.Not.Null);
            Assert.That(tree.GraphLayout.Version, Is.EqualTo(GraphLayoutData.CurrentVersion));
            Assert.That(tree.GraphLayout.TryGetPosition(child.uuid, out Vector2 childPosition), Is.True);
            Assert.That(childPosition, Is.EqualTo(new Vector2(321f, 654f)));
            Assert.That(tree.GraphLayout.TryGetPosition(staleUUID, out _), Is.False);
            Assert.That(tree.GraphLayout.Positions.Count, Is.EqualTo(2));
            Assert.That(EditorUtility.IsDirty(tree), Is.True);
        }

        [Test]
        public void CommitNodeMove_UndoRedoRestoresLayoutWrite()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(window);
            window.Load(tree);
            GraphEditorModule module = new(window);
            module.Attach(CreateDeclaredGraphHost(window));
            module.MoveNode(module.Topology.FindNode(head.uuid), new Vector2(75f, 125f));

            module.CommitNodeMove();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 committed), Is.True);
            Assert.That(committed, Is.EqualTo(new Vector2(75f, 125f)));

            Undo.PerformUndo();
            Assert.That(tree.GraphLayout, Is.Null);

            Undo.PerformRedo();
            Assert.That(tree.GraphLayout.TryGetPosition(head.uuid, out Vector2 redone), Is.True);
            Assert.That(redone, Is.EqualTo(new Vector2(75f, 125f)));
        }

        [Test]
        public void Resolve_VersionOneCoordinatesRemainSupportedWithoutDirtyingTree()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(41f, 73f)),
            });
            typeof(GraphLayoutData).GetField("version", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(tree.GraphLayout, 1);
            EditorUtility.ClearDirty(tree);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.Resolve(tree, topology);

            Assert.That(topology.FindNode(head.uuid).Position, Is.EqualTo(new Vector2(41f, 73f)));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void MoveHost_MovesEnabledServiceScopeAndCommitsVersionTwoOnce()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, service, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Vector2 headStart = module.Topology.FindNode(head.uuid).Position;
            Vector2 serviceStart = module.Topology.FindNode(service.uuid).Position;
            Vector2 childStart = module.Topology.FindNode(child.uuid).Position;
            Vector2 delta = new(37f, 29f);

            module.MoveNode(module.Topology.FindNode(head.uuid), headStart + delta);

            Assert.That(module.Topology.FindNode(service.uuid).Position, Is.EqualTo(serviceStart + delta));
            Assert.That(module.Topology.FindNode(child.uuid).Position, Is.EqualTo(childStart + delta));
            Assert.That(tree.GraphLayout, Is.Null);
            module.CommitNodeMove();
            Assert.That(tree.GraphLayout.Version, Is.EqualTo(2));
            Assert.That(tree.GraphLayout.GetServiceFollowParent(service.uuid), Is.True);
        }

        [Test]
        public void MoveHost_DoesNotMoveDisabledServiceScope()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, service, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.ToggleServiceFollowParent(service.uuid);
            Vector2 serviceStart = module.Topology.FindNode(service.uuid).Position;
            Vector2 childStart = module.Topology.FindNode(child.uuid).Position;
            GraphNodeDescriptor headNode = module.Topology.FindNode(head.uuid);

            module.MoveNode(headNode, headNode.Position + new Vector2(50f, 25f));

            Assert.That(module.Topology.FindNode(service.uuid).Position, Is.EqualTo(serviceStart));
            Assert.That(module.Topology.FindNode(child.uuid).Position, Is.EqualTo(childStart));
            Assert.That(tree.GraphLayout.GetServiceFollowParent(service.uuid), Is.False);
        }

        [Test]
        public void MoveServiceCard_MovesCompleteScopeRegardlessOfFollowSetting()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            TestNode child = Node<TestNode>("Service Child");
            head.services = new List<NodeReference> { service.ToReference() };
            service.child = child.ToReference();
            BehaviourTreeData tree = Tree(head, service, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.ToggleServiceFollowParent(service.uuid);
            GraphNodeDescriptor serviceNode = module.Topology.FindNode(service.uuid);
            Vector2 childStart = module.Topology.FindNode(child.uuid).Position;
            Vector2 delta = new(-30f, 44f);

            module.MoveNode(serviceNode, serviceNode.Position + delta);

            Assert.That(module.Topology.FindNode(child.uuid).Position, Is.EqualTo(childStart + delta));
        }

        [Test]
        public void MoveServiceCard_RespectsNestedServiceFollowSetting()
        {
            TestHost head = Node<TestHost>("Head");
            TestService outerService = Node<TestService>("Outer Service");
            TestHost nestedHost = Node<TestHost>("Nested Host");
            TestService nestedService = Node<TestService>("Nested Service");
            head.services = new List<NodeReference> { outerService.ToReference() };
            outerService.child = nestedHost.ToReference();
            nestedHost.services = new List<NodeReference> { nestedService.ToReference() };
            BehaviourTreeData tree = Tree(head, outerService, nestedHost, nestedService);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            module.ToggleServiceFollowParent(nestedService.uuid);
            GraphNodeDescriptor outer = module.Topology.FindNode(outerService.uuid);
            Vector2 nestedHostStart = module.Topology.FindNode(nestedHost.uuid).Position;
            Vector2 nestedServiceStart = module.Topology.FindNode(nestedService.uuid).Position;
            Vector2 delta = new(28f, 36f);

            module.MoveNode(outer, outer.Position + delta);

            Assert.That(module.Topology.FindNode(nestedHost.uuid).Position, Is.EqualTo(nestedHostStart + delta));
            Assert.That(module.Topology.FindNode(nestedService.uuid).Position, Is.EqualTo(nestedServiceStart));
        }

        /// <summary>Verifies missing custom styles retain the exact safe Painter2D defaults.</summary>
        [Test]
        public void GraphAppearance_MissingCustomStylesUseNonZeroFallbacks()
        {
            GraphCanvasAppearance appearance = new();

            appearance.Resolve(null);

            Assert.That(appearance.HasResolvedCustomStyles, Is.False);
            Assert.That(appearance.FlowEdge, Is.EqualTo(new Color(0.25f, 0.72f, 0.92f, 1f)));
            Assert.That(appearance.NodeLineWidth, Is.EqualTo(1.5f));
            Assert.That(appearance.AuthoredLineWidth, Is.EqualTo(2f));
            Assert.That(appearance.DerivedMarkLength, Is.EqualTo(8f));
            Assert.That(appearance.PlaceholderGapLength, Is.EqualTo(6f));
        }

        /// <summary>Verifies the shell USS resolves once and repaints shared painters without rebuilding graph state.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_ResolvesSharedPainterAppearanceWithoutMutatingGraphState()
        {
            Sequence head = Node<Sequence>("Head");
            TestNode child = Node<TestNode>("Child");
            head.events = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, child);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            GraphEdgeLayerElement edgeLayer = window.rootVisualElement.Q<GraphEdgeLayerElement>();
            GraphSequenceScopeElement scope = window.rootVisualElement.Q<GraphSequenceScopeElement>();
            GraphPresentation presentation = canvas.Presentation;
            canvas.Zoom = 1.2f;
            canvas.Pan = new Vector2(37f, 49f);
            window.SelectedNode = child;
            Vector2 pan = canvas.Pan;
            float zoom = canvas.Zoom;

            Assert.That(canvas.Appearance.HasResolvedCustomStyles, Is.True);
            Assert.That(canvas.Appearance.AuthoredLineWidth, Is.EqualTo(2f));
            Assert.That(edgeLayer.Appearance, Is.SameAs(canvas.Appearance));
            Assert.That(scope.Appearance, Is.SameAs(canvas.Appearance));

            canvas.ResolveAppearance(canvas.customStyle);

            Assert.That(canvas.Presentation, Is.SameAs(presentation));
            Assert.That(canvas.Pan, Is.EqualTo(pan));
            Assert.That(canvas.Zoom, Is.EqualTo(zoom));
            Assert.That(window.SelectedNode, Is.SameAs(child));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphWindow_UsesOneInspectorAndMirrorsNodeSelection()
        {
            Sequence head = Node<Sequence>("Head");
            Sequence child = Node<Sequence>("Child");
            head.events = new[] { child.ToReference() };
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            ToolbarToggle graphTab = window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab");
            graphTab.value = true;

            Assert.That(window.rootVisualElement.Q<VisualElement>("ai-editor-graph-host")
                .Query<IMGUIContainer>().ToList().Count, Is.EqualTo(1));

            GraphNodeElement childElement = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{child.uuid}");
            Assert.That(childElement, Is.Not.Null);
            window.SelectedNode = child;
            Assert.That(childElement.ClassListContains("ai-editor-graph-node-selected"), Is.True);
            List<GraphSequenceScopeElement> scopes = window.rootVisualElement.Query<GraphSequenceScopeElement>().ToList();
            List<GraphFlowCompletionElement> completions = window.rootVisualElement.Query<GraphFlowCompletionElement>().ToList();
            Assert.That(scopes.Count, Is.EqualTo(2));
            Assert.That(scopes.All(scope => scope.pickingMode == PickingMode.Ignore), Is.True);
            Assert.That(completions.Count, Is.EqualTo(2));
            Assert.That(completions.All(completion => completion.pickingMode == PickingMode.Position), Is.True);
        }

        /// <summary>Verifies the first graph frame favors the Head execution context instead of distant unreachable content.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_InitialFrameKeepsHeadContextReadable()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode unreachable = Node<TestNode>("Unreachable");
            head.children = new[] { first.ToReference() };
            first.child = second.ToReference();
            BehaviourTreeData tree = Tree(head, first, second, unreachable);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(first.uuid, new Vector2(0f, 160f)),
                new GraphLayoutEntry(second.uuid, new Vector2(0f, 320f)),
                new GraphLayoutEntry(unreachable.uuid, new Vector2(12000f, 12000f)),
            });
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            Assert.That(canvas.Zoom, Is.GreaterThanOrEqualTo(0.45f));
            Assert.That(canvas.Presentation.Find(unreachable.uuid).Node.IsReachable, Is.False);
            Assert.That(tree.GraphLayout.Positions.Count, Is.EqualTo(4));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies a Sequence Head and its first two authored execution levels are inside the initial viewport.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_InitialFrameContainsSequenceHeadExecutionContext()
        {
            Sequence head = Node<Sequence>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode unreachable = Node<TestNode>("Unreachable");
            head.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(head, first, second, unreachable);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(first.uuid, new Vector2(-240f, 220f)),
                new GraphLayoutEntry(second.uuid, new Vector2(240f, 440f)),
                new GraphLayoutEntry(unreachable.uuid, new Vector2(12000f, 12000f)),
            });
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            AssertPresentationItemsInsideViewport(canvas, head.uuid, first.uuid, second.uuid);
            Assert.That(canvas.Zoom, Is.GreaterThanOrEqualTo(0.45f));
        }

        /// <summary>Verifies a Condition Head compound and both authored branches are inside the initial viewport.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_InitialFrameContainsConditionHeadExecutionContext()
        {
            Condition head = Node<Condition>("Head");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode whenTrue = Node<TestNode>("True");
            TestNode whenFalse = Node<TestNode>("False");
            TestNode unreachable = Node<TestNode>("Unreachable");
            head.condition = predicate.ToReference();
            head.trueNode = whenTrue.ToReference();
            head.falseNode = whenFalse.ToReference();
            BehaviourTreeData tree = Tree(head, predicate, whenTrue, whenFalse, unreachable);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(predicate.uuid, new Vector2(0f, 180f)),
                new GraphLayoutEntry(whenTrue.uuid, new Vector2(-260f, 380f)),
                new GraphLayoutEntry(whenFalse.uuid, new Vector2(260f, 380f)),
                new GraphLayoutEntry(unreachable.uuid, new Vector2(12000f, 12000f)),
            });
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            AssertPresentationItemsInsideViewport(canvas, head.uuid, predicate.uuid, whenTrue.uuid, whenFalse.uuid);
            Assert.That(canvas.Zoom, Is.GreaterThanOrEqualTo(0.45f));
        }

        /// <summary>Verifies detached nodes remain ordinary selectable cards without a presentation-only grouping container.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_DetachedNodesDoNotCreateGrouping()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode unreachable = Node<TestNode>("Unreachable");
            BehaviourTreeData tree = Tree(head, unreachable);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            Assert.That(window.rootVisualElement.Q("ai-editor-graph-unreachable-area"), Is.Null);
            GraphNodeElement node = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{unreachable.uuid}");
            Assert.That(node, Is.Not.Null);
            Assert.That(node.pickingMode, Is.EqualTo(PickingMode.Position));
            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            Assert.That(canvas.Presentation.Find(unreachable.uuid).Node.IsReachable, Is.False);
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies a Service range stays hidden until its owning Service is selected.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_ServiceScopeAppearsOnlyForOwnerSelection()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            head.services = new List<NodeReference> { service.ToReference() };
            BehaviourTreeData tree = Tree(head, service);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphServiceScopeElement scope = window.rootVisualElement.Q<GraphServiceScopeElement>();
            window.SelectedNode = null;
            Assert.That(scope, Is.Not.Null);
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.None));

            window.SelectedNode = service;
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(scope.ClassListContains("ai-editor-graph-service-scope-selected"), Is.True);

            window.SelectedNode = head;
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>
        /// Verifies Condition brackets, fallback cards, and completion markers remain presentation-only.
        /// </summary>
        [UnityTest]
        public IEnumerator GraphWindow_ConditionFallbackElementsAreNonInteractiveAndFollowOwnerSelection()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            condition.condition = predicate.ToReference();
            condition.trueNode = NodeReference.Empty;
            condition.falseNode = NodeReference.Empty;
            BehaviourTreeData tree = Tree(condition, predicate);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;
            window.SelectedNode = null;

            GraphConditionScopeElement scope = window.rootVisualElement.Q<GraphConditionScopeElement>();
            GraphFlowCompletionElement completion = window.rootVisualElement.Query<GraphFlowCompletionElement>()
                .ToList().Single(element => element.Scope.Owner.Node?.Node == condition);
            List<GraphConditionPlaceholderElement> placeholders = window.rootVisualElement
                .Query<GraphConditionPlaceholderElement>().ToList();

            Assert.That(scope, Is.Not.Null);
            Assert.That(scope.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(completion.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(placeholders.Count, Is.EqualTo(2));
            Assert.That(placeholders.All(placeholder => placeholder.pickingMode == PickingMode.Ignore), Is.True);
            EditorUtility.ClearDirty(tree);
            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            Vector2 panBeforeClick = canvas.Pan;
            VisualElement picked = completion.panel.Pick(completion.worldBound.center);
            Assert.That(picked, Is.SameAs(completion));
            Event systemEvent = new()
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = completion.worldBound.center,
            };
            using PointerDownEvent pointerDown = PointerDownEvent.GetPooled(systemEvent);
            picked.SendEvent(pointerDown);
            Assert.That(window.SelectedNode, Is.SameAs(condition));
            Assert.That(scope.ClassListContains("ai-editor-graph-condition-scope-selected"), Is.True);
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(completion.ClassListContains("ai-editor-graph-flow-end-selected"), Is.True);
            Assert.That(canvas.Pan, Is.EqualTo(panBeforeClick));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>
        /// Verifies completion markers reserve adaptive space while retaining a bounded presentation footprint.
        /// </summary>
        [Test]
        public void Presentation_FlowCompletionWidthAdaptsAndClamps()
        {
            Vector2 shortSize = GraphPresentationMetrics.GetFlowCompletionSize("Flow");
            Vector2 longSize = GraphPresentationMetrics.GetFlowCompletionSize(new string('W', 100));
            Vector2 wideCharacterSize = GraphPresentationMetrics.GetFlowCompletionSize("循环条件节点名称");

            Assert.That(shortSize.x, Is.EqualTo(GraphPresentationMetrics.FlowCompletionMinimumWidth));
            Assert.That(longSize.x, Is.EqualTo(GraphPresentationMetrics.FlowCompletionMaximumWidth));
            Assert.That(wideCharacterSize.x, Is.GreaterThan(shortSize.x));
            Assert.That(shortSize.y, Is.EqualTo(GraphPresentationMetrics.FlowCompletionHeight));
        }

        /// <summary>
        /// Verifies Loop scope controls and placeholders remain presentation-only.
        /// </summary>
        [Test]
        public void GraphWindow_LoopControlsAreNonInteractiveAndFollowOwnerSelection()
        {
            Loop loop = Node<Loop>("Loop");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = NodeReference.Empty;
            loop.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = Tree(loop);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            window.SelectedNode = null;

            GraphLoopScopeElement scope = window.rootVisualElement.Q<GraphLoopScopeElement>();
            GraphFlowCompletionElement completion = window.rootVisualElement.Query<GraphFlowCompletionElement>()
                .ToList().Single(element => element.Scope.Owner.Node?.Node == loop);
            List<GraphLoopPlaceholderElement> placeholders = window.rootVisualElement
                .Query<GraphLoopPlaceholderElement>().ToList();
            List<GraphLoopJunctionElement> junctions = window.rootVisualElement
                .Query<GraphLoopJunctionElement>().ToList();

            Assert.That(scope, Is.Not.Null);
            Assert.That(scope.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(completion.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(placeholders.Count, Is.EqualTo(2));
            Assert.That(placeholders.All(element => element.pickingMode == PickingMode.Ignore), Is.True);
            Assert.That(junctions.Count, Is.Zero);
            Assert.That(junctions.All(element => element.pickingMode == PickingMode.Ignore), Is.True);
            EditorUtility.ClearDirty(tree);
            window.SelectedNode = loop;
            Assert.That(scope.ClassListContains("ai-editor-graph-loop-body-frame-selected"), Is.True);
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(completion.ClassListContains("ai-editor-graph-flow-end-selected"), Is.True);
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>
        /// Verifies the real UI Toolkit wheel event path against panel and viewport coordinates.
        /// </summary>
        [UnityTest]
        public IEnumerator GraphCanvas_WheelZoomKeepsPointerGraphCoordinate()
        {
            TestHost head = Node<TestHost>("Head");
            BehaviourTreeData tree = Tree(head);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            VisualElement content = canvas.Q<VisualElement>("ai-editor-graph-content");
            Assert.That(canvas.layout.width, Is.GreaterThan(0f));
            Assert.That(canvas.layout.height, Is.GreaterThan(0f));
            Assert.That(content.resolvedStyle.transformOrigin.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(content.resolvedStyle.transformOrigin.y, Is.EqualTo(0f).Within(0.001f));

            canvas.Zoom = 1f;
            canvas.Pan = new Vector2(120f, 80f);
            Vector2 viewportPoint = new(canvas.layout.width * 0.35f, canvas.layout.height * 0.4f);
            Vector2 graphPoint = canvas.ViewportToGraph(viewportPoint);
            Event systemEvent = new()
            {
                type = EventType.ScrollWheel,
                mousePosition = canvas.LocalToWorld(viewportPoint),
                delta = new Vector2(0f, -3f),
            };
            using WheelEvent wheel = WheelEvent.GetPooled(systemEvent);
            canvas.SendEvent(wheel);

            Assert.That(canvas.Zoom, Is.GreaterThan(1f));
            Assert.That(Vector2.Distance(canvas.GraphToViewport(graphPoint), viewportPoint), Is.LessThan(0.01f));
        }

        /// <summary>
        /// Verifies that Fit All and Frame Selected use resolved presentation bounds.
        /// </summary>
        [UnityTest]
        public IEnumerator GraphCanvas_FitAndFrameRemainInsideResolvedViewport()
        {
            Sequence head = Node<Sequence>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            head.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(head, first, second);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            canvas.FitAll();
            Rect allBounds = canvas.PresentationBounds;
            Vector2 fittedMin = canvas.GraphToViewport(allBounds.min);
            Vector2 fittedMax = canvas.GraphToViewport(allBounds.max);
            Assert.That(fittedMin.x, Is.GreaterThanOrEqualTo(0f));
            Assert.That(fittedMin.y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(fittedMax.x, Is.LessThanOrEqualTo(canvas.layout.width));
            Assert.That(fittedMax.y, Is.LessThanOrEqualTo(canvas.layout.height));

            window.SelectedNode = head;
            canvas.FrameSelected();
            Rect selectedBounds = GraphPresentationLayout.GetBounds(canvas.Presentation.Find(head.uuid));
            Vector2 selectedCenter = canvas.GraphToViewport(selectedBounds.center);
            Vector2 viewportCenter = new(canvas.layout.width * 0.5f, canvas.layout.height * 0.5f);
            Assert.That(Vector2.Distance(selectedCenter, viewportCenter), Is.LessThan(0.01f));
        }

        [Test]
        public void Presentation_UsesSequenceOrderAndNestedConditionSlots()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            sequence.events = new[] { first.ToReference(), condition.ToReference() };
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(sequence, first, condition, predicate, trueNode, falseNode);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem sequenceItem = presentation.Find(sequence.uuid);
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);

            Assert.That(sequenceItem.Kind, Is.EqualTo(GraphPresentationKind.Sequence));
            Assert.That(sequenceItem.IsContainer, Is.False);
            Assert.That(presentation.Roots.Any(item => item.TargetUUID == first.uuid), Is.True);
            Assert.That(presentation.Roots.Any(item => item.TargetUUID == condition.uuid), Is.True);
            Assert.That(conditionItem.Slots.Select(slot => slot.Label), Is.EqualTo(new[] { "Condition" }));
            Assert.That(conditionItem.Slots[0].Content.Node.Node, Is.SameAs(predicate));
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceStart && edge.TargetUUID == first.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceNext && edge.TargetUUID == condition.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ConditionTrue && edge.TargetUUID == trueNode.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ConditionFalse && edge.TargetUUID == falseNode.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.FlowComplete), Is.True);
        }

        /// <summary>
        /// Verifies that nested Sequence continuation originates from the inner completion endpoint.
        /// </summary>
        [Test]
        public void Presentation_NestedSequenceUsesCompletionBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode first = Node<TestNode>("A");
            Sequence inner = Node<Sequence>("Inner");
            TestNode innerFirst = Node<TestNode>("B");
            TestNode innerLast = Node<TestNode>("C");
            TestNode outerLast = Node<TestNode>("D");
            outer.events = new[] { first.ToReference(), inner.ToReference(), outerLast.ToReference() };
            inner.events = new[] { innerFirst.ToReference(), innerLast.ToReference() };
            BehaviourTreeData tree = Tree(outer, first, inner, innerFirst, innerLast, outerLast);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation outerNext = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == outerLast);
            GraphPresentationRelation innerComplete = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.FlowComplete
                && relation.Target.Item?.Node?.Node == inner);
            Assert.That(outerNext.Source.Item.Node.Node, Is.SameAs(inner));
            Assert.That(outerNext.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(innerComplete.Source.Item.Node.Node, Is.SameAs(innerLast));
            Assert.That(innerComplete.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Output));
            Assert.That(innerComplete.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
            Assert.That(outerNext.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(innerComplete.Origin, Is.Null);
            Assert.That(outerNext.Origin, Is.Not.Null);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Source.Item?.Node?.Node == inner
                && relation.Source.Anchor == GraphPresentationAnchorKind.Output
                && relation.Target.Item?.Node?.Node == outerLast), Is.False);
        }

        /// <summary>
        /// Verifies that an inner Sequence composes correctly at every outer collection position.
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Presentation_NestedSequenceCompletionSupportsFirstMiddleAndLastPositions(int innerIndex)
        {
            Sequence outer = Node<Sequence>("Outer");
            Sequence inner = Node<Sequence>("Inner");
            TestNode before = Node<TestNode>("Before");
            TestNode innerEvent = Node<TestNode>("Inner Event");
            TestNode after = Node<TestNode>("After");
            TreeNode[] authoredEvents = { before, after };
            NodeReference[] eventReferences = authoredEvents.Select(node => node.ToReference()).ToArray();
            outer.events = eventReferences.Take(innerIndex)
                .Append(inner.ToReference())
                .Concat(eventReferences.Skip(innerIndex))
                .ToArray();
            inner.events = new[] { innerEvent.ToReference() };
            BehaviourTreeData tree = Tree(outer, before, inner, innerEvent, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation innerStart = presentation.Relations.Single(relation =>
                relation.Target.Item?.Node?.Node == inner
                && relation.Target.Anchor == GraphPresentationAnchorKind.Entry);
            GraphPresentationRelation innerCompletion = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.FlowComplete
                && relation.Target.Item?.Node?.Node == inner);
            TreeNode expectedPredecessor = innerIndex == 0
                ? outer
                : authoredEvents[innerIndex - 1];
            Assert.That(innerStart.Source.Item?.Node?.Node,
                Is.SameAs(expectedPredecessor));
            Assert.That(innerCompletion.Source.Item.Node.Node, Is.SameAs(innerEvent));

            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Source.Item?.Node?.Node == inner
                && relation.Source.Anchor == GraphPresentationAnchorKind.FlowComplete);
            if (innerIndex == outer.events.Length - 1)
            {
                Assert.That(continuation.Target.Item.Node.Node, Is.SameAs(outer));
                Assert.That(continuation.Target.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            }
            else
            {
                Assert.That(continuation.Target.Item.Node.Node, Is.SameAs(authoredEvents[innerIndex]));
                Assert.That(continuation.Target.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Entry));
            }
        }

        /// <summary>
        /// Verifies that completion endpoints compose through more than one nested Sequence level.
        /// </summary>
        [Test]
        public void Presentation_DeeplyNestedSequencesComposeCompletionEndpoints()
        {
            Sequence outer = Node<Sequence>("Outer");
            Sequence middle = Node<Sequence>("Middle");
            Sequence inner = Node<Sequence>("Inner");
            TestNode leaf = Node<TestNode>("Leaf");
            TestNode tail = Node<TestNode>("Tail");
            outer.events = new[] { middle.ToReference(), tail.ToReference() };
            middle.events = new[] { inner.ToReference() };
            inner.events = new[] { leaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, middle, inner, leaf, tail);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation innerToMiddle = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.FlowComplete
                && relation.Target.Item?.Node?.Node == middle);
            GraphPresentationRelation middleToTail = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == tail);
            Assert.That(innerToMiddle.Source.Item.Node.Node, Is.SameAs(inner));
            Assert.That(innerToMiddle.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(middleToTail.Source.Item.Node.Node, Is.SameAs(middle));
            Assert.That(middleToTail.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
        }

        /// <summary>
        /// Verifies that an empty Sequence still exposes one derived completion endpoint.
        /// </summary>
        [Test]
        public void Presentation_EmptySequenceConnectsDirectlyToCompletion()
        {
            Sequence sequence = Node<Sequence>("Empty");
            BehaviourTreeData tree = Tree(sequence);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphPresentationRelation relation = presentation.Relations.Single();
            Assert.That(relation.Kind, Is.EqualTo(GraphPresentationRelationKind.FlowComplete));
            Assert.That(relation.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.Output));
            Assert.That(relation.Target.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(presentation.CompletionScopes.Count, Is.EqualTo(1));
        }

        [Test]
        public void Presentation_UsesProxyForDuplicateAndMissingReferences()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode child = Node<TestNode>("Child");
            UUID missing = UUID.NewUUID();
            sequence.events = new[] { child.ToReference(), child.ToReference(), new NodeReference(missing) };
            BehaviourTreeData tree = Tree(sequence, child);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            Assert.That(presentation.Roots.Count(item => item.TargetUUID == child.uuid), Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(edge => edge.TargetUUID == child.uuid), Is.EqualTo(2));
            Assert.That(presentation.Relations.Where(edge => edge.TargetUUID == child.uuid)
                .Select(edge => edge.OccurrenceId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceStart && edge.TargetUUID == child.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.SequenceNext && edge.TargetUUID == child.uuid), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.IsMissingTarget), Is.True);
        }

        /// <summary>
        /// Verifies that an outer Sequence continues only after both Condition branches converge.
        /// </summary>
        [Test]
        public void Presentation_ConditionConvergesBeforeOuterSequenceContinuation()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode before = Node<TestNode>("Before");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { before.ToReference(), condition.ToReference(), after.ToReference() };
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(outer, before, condition, predicate, trueNode, falseNode, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);
            GraphPresentationRelation[] completions = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == conditionItem.FlowComplete).ToArray();

            Assert.That(conditionItem.ConditionScope, Is.Not.Null);
            Assert.That(completions.Length, Is.EqualTo(2));
            Assert.That(completions.Select(relation => relation.Source.Item.Node.Node),
                Is.EquivalentTo(new TreeNode[] { trueNode, falseNode }));
            Assert.That(completions.All(relation => relation.Source.Anchor == GraphPresentationAnchorKind.Output), Is.True);
            Assert.That(completions.All(relation => !relation.IsEditableReference), Is.True);
            Assert.That(continuation.Source, Is.EqualTo(conditionItem.FlowComplete));
            Assert.That(continuation.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(continuation.IsEditableReference, Is.True);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Source == conditionItem.Output
                && relation.Target.Item?.Node?.Node == after), Is.False);
        }

        /// <summary>
        /// Verifies that composite branch targets converge from their own Flow completion endpoints.
        /// </summary>
        [Test]
        public void Presentation_ConditionSequenceBranchesConvergeFromSequenceEnds()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            Sequence trueSequence = Node<Sequence>("True Sequence");
            Sequence falseSequence = Node<Sequence>("False Sequence");
            TestNode trueLeaf = Node<TestNode>("True Leaf");
            TestNode falseLeaf = Node<TestNode>("False Leaf");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueSequence.ToReference();
            condition.falseNode = falseSequence.ToReference();
            trueSequence.events = new[] { trueLeaf.ToReference() };
            falseSequence.events = new[] { falseLeaf.ToReference() };
            BehaviourTreeData tree = Tree(condition, predicate, trueSequence, falseSequence, trueLeaf, falseLeaf);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem conditionItem = presentation.Find(condition.uuid);
            GraphPresentationRelation[] branchCompletions = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == conditionItem.FlowComplete).ToArray();

            Assert.That(branchCompletions.Length, Is.EqualTo(2));
            Assert.That(branchCompletions.All(relation => relation.Source.Anchor == GraphPresentationAnchorKind.FlowComplete), Is.True);
            Assert.That(branchCompletions.Select(relation => relation.Source.Item.Node.Node),
                Is.EquivalentTo(new TreeNode[] { trueSequence, falseSequence }));
        }

        /// <summary>
        /// Verifies empty and missing Condition branches use distinct non-persistent fallback cards.
        /// </summary>
        [Test]
        public void Presentation_ConditionCreatesEmptyAndMissingFallbackPlaceholders()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            UUID missingUUID = UUID.NewUUID();
            condition.condition = predicate.ToReference();
            condition.trueNode = NodeReference.Empty;
            condition.falseNode = new NodeReference(missingUUID);
            BehaviourTreeData tree = Tree(condition, predicate);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationItem[] placeholders = presentation.Roots.Where(item => item.Placeholder != null).ToArray();
            GraphPresentationItem empty = placeholders.Single(item => item.Placeholder.Branch == GraphConditionBranch.True);
            GraphPresentationItem missing = placeholders.Single(item => item.Placeholder.Branch == GraphConditionBranch.False);

            Assert.That(empty.Placeholder.Title, Is.EqualTo("EMPTY TRUE"));
            Assert.That(empty.Placeholder.Subtitle, Is.EqualTo("Returns Success"));
            Assert.That(empty.Placeholder.IsMissing, Is.False);
            Assert.That(missing.Placeholder.Title, Is.EqualTo("MISSING FALSE"));
            Assert.That(missing.Placeholder.Subtitle, Is.EqualTo("Returns Failed"));
            Assert.That(missing.Placeholder.MissingUUID, Is.EqualTo(missingUUID));
            Assert.That(missing.Warning, Does.Contain(missingUUID.ToString()));
            Assert.That(topology.FindNode(condition.uuid).HasWarning, Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint), Is.EqualTo(2));
            Assert.That(presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint)
                .All(relation => !relation.IsEditableReference), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target.Item == presentation.Find(condition.uuid)), Is.EqualTo(2));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Count, Is.EqualTo(topology.Nodes.Count));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Any(entry => entry.UUID == UUID.Empty), Is.False);
        }

        /// <summary>
        /// Verifies duplicate branch targets keep two semantic occurrences while sharing one card position.
        /// </summary>
        [Test]
        public void Presentation_ConditionDuplicateTargetKeepsBothBranchOccurrences()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode shared = Node<TestNode>("Shared");
            condition.condition = predicate.ToReference();
            condition.trueNode = shared.ToReference();
            condition.falseNode = shared.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, shared);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationRelation[] authored = presentation.Relations.Where(relation =>
                relation.TargetUUID == shared.uuid
                && relation.Role == GraphPresentationRelationRole.AuthoredReference).ToArray();
            GraphPresentationRelation[] derived = presentation.Relations.Where(relation =>
                relation.TargetUUID == shared.uuid
                && relation.Role == GraphPresentationRelationRole.DerivedCompletion).ToArray();

            Assert.That(authored.Length, Is.EqualTo(2));
            Assert.That(derived.Length, Is.EqualTo(2));
            Assert.That(authored.Select(relation => relation.OccurrenceId), Is.EquivalentTo(derived.Select(relation => relation.OccurrenceId)));
            Assert.That(presentation.Roots.Count(item => item.Node?.Node == shared), Is.EqualTo(1));
        }

        /// <summary>
        /// Verifies known constant weights produce authored candidates, disabled zero lanes,
        /// and one shared completion before the outer Sequence continues.
        /// </summary>
        [Test]
        public void Presentation_ProbabilityConvergesEligibleWeightedBranchesBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            TestNode before = Node<TestNode>("Before");
            Probability probability = Node<Probability>("Probability");
            TestNode enabled = Node<TestNode>("Enabled");
            TestNode disabled = Node<TestNode>("Disabled");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { before.ToReference(), probability.ToReference(), after.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 3, reference = enabled.ToReference() },
                new Probability.EventWeight { weight = 0, reference = disabled.ToReference() },
            };
            BehaviourTreeData tree = Tree(outer, before, probability, enabled, disabled, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem probabilityItem = presentation.Find(probability.uuid);
            GraphPresentationRelation[] candidates = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch).ToArray();
            GraphPresentationRelation[] completions = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == probabilityItem.FlowComplete).ToArray();
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);

            Assert.That(probabilityItem.ProbabilityScope, Is.Not.Null);
            Assert.That(probabilityItem.ProbabilityScope.Subtitle, Is.EqualTo("PICK ONE"));
            Assert.That(candidates.Select(relation => relation.Label), Is.EqualTo(new[]
            {
                "Option 1 · Weight 3 · 100%",
                "Option 2 · Weight 0 · 0% · Disabled",
            }));
            Assert.That(candidates.Single(relation => relation.TargetUUID == disabled.uuid).IsVisuallyDisabled, Is.True);
            Assert.That(candidates.All(relation => relation.IsEditableReference), Is.True);
            Assert.That(completions.Length, Is.EqualTo(1));
            Assert.That(completions[0].Source.Item.Node.Node, Is.SameAs(enabled));
            Assert.That(continuation.Source, Is.EqualTo(probabilityItem.FlowComplete));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Source == probabilityItem.Output
                && relation.Target.Item?.Node?.Node == after), Is.False);
        }

        /// <summary>Verifies all-zero constants use the runtime uniform fallback instead of disabling candidates.</summary>
        [Test]
        public void Presentation_ProbabilityAllZeroWeightsUseUniformFallback()
        {
            Probability probability = Node<Probability>("Probability");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 0, reference = first.ToReference() },
                new Probability.EventWeight { weight = -5, reference = second.ToReference() },
            };
            BehaviourTreeData tree = Tree(probability, first, second);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationRelation[] candidates = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch).ToArray();

            Assert.That(candidates.Select(relation => relation.Label), Is.EqualTo(new[]
            {
                "Option 1 · Uniform fallback",
                "Option 2 · Uniform fallback",
            }));
            Assert.That(candidates.All(relation => !relation.IsVisuallyDisabled), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete), Is.EqualTo(2));
        }

        /// <summary>Verifies dynamic PseudoProbability weights remain potentially eligible and use tree variable names.</summary>
        [Test]
        public void Presentation_PseudoProbabilityDescribesDynamicWeightsWithoutStaticPercentages()
        {
            PseudoProbability probability = Node<PseudoProbability>("Pseudo");
            TestNode dynamicTarget = Node<TestNode>("Dynamic");
            TestNode constantTarget = Node<TestNode>("Constant");
            TestNode missingTarget = Node<TestNode>("Missing Variable");
            VariableData dynamicWeight = new("Combat Weight", VariableType.Int);
            VariableData missingWeight = new("Detached Weight", VariableType.Int);
            VariableField<int> dynamicField = new();
            VariableField<int> missingField = new();
            dynamicField.SetReference(dynamicWeight);
            missingField.SetReference(missingWeight);
            probability.maxConsecutiveBranch = 2;
            probability.events = new[]
            {
                new PseudoProbability.EventWeight { weight = dynamicField, reference = dynamicTarget.ToReference() },
                new PseudoProbability.EventWeight { weight = 0, reference = constantTarget.ToReference() },
                new PseudoProbability.EventWeight { weight = missingField, reference = missingTarget.ToReference() },
            };
            BehaviourTreeData tree = Tree(probability, dynamicTarget, constantTarget, missingTarget);
            tree.variables.Add(dynamicWeight);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationRelation[] candidates = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch).ToArray();

            Assert.That(owner.ProbabilityScope.Subtitle, Is.EqualTo("PICK ONE · MAX STREAK 2"));
            Assert.That(candidates.Select(relation => relation.Label), Is.EqualTo(new[]
            {
                "Option 1 · Weight · Combat Weight",
                "Option 2 · Weight 0",
                "Option 3 · Weight · MISSING",
            }));
            Assert.That(candidates.All(relation => !relation.IsVisuallyDisabled), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete), Is.EqualTo(3));
            Assert.That(owner.Node.Warning, Does.Contain(missingWeight.UUID.ToString()));
        }

        /// <summary>Verifies empty and missing candidate slots remain explicit invalid terminal paths.</summary>
        [Test]
        public void Presentation_ProbabilityInvalidOptionsDoNotCreateFalseCompletions()
        {
            Probability probability = Node<Probability>("Probability");
            UUID missingUUID = UUID.NewUUID();
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 1, reference = NodeReference.Empty },
                new Probability.EventWeight { weight = 1, reference = new NodeReference(missingUUID) },
            };
            BehaviourTreeData tree = Tree(probability);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationItem[] placeholders = presentation.Roots.Where(item =>
                item.ProbabilityPlaceholder != null).ToArray();

            Assert.That(placeholders.Select(item => item.ProbabilityPlaceholder.Title), Is.EqualTo(new[]
            {
                "EMPTY OPTION [0]",
                "MISSING OPTION [1]",
            }));
            Assert.That(placeholders.All(item => item.ProbabilityPlaceholder.IsInvalidSelection), Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete), Is.False);
            Assert.That(topology.FindNode(probability.uuid).HasWarning, Is.True);
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Any(entry => entry.UUID == UUID.Empty), Is.False);
        }

        /// <summary>Verifies an empty option array models the Flow's normal Failed return through END.</summary>
        [Test]
        public void Presentation_ProbabilityNoOptionsReturnsFailedThroughCompletion()
        {
            Probability probability = Node<Probability>("Probability");
            BehaviourTreeData tree = Tree(probability);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationItem placeholder = presentation.Roots.Single(item =>
                item.ProbabilityPlaceholder?.Kind == GraphProbabilityPlaceholderKind.NoOptions);

            Assert.That(placeholder.ProbabilityPlaceholder.Subtitle, Is.EqualTo("Returns Failed"));
            Assert.That(presentation.Relations.Single(relation =>
                relation.Source.Item == placeholder
                && relation.Target == owner.FlowComplete).Role,
                Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
        }

        /// <summary>Verifies duplicate and nested Flow candidates preserve occurrences and converge from nested END.</summary>
        [Test]
        public void Presentation_ProbabilityNestedAndDuplicateCandidatesKeepCompletionSemantics()
        {
            Probability probability = Node<Probability>("Probability");
            Sequence nested = Node<Sequence>("Nested");
            TestNode leaf = Node<TestNode>("Leaf");
            nested.events = new[] { leaf.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 1, reference = nested.ToReference() },
                new Probability.EventWeight { weight = 1, reference = nested.ToReference() },
            };
            BehaviourTreeData tree = Tree(probability, nested, leaf);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(probability.uuid);
            GraphPresentationRelation[] authored = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.ProbabilityBranch
                && relation.TargetUUID == nested.uuid).ToArray();
            GraphPresentationRelation[] derived = presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && relation.Target == owner.FlowComplete).ToArray();

            Assert.That(authored.Length, Is.EqualTo(2));
            Assert.That(derived.Length, Is.EqualTo(2));
            Assert.That(derived.All(relation => relation.Source.Item.Node.Node == nested), Is.True);
            Assert.That(derived.All(relation => relation.Source.Anchor == GraphPresentationAnchorKind.FlowComplete), Is.True);
            Assert.That(authored.Select(relation => relation.OccurrenceId),
                Is.EquivalentTo(derived.Select(relation => relation.OccurrenceId)));
            Assert.That(presentation.Roots.Count(item => item.Node?.Node == nested), Is.EqualTo(1));
        }

        /// <summary>Verifies structured Probability layout places candidates before END and outer continuation.</summary>
        [Test]
        public void AutoLayout_ProbabilityPlacesCompletionBelowCandidateEnvelopes()
        {
            Sequence outer = Node<Sequence>("Outer");
            Probability probability = Node<Probability>("Probability");
            TestHost first = Node<TestHost>("First");
            TestNode firstChild = Node<TestNode>("First Child");
            TestNode second = Node<TestNode>("Second");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { probability.ToReference(), after.ToReference() };
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 2, reference = first.ToReference() },
                new Probability.EventWeight { weight = 1, reference = second.ToReference() },
            };
            first.children = new[] { firstChild.ToReference() };
            BehaviourTreeData tree = Tree(outer, probability, first, firstChild, second, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphProbabilityScope scope = presentation.Find(probability.uuid).ProbabilityScope;
            Rect firstBounds = GraphPresentationLayout.GetBounds(presentation.Find(first.uuid));
            Rect secondBounds = GraphPresentationLayout.GetBounds(presentation.Find(second.uuid));

            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(Mathf.Max(firstBounds.yMax, secondBounds.yMax)));
            Assert.That(topology.FindNode(after.uuid).Position.y, Is.GreaterThan(scope.CompletionPosition.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>Verifies Probability scope, END, and invalid placeholders remain presentation-only UI.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_ProbabilityScopeAndPlaceholdersFollowOwnerSelectionWithoutDirtying()
        {
            Probability probability = Node<Probability>("Probability");
            probability.events = new[]
            {
                new Probability.EventWeight { weight = 1, reference = NodeReference.Empty },
            };
            BehaviourTreeData tree = Tree(probability);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;
            window.SelectedNode = null;

            GraphProbabilityScopeElement scope = window.rootVisualElement.Q<GraphProbabilityScopeElement>();
            GraphProbabilityPlaceholderElement placeholder = window.rootVisualElement.Q<GraphProbabilityPlaceholderElement>();
            GraphFlowCompletionElement completion = window.rootVisualElement.Query<GraphFlowCompletionElement>()
                .ToList().Single(element => element.Scope.Owner.Node?.Node == probability);

            Assert.That(scope, Is.Not.Null);
            Assert.That(scope.pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(placeholder, Is.Not.Null);
            Assert.That(placeholder.pickingMode, Is.EqualTo(PickingMode.Position));
            window.SelectedNode = probability;
            Assert.That(scope.ClassListContains("ai-editor-graph-probability-scope-selected"), Is.True);
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(completion.ClassListContains("ai-editor-graph-flow-end-selected"), Is.True);
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>
        /// Verifies a While Loop exposes authored condition/body references and derived repeat/exit control.
        /// </summary>
        [Test]
        public void Presentation_WhileLoopUsesRepeatAndCompletionBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            TestNode body = Node<TestNode>("Body");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { loop.ToReference(), after.ToReference() };
            loop.loopType = Loop.LoopType.@while;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(outer, loop, condition, body, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem loopItem = presentation.Find(loop.uuid);
            GraphPresentationRelation conditionRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopCondition
                && relation.Target.Item?.Node?.Node == condition);
            GraphPresentationRelation bodyRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopBody
                && relation.Target.Item?.Node?.Node == body);
            GraphPresentationRelation exit = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit);
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);

            Assert.That(loopItem.Kind, Is.EqualTo(GraphPresentationKind.Loop));
            Assert.That(loopItem.LoopScope.Mode, Is.EqualTo(Loop.LoopType.@while));
            Assert.That(conditionRelation.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(bodyRelation.Role, Is.EqualTo(GraphPresentationRelationRole.AuthoredReference));
            Assert.That(conditionRelation.IsEditableReference, Is.True);
            Assert.That(bodyRelation.IsEditableReference, Is.True);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat
                && relation.Role == GraphPresentationRelationRole.DerivedControl), Is.EqualTo(1));
            Assert.That(exit.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
            Assert.That(exit.Target, Is.EqualTo(loopItem.FlowComplete));
            Assert.That(continuation.Source, Is.EqualTo(loopItem.FlowComplete));
            Assert.That(presentation.Relations.Where(relation =>
                relation.Role == GraphPresentationRelationRole.DerivedControl)
                .All(relation => !relation.IsEditableReference), Is.True);
        }

        /// <summary>
        /// Verifies DoWhile executes its body before the authored condition and then exposes repeat and exit paths.
        /// </summary>
        [Test]
        public void Presentation_DoWhileLoopStartsWithBodyBeforeCondition()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.doWhile;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(loop, condition, body);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem loopItem = presentation.Find(loop.uuid);
            GraphPresentationRelation bodyStart = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopBody);
            GraphPresentationRelation conditionRelation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopCondition);
            GraphPresentationRelation repeatBack = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat
                && relation.Target.Item?.Node?.Node == body);

            Assert.That(bodyStart.Source, Is.EqualTo(loopItem.Output));
            Assert.That(bodyStart.Target.Item.Node.Node, Is.SameAs(body));
            Assert.That(conditionRelation.Source.Item.Node.Node, Is.SameAs(body));
            Assert.That(conditionRelation.Target.Item.Node.Node, Is.SameAs(condition));
            Assert.That(repeatBack.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedControl));
            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit).Source.Item.Node.Node,
                Is.SameAs(condition));
        }

        /// <summary>
        /// Verifies For uses a derived count check instead of presenting its unused condition field as executable control.
        /// </summary>
        [Test]
        public void Presentation_ForLoopUsesDerivedCountCheck()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode unusedCondition = Node<TestNode>("Unused Condition");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@for;
            loop.condition = unusedCondition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(loop, unusedCondition, body);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;

            Assert.That(scope.Condition.LoopJunction.Kind, Is.EqualTo(GraphLoopJunctionKind.CountCheck));
            Assert.That(presentation.Roots.Count(item => item.LoopJunction != null), Is.EqualTo(1));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopCondition
                && relation.Target.Item == scope.Condition
                && relation.Role == GraphPresentationRelationRole.DerivedControl), Is.True);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Target.Item?.Node?.Node == unusedCondition), Is.False);
            Assert.That(presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit).Label, Is.EqualTo("Exhausted"));
        }

        /// <summary>
        /// Verifies empty and unresolved Loop slots use non-persistent placeholders with distinct diagnostics.
        /// </summary>
        [Test]
        public void Presentation_LoopCreatesEmptyAndMissingPlaceholders()
        {
            Loop loop = Node<Loop>("Loop");
            UUID missingCondition = UUID.NewUUID();
            loop.loopType = Loop.LoopType.@while;
            loop.condition = new NodeReference(missingCondition);
            loop.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = Tree(loop);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationItem[] placeholders = presentation.Roots
                .Where(item => item.LoopPlaceholder != null).ToArray();
            GraphPresentationItem condition = placeholders.Single(item =>
                item.LoopPlaceholder.Part == GraphLoopPart.Condition);
            GraphPresentationItem body = placeholders.Single(item =>
                item.LoopPlaceholder.Part == GraphLoopPart.Body);

            Assert.That(condition.LoopPlaceholder.Title, Is.EqualTo("MISSING CONDITION"));
            Assert.That(condition.LoopPlaceholder.MissingUUID, Is.EqualTo(missingCondition));
            Assert.That(body.LoopPlaceholder.Title, Is.EqualTo("EMPTY BODY"));
            Assert.That(body.LoopPlaceholder.IsMissing, Is.False);
            Assert.That(presentation.Relations.Count(relation =>
                relation.Role == GraphPresentationRelationRole.PlaceholderHint), Is.EqualTo(2));
            Assert.That(GraphLayoutResolver.CreateLayout(topology).Positions.Any(entry =>
                entry.UUID == UUID.Empty), Is.False);
        }

        /// <summary>
        /// Verifies all Loop modes reserve their derived controls without visible collisions.
        /// </summary>
        [TestCase(Loop.LoopType.@while)]
        [TestCase(Loop.LoopType.doWhile)]
        [TestCase(Loop.LoopType.@for)]
        public void AutoLayout_LoopModesRemainCollisionFree(Loop.LoopType mode)
        {
            Sequence outer = Node<Sequence>("Outer");
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            Sequence body = Node<Sequence>("Body");
            TestNode bodyLeaf = Node<TestNode>("Body Leaf");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { loop.ToReference(), after.ToReference() };
            loop.loopType = mode;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            body.events = new[] { bodyLeaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, loop, condition, body, bodyLeaf, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            Rect bodyBounds = GraphPresentationLayout.GetBounds(presentation.Find(body.uuid));
            Rect conditionBounds = GraphPresentationLayout.GetBounds(scope.Condition);
            Rect completionBounds = new(scope.CompletionPosition, scope.CompletionSize);
            Rect afterBounds = GraphPresentationLayout.GetBounds(presentation.Find(after.uuid));
            Rect structureBounds = Rect.MinMaxRect(
                Mathf.Min(conditionBounds.xMin, scope.BodyFrameBounds.xMin),
                Mathf.Min(conditionBounds.yMin, scope.BodyFrameBounds.yMin),
                Mathf.Max(conditionBounds.xMax, scope.BodyFrameBounds.xMax),
                Mathf.Max(conditionBounds.yMax, scope.BodyFrameBounds.yMax));
            Assert.That(scope.BodyFrameBounds.xMin, Is.LessThanOrEqualTo(bodyBounds.xMin));
            Assert.That(scope.BodyFrameBounds.xMax, Is.GreaterThanOrEqualTo(bodyBounds.xMax));
            Assert.That(scope.BodyFrameBounds.yMin, Is.LessThanOrEqualTo(bodyBounds.yMin));
            Assert.That(scope.BodyFrameBounds.yMax, Is.GreaterThanOrEqualTo(bodyBounds.yMax));
            Assert.That(scope.BodyFrameBounds.Overlaps(conditionBounds), Is.False);
            Assert.That(scope.BodyFrameBounds.Overlaps(completionBounds), Is.False);
            Assert.That(completionBounds.yMin, Is.GreaterThan(structureBounds.yMax));
            Assert.That(completionBounds.center.x, Is.EqualTo(structureBounds.center.x).Within(0.01f));
            Assert.That(scope.ReturnRailX, Is.LessThan(scope.BodyFrameBounds.xMin));
            Assert.That(scope.ExitRailX, Is.GreaterThan(scope.BodyFrameBounds.xMax));
            Assert.That(scope.Bounds.xMin, Is.LessThanOrEqualTo(scope.ReturnRailX));
            Assert.That(scope.Bounds.xMax, Is.GreaterThanOrEqualTo(scope.ExitRailX));
            Assert.That(afterBounds.yMin, Is.GreaterThan(completionBounds.yMax));
            Assert.That(afterBounds.center.x, Is.EqualTo(completionBounds.center.x).Within(0.01f));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>
        /// Verifies moving a Loop Body member recomputes derived frame and completion geometry without asset writes.
        /// </summary>
        [Test]
        public void Presentation_MovingLoopBodyRecalculatesCompletionWithoutLayoutWrite()
        {
            Loop loop = Node<Loop>("Loop");
            TestNode condition = Node<TestNode>("Condition");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = condition.ToReference();
            loop.events = new[] { body.ToReference() };
            BehaviourTreeData tree = Tree(loop, condition, body);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationLayout.Layout(presentation);
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            Vector2 initialCompletion = scope.CompletionPosition;
            GraphPresentationItem bodyItem = presentation.Find(body.uuid);
            Vector2 movedPosition = bodyItem.Position + new Vector2(240f, 120f);
            EditorUtility.ClearDirty(tree);

            presentation.MoveRoot(body.uuid, movedPosition);
            GraphPresentationLayout.Layout(presentation);

            Rect movedBounds = GraphPresentationLayout.GetBounds(bodyItem);
            Assert.That(scope.CompletionPosition, Is.Not.EqualTo(initialCompletion));
            Assert.That(scope.BodyFrameBounds.xMin, Is.LessThanOrEqualTo(movedBounds.xMin));
            Assert.That(scope.BodyFrameBounds.xMax, Is.GreaterThanOrEqualTo(movedBounds.xMax));
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(scope.BodyFrameBounds.yMax));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>
        /// Verifies self-referencing Loop condition and body occurrences terminate presentation and layout safely.
        /// </summary>
        [Test]
        public void AutoLayout_SelfReferencingLoopTerminatesWithOneCompletion()
        {
            Loop loop = Node<Loop>("Loop");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = loop.ToReference();
            loop.events = new[] { loop.ToReference() };
            BehaviourTreeData tree = Tree(loop);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.DoesNotThrow(() => GraphLayoutResolver.ApplyAutoLayout(tree, topology));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            Assert.DoesNotThrow(() => GraphPresentationLayout.Layout(presentation));
            Assert.That(presentation.CompletionScopes.Count(scope => scope.Owner.Node?.Node == loop), Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat), Is.EqualTo(1));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>
        /// Verifies structured Condition layout places its END and outer continuation after both branches.
        /// </summary>
        [Test]
        public void AutoLayout_ConditionBlockPlacesCompletionBeforeOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            Sequence trueSequence = Node<Sequence>("True Sequence");
            TestNode trueLeaf = Node<TestNode>("True Leaf");
            TestNode falseNode = Node<TestNode>("False");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { condition.ToReference(), after.ToReference() };
            condition.condition = predicate.ToReference();
            condition.trueNode = trueSequence.ToReference();
            condition.falseNode = falseNode.ToReference();
            trueSequence.events = new[] { trueLeaf.ToReference() };
            BehaviourTreeData tree = Tree(outer, condition, predicate, trueSequence, trueLeaf, falseNode, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphConditionScope scope = presentation.Find(condition.uuid).ConditionScope;
            Rect trueBounds = GraphPresentationLayout.GetBounds(presentation.Find(trueSequence.uuid));
            Rect falseBounds = GraphPresentationLayout.GetBounds(presentation.Find(falseNode.uuid));
            Assert.That(trueBounds.Overlaps(falseBounds), Is.False);
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(Mathf.Max(trueBounds.yMax, falseBounds.yMax)));
            Assert.That(topology.FindNode(after.uuid).Position.y, Is.GreaterThan(scope.CompletionPosition.y + scope.CompletionSize.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>
        /// Verifies a Condition reserves its branch Service subtree before placing the END marker.
        /// </summary>
        [Test]
        public void AutoLayout_ConditionCompletionClearsBranchServiceSubtree()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestHost trueHost = Node<TestHost>("True Host");
            TestNode trueChild = Node<TestNode>("True Child");
            TestService service = Node<TestService>("Service");
            TestNode serviceChild = Node<TestNode>("Service Child");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueHost.ToReference();
            condition.falseNode = falseNode.ToReference();
            trueHost.children = new[] { trueChild.ToReference() };
            trueHost.services = new List<NodeReference> { service.ToReference() };
            service.child = serviceChild.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, trueHost, trueChild, service, serviceChild, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphConditionScope scope = presentation.Find(condition.uuid).ConditionScope;
            float serviceBottom = topology.FindNode(serviceChild.uuid).Position.y
                + GraphLayoutResolver.GetNodeSize(topology.FindNode(serviceChild.uuid)).y;
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(serviceBottom));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>
        /// Verifies free branch movement only recalculates derived Condition geometry.
        /// </summary>
        [Test]
        public void Presentation_MovingConditionBranchRecalculatesDerivedScopeOnly()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, trueNode, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphConditionScope scope = presentation.Find(condition.uuid).ConditionScope;
            float originalCompletionY = scope.CompletionPosition.y;
            Vector2 descriptorPosition = topology.FindNode(trueNode.uuid).Position;

            presentation.MoveRoot(trueNode.uuid, presentation.Find(trueNode.uuid).Position + Vector2.up * 400f);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(originalCompletionY));
            Assert.That(topology.FindNode(trueNode.uuid).Position, Is.EqualTo(descriptorPosition));
            Assert.That(tree.GraphLayout, Is.Null);
        }

        /// <summary>
        /// Verifies self references terminate presentation and deterministic layout traversal safely.
        /// </summary>
        [Test]
        public void AutoLayout_SelfReferencingConditionTerminatesWithOneCompletion()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            condition.condition = predicate.ToReference();
            condition.trueNode = condition.ToReference();
            condition.falseNode = NodeReference.Empty;
            BehaviourTreeData tree = Tree(condition, predicate);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.DoesNotThrow(() => GraphLayoutResolver.ApplyAutoLayout(tree, topology));
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            Assert.That(presentation.CompletionScopes.Count(scope => scope.Owner.Node?.Node == condition), Is.EqualTo(1));
            Assert.That(presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.ConditionTrue
                && relation.Target.Item?.Node?.Node == condition), Is.True);
        }

        /// <summary>
        /// Verifies an unreachable Condition is placed as one structured block rather than flattened cards.
        /// </summary>
        [Test]
        public void AutoLayout_UnreachableConditionBlockRemainsCollisionFree()
        {
            TestNode head = Node<TestNode>("Head");
            Condition condition = Node<Condition>("Unreachable Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode trueNode = Node<TestNode>("True");
            TestNode falseNode = Node<TestNode>("False");
            condition.condition = predicate.ToReference();
            condition.trueNode = trueNode.ToReference();
            condition.falseNode = falseNode.ToReference();
            BehaviourTreeData tree = Tree(head, condition, predicate, trueNode, falseNode);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            Assert.That(presentation.Find(condition.uuid).ConditionScope.CompletionPosition.y,
                Is.GreaterThan(topology.FindNode(condition.uuid).Position.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>Verifies Decision keeps direct authored branches while completing through one END.</summary>
        [Test]
        public void Presentation_DecisionUsesDirectBranchesAndOrderedReturnSemantics()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode before = Node<TestNode>("Before");
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode after = Node<TestNode>("After");
            sequence.events = new[] { before.ToReference(), decision.ToReference(), after.ToReference() };
            decision.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(sequence, before, decision, first, second, after);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationRelation[] authored = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionBranch).ToArray();
            GraphPresentationRelation[] completion = presentation.Relations.Where(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionSuccess).ToArray();
            GraphPresentationRelation failure = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionFailure);
            GraphPresentationRelation continuation = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceNext
                && relation.Target.Item?.Node?.Node == after);

            Assert.That(owner.DecisionScope, Is.Not.Null);
            Assert.That(authored.Select(relation => relation.Source), Is.All.EqualTo(owner.Output));
            Assert.That(authored.Select(relation => relation.Target.Item.Node.Node),
                Is.EqualTo(new TreeNode[] { first, second }));
            Assert.That(completion.Select(relation => relation.Label), Is.EqualTo(new[] { "Success", "Complete" }));
            Assert.That(completion.All(relation => relation.Target == owner.FlowComplete), Is.True);
            Assert.That(failure.Role, Is.EqualTo(GraphPresentationRelationRole.DerivedControl));
            Assert.That(failure.ContextualOwner, Is.SameAs(owner));
            Assert.That(failure.IsVisibleFor(null), Is.False);
            Assert.That(failure.IsVisibleFor(decision), Is.True);
            Assert.That(continuation.Source, Is.EqualTo(owner.FlowComplete));
        }

        /// <summary>Verifies empty Decision collections return Failed through a presentation placeholder.</summary>
        [Test]
        public void Presentation_DecisionNoOptionsReturnsFailedThroughCompletion()
        {
            Decision decision = Node<Decision>("Decision");
            decision.events = Array.Empty<NodeReference>();
            BehaviourTreeData tree = Tree(decision);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationItem placeholder = presentation.Roots.Single(item =>
                item.DecisionPlaceholder?.Kind == GraphDecisionPlaceholderKind.NoOptions);

            Assert.That(placeholder.DecisionPlaceholder.Subtitle, Is.EqualTo("Returns Failed"));
            Assert.That(presentation.Relations.Single(relation =>
                relation.Source.Item == placeholder
                && relation.Target == owner.FlowComplete).Role,
                Is.EqualTo(GraphPresentationRelationRole.DerivedCompletion));
        }

        /// <summary>Verifies invalid Decision occurrences terminate with Error instead of advancing or completing.</summary>
        [Test]
        public void Presentation_DecisionInvalidOptionsRemainErrorTerminals()
        {
            Decision decision = Node<Decision>("Decision");
            UUID missingUUID = UUID.NewUUID();
            decision.events = new[] { NodeReference.Empty, new NodeReference(missingUUID) };
            BehaviourTreeData tree = Tree(decision);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationItem[] placeholders = presentation.Roots.Where(item =>
                item.DecisionPlaceholder != null).ToArray();

            Assert.That(placeholders.Select(item => item.DecisionPlaceholder.Title), Is.EqualTo(new[]
            {
                "EMPTY OPTION [0]",
                "MISSING OPTION [1]",
            }));
            Assert.That(placeholders.All(item => item.DecisionPlaceholder.Subtitle == "Returns Error"), Is.True);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Target == owner.FlowComplete), Is.False);
            Assert.That(presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionFailure), Is.False);
            Assert.That(owner.Node.Warning, Does.Contain("Empty Decision option"));
            Assert.That(owner.Node.Warning, Does.Contain(missingUUID.ToString()));
        }

        /// <summary>Verifies duplicate Decision occurrences keep one card and explicit diagnostics.</summary>
        [Test]
        public void Presentation_DecisionDuplicateTargetsKeepOccurrencesWithoutProxyCards()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode target = Node<TestNode>("Target");
            decision.events = new[] { target.ToReference(), target.ToReference() };
            BehaviourTreeData tree = Tree(decision, target);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);

            Assert.That(owner.DecisionScope.Options.Count, Is.EqualTo(2));
            Assert.That(owner.DecisionScope.Options.All(option => option.Item == presentation.Find(target.uuid)), Is.True);
            Assert.That(presentation.Roots.Count(item => item.TargetUUID == target.uuid), Is.EqualTo(1));
            Assert.That(presentation.Relations.Count(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionBranch), Is.EqualTo(2));
            Assert.That(owner.Node.Warning, Does.Contain("Repeated Decision target"));
        }

        /// <summary>Verifies nested composite alternatives return from their own END markers.</summary>
        [Test]
        public void Presentation_DecisionNestedFlowReturnsFromChildCompletion()
        {
            Decision decision = Node<Decision>("Decision");
            Sequence nested = Node<Sequence>("Nested");
            TestNode nestedChild = Node<TestNode>("Nested Child");
            TestNode fallback = Node<TestNode>("Fallback");
            nested.events = new[] { nestedChild.ToReference() };
            decision.events = new[] { nested.ToReference(), fallback.ToReference() };
            BehaviourTreeData tree = Tree(decision, nested, nestedChild, fallback);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationItem owner = presentation.Find(decision.uuid);
            GraphPresentationRelation success = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionSuccess
                && relation.Label == "Success");
            GraphPresentationRelation failure = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.DecisionFailure);

            Assert.That(success.Source.Item.Node.Node, Is.SameAs(nested));
            Assert.That(success.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
            Assert.That(success.Target, Is.EqualTo(owner.FlowComplete));
            Assert.That(failure.Source.Anchor, Is.EqualTo(GraphPresentationAnchorKind.FlowComplete));
        }

        /// <summary>Verifies Auto Layout presents Decision as a vertical tree with horizontal siblings.</summary>
        [Test]
        public void AutoLayout_DecisionPlacesBranchesAboveCompletionAndOuterNext()
        {
            Sequence outer = Node<Sequence>("Outer");
            Decision decision = Node<Decision>("Decision");
            TestHost first = Node<TestHost>("First");
            TestNode firstChild = Node<TestNode>("First Child");
            TestNode second = Node<TestNode>("Second");
            TestNode after = Node<TestNode>("After");
            outer.events = new[] { decision.ToReference(), after.ToReference() };
            decision.events = new[] { first.ToReference(), second.ToReference() };
            first.children = new[] { firstChild.ToReference() };
            BehaviourTreeData tree = Tree(outer, decision, first, firstChild, second, after);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecisionScope scope = presentation.Find(decision.uuid).DecisionScope;
            Rect firstBounds = GraphPresentationLayout.GetBounds(presentation.Find(first.uuid));
            Rect secondBounds = GraphPresentationLayout.GetBounds(presentation.Find(second.uuid));

            Assert.That(topology.FindNode(first.uuid).Position.y, Is.GreaterThan(topology.FindNode(decision.uuid).Position.y));
            Assert.That(topology.FindNode(first.uuid).Position.x, Is.LessThan(topology.FindNode(second.uuid).Position.x));
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(Mathf.Max(firstBounds.yMax, secondBounds.yMax)));
            Assert.That(topology.FindNode(after.uuid).Position.y,
                Is.GreaterThan(scope.CompletionPosition.y + scope.CompletionSize.y));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>Verifies branch dragging only recalculates derived Decision geometry.</summary>
        [Test]
        public void Presentation_MovingDecisionBranchRecalculatesCompletionWithoutLayoutWrite()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            decision.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(decision, first, second);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphDecisionScope scope = presentation.Find(decision.uuid).DecisionScope;
            float originalCompletionY = scope.CompletionPosition.y;
            Vector2 descriptorPosition = topology.FindNode(first.uuid).Position;

            presentation.MoveRoot(first.uuid, presentation.Find(first.uuid).Position + Vector2.up * 400f);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(originalCompletionY));
            Assert.That(topology.FindNode(first.uuid).Position, Is.EqualTo(descriptorPosition));
            Assert.That(tree.GraphLayout, Is.Null);
        }

        /// <summary>Verifies Decision failure progression follows owner selection while success remains visible.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_DecisionFailureHintsFollowOwnerSelectionWithoutDirtying()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            decision.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(decision, first, second);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphEdgeLayerElement edges = window.rootVisualElement.Q<GraphEdgeLayerElement>("ai-editor-graph-edge-layer");
            Label failed = edges.Query<Label>().ToList().Single(label => label.text == "Failed");
            Label success = edges.Query<Label>().ToList().Single(label => label.text == "Success");
            GraphFlowCompletionElement completion = window.rootVisualElement.Query<GraphFlowCompletionElement>()
                .ToList().Single(element => element.Scope.Owner.Node?.Node == decision);

            window.SelectedNode = null;
            Assert.That(failed.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(success.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            window.SelectedNode = decision;
            Assert.That(failed.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(completion.pickingMode, Is.EqualTo(PickingMode.Position));
            window.SelectedNode = first;
            Assert.That(failed.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void Presentation_ClassifiesDecisionProbabilityAndParallelBranches()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode decisionA = Node<TestNode>("Decision A");
            TestNode decisionB = Node<TestNode>("Decision B");
            decision.events = new[] { decisionA.ToReference(), decisionB.ToReference() };

            Probability probability = Node<Probability>("Probability");
            TestNode probabilityA = Node<TestNode>("Probability A");
            probability.events = new[]
            {
                new Probability.EventWeight { reference = probabilityA.ToReference(), weight = 25 },
            };

            Parallel parallel = Node<Parallel>("Parallel");
            TestNode parallelA = Node<TestNode>("Parallel A");
            parallel.events = new[] { parallelA.ToReference() };

            BehaviourTreeData tree = Tree(decision, decisionA, decisionB, probability, probabilityA, parallel, parallelA);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            Assert.That(presentation.Relations.Count(edge => edge.Kind == GraphPresentationRelationKind.DecisionBranch), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ProbabilityBranch && edge.Label.Contains("Weight")), Is.True);
            Assert.That(presentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.ParallelBranch), Is.True);
            Assert.That(presentation.Roots.Count(item => item.IsContainer), Is.EqualTo(0));
        }

        /// <summary>
        /// Verifies every card family consumes the shared compact presentation metrics.
        /// </summary>
        [Test]
        public void Presentation_NodeFamiliesUseSharedCompactSizes()
        {
            TestNode normal = Node<TestNode>("Normal");
            Sequence flow = Node<Sequence>("Flow");
            Condition branch = Node<Condition>("Branch");
            TestService service = Node<TestService>("Service");
            BehaviourTreeData tree = Tree(normal, flow, branch, service);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(normal.uuid)),
                Is.EqualTo(GraphPresentationMetrics.NormalNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(flow.uuid)),
                Is.EqualTo(GraphPresentationMetrics.FlowNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(branch.uuid)),
                Is.EqualTo(GraphPresentationMetrics.BranchNodeSize));
            Assert.That(GraphLayoutResolver.GetNodeSize(topology.FindNode(service.uuid)),
                Is.EqualTo(GraphPresentationMetrics.ServiceNodeSize));
            Assert.That(GraphPresentationMetrics.NormalNodeSize.x, Is.LessThan(200f));
            Assert.That(GraphPresentationMetrics.LevelGap,
                Is.LessThan(GraphPresentationMetrics.NormalNodeSize.y));
        }

        /// <summary>
        /// Verifies ordered Sequence members and their completion use the compact vertical rhythm.
        /// </summary>
        [Test]
        public void AutoLayout_SequenceUsesCompactVerticalRhythm()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            BehaviourTreeData tree = Tree(sequence, first, second);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);

            GraphNodeDescriptor sequenceNode = topology.FindNode(sequence.uuid);
            GraphNodeDescriptor firstNode = topology.FindNode(first.uuid);
            GraphNodeDescriptor secondNode = topology.FindNode(second.uuid);
            Assert.That(firstNode.Position.y - sequenceNode.Position.y - GraphPresentationMetrics.FlowNodeSize.y,
                Is.EqualTo(GraphPresentationMetrics.LevelGap).Within(0.01f));
            Assert.That(secondNode.Position.y - firstNode.Position.y - GraphPresentationMetrics.NormalNodeSize.y,
                Is.EqualTo(GraphPresentationMetrics.LevelGap).Within(0.01f));

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphSequenceScope scope = presentation.Find(sequence.uuid).SequenceScope;
            Assert.That(scope.CompletionPosition.y - secondNode.Position.y - GraphPresentationMetrics.NormalNodeSize.y,
                Is.EqualTo(GraphPresentationMetrics.FlowCompletionGap).Within(0.01f));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        [Test]
        public void Presentation_UsesCycleProxyAndKeepsRawReferenceExternal()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            sequence.events = new[] { sequence.ToReference() };
            BehaviourTreeData cycleTree = Tree(sequence);
            GraphPresentation cyclePresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(cycleTree));

            Assert.That(cyclePresentation.Roots.Count(item => item.TargetUUID == sequence.uuid), Is.EqualTo(1));
            Assert.That(cyclePresentation.Relations.Single().Kind, Is.EqualTo(GraphPresentationRelationKind.SequenceStart));
            Assert.That(cyclePresentation.Relations.Single().TargetUUID, Is.EqualTo(sequence.uuid));

            TestHost head = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            head.raw = new RawNodeReference { UUID = child.uuid };
            BehaviourTreeData rawTree = Tree(head, child);
            GraphPresentation rawPresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(rawTree, includeRawReferences: true));

            Assert.That(rawPresentation.Relations.Any(edge => edge.Kind == GraphPresentationRelationKind.Raw), Is.True);
        }

        [Test]
        public void Presentation_LayoutDoesNotRewriteNodeCoordinates()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode child = Node<TestNode>("Child");
            sequence.events = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(sequence, child);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);
            Vector2 original = topology.FindNode(child.uuid).Position;

            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);

            Assert.That(topology.FindNode(child.uuid).Position, Is.EqualTo(original));
            Assert.That(presentation.Find(sequence.uuid).Size, Is.EqualTo(GraphLayoutResolver.GetNodeSize(topology.FindNode(sequence.uuid))));
        }

        [Test]
        public void Resolve_ReadsLegacyCoordinatesWithoutDirtyingTree()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, child);
            GraphNode legacyNode = new(new Vector2(123f, 456f), 200f, 80f) { uuid = child.uuid };
            tree.Graph.graphNodes = new List<GraphNode> { legacyNode };
            EditorUtility.ClearDirty(tree);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.Resolve(tree, topology);

            Assert.That(topology.FindNode(child.uuid).Position, Is.EqualTo(new Vector2(123f, 456f)));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void Resolve_PersistedCoordinatesOverrideLegacyWithoutDirtyingTree()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            tree.Graph.graphNodes = new List<GraphNode>
            {
                new GraphNode(new Vector2(10f, 20f), 200f, 80f) { uuid = head.uuid },
            };
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(head.uuid, new Vector2(30f, 40f)),
            });
            EditorUtility.ClearDirty(tree);
            GraphTopology topology = GraphTopologyBuilder.Build(tree);

            GraphLayoutResolver.Resolve(tree, topology);

            Assert.That(topology.FindNode(head.uuid).Position, Is.EqualTo(new Vector2(30f, 40f)));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void OpenGraph_WithLegacyLayoutDoesNotDirtyTree()
        {
            TestNode head = Node<TestNode>("Head");
            BehaviourTreeData tree = Tree(head);
            tree.Graph.graphNodes = new List<GraphNode>
            {
                new GraphNode(new Vector2(100f, 200f), 200f, 80f) { uuid = head.uuid },
            };
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();

            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            window.Refresh();

            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(tree.GraphLayout, Is.Null);
        }

        [Test]
        public void Presentation_ParallelWaitAllUsesOneCompletionPerScheduledTarget()
        {
            Parallel parallel = Node<Parallel>("Parallel");
            parallel.mode = Parallel.Mode.WaitAll;
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            parallel.events = new[] { first.ToReference(), second.ToReference(), first.ToReference() };
            BehaviourTreeData tree = Tree(parallel, first, second);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphParallelScope scope = presentation.Find(parallel.uuid).ParallelScope;

            Assert.That(scope.Branches, Is.EqualTo(new[] { presentation.Find(first.uuid), presentation.Find(second.uuid) }));
            Assert.That(presentation.Relations.Count(relation => relation.Kind == GraphPresentationRelationKind.ParallelBranch), Is.EqualTo(3));
            Assert.That(presentation.Relations.Count(relation => relation.Kind == GraphPresentationRelationKind.ParallelComplete), Is.EqualTo(2));
            Assert.That(presentation.Relations.Any(relation => relation.Label == "Shared stack"), Is.True);
            Assert.That(presentation.Find(parallel.uuid).Node.HasWarning, Is.True);
        }

        [Test]
        public void Presentation_ParallelInvalidBranchesMatchWaitMode()
        {
            UUID missing = UUID.NewUUID();
            Parallel waitAll = Node<Parallel>("Wait All");
            waitAll.mode = Parallel.Mode.WaitAll;
            waitAll.events = new[] { new NodeReference(missing) };
            Parallel waitAny = Node<Parallel>("Wait Any");
            waitAny.mode = Parallel.Mode.WaitAny;
            waitAny.events = new[] { NodeReference.Empty };
            BehaviourTreeData tree = Tree(waitAll, waitAny);
            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));

            GraphParallelPlaceholder allPlaceholder = presentation.Find(waitAll.uuid).ParallelScope.Branches.Single().ParallelPlaceholder;
            GraphParallelPlaceholder anyPlaceholder = presentation.Find(waitAny.uuid).ParallelScope.Branches.Single().ParallelPlaceholder;

            Assert.That(allPlaceholder.Kind, Is.EqualTo(GraphParallelPlaceholderKind.IgnoredBranch));
            Assert.That(anyPlaceholder.Kind, Is.EqualTo(GraphParallelPlaceholderKind.ImmediateCompletion));
            Assert.That(presentation.Relations.Any(relation => relation.Source.Item == presentation.Find(waitAll.uuid).ParallelScope.Branches.Single()
                && relation.Kind == GraphPresentationRelationKind.ParallelComplete), Is.False);
            Assert.That(presentation.Relations.Any(relation => relation.Source.Item == presentation.Find(waitAny.uuid).ParallelScope.Branches.Single()
                && relation.Kind == GraphPresentationRelationKind.ParallelComplete), Is.True);
        }

        [Test]
        public void Presentation_ForEachMissingEnumerableReturnsFailedWithoutPersistedItems()
        {
            ForEach flow = Node<ForEach>("For Each");
            BehaviourTreeData tree = Tree(flow);
            EditorUtility.ClearDirty(tree);

            GraphPresentation presentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(tree));
            GraphPresentationLayout.Layout(presentation);
            GraphForEachScope scope = presentation.Find(flow.uuid).ForEachScope;

            Assert.That(scope.Check.ForEachJunction.Kind, Is.EqualTo(GraphForEachJunctionKind.EnumerableCheck));
            Assert.That(scope.Body.ForEachPlaceholder.Kind, Is.EqualTo(GraphForEachPlaceholderKind.MissingEnumerable));
            Assert.That(presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.ForEachExit
                && relation.Label == "Returns Failed"), Is.True);
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void Presentation_ForEachRepeatsBodyAndExitsAfterEnumeration()
        {
            ForEach flow = Node<ForEach>("For Each");
            TestNode body = Node<TestNode>("Body");
            VariableData enumerable = new("Items", VariableType.Generic);
            flow.enumerable = new VariableReference();
            flow.enumerable.SetReference(enumerable);
            flow.@event = body.ToReference();
            BehaviourTreeData tree = Tree(flow, body);
            tree.variables.Add(enumerable);

            GraphTopology topology = GraphTopologyBuilder.Build(tree);
            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            GraphForEachScope scope = presentation.Find(flow.uuid).ForEachScope;

            Assert.That(scope.Body.Node.Node, Is.SameAs(body));
            Assert.That(presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.ForEachRepeat
                && relation.Source.Item.Node.Node == body && relation.Target.Item == scope.Check), Is.True);
            Assert.That(presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.ForEachExit
                && relation.Target == presentation.Find(flow.uuid).FlowComplete), Is.True);
            Assert.That(scope.CompletionPosition.y, Is.GreaterThan(scope.BodyFrameBounds.yMax));
            Assert.That(GraphLayoutResolver.FindPresentationOverlaps(presentation), Is.Empty);
        }

        /// <summary>Verifies contextual ForEach failure diagnostics follow the authoritative window selection.</summary>
        [UnityTest]
        public IEnumerator GraphWindow_ForEachContextualFailureAppearsOnlyWhenOwnerSelected()
        {
            ForEach flow = Node<ForEach>("For Each");
            TestNode detached = Node<TestNode>("Detached");
            VariableData enumerable = new("Items", VariableType.Generic);
            flow.enumerable = new VariableReference();
            flow.enumerable.SetReference(enumerable);
            BehaviourTreeData tree = Tree(flow, detached);
            tree.variables.Add(enumerable);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            Label failure = window.rootVisualElement.Query<Label>().ToList().Single(label =>
                label.text == "Not IEnumerable · Returns Failed");
            window.SelectedNode = detached;
            Assert.That(failure.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            window.SelectedNode = flow;
            Assert.That(failure.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            GraphForEachPlaceholderElement itemHint = window.rootVisualElement.Q<GraphForEachPlaceholderElement>(
                "ai-editor-graph-foreach-placeholder-missingitemoutput");
            Assert.That(itemHint, Is.Not.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        private BehaviourTreeData Tree(params TreeNode[] nodes)
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            tree.headNodeUUID = nodes[0].uuid;
            tree.nodes.AddRange(nodes);
            trees.Add(tree);
            return tree;
        }

        /// <summary>Creates a hidden graph module whose window is owned by this test fixture.</summary>
        private GraphEditorModule CreateHiddenGraphModule(BehaviourTreeData tree)
        {
            AIEditorWindow window = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(window);
            window.Load(tree);
            GraphEditorModule module = new(window);
            module.Attach(CreateDeclaredGraphHost(window));
            return module;
        }

        /// <summary>Clones the editor's authoritative default-reference UXML and returns its Graph host.</summary>
        private static VisualElement CreateDeclaredGraphHost(AIEditorWindow window)
        {
            SerializedObject serializedWindow = new(window);
            VisualTreeAsset shellAsset = serializedWindow.FindProperty("shellAsset").objectReferenceValue as VisualTreeAsset;
            Assert.That(shellAsset, Is.Not.Null);
            VisualElement root = new();
            shellAsset.CloneTree(root);
            return root.Q<VisualElement>("ai-editor-graph-host");
        }

        /// <summary>Asserts that card bounds for the requested presentation items remain inside the live viewport.</summary>
        private static void AssertPresentationItemsInsideViewport(GraphCanvasElement canvas, params UUID[] uuids)
        {
            foreach (UUID uuid in uuids)
            {
                GraphPresentationItem item = canvas.Presentation.Find(uuid);
                Rect bounds = new(item.Position, item.Size);
                Vector2 minimum = canvas.GraphToViewport(bounds.min);
                Vector2 maximum = canvas.GraphToViewport(bounds.max);
                Assert.That(minimum.x, Is.GreaterThanOrEqualTo(0f), uuid.ToString());
                Assert.That(minimum.y, Is.GreaterThanOrEqualTo(0f), uuid.ToString());
                Assert.That(maximum.x, Is.LessThanOrEqualTo(canvas.layout.width), uuid.ToString());
                Assert.That(maximum.y, Is.LessThanOrEqualTo(canvas.layout.height), uuid.ToString());
            }
        }

        private static T Node<T>(string name) where T : TreeNode, new()
        {
            return new T
            {
                name = name,
                uuid = UUID.NewUUID(),
            };
        }

        [Serializable]
        private sealed class TestNode : TreeNode
        {
            public NodeReference child;
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        private sealed class TestHost : ServiceHostNode
        {
            public NodeReference[] children = Array.Empty<NodeReference>();
            public RawNodeReference raw;

            public override void Initialize() { }
            public override State Execute() => State.Success;
        }

        [Serializable]
        private sealed class TestService : Service
        {
            public NodeReference child;

            public override bool IsReady => true;
            public override void UpdateTimer() { }
            public override void Initialize() { }
            public override State Execute() => State.Success;
        }
    }
}
