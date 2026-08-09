using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Visual;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<AIEditorWindow> windows = new();

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

            foreach (AIEditorWindow window in windows)
            {
                if (window)
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }

            windows.Clear();
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
            windows.Add(window);
            window.Load(tree);
            GraphEditorModule module = new(window);
            module.Attach(new VisualElement());
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
            windows.Add(window);
            window.Load(tree);
            GraphEditorModule module = new(window);
            module.Attach(new VisualElement());
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
        public void GraphWindow_UsesOneInspectorAndMirrorsNodeSelection()
        {
            Sequence head = Node<Sequence>("Head");
            Sequence child = Node<Sequence>("Child");
            head.events = new[] { child.ToReference() };
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            windows.Add(window);
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
            Assert.That(completions.All(completion => completion.pickingMode == PickingMode.Ignore), Is.True);
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
            windows.Add(window);
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
            windows.Add(window);
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
            windows.Add(window);
            window.CreateGUI();

            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            window.Refresh();

            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(tree.GraphLayout, Is.Null);
        }

        private BehaviourTreeData Tree(params TreeNode[] nodes)
        {
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            tree.headNodeUUID = nodes[0].uuid;
            tree.nodes.AddRange(nodes);
            trees.Add(tree);
            return tree;
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
