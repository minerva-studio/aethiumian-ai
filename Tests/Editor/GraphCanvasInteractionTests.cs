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
    /// <summary>Graph Editor GraphCanvasInteraction contract tests.</summary>
    [Category("GraphEditor")]
    public sealed class GraphCanvasInteractionTests : GraphEditorTestFixture
    {
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
            Assert.That(canvas.Q<GraphNodeCreationPalette>(), Is.Not.Null);
            Assert.That(SendKeyDown(canvas, KeyCode.F), Is.False);
            Assert.That(SendKeyDown(canvas, KeyCode.Delete), Is.False);
            Assert.That(SendKeyDown(canvas, KeyCode.Escape), Is.False);
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

        [Test]
        public void GraphView_GridToggleDoesNotDirtyTree()
        {
            BehaviourTreeData tree = Tree(Node<TestNode>("Head"));
            GraphEditorModule module = CreateHiddenGraphModule(tree);
            EditorUtility.ClearDirty(tree);

            Assert.That(module.SnapToGrid, Is.False);
            module.ShowGrid = false;

            Assert.That(module.ShowGrid, Is.False);
            Assert.That(module.Canvas.GridVisible, Is.False);
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
            Assert.That(module.Canvas.GridVisible, Is.True);
            Assert.That(snapButton.ClassListContains("ai-editor-graph-view-options-button-active"), Is.True);
            Assert.That(EditorUtility.IsDirty(tree), Is.False);

            module.ShowGrid = false;
            Assert.That(module.SnapToGrid, Is.True);
            Assert.That(module.Canvas.GridVisible, Is.False);
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
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-services").Q<Image>(), Is.Not.Null);
            Assert.That(module.Canvas.Q<Button>("ai-editor-graph-view-options-raw-references").Q<Image>(), Is.Not.Null);
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
            GraphPresentationRelation relation = canvas.Presentation.Relations.Single(value => value.Origin != null);
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
