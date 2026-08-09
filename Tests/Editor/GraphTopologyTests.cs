using Aethiumian.AI.Accessors;
using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Visual;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
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

            GraphContainerElement childElement = window.rootVisualElement.Q<GraphContainerElement>($"ai-editor-graph-container-{child.uuid}");
            Assert.That(childElement, Is.Not.Null);
            window.SelectedNode = child;
            Assert.That(childElement.ClassListContains("ai-editor-graph-container-selected"), Is.True);
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
            GraphPresentationItem root = presentation.Find(sequence.uuid);

            Assert.That(root.Kind, Is.EqualTo(GraphPresentationKind.Sequence));
            Assert.That(root.Slots.Select(slot => slot.Label), Is.EqualTo(new[] { "1", "2" }));
            Assert.That(root.Slots[0].Content.Node.Node, Is.SameAs(first));
            Assert.That(root.Slots[1].Content.Kind, Is.EqualTo(GraphPresentationKind.Condition));
            Assert.That(root.Slots[1].Content.Slots.Select(slot => slot.Label), Is.EqualTo(new[] { "Condition", "True", "False" }));
            Assert.That(presentation.ExternalEdges, Is.Empty);
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
            GraphPresentationItem root = presentation.Find(sequence.uuid);

            Assert.That(root.Slots[0].Content.Kind, Is.EqualTo(GraphPresentationKind.Card));
            Assert.That(root.Slots[1].Content.Kind, Is.EqualTo(GraphPresentationKind.ReferenceProxy));
            Assert.That(root.Slots[2].Content.Kind, Is.EqualTo(GraphPresentationKind.Missing));
            Assert.That(presentation.Find(child.uuid), Is.SameAs(root.Slots[0].Content));
            Assert.That(presentation.ExternalEdges.Count(edge => edge.TargetUUID == child.uuid), Is.EqualTo(1));
        }

        [Test]
        public void Presentation_UsesCycleProxyAndKeepsRawReferenceExternal()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            sequence.events = new[] { sequence.ToReference() };
            BehaviourTreeData cycleTree = Tree(sequence);
            GraphPresentation cyclePresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(cycleTree));

            Assert.That(cyclePresentation.Find(sequence.uuid).Slots[0].Content.Kind, Is.EqualTo(GraphPresentationKind.ReferenceProxy));
            Assert.That(cyclePresentation.Find(sequence.uuid).Slots[0].Content.Warning, Does.Contain("Cycle"));
            Assert.That(cyclePresentation.ExternalEdges.Count, Is.EqualTo(1));

            TestHost head = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            head.children = new[] { child.ToReference() };
            head.raw = new RawNodeReference { UUID = child.uuid };
            BehaviourTreeData rawTree = Tree(head, child);
            GraphPresentation rawPresentation = GraphPresentationBuilder.Build(GraphTopologyBuilder.Build(rawTree, includeRawReferences: true));

            Assert.That(rawPresentation.ExternalEdges.Any(edge => edge.Kind == GraphEdgeKind.Raw), Is.True);
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
            Assert.That(presentation.Find(sequence.uuid).Size.y, Is.GreaterThan(GraphLayoutResolver.GetNodeSize(topology.FindNode(sequence.uuid)).y));
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
