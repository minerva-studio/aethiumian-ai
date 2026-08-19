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
using System.IO;
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
    /// <summary>Graph Editor GraphCanvasInteraction contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphCanvasInteractionTests : GraphEditorTestFixture
    {
        /// <summary>Verifies the light shell and a real Head node resolve readable canvas and title styles.</summary>
        [UnityTest]
        public IEnumerator CreateGUI_LightTheme_ResolvesReadableCanvasAndGraphText()
        {
            if (EditorGUIUtility.isProSkin)
            {
                Assert.Ignore("The current Editor skin is dark; run this focused assertion in a light Editor session.");
            }

            TestNode head = Node<TestNode>("Head");
            AIEditorWindow window = ShowGraphWindow(Tree(head));
            yield return null;

            VisualElement shell = window.rootVisualElement.Q<VisualElement>("ai-editor-shell");
            GraphCanvasElement canvas = GetGraphModule(window).Canvas;
            GraphNodeElement node = canvas.Q<GraphNodeElement>($"ai-editor-graph-node-{head.uuid}");
            Label title = node?.Q<Label>(className: "ai-editor-graph-node-title");

            Assert.That(shell.ClassListContains("ai-editor-theme-light"), Is.True);
            Assert.That(canvas.resolvedStyle.backgroundColor.r, Is.GreaterThan(0.5f));
            Assert.That(node, Is.Not.Null);
            Assert.That(title, Is.Not.Null);
            Assert.That(title.resolvedStyle.color.r, Is.LessThan(canvas.resolvedStyle.backgroundColor.r));
        }

        [Test]
        public void GraphGroupDescendants_AreCanvasInteractionTargetsAndPointerCancelStopsDrag()
        {
            TestNode node = Node<TestNode>("Grouped");
            BehaviourTreeData tree = Tree(node);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphGroupLayoutEntry groupData = new(UUID.NewUUID(), "Frame", Color.blue, new[] { node.uuid });
            GraphGroupElement group = new(module, groupData, new Rect(0f, 0f, 200f, 120f));
            TextField rename = group.Q<TextField>("rename");
            MethodInfo isNodeTarget = typeof(GraphCanvasElement).GetMethod("IsNodeTarget", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(isNodeTarget, Is.Not.Null);
            Assert.That((bool)isNodeTarget.Invoke(null, new object[] { rename }), Is.True);

            VisualElement titleBar = group.Q<VisualElement>("title-bar");
            Assert.That(titleBar.style.paddingLeft.value.value, Is.EqualTo(10f));
            Assert.That(titleBar.style.paddingRight.value.value, Is.EqualTo(10f));
            Assert.That(titleBar.style.paddingTop.value.value, Is.EqualTo(4f));
            Assert.That(titleBar.style.paddingBottom.value.value, Is.EqualTo(4f));
            Event downEvent = new()
            {
                type = EventType.MouseDown,
                button = 0,
                mousePosition = new Vector2(10f, 10f),
            };
            using PointerDownEvent down = PointerDownEvent.GetPooled(downEvent);
            titleBar.SendEvent(down);
            using PointerCancelEvent cancel = PointerCancelEvent.GetPooled(down);
            titleBar.SendEvent(cancel);
            Assert.That(tree.GraphLayout, Is.Null);
        }

        [Test]
        public void GraphSequence_ShortCircuitPathsFollowOnlyTheSelectedDirectMember()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            sequence.events = new[] { first.ToReference(), second.ToReference() };
            first.parent = sequence.ToReference();
            second.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphCanvasElement canvas = module.Canvas;
            GraphPresentation originalPresentation = canvas.Presentation;
            GraphEdgeLayerElement edges = canvas.Q<GraphEdgeLayerElement>();
            EditorUtility.ClearDirty(tree);

            Assert.That(VisibleEdgeLabels(edges), Does.Contain("Next"));
            Assert.That(VisibleEdgeLabels(edges), Does.Contain("Complete"));
            Assert.That(VisibleEdgeLabels(edges), Does.Not.Contain("False · Failed"));

            canvas.SetSelectedNode(first);
            Assert.That(VisibleEdgeLabels(edges), Does.Contain("True · Next"));
            Assert.That(VisibleEdgeLabels(edges).Count(label => label == "False · Failed"), Is.EqualTo(1));

            canvas.SetSelectedNode(second);
            Assert.That(VisibleEdgeLabels(edges), Does.Contain("Next"));
            Assert.That(VisibleEdgeLabels(edges), Does.Contain("True · Success"));
            Assert.That(VisibleEdgeLabels(edges).Count(label => label == "False · Failed"), Is.EqualTo(1));

            canvas.SetSelectedNode(sequence);
            Assert.That(VisibleEdgeLabels(edges), Does.Not.Contain("False · Failed"));
            Assert.That(VisibleEdgeLabels(edges), Does.Contain("Complete"));

            canvas.SetSelectedNodes(new[] { first.uuid, second.uuid });
            Assert.That(VisibleEdgeLabels(edges), Does.Not.Contain("False · Failed"));
            Assert.That(canvas.Presentation, Is.SameAs(originalPresentation));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphSequence_FailureUsesMemberAndCompletionRightCenters()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode child = Node<TestNode>("Child");
            sequence.events = new[] { child.ToReference() };
            child.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, child);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPresentationRelation failure = module.Canvas.Presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.SequenceFailure);
            GraphPresentationItem childItem = module.Canvas.Presentation.Find(child.uuid);
            GraphSequenceScope scope = module.Canvas.Presentation.Find(sequence.uuid).SequenceScope;
            GraphEdgeLayerElement edges = module.Canvas.Q<GraphEdgeLayerElement>();
            EditorUtility.ClearDirty(tree);

            Assert.That(edges.GetSourceAnchor(failure), Is.EqualTo(new Vector2(
                childItem.Position.x + childItem.Size.x,
                childItem.Position.y + childItem.Size.y * 0.5f)).Within(0.01f));
            Assert.That(edges.GetTargetAnchor(failure), Is.EqualTo(new Vector2(
                scope.CompletionPosition.x + scope.CompletionSize.x,
                scope.CompletionPosition.y + scope.CompletionSize.y * 0.5f)).Within(0.01f));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphLoop_SideRailsUseDecoratorStackAndBodySideCenters()
        {
            Loop loop = Node<Loop>("Loop");
            Inverter inverter = Node<Inverter>("Inverter");
            Always always = Node<Always>("Always");
            Aethiumian.AI.Nodes.Boolean predicate = Node<Aethiumian.AI.Nodes.Boolean>("Predicate");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = inverter.ToReference();
            loop.events = new[] { body.ToReference() };
            inverter.node = always.ToReference();
            always.node = predicate.ToReference();
            inverter.parent = loop.ToReference();
            always.parent = inverter.ToReference();
            predicate.parent = always.ToReference();
            body.parent = loop.ToReference();
            BehaviourTreeData tree = Tree(loop, inverter, always, predicate, body);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPresentation presentation = module.Canvas.Presentation;
            GraphEdgeLayerElement edges = module.Canvas.Q<GraphEdgeLayerElement>();
            GraphPresentationRelation repeat = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopRepeat);
            GraphPresentationRelation exit = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.LoopExit);
            GraphDecoratorStack stack = presentation.FindDecoratorStack(inverter.uuid);
            Rect stackBounds = GetDecoratorStackBounds(stack);
            GraphPresentationItem bodyItem = presentation.Find(body.uuid);
            GraphLoopScope scope = presentation.Find(loop.uuid).LoopScope;
            EditorUtility.ClearDirty(tree);

            Assert.That(edges.GetSourceAnchor(repeat), Is.EqualTo(new Vector2(
                bodyItem.Position.x,
                bodyItem.Position.y + bodyItem.Size.y * 0.5f)).Within(0.01f));
            Assert.That(edges.GetTargetAnchor(repeat), Is.EqualTo(new Vector2(
                stackBounds.xMin,
                stackBounds.center.y)).Within(0.01f));
            Assert.That(edges.GetSourceAnchor(exit), Is.EqualTo(new Vector2(
                stackBounds.xMax,
                stackBounds.center.y)).Within(0.01f));
            Assert.That(edges.GetTargetAnchor(exit), Is.EqualTo(new Vector2(
                scope.CompletionPosition.x + scope.CompletionSize.x,
                scope.CompletionPosition.y + scope.CompletionSize.y * 0.5f)).Within(0.01f));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphForEach_RepeatUsesBodyAndCheckLeftCenters()
        {
            ForEach flow = Node<ForEach>("For Each");
            TestNode body = Node<TestNode>("Body");
            VariableData enumerable = new("Items", VariableType.Generic);
            VariableData item = new("Item", VariableType.Generic);
            flow.enumerable = new VariableReference();
            flow.enumerable.SetReference(enumerable);
            flow.item = new VariableReference();
            flow.item.SetReference(item);
            flow.@event = body.ToReference();
            body.parent = flow.ToReference();
            BehaviourTreeData tree = Tree(flow, body);
            tree.variables.Add(enumerable);
            tree.variables.Add(item);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphPresentation presentation = module.Canvas.Presentation;
            GraphPresentationRelation repeat = presentation.Relations.Single(relation =>
                relation.Kind == GraphPresentationRelationKind.ForEachRepeat);
            GraphPresentationItem bodyItem = presentation.Find(body.uuid);
            GraphPresentationItem check = presentation.Find(flow.uuid).ForEachScope.Check;
            GraphEdgeLayerElement edges = module.Canvas.Q<GraphEdgeLayerElement>();
            EditorUtility.ClearDirty(tree);

            Assert.That(edges.GetSourceAnchor(repeat), Is.EqualTo(new Vector2(
                bodyItem.Position.x,
                bodyItem.Position.y + bodyItem.Size.y * 0.5f)).Within(0.01f));
            Assert.That(edges.GetTargetAnchor(repeat), Is.EqualTo(new Vector2(
                check.Position.x,
                check.Position.y + check.Size.y * 0.5f)).Within(0.01f));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Gets the complete canvas bounds occupied by one attached decorator stack.</summary>
        private static Rect GetDecoratorStackBounds(GraphDecoratorStack stack)
        {
            Rect bounds = new(stack.Anchor.Position, stack.Anchor.Size);
            foreach (GraphPresentationItem badge in stack.Badges)
            {
                Rect badgeBounds = new(badge.Position, badge.Size);
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, badgeBounds.xMin),
                    Mathf.Min(bounds.yMin, badgeBounds.yMin),
                    Mathf.Max(bounds.xMax, badgeBounds.xMax),
                    Mathf.Max(bounds.yMax, badgeBounds.yMax));
            }

            return bounds;
        }

        /// <summary>Gets the currently displayed semantic edge labels.</summary>
        private static List<string> VisibleEdgeLabels(GraphEdgeLayerElement edges)
        {
            return edges.Query<Label>().ToList()
                .Where(label => label.style.display.value != DisplayStyle.None)
                .Select(label => label.text)
                .ToList();
        }

        [Test]
        public void GraphGroup_MoveAppliesTheSameDeltaToAllMembers()
        {
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(first, second);
            UUID groupUUID = UUID.NewUUID();
            Vector2 firstPosition = new(10f, 20f);
            Vector2 secondPosition = new(70f, 45f);
            tree.GraphLayout = GraphLayoutData.Create(
                new[]
                {
                    new GraphLayoutEntry(first.uuid, firstPosition),
                    new GraphLayoutEntry(second.uuid, secondPosition),
                },
                groupEntries: new[]
                {
                    new GraphGroupLayoutEntry(groupUUID, "Frame", Color.blue, new[] { first.uuid, second.uuid }),
                });
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            Vector2 delta = new(25f, 10f);

            Assert.That(module.MoveGroup(groupUUID, delta), Is.True);

            Assert.That(module.Topology.FindNode(first.uuid).Position, Is.EqualTo(firstPosition + delta));
            Assert.That(module.Topology.FindNode(second.uuid).Position, Is.EqualTo(secondPosition + delta));
            Assert.That(module.Topology.FindNode(second.uuid).Position - module.Topology.FindNode(first.uuid).Position,
                Is.EqualTo(secondPosition - firstPosition));
        }

        [UnityTest]
        public IEnumerator GraphGroup_BodyClickSelectsAndEscapeOrBlankClearsSelection()
        {
            TestNode node = Node<TestNode>("Grouped");
            BehaviourTreeData tree = Tree(node);
            UUID groupUUID = UUID.NewUUID();
            tree.GraphLayout = GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>(), groupEntries: new[]
            {
                new GraphGroupLayoutEntry(groupUUID, "Frame", Color.blue, new[] { node.uuid }),
            });
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;
            GraphEditorModule module = GetGraphModule(window);
            GraphCanvasElement canvas = module.Canvas;
            GraphGroupElement group = canvas.Q<GraphGroupElement>($"ai-editor-graph-group-{groupUUID}");

            Assert.That(group, Is.Not.Null);
            Assert.That(group.pickingMode, Is.EqualTo(PickingMode.Position));
            Vector2 bodyPoint = new(group.worldBound.xMax - 4f, group.worldBound.yMax - 4f);
            VisualElement bodyTarget = canvas.panel.Pick(bodyPoint);
            Assert.That(bodyTarget, Is.Not.Null);
            Assert.That(bodyTarget, Is.Not.SameAs(group.Q<VisualElement>("title-bar")));
            GraphGroupElement resolvedGroup = bodyTarget as GraphGroupElement ?? bodyTarget.GetFirstAncestorOfType<GraphGroupElement>();
            Assert.That(resolvedGroup, Is.SameAs(group));
            SendPointerDown(bodyTarget, 0, bodyPoint);
            SendPointerUp(bodyTarget, 0, bodyPoint);
            Assert.That(module.SelectedGroupUUID, Is.EqualTo(groupUUID));
            Assert.That(group.ClassListContains("ai-editor-graph-group-selected"), Is.True);

            Assert.That(SendKeyDown(canvas, KeyCode.Escape), Is.True);
            Assert.That(module.SelectedGroupUUID, Is.EqualTo(UUID.Empty));
            Assert.That(group.ClassListContains("ai-editor-graph-group-selected"), Is.False);

            SendPointerDown(bodyTarget, 0, bodyPoint);
            SendPointerUp(bodyTarget, 0, bodyPoint);
            Vector2 blank = canvas.LocalToWorld(new Vector2(canvas.layout.width - 24f, canvas.layout.height - 24f));
            SendPointerDown(canvas, 0, blank);
            SendPointerUp(canvas, 0, blank);
            Assert.That(module.SelectedGroupUUID, Is.EqualTo(UUID.Empty));
            Assert.That(group.ClassListContains("ai-editor-graph-group-selected"), Is.False);

            SendPointerDown(bodyTarget, 0, bodyPoint);
            SendPointerUp(bodyTarget, 0, bodyPoint);
            Assert.That(SendKeyDown(canvas, KeyCode.Delete), Is.True);
            Assert.That(tree.GetNode(node.uuid), Is.SameAs(node));
            Assert.That(tree.GraphLayout.Groups, Is.Empty);
        }

        [UnityTest]
        public IEnumerator GraphGroup_F2OpensRenameEditorAndEscapeDoesNotRenameOrDirty()
        {
            TestNode node = Node<TestNode>("Grouped");
            BehaviourTreeData tree = Tree(node);
            UUID groupUUID = UUID.NewUUID();
            const string title = "Frame";
            tree.GraphLayout = GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>(), groupEntries: new[]
            {
                new GraphGroupLayoutEntry(groupUUID, title, Color.blue, new[] { node.uuid }),
            });
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;

            GraphEditorModule module = GetGraphModule(window);
            GraphCanvasElement canvas = module.Canvas;
            GraphGroupElement group = canvas.Q<GraphGroupElement>($"ai-editor-graph-group-{groupUUID}");
            TextField renameEditor = group.Q<TextField>("rename");
            module.SelectGroup(groupUUID);
            EditorUtility.ClearDirty(tree);

            Assert.That(SendKeyDown(canvas, KeyCode.F2), Is.True);
            Assert.That(renameEditor.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(renameEditor.panel.focusController.focusedElement, Is.SameAs(renameEditor));
            Assert.That(renameEditor.value, Is.EqualTo(title));

            Assert.That(SendKeyDown(renameEditor, KeyCode.Escape), Is.False);
            Assert.That(tree.GraphLayout.Groups.Single(groupData => groupData.UUID == groupUUID).Title, Is.EqualTo(title));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [UnityTest]
        public IEnumerator GraphGroup_SequenceFrameIncludesOwnedCompletionBounds()
        {
            Sequence sequence = Node<Sequence>("Sequence");
            TestNode child = Node<TestNode>("Child");
            sequence.events = new[] { child.ToReference() };
            child.parent = sequence.ToReference();
            BehaviourTreeData tree = Tree(sequence, child);
            UUID groupUUID = UUID.NewUUID();
            tree.GraphLayout = GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>(), groupEntries: new[]
            {
                new GraphGroupLayoutEntry(groupUUID, "Sequence Frame", Color.blue, new[] { sequence.uuid }),
            });
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;
            GraphCanvasElement canvas = GetGraphModule(window).Canvas;
            GraphFlowScope scope = canvas.Presentation.CompletionScopes.Single(value =>
                value.Owner.TargetUUID == sequence.uuid);
            GraphGroupElement group = canvas.Q<GraphGroupElement>($"ai-editor-graph-group-{groupUUID}");

            Assert.That(group, Is.Not.Null);
            float frameYMax = group.style.top.value.value + group.style.height.value.value;
            float completionYMax = scope.CompletionPosition.y + scope.CompletionSize.y;
            Assert.That(frameYMax, Is.GreaterThanOrEqualTo(completionYMax + 18f));
        }
        private static void AssertPresentationItemsInsideViewport(GraphCanvasElement canvas, params UUID[] uuids)
        {
            foreach (UUID uuid in uuids)
            {
                GraphPresentationItem item = canvas.Presentation.Find(uuid);
                Rect bounds = GraphPresentationLayout.GetBounds(item);
                Vector2 minimum = canvas.GraphToViewport(bounds.min);
                Vector2 maximum = canvas.GraphToViewport(bounds.max);
                Assert.That(minimum.x, Is.GreaterThanOrEqualTo(0f), uuid.ToString());
                Assert.That(minimum.y, Is.GreaterThanOrEqualTo(0f), uuid.ToString());
                Assert.That(maximum.x, Is.LessThanOrEqualTo(canvas.layout.width), uuid.ToString());
                Assert.That(maximum.y, Is.LessThanOrEqualTo(canvas.layout.height), uuid.ToString());
            }
        }

        [Test]
        public void GraphSelection_MultipleNodesClearWindowSelectionAndPreserveOrderedSet()
        {
            TestHost head = Node<TestHost>("Head");
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            BehaviourTreeData tree = Tree(head, first, second);
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            module.SetGraphSelection(new TreeNode[] { first, second, first });

            Assert.That(module.SelectedNodes, Is.EqualTo(new TreeNode[] { first, second }));
            Assert.That(module.SelectedNode, Is.Null);
            Assert.That(module.IsNodeSelected(first), Is.True);
            Assert.That(module.IsNodeSelected(head), Is.False);
        }

        [Test]
        public void GraphNavigation_UsesSpatialCandidatesAndOrderedShiftSelection()
        {
            TestNode center = Node<TestNode>("Center");
            TestNode right = Node<TestNode>("Right");
            TestNode down = Node<TestNode>("Down");
            TestNode diagonal = Node<TestNode>("Diagonal");
            BehaviourTreeData tree = Tree(center, right, down, diagonal);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(center.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(right.uuid, new Vector2(220f, 0f)),
                new GraphLayoutEntry(down.uuid, new Vector2(0f, 180f)),
                new GraphLayoutEntry(diagonal.uuid, new Vector2(220f, 180f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            module.SetGraphSelection(new[] { center });
            Assert.That(module.NavigateSelection(GraphNavigationDirection.Right, extend: false), Is.True);
            Assert.That(module.SelectedNodes, Is.EqualTo(new TreeNode[] { right }));
            Assert.That(module.NavigateSelection(GraphNavigationDirection.Right, extend: false), Is.True);
            Assert.That(module.SelectedNodes, Is.EqualTo(new[] { right }));
            Assert.That(module.NavigateSelection(GraphNavigationDirection.Down, extend: true), Is.True);
            Assert.That(module.SelectedNodes, Is.EqualTo(new[] { right, diagonal }));
        }

        [UnityTest]
        public IEnumerator GraphNavigation_EmptySelectionStartsAtViewportCenter()
        {
            TestNode near = Node<TestNode>("Near");
            TestNode far = Node<TestNode>("Far");
            BehaviourTreeData tree = Tree(near, far);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(near.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(far.uuid, new Vector2(900f, 900f)),
            });
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            GraphEditorModule module = (GraphEditorModule)typeof(AIEditorWindow)
                .GetField("graphModule", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(window);
            module.SetGraphSelection(Array.Empty<TreeNode>());
            yield return null;

            module.Canvas.Pan = new Vector2(320f, 240f);
            module.Canvas.Zoom = 1f;
            Assert.That(module.NavigateSelection(GraphNavigationDirection.Right, extend: true), Is.True);
            Assert.That(module.SelectedNodes, Is.EqualTo(new TreeNode[] { near }));
        }

        [Test]
        public void GraphNavigation_ExcludesEmbeddedConditionPredicate()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode sibling = Node<TestNode>("Sibling");
            condition.condition = predicate.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, sibling);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(condition.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(predicate.uuid, new Vector2(0f, 160f)),
                new GraphLayoutEntry(sibling.uuid, new Vector2(260f, 0f)),
            });
            GraphEditorModule module = CreateHiddenGraphModule(tree);

            IReadOnlyList<GraphNavigationCandidate> candidates = module.Canvas.GetNavigableCandidates();

            Assert.That(candidates.Any(candidate => candidate.UUID == condition.uuid), Is.True);
            Assert.That(candidates.Any(candidate => candidate.UUID == sibling.uuid), Is.True);
            Assert.That(candidates.Any(candidate => candidate.UUID == predicate.uuid), Is.False);
        }

        [UnityTest]
        public IEnumerator GraphNavigation_RevealPreservesZoomAndDoesNotWriteLayout()
        {
            TestNode node = Node<TestNode>("Node");
            BehaviourTreeData tree = Tree(node);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(node.uuid, new Vector2(900f, 700f)),
            });
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            canvas.Zoom = 1.25f;
            canvas.Pan = Vector2.zero;
            Vector2 beforePan = canvas.Pan;
            float beforeZoom = canvas.Zoom;

            Assert.That(canvas.RevealGraphBounds(new Rect(900f, 700f, 168f, 40f)), Is.True);
            Assert.That(canvas.Zoom, Is.EqualTo(beforeZoom));
            Assert.That(canvas.Pan, Is.Not.EqualTo(beforePan));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [UnityTest]
        public IEnumerator GraphKeyboard_ArrowShiftAndFrameEvents()
        {
            TestNode center = Node<TestNode>("Center");
            TestNode right = Node<TestNode>("Right");
            TestNode far = Node<TestNode>("Far");
            BehaviourTreeData tree = Tree(center, right, far);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(center.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(right.uuid, new Vector2(220f, 0f)),
                new GraphLayoutEntry(far.uuid, new Vector2(500f, 0f)),
            });
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            GraphEditorModule graphModule = GetGraphModule(window);
            EditorUtility.ClearDirty(tree);
            graphModule.SetGraphSelection(new[] { center });
            Assert.That(graphModule.SelectedNodes, Is.EqualTo(new[] { center }));
            Assert.That(SendKeyDown(canvas, KeyCode.RightArrow), Is.True);
            Assert.That(window.SelectedNode, Is.SameAs(right));

            Assert.That(SendKeyDown(canvas, KeyCode.RightArrow, EventModifiers.Shift), Is.True);
            Assert.That(GetGraphModule(window).SelectedNodes, Is.EqualTo(new TreeNode[] { right, far }));

            Vector2 pan = canvas.Pan;
            float zoom = canvas.Zoom;
            EditorUtility.ClearDirty(tree);
            Assert.That(SendKeyDown(canvas, KeyCode.RightArrow), Is.True);
            Assert.That(GetGraphModule(window).SelectedNodes, Is.EqualTo(new TreeNode[] { right, far }));
            Assert.That(SendKeyDown(canvas, KeyCode.RightArrow), Is.True);
            Assert.That(GetGraphModule(window).SelectedNodes, Is.EqualTo(new TreeNode[] { right, far }));
            Assert.That(canvas.Pan, Is.EqualTo(pan).Within(0.001f));
            Assert.That(canvas.Zoom, Is.EqualTo(zoom).Within(0.001f));
            EditorUtility.ClearDirty(tree);
            Assert.That(SendKeyDown(canvas, KeyCode.RightArrow), Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            GetGraphModule(window).SetGraphSelection(Array.Empty<TreeNode>());
            Vector2 emptyPan = canvas.Pan;
            float emptyZoom = canvas.Zoom;
            Assert.That(SendKeyDown(canvas, KeyCode.F), Is.False);
            Assert.That(canvas.Pan, Is.EqualTo(emptyPan));
            Assert.That(canvas.Zoom, Is.EqualTo(emptyZoom));

            GetGraphModule(window).SetGraphSelection(new[] { center });
            Assert.That(SendKeyDown(canvas, KeyCode.F), Is.True);
            Rect bounds = GraphPresentationLayout.GetBounds(canvas.Presentation.Find(center.uuid));
            Assert.That(Vector2.Distance(canvas.GraphToViewport(bounds.center),
                new Vector2(canvas.layout.width * 0.5f, canvas.layout.height * 0.5f)), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator GraphKeyboard_EscapeAndEdgePriority()
        {
            TestNode node = Node<TestNode>("Node");
            BehaviourTreeData tree = Tree(node);
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            GetGraphModule(window).SetGraphSelection(new[] { node });
            EditorUtility.ClearDirty(tree);
            Assert.That(SendKeyDown(canvas, KeyCode.Escape), Is.True);
            Assert.That(GetGraphModule(window).SelectedNodes, Is.Empty);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(SendKeyDown(canvas, KeyCode.Escape), Is.False);

            TestHost host = Node<TestHost>("Host");
            TestNode child = Node<TestNode>("Child");
            host.children = new[] { child.ToReference() };
            child.parent = host.ToReference();
            BehaviourTreeData edgeTree = Tree(host, child);
            AIEditorWindow edgeWindow = ShowGraphWindow(edgeTree);
            yield return null;

            GraphCanvasElement edgeCanvas = edgeWindow.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            GraphEdgeLayerElement edgeLayer = GetPrivateField<GraphEdgeLayerElement>(edgeCanvas, "edgeLayer");
            GraphPresentationRelation relation = edgeCanvas.Presentation.Relations.Single(item => item.Origin != null);
            Vector2 from = edgeLayer.GetSourceAnchor(relation);
            Vector2 to = GraphPortLayerElement.GetTargetPosition(edgeCanvas.Presentation.Find(child.uuid));
            Assert.That(edgeLayer.SelectAt((from + to) * 0.5f, 8f), Is.True);
            Assert.That(SendKeyDown(edgeCanvas, KeyCode.Escape), Is.True);
            Assert.That(edgeLayer.SelectedRelation, Is.Null);

            Assert.That(edgeLayer.SelectAt((from + to) * 0.5f, 8f), Is.True);
            Assert.That(SendKeyDown(edgeCanvas, KeyCode.Delete), Is.True);
            Assert.That(host.children, Is.Empty);
        }

        [UnityTest]
        public IEnumerator GraphKeyboard_TextFieldOwnsShortcuts()
        {
            TestNode node = Node<TestNode>("Node");
            TestNode right = Node<TestNode>("Right");
            BehaviourTreeData tree = Tree(node, right);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(node.uuid, Vector2.zero),
                new GraphLayoutEntry(right.uuid, new Vector2(220f, 0f)),
            });
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            window.SelectedNode = node;
            TextField field = new() { value = "editing" };
            canvas.Add(field);
            field.Focus();
            Assert.That(SendKeyDown(field, KeyCode.RightArrow), Is.False);
            Assert.That(SendKeyDown(field, KeyCode.F), Is.False);
            Assert.That(SendKeyDown(field, KeyCode.Delete), Is.False);
            Assert.That(SendKeyDown(field, KeyCode.Escape), Is.False);
            Assert.That(window.SelectedNode, Is.SameAs(node));

            Vector2 blank = canvas.LocalToWorld(new Vector2(canvas.layout.width - 24f, canvas.layout.height - 24f));
            SendPointerDown(canvas, 1, blank);
            SendPointerUp(canvas, 1, blank);
            yield return null;
            GraphNodeCreationPalette palette = canvas.Q<GraphNodeCreationPalette>();
            Assert.That(palette, Is.Not.Null);
            ToolbarSearchField searchField = palette.Q<ToolbarSearchField>("ai-editor-graph-node-creation-search");
            Assert.That(SendKeyDown(searchField, KeyCode.F), Is.False);
            Assert.That(SendKeyDown(searchField, KeyCode.Delete), Is.False);
            Assert.That(SendKeyDown(searchField, KeyCode.Escape), Is.False);
        }

        [UnityTest]
        public IEnumerator GraphKeyboard_SelectionChangeAndRebuildResetAnchor()
        {
            TestNode first = Node<TestNode>("First");
            TestNode second = Node<TestNode>("Second");
            TestNode third = Node<TestNode>("Third");
            BehaviourTreeData tree = Tree(first, second, third);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(first.uuid, Vector2.zero),
                new GraphLayoutEntry(second.uuid, new Vector2(220f, 0f)),
                new GraphLayoutEntry(third.uuid, new Vector2(440f, 0f)),
            });
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;

            GraphEditorModule module = GetGraphModule(window);
            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            module.SetGraphSelection(new[] { first });
            Assert.That(SendKeyDown(canvas, KeyCode.RightArrow), Is.True);
            Assert.That(window.SelectedNode, Is.SameAs(second));

            module.SetGraphSelection(new[] { third });
            module.RebuildTopology();
            Assert.That(SendKeyDown(canvas, KeyCode.LeftArrow), Is.True);
            Assert.That(window.SelectedNode, Is.SameAs(second));
        }

        [UnityTest]
        public IEnumerator GraphSelection_MarqueeSelectsConditionHeaderInBothDirectionsAndAdditively()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            TestNode existing = Node<TestNode>("Existing");
            condition.condition = predicate.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate, existing);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(condition.uuid, new Vector2(0f, 0f)),
                new GraphLayoutEntry(predicate.uuid, new Vector2(0f, 120f)),
                new GraphLayoutEntry(existing.uuid, new Vector2(420f, 0f)),
            });
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            canvas.Zoom = 1.25f;
            canvas.Pan = new Vector2(37f, 49f);
            yield return null;

            GraphConditionElement conditionElement = window.rootVisualElement.Q<GraphConditionElement>();
            GraphNodeElement existingElement = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{existing.uuid}");
            Assert.That(conditionElement, Is.Not.Null);
            Assert.That(existingElement, Is.Not.Null);
            Rect headerBounds = conditionElement.MarqueeWorldBound;
            Vector2 start = canvas.PanelToViewport(headerBounds.min - new Vector2(3f, 3f));
            Vector2 end = canvas.PanelToViewport(headerBounds.max + new Vector2(3f, 3f));

            canvas.CompleteMarqueeSelection(start, end, additive: false);
            Assert.That(window.SelectedNode, Is.SameAs(condition));
            Assert.That(conditionElement.ClassListContains("ai-editor-graph-condition-selected"), Is.True);

            canvas.CompleteMarqueeSelection(end, start, additive: false);
            Assert.That(window.SelectedNode, Is.SameAs(condition));

            window.SelectedNode = existing;
            canvas.CompleteMarqueeSelection(start, end, additive: true);
            Assert.That(window.SelectedNode, Is.Null);

            Assert.That(conditionElement.ClassListContains("ai-editor-graph-condition-selected"), Is.True);
            Assert.That(existingElement.ClassListContains("ai-editor-graph-node-selected"), Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [UnityTest]
        public IEnumerator GraphSelection_MarqueeConditionBodySelectsPredicateWithoutOwner()
        {
            Condition condition = Node<Condition>("Condition");
            TestNode predicate = Node<TestNode>("Predicate");
            condition.condition = predicate.ToReference();
            BehaviourTreeData tree = Tree(condition, predicate);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            GraphConditionElement conditionElement = window.rootVisualElement.Q<GraphConditionElement>();
            GraphNodeElement predicateElement = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{predicate.uuid}");
            Assert.That(conditionElement, Is.Not.Null);
            Assert.That(predicateElement, Is.Not.Null);
            Assert.That(predicateElement.parent, Is.SameAs(conditionElement));
            Assert.That(conditionElement.MarqueeWorldBound.Overlaps(predicateElement.worldBound), Is.False);

            Rect predicateBounds = predicateElement.worldBound;
            canvas.CompleteMarqueeSelection(
                canvas.PanelToViewport(predicateBounds.min - new Vector2(2f, 2f)),
                canvas.PanelToViewport(predicateBounds.max + new Vector2(2f, 2f)),
                additive: false);

            Assert.That(window.SelectedNode, Is.SameAs(predicate));
            Assert.That(predicateElement.ClassListContains("ai-editor-graph-node-selected"), Is.True);
            Assert.That(conditionElement.ClassListContains("ai-editor-graph-condition-selected"), Is.False);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [UnityTest]
        public IEnumerator GraphWindow_NestedConditionsHostTheirOwnVisiblePredicates()
        {
            Condition outer = Node<Condition>("Outer Condition");
            Aethiumian.AI.Nodes.Boolean outerPredicate = Node<Aethiumian.AI.Nodes.Boolean>("Outer Predicate");
            Condition nested = Node<Condition>("Nested Condition");
            Aethiumian.AI.Nodes.Boolean nestedPredicate = Node<Aethiumian.AI.Nodes.Boolean>("Nested Predicate");
            TestNode success = Node<TestNode>("Success");
            TestNode failure = Node<TestNode>("Failure");
            outer.condition = outerPredicate.ToReference();
            outer.falseNode = nested.ToReference();
            nested.condition = nestedPredicate.ToReference();
            nested.trueNode = success.ToReference();
            nested.falseNode = failure.ToReference();
            BehaviourTreeData tree = Tree(outer, outerPredicate, nested, nestedPredicate, success, failure);
            EditorUtility.ClearDirty(tree);

            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphConditionElement[] conditions = window.rootVisualElement.Query<GraphConditionElement>().ToList().ToArray();
            GraphConditionElement outerElement = conditions.Single(element => element.AuthoredNode == outer);
            GraphConditionElement nestedElement = conditions.Single(element => element.AuthoredNode == nested);
            GraphNodeElement outerPredicateElement = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{outerPredicate.uuid}");
            GraphNodeElement nestedPredicateElement = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{nestedPredicate.uuid}");

            Assert.That(outerPredicateElement.parent, Is.SameAs(outerElement));
            Assert.That(nestedPredicateElement.parent, Is.SameAs(nestedElement));
            Assert.That(outerElement.worldBound.Contains(outerPredicateElement.worldBound.center), Is.True);
            Assert.That(nestedElement.worldBound.Contains(nestedPredicateElement.worldBound.center), Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphView_GridToggleDoesNotDirtyTree()
        {
            BehaviourTreeData tree = Tree(Node<TestNode>("Head"));
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            EditorUtility.ClearDirty(tree);

            Assert.That(module.SnapToGrid, Is.False);
            module.ShowGrid = false;

            Assert.That(module.ShowGrid, Is.False);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-grid")
                .ClassListContains("ai-editor-graph-view-options-button-active"), Is.False);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        [Test]
        public void GraphView_SnapToggleIsIndependentAndDoesNotDirtyTree()
        {
            BehaviourTreeData tree = Tree(Node<TestNode>("Head"));
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            EditorUtility.ClearDirty(tree);
            int undoGroup = Undo.GetCurrentGroup();
            Button snapButton = module.Canvas.Q<Button>("ai-editor-graph-view-options-snap");

            Assert.That(snapButton, Is.Not.Null);
            Assert.That(module.SnapToGrid, Is.False);
            Assert.That(snapButton.ClassListContains("ai-editor-graph-view-options-button-active"), Is.False);

            module.SnapToGrid = true;

            Assert.That(module.SnapToGrid, Is.True);
            Assert.That(module.ShowGrid, Is.True);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-grid")
                .ClassListContains("ai-editor-graph-view-options-button-active"), Is.True);
            Assert.That(snapButton.ClassListContains("ai-editor-graph-view-options-button-active"), Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            module.ShowGrid = false;
            Assert.That(module.SnapToGrid, Is.True);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-grid")
                .ClassListContains("ai-editor-graph-view-options-button-active"), Is.False);
            module.SnapToGrid = false;
            Assert.That(module.ShowGrid, Is.False);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(Undo.GetCurrentGroup(), Is.EqualTo(undoGroup));
        }

        [Test]
        public void GraphView_UsesCollapsibleIconToolbar()
        {
            GraphEditorModule module = CreateHiddenGraphModule(Tree(Node<TestNode>("Head")));

            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-expand"), Is.Not.Null);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-grid"), Is.Not.Null);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-snap"), Is.Not.Null);
            Assert.That(module.Canvas.Q<Toggle>("ai-editor-graph-view-options-grid"), Is.Null);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-grid").text, Is.EqualTo("▦"));
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-grid").Q<Image>(), Is.Null);
            Image fitAllIcon = module.Canvas.Q<Button>("ai-editor-graph-view-options-fit-all").Q<Image>();
            Image frameSelectedIcon = module.Canvas.Q<Button>("ai-editor-graph-view-options-frame-selected").Q<Image>();
            Assert.That(fitAllIcon, Is.Not.Null);
            Assert.That(frameSelectedIcon, Is.Not.Null);
            StringAssert.StartsWith("d_BoundsField", fitAllIcon.image.name);
            StringAssert.StartsWith("d_RectTool", frameSelectedIcon.image.name);
            Assert.That(module.Canvas.Q<VisualElement>("ai-editor-graph-visibility-options"), Is.Not.Null);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-visibility-options-services").Q<Image>(), Is.Not.Null);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-visibility-options-raw-references").Q<Image>(), Is.Not.Null);
        }

        [Test]
        public void GraphView_VisibilityButtonsReceiveClicks()
        {
            AIEditorWindow window = ShowGraphWindow(Tree(Node<TestNode>("Head")));
            GraphEditorModule module = GetGraphModule(window);
            Button expandButton = module.Canvas.Q<Button>("ai-editor-graph-view-options-expand");
            Button servicesButton = module.Canvas.Q<Button>("ai-editor-graph-visibility-options-services");
            Button rawReferencesButton = module.Canvas.Q<Button>("ai-editor-graph-visibility-options-raw-references");

            Assert.That(module.Canvas.panel, Is.Not.Null);
            Assert.That(expandButton, Is.Not.Null);
            Assert.That(servicesButton.panel, Is.SameAs(module.Canvas.panel));
            Assert.That(rawReferencesButton.panel, Is.SameAs(module.Canvas.panel));
            Assert.That(module.ShowServices, Is.False);
            Assert.That(module.ShowRawReferences, Is.False);

            InvokeButtonClickable(expandButton);
            Assert.That(module.ViewOptionsExpanded, Is.True);

            InvokeButtonClickable(servicesButton);
            Assert.That(module.ShowServices, Is.True);

            InvokeButtonClickable(rawReferencesButton);

            Assert.That(module.ShowRawReferences, Is.True);
        }

        /// <summary>Verifies Graph sidebar state is isolated and serialized per editor window.</summary>
        [Test]
        public void GraphView_SidebarStateIsPerEditorWindow()
        {
            BehaviourTreeData tree = Tree(Node<TestNode>("Head"));
            AIEditorWindow firstWindow = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(firstWindow);
            firstWindow.Load(tree);
            GraphEditorModule firstModule = new(firstWindow);
            firstModule.Attach(CreateDeclaredGraphHost(firstWindow));
            firstModule.ToggleViewOptions();
            firstModule.ShowGrid = false;
            firstModule.SnapToGrid = true;
            firstModule.ShowServices = true;
            firstModule.ToggleRawReferences();
            firstModule.CollapseInspector();

            AIEditorWindow secondWindow = ScriptableObject.CreateInstance<AIEditorWindow>();
            hiddenWindows.Add(secondWindow);
            secondWindow.Load(tree);
            GraphEditorModule secondModule = new(secondWindow);
            secondModule.Attach(CreateDeclaredGraphHost(secondWindow));

            Assert.That(firstModule.ViewOptionsExpanded, Is.True);
            Assert.That(firstModule.ShowGrid, Is.False);
            Assert.That(firstModule.SnapToGrid, Is.True);
            Assert.That(firstModule.ShowServices, Is.True);
            Assert.That(firstModule.ShowRawReferences, Is.True);
            Assert.That(firstModule.InspectorVisible, Is.False);
            Assert.That(secondModule.ViewOptionsExpanded, Is.False);
            Assert.That(secondModule.ShowGrid, Is.True);
            Assert.That(secondModule.SnapToGrid, Is.False);
            Assert.That(secondModule.ShowServices, Is.False);
            Assert.That(secondModule.ShowRawReferences, Is.False);
            Assert.That(secondModule.InspectorVisible, Is.True);

            SerializedObject serializedWindow = new(firstWindow);
            SerializedProperty sidebarState = serializedWindow.FindProperty("graphSidebarState");
            Assert.That(sidebarState, Is.Not.Null);
            Assert.That(sidebarState.FindPropertyRelative("viewOptionsExpanded").boolValue, Is.True);
            Assert.That(sidebarState.FindPropertyRelative("showGrid").boolValue, Is.False);
        }

        [Test]
        public void GraphView_ServiceVisibilityToggleShowsAllScopesWithoutDirtyingTree()
        {
            TestHost head = Node<TestHost>("Head");
            TestService service = Node<TestService>("Service");
            head.services = new List<NodeReference> { service.ToReference() };
            BehaviourTreeData tree = Tree(head, service);
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            GraphServiceScopeElement scope = module.Canvas.Q<GraphServiceScopeElement>();
            Assert.That(scope, Is.Not.Null);
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.None));

            EditorUtility.ClearDirty(tree);
            module.ShowServices = true;

            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            module.ShowServices = false;
            Assert.That(scope.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

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

        [UnityTest]
        public IEnumerator GraphWindow_WhileDecoratorPredicateRendersAsAttachedBadge()
        {
            Loop loop = Node<Loop>("Loop");
            Inverter inverter = Node<Inverter>("Inverter");
            Aethiumian.AI.Nodes.Boolean predicate = Node<Aethiumian.AI.Nodes.Boolean>("Predicate");
            Aethiumian.AI.Nodes.Boolean replacement = Node<Aethiumian.AI.Nodes.Boolean>("Replacement");
            TestNode body = Node<TestNode>("Body");
            loop.loopType = Loop.LoopType.@while;
            loop.condition = inverter.ToReference();
            loop.events = new[] { body.ToReference() };
            inverter.node = predicate.ToReference();
            inverter.parent = loop.ToReference();
            predicate.parent = inverter.ToReference();
            body.parent = loop.ToReference();
            BehaviourTreeData tree = Tree(loop, inverter, predicate, replacement, body);
            tree.GraphLayout = GraphLayoutData.Create(new[]
            {
                new GraphLayoutEntry(loop.uuid, new Vector2(100f, 80f)),
                new GraphLayoutEntry(inverter.uuid, new Vector2(900f, 700f)),
                new GraphLayoutEntry(predicate.uuid, new Vector2(-600f, 420f)),
                new GraphLayoutEntry(replacement.uuid, new Vector2(1100f, 420f)),
                new GraphLayoutEntry(body.uuid, new Vector2(180f, 520f)),
            });
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            GraphNodeElement inverterElement = canvas.Q<GraphNodeElement>($"ai-editor-graph-node-{inverter.uuid}");
            GraphNodeElement predicateElement = canvas.Q<GraphNodeElement>($"ai-editor-graph-node-{predicate.uuid}");
            GraphPresentationItem inverterItem = canvas.Presentation.Find(inverter.uuid);
            GraphPresentationItem predicateItem = canvas.Presentation.Find(predicate.uuid);
            GraphDecoratorStack stack = canvas.Presentation.FindDecoratorStack(inverter.uuid);

            Assert.That(stack, Is.Not.Null);
            Assert.That(stack.Anchor, Is.SameAs(predicateItem));
            Assert.That(inverterElement.ClassListContains("ai-editor-graph-decorator-badge"), Is.True);
            Assert.That(inverterItem.Position.y + inverterItem.Size.y, Is.EqualTo(predicateItem.Position.y).Within(0.01f));
            Assert.That(canvas.Q<GraphEdgeLayerElement>().Query<Label>().ToList().Any(label => label.text == nameof(Inverter.node)), Is.False);
            Assert.That(canvas.Ports.Any(port => port.OwnerUUID == inverter.uuid && port.FieldName == nameof(Inverter.node)), Is.True);
            Assert.That(canvas.GetMoveAnchor(GetGraphModule(window).Topology.FindNode(inverter.uuid)).Node, Is.SameAs(loop));
            Assert.That(predicateElement, Is.Not.Null);

            window.SelectedNode = inverter;
            Assert.That(inverterElement.ClassListContains("ai-editor-graph-node-selected"), Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(tree.GraphLayout.TryGetPosition(inverter.uuid, out Vector2 storedInverter), Is.True);
            Assert.That(storedInverter, Is.EqualTo(new Vector2(900f, 700f)));

            GraphEditorModule module = GetGraphModule(window);
            GraphPortDescriptor childPort = canvas.Ports.Single(port =>
                port.OwnerUUID == inverter.uuid && port.FieldName == nameof(Inverter.node));
            Assert.That(module.Assign(childPort, replacement.uuid), Is.True);
            GraphDecoratorStack rebuilt = module.Canvas.Presentation.FindDecoratorStack(inverter.uuid);
            Assert.That(rebuilt, Is.Not.Null);
            Assert.That(rebuilt.Anchor.Node.Node, Is.SameAs(replacement));
            Assert.That(module.Canvas.Q<GraphNodeElement>($"ai-editor-graph-node-{inverter.uuid}")
                .ClassListContains("ai-editor-graph-decorator-badge"), Is.True);
        }

        [UnityTest]
        public IEnumerator GraphWindow_DecoratorBadgesKeepSemanticTitlesForCustomNames()
        {
            Inverter inverter = Node<Inverter>("Custom Inverter");
            Always alwaysTrue = Node<Always>("Custom Always");
            Always alwaysVariable = Node<Always>("Dynamic Always");
            Capture capture = Node<Capture>("Custom Capture");
            ResultChanged resultChanged = Node<ResultChanged>("Custom Result Changed");
            TestNode captureChild = Node<TestNode>("Capture Child");
            TestNode resultChangedChild = Node<TestNode>("Result Changed Child");
            capture.node = captureChild.ToReference();
            resultChanged.node = resultChangedChild.ToReference();
            VariableData captureResult = new("Captured Result", VariableType.Bool);
            capture.result.SetReference(captureResult);
            alwaysTrue.returnValue = true;
            VariableData dynamicResult = new("Dynamic Always Result", VariableType.Bool);
            alwaysVariable.returnValue.SetReference(dynamicResult);
            BehaviourTreeData tree = Tree(inverter, alwaysTrue, alwaysVariable, capture, resultChanged, captureChild, resultChangedChild);
            tree.variables.Add(dynamicResult);
            tree.variables.Add(captureResult);
            EditorUtility.ClearDirty(tree);

            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            AssertDecoratorTitle(window, inverter.uuid, "NOT", "Custom Inverter");
            AssertDecoratorTitle(window, alwaysTrue.uuid, "ALWAYS T", "Custom Always");
            AssertDecoratorTitle(window, alwaysVariable.uuid, "ALWAYS VAR", "Dynamic Always");
            Assert.That(GetGraphModule(window).Canvas.Presentation.Find(capture.uuid).Node.DisplayName,
                Is.EqualTo("Custom Capture"));
            AssertDecoratorTitle(window, capture.uuid, "CAPTURE → $Captured Result", null);
            AssertDecoratorTitle(window, resultChanged.uuid, "CHANGED", "Custom Result Changed");
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Asserts a compact decorator title and its authored-name tooltip.</summary>
        private static void AssertDecoratorTitle(
            AIEditorWindow window,
            UUID uuid,
            string semanticTitle,
            string authoredName)
        {
            GraphNodeElement element = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{uuid}");
            Label title = element.Q<Label>(className: "ai-editor-graph-node-title");
            Assert.That(title.text, Is.EqualTo(semanticTitle));
            if (!string.IsNullOrEmpty(authoredName))
            {
                Assert.That(title.tooltip, Does.Contain(authoredName));
                Assert.That(title.tooltip, Does.Contain(semanticTitle));
            }
        }

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

        [UnityTest]
        public IEnumerator GraphCanvas_EmptyRightClickOpensCreationPalette()
        {
            Sequence head = Node<Sequence>("Head");
            TestNode child = Node<TestNode>("Child");
            head.events = new[] { child.ToReference() };
            BehaviourTreeData tree = Tree(head, child);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            Vector2 blankPosition = canvas.LocalToWorld(new Vector2(canvas.layout.width - 24f, canvas.layout.height - 24f));
            SendPointerDown(canvas, 1, blankPosition);
            SendPointerUp(canvas, 1, blankPosition);
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Not.Null);

            canvas.CloseCreationPalette();
            GraphNodeElement node = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{head.uuid}");
            Vector2 nodePosition = node.worldBound.center;
            EditorUtility.ClearDirty(tree);
            GraphEdgeLayerElement edgeLayer = canvas.Q<GraphEdgeLayerElement>();
            GraphPresentationRelation relation = canvas.Presentation.Relations.Single(
                value => value.Origin != null && value.IsVisibleFor(null));
            Assert.That(edgeLayer.SelectAt(edgeLayer.GetSourceAnchor(relation), 8f), Is.True);
            Assert.That(SendPointerDownAndGetPropagationState(node, 1, nodePosition), Is.False);
            Assert.That(window.SelectedNode, Is.SameAs(head));
            Assert.That(node.ClassListContains("ai-editor-graph-node-selected"), Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
            Assert.That(edgeLayer.SelectedRelation, Is.Null);
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Null);

            GraphPortDescriptor port = canvas.Ports.First();
            GraphPortLayerElement portLayer = canvas.Q<GraphPortLayerElement>();
            Vector2 portPosition = canvas.LocalToWorld(canvas.GraphToViewport(portLayer.GetSourcePosition(port)));
            VisualElement portTarget = canvas.panel.Pick(portPosition);
            SendPointerDown(portTarget, 1, portPosition);
            SendPointerUp(portTarget, 1, portPosition);
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Null);

            Assert.That(edgeLayer.SelectAt(edgeLayer.GetSourceAnchor(relation), 8f), Is.True);
            SendPointerUp(canvas, 1, blankPosition);
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator GraphPalette_ClicksNavigateAndCreateNode()
        {
            Sequence head = Node<Sequence>("Head");
            BehaviourTreeData tree = Tree(head);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            Vector2 blankPosition = canvas.LocalToWorld(new Vector2(canvas.layout.width - 24f, canvas.layout.height - 24f));
            SendPointerDown(canvas, 1, blankPosition);
            SendPointerUp(canvas, 1, blankPosition);
            yield return null;
            GraphNodeCreationPalette palette = canvas.Q<GraphNodeCreationPalette>();
            Assert.That(palette, Is.Not.Null);
            Label details = palette.Q<Label>("ai-editor-graph-node-creation-detail");
            Assert.That(details, Is.Not.Null);
            Button rootBack = palette.Q<Button>("ai-editor-graph-node-creation-back");
            Label rootTitle = palette.Q<Label>("ai-editor-graph-node-creation-title");
            Assert.That(rootTitle.text, Is.EqualTo("Nodes"));
            Assert.That(rootBack.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));

            VisualElement folder = palette.Query<VisualElement>(className: "ai-editor-graph-node-creation-row")
                .ToList().First(row => row.Q<Label>(className: "ai-editor-graph-node-creation-row-detail").text == "Browse category");
            Assert.That(folder.worldBound.width, Is.GreaterThan(0f));
            Assert.That(folder.worldBound.height, Is.GreaterThan(0f));
            Assert.That(folder.Q<Label>(className: "ai-editor-graph-node-creation-row-detail").resolvedStyle.display,
                Is.EqualTo(DisplayStyle.None));
            SendPointerClick(folder);
            yield return null;
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.SameAs(palette));
            Button back = palette.Q<Button>("ai-editor-graph-node-creation-back");
            Label title = palette.Q<Label>("ai-editor-graph-node-creation-title");
            Assert.That(title.text, Is.Not.EqualTo("Nodes"));
            Assert.That(back.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));


            SendPointerClick(back);
            yield return null;
            Assert.That(title.text, Is.EqualTo("Nodes"));
            Assert.That(back.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.SameAs(palette));

            ToolbarSearchField search = palette.Q<ToolbarSearchField>("ai-editor-graph-node-creation-search");
            search.value = "a";
            yield return null;
            ListView list = palette.Q<ListView>("ai-editor-graph-node-creation-results");
            ScrollView scroll = list.Q<ScrollView>();
            VisualElement wheelTarget = list.Query<VisualElement>(className: "ai-editor-graph-node-creation-row").First();
            Vector2 scrollBefore = scroll.scrollOffset;
            float zoomBeforeWheel = canvas.Zoom;
            Event wheelEvent = new()
            {
                type = EventType.ScrollWheel,
                mousePosition = wheelTarget.worldBound.center,
                delta = new Vector2(0f, 8f),
            };
            using WheelEvent wheel = WheelEvent.GetPooled(wheelEvent);
            wheelTarget.SendEvent(wheel);
            yield return null;
            Assert.That(scroll.scrollOffset.y, Is.GreaterThan(scrollBefore.y));
            Assert.That(canvas.Zoom, Is.EqualTo(zoomBeforeWheel));

            SendPointerClick(palette.Q<Button>("ai-editor-graph-node-creation-back"));
            yield return null;
            Assert.That(details.text, Is.Empty);

            search.value = "Call";
            yield return null;
            List<VisualElement> searchRows = palette.Query<VisualElement>(className: "ai-editor-graph-node-creation-row").ToList();
            Assert.That(searchRows.Count, Is.GreaterThanOrEqualTo(2));
            VisualElement selectedRow = searchRows[1];
            VisualElement hoverRow = searchRows[0];
            list.selectedIndex = 1;
            list.RefreshItems();
            yield return null;
            searchRows = palette.Query<VisualElement>(className: "ai-editor-graph-node-creation-row").ToList();
            hoverRow = searchRows[0];
            selectedRow = searchRows[1];
            string selectedTip = selectedRow.tooltip;
            string hoverTip = hoverRow.tooltip;
            using (PointerEnterEvent enter = PointerEnterEvent.GetPooled())
            {
                enter.target = hoverRow;
                hoverRow.SendEvent(enter);
            }
            Assert.That(details.text, Is.EqualTo(hoverTip));
            using (PointerLeaveEvent leave = PointerLeaveEvent.GetPooled())
            {
                leave.target = hoverRow;
                hoverRow.SendEvent(leave);
            }
            Assert.That(details.text, Is.EqualTo(selectedTip));
            search.Focus();
            int selectedBeforeDown = list.selectedIndex;
            using (KeyDownEvent down = KeyDownEvent.GetPooled('\0', KeyCode.DownArrow, EventModifiers.None))
            {
                palette.SendEvent(down);
            }
            Assert.That(list.selectedIndex, Is.Not.EqualTo(selectedBeforeDown));
            VisualElement newSelectedRow = palette.Query<VisualElement>(className: "ai-editor-graph-node-creation-row")
                .ToList()[list.selectedIndex];
            Assert.That(details.text, Is.EqualTo(newSelectedRow.tooltip));

            search.value = "no-node-with-this-name";
            yield return null;
            Assert.That(details.text, Is.Empty);
            search.value = "Call";
            yield return null;
            Assert.That(details.text, Is.Not.Empty);
            VisualElement node = palette.Query<VisualElement>(className: "ai-editor-graph-node-creation-row").First();
            int nodeCount = tree.nodes.Count;
            SendPointerClick(node);
            yield return null;

            Assert.That(tree.nodes.Count, Is.EqualTo(nodeCount + 1));
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator GraphPalette_ExternalClickAndFocusLossClosePalette()
        {
            Sequence head = Node<Sequence>("Head");
            BehaviourTreeData tree = Tree(head);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.position = new Rect(100f, 100f, 1000f, 700f);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphCanvasElement canvas = window.rootVisualElement.Q<GraphCanvasElement>("ai-editor-graph-canvas");
            Vector2 anchor = canvas.LocalToWorld(new Vector2(canvas.layout.width - 24f, canvas.layout.height - 24f));
            SendPointerDown(canvas, 1, anchor);
            SendPointerUp(canvas, 1, anchor);
            yield return null;
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Not.Null);
            EditorUtility.ClearDirty(tree);

            Vector2 outsidePosition = canvas.LocalToWorld(new Vector2(12f, 12f));
            VisualElement outsideTarget = canvas.panel.Pick(outsidePosition);
            Assert.That(outsideTarget, Is.Not.Null);
            SendPointerDown(outsideTarget, 0, outsidePosition);
            SendPointerUp(outsideTarget, 0, outsidePosition);
            yield return null;
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            SendPointerDown(canvas, 1, anchor);
            SendPointerUp(canvas, 1, anchor);
            yield return null;
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Not.Null);
            EditorUtility.ClearDirty(tree);

            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").Focus();
            yield return null;
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

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
            Rect allBounds = GraphPresentationLayout.GetBounds(canvas.Presentation.Roots[0]);
            for (int i = 1; i < canvas.Presentation.Roots.Count; i++)
            {
                Rect next = GraphPresentationLayout.GetBounds(canvas.Presentation.Roots[i]);
                allBounds = Rect.MinMaxRect(
                    Mathf.Min(allBounds.xMin, next.xMin),
                    Mathf.Min(allBounds.yMin, next.yMin),
                    Mathf.Max(allBounds.xMax, next.xMax),
                    Mathf.Max(allBounds.yMax, next.yMax));
            }
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

        /// <summary>Verifies the mounted 500-node canvas presents every authored node and frames a selected node in its viewport.</summary>
        [UnityTest]
        public IEnumerator GraphCanvas_500NodeWindowFitAndFrameStayInsideResolvedViewport()
        {
            TestNode[] nodes = Enumerable.Range(0, 500).Select(index => Node<TestNode>($"Synthetic {index}")).ToArray();
            for (int index = 0; index + 1 < nodes.Length; index++)
            {
                nodes[index].child = nodes[index + 1].ToReference();
            }

            BehaviourTreeData tree = Tree(nodes);
            AIEditorWindow window = ShowGraphWindow(tree);
            yield return null;
            EditorUtility.ClearDirty(tree);
            GraphCanvasElement canvas = GetGraphModule(window).Canvas;

            canvas.FitAll();
            UUID[] authoredUUIDs = nodes.Select(node => node.uuid).ToArray();
            Assert.That(authoredUUIDs, Has.Length.EqualTo(500));
            foreach (UUID uuid in authoredUUIDs)
            {
                Assert.That(canvas.Presentation.Find(uuid), Is.Not.Null, uuid.ToString());
            }

            AssertPresentationItemsInsideViewport(canvas, authoredUUIDs);

            window.SelectedNode = nodes[^1];
            canvas.FrameSelected();
            Rect selectedBounds = GraphPresentationLayout.GetBounds(canvas.Presentation.Find(nodes[^1].uuid));
            Vector2 selectedCenter = canvas.GraphToViewport(selectedBounds.center);
            Vector2 viewportCenter = new(canvas.layout.width * 0.5f, canvas.layout.height * 0.5f);
            Assert.That(Vector2.Distance(selectedCenter, viewportCenter), Is.LessThan(0.01f));
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

        /// <summary>Verifies that real graph-window read operations preserve a serialized temporary tree byte-for-byte.</summary>
        [UnityTest]
        public IEnumerator GraphCanvas_ReadOperationsDoNotRewriteYaml_LayoutMutationStaysInGraphLayout()
        {
            TestNode head = Node<TestNode>("Head");
            TestNode child = Node<TestNode>("Child");
            head.child = child.ToReference();
            child.parent = head.ToReference();
            BehaviourTreeData tree = Tree(head, child);
            const string testFolder = "Assets/__AethiumianAITestAssets";
            string assetPath = null;
            bool folderCreated = false;
            bool assetCreated = false;

            try
            {
                if (!AssetDatabase.IsValidFolder(testFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "__AethiumianAITestAssets");
                    folderCreated = true;
                }

                assetPath = AssetDatabase.GenerateUniqueAssetPath($"{testFolder}/GraphYaml.asset");
                AssetDatabase.CreateAsset(tree, assetPath);
                assetCreated = true;
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                byte[] baseline = File.ReadAllBytes(assetPath);
                EditorUtility.ClearDirty(tree);

                AIEditorWindow window = ShowGraphWindow(tree);
                yield return null;
                GraphEditorModule module = GetGraphModule(window);
                GraphCanvasElement canvas = module.Canvas;

                window.Refresh();
                module.Canvas.SetTopology(GraphTopologyBuilder.Build(tree));
                module.Canvas.SetTopology(GraphTopologyBuilder.Build(tree));
                Assert.That(module.Canvas.Presentation.Find(head.uuid), Is.Not.Null);
                Assert.That(module.Canvas.Presentation.Find(child.uuid), Is.Not.Null);
                Assert.That(EditorUtility.IsDirty(tree), Is.False);
                canvas.FitAll();
                window.SelectedNode = child;
                canvas.FrameSelected();
                canvas.Pan = new Vector2(31f, 19f);
                canvas.Zoom = 1.2f;
                InvokeButtonClickable(canvas.Q<Button>("ai-editor-graph-visibility-options-services"));
                AssetDatabase.SaveAssets();

                Assert.That(File.ReadAllBytes(assetPath), Is.EqualTo(baseline));
                Assert.That(EditorUtility.IsDirty(tree), Is.False);

                Vector2 originalPosition = module.Topology.FindNode(child.uuid).Position;
                module.MoveNode(module.Topology.FindNode(child.uuid), originalPosition + new Vector2(75f, 45f));
                module.CommitNodeMove();
                Vector2 expectedChildPosition = module.Topology.FindNode(child.uuid).Position;
                AssetDatabase.SaveAssets();
                byte[] mutated = File.ReadAllBytes(assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                BehaviourTreeData reloaded = AssetDatabase.LoadAssetAtPath<BehaviourTreeData>(assetPath);

                Assert.That(mutated, Is.Not.EqualTo(baseline));
                Assert.That(reloaded.GraphLayout, Is.Not.Null);
                Assert.That(reloaded.GraphLayout.TryGetPosition(child.uuid, out Vector2 persistedChildPosition), Is.True);
                Assert.That(persistedChildPosition, Is.EqualTo(expectedChildPosition));
                AssertAuthoredGraphPayload(reloaded, head.uuid, child.uuid);
            }
            finally
            {
                if (assetCreated && !string.IsNullOrEmpty(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                if (folderCreated && AssetDatabase.IsValidFolder(testFolder))
                {
                    AssetDatabase.DeleteAsset(testFolder);
                }

                AssetDatabase.Refresh();
            }
        }

        /// <summary>Verifies the authored data fields exercised by this graph-layout persistence fixture.</summary>
        private static void AssertAuthoredGraphPayload(BehaviourTreeData tree, UUID expectedHeadUUID, UUID expectedChildUUID)
        {
            Assert.That(tree, Is.Not.Null);
            Assert.That(tree.headNodeUUID, Is.EqualTo(expectedHeadUUID));
            Assert.That(tree.nodes, Has.Count.EqualTo(2));
            Assert.That(tree.nodes.Select(node => node.uuid), Is.EqualTo(new[] { expectedHeadUUID, expectedChildUUID }));
            Assert.That(tree.nodes[0], Is.TypeOf<TestNode>());
            Assert.That(tree.nodes[1], Is.TypeOf<TestNode>());

            TestNode head = (TestNode)tree.nodes[0];
            TestNode child = (TestNode)tree.nodes[1];
            Assert.That(head.name, Is.EqualTo("Head"));
            Assert.That(child.name, Is.EqualTo("Child"));
            Assert.That(head.child, Is.Not.Null);
            Assert.That(head.child.UUID, Is.EqualTo(expectedChildUUID));
            Assert.That(child.parent, Is.Not.Null);
            Assert.That(child.parent.UUID, Is.EqualTo(expectedHeadUUID));
            // RawNodeReference uses a serialized empty object for an unassigned value.
            // Its UUID is the authored target identity; the runtime Node cache is not serialized.
            Assert.That(head.raw, Is.Not.Null);
            Assert.That(head.raw.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(head.raw.HasEditorReference, Is.False);
            Assert.That(head.raw.HasReference, Is.False);
            Assert.That(child.raw, Is.Not.Null);
            Assert.That(child.raw.UUID, Is.EqualTo(UUID.Empty));
            Assert.That(child.raw.HasEditorReference, Is.False);
            Assert.That(child.raw.HasReference, Is.False);
        }

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

        [UnityTest]
        public IEnumerator GraphWindow_DecisionShowsEmbeddedOrderStripAndStandardPorts()
        {
            Decision decision = Node<Decision>("Decision");
            TestNode first = Node<TestNode>("First Option With A Long Name");
            UUID missing = UUID.NewUUID();
            decision.events = new[]
            {
                first.ToReference(),
                NodeReference.Empty,
                new NodeReference(missing),
            };
            BehaviourTreeData tree = Tree(decision, first);
            EditorUtility.ClearDirty(tree);
            AIEditorWindow window = AIEditorWindow.ShowWindow(tree);
            shownWindows.Add(window);
            window.CreateGUI();
            window.rootVisualElement.Q<ToolbarToggle>("ai-editor-graph-tab").value = true;
            yield return null;

            GraphNodeElement node = window.rootVisualElement.Q<GraphNodeElement>($"ai-editor-graph-node-{decision.uuid}");
            GraphDecisionOrderStripElement strip = node.Q<GraphDecisionOrderStripElement>();
            List<string> labels = strip.Query<Label>().ToList().Select(label => label.text).ToList();

            Assert.That(node.ClassListContains("ai-editor-graph-node-decision"), Is.True);
            Assert.That(node.resolvedStyle.width, Is.EqualTo(360f).Within(0.01f));
            Assert.That(node.resolvedStyle.height, Is.EqualTo(76f).Within(0.01f));
            Assert.That(labels, Is.EqualTo(new[]
            {
                "1  First Option With A Long Name",
                "2  Empty",
                "3  Missing",
            }));
            Assert.That(strip.Query<VisualElement>(className: "ai-editor-graph-decision-append").ToList(), Is.Empty);
            GraphPortLayerElement portLayer = window.rootVisualElement.Q<GraphPortLayerElement>();
            GraphPortDescriptor[] decisionPorts = portLayer.Ports
                .Where(port => port.OwnerUUID == decision.uuid && port.FieldName == nameof(Decision.events))
                .ToArray();
            Assert.That(decisionPorts.Count(port => port.Operation == GraphPortOperation.Insert), Is.EqualTo(2));
            Assert.That(decisionPorts.Count(port => port.Operation == GraphPortOperation.Replace), Is.EqualTo(3));
            Assert.That(GraphDecisionOrderStripElement.GetInsertionIndicatorLeft(0), Is.EqualTo(-1.5f));
            Assert.That(GraphDecisionOrderStripElement.GetInsertionIndicatorLeft(2), Is.EqualTo(188.5f));
            Assert.That(tree.GraphLayout, Is.Null);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);
        }

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
    }
}
