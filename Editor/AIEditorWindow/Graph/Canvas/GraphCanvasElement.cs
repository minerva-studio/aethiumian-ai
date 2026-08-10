using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Native UI Toolkit canvas for the graph topology.
    /// </summary>
    internal sealed class GraphCanvasElement : VisualElement
    {
        internal const float MinimumZoom = 0.05f;
        internal const float MaximumZoom = 2.5f;
        private const float MaximumFitZoom = 1.5f;
        private const float MinimumInitialFrameZoom = 0.45f;
        private const float FramePadding = 48f;
        private const float WheelZoomSensitivity = 0.035f;

        private readonly GraphEditorModule module;
        private readonly GraphCanvasAppearance appearance = new();
        private readonly VisualElement content;
        private readonly VisualElement scopeLayer;
        private readonly GraphEdgeLayerElement edgeLayer;
        private readonly VisualElement nodeLayer;
        private readonly VisualElement interactionLayer;
        private GraphPresentation presentation;
        private bool panning;
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStart;
        private float zoom = 1f;
        private Vector2 pan;
        private bool fitAllWhenGeometryIsValid;
        private bool initialFrameWhenGeometryIsValid;

        /// <summary>
        /// Initializes a graph canvas owned by a graph editor module.
        /// </summary>
        /// <param name="module">The owning graph module.</param>
        internal GraphCanvasElement(GraphEditorModule module)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            name = "ai-editor-graph-canvas";
            AddToClassList("ai-editor-graph-canvas");
            style.flexGrow = 1f;
            style.position = UIPosition.Relative;
            style.overflow = Overflow.Hidden;
            focusable = true;
            generateVisualContent += DrawBackground;

            content = new VisualElement
            {
                name = "ai-editor-graph-content",
            };
            content.AddToClassList("ai-editor-graph-content");
            content.style.position = UIPosition.Absolute;
            content.style.left = 0f;
            content.style.top = 0f;
            content.style.width = 1f;
            content.style.height = 1f;

            edgeLayer = new GraphEdgeLayerElement(appearance)
            {
                name = "ai-editor-graph-edge-layer",
            };
            edgeLayer.AddToClassList("ai-editor-graph-edge-layer");
            edgeLayer.pickingMode = PickingMode.Ignore;
            edgeLayer.style.position = UIPosition.Absolute;
            edgeLayer.style.left = 0f;
            edgeLayer.style.top = 0f;

            scopeLayer = new VisualElement
            {
                name = "ai-editor-graph-scope-layer",
            };
            scopeLayer.AddToClassList("ai-editor-graph-scope-layer");
            scopeLayer.pickingMode = PickingMode.Ignore;
            scopeLayer.style.position = UIPosition.Absolute;
            scopeLayer.style.left = 0f;
            scopeLayer.style.top = 0f;

            nodeLayer = new VisualElement
            {
                name = "ai-editor-graph-node-layer",
            };
            nodeLayer.AddToClassList("ai-editor-graph-node-layer");
            nodeLayer.style.position = UIPosition.Absolute;
            nodeLayer.style.left = 0f;
            nodeLayer.style.top = 0f;

            interactionLayer = new VisualElement
            {
                name = "ai-editor-graph-interaction-layer",
            };
            interactionLayer.AddToClassList("ai-editor-graph-interaction-layer");
            interactionLayer.pickingMode = PickingMode.Ignore;
            interactionLayer.style.position = UIPosition.Absolute;
            interactionLayer.style.left = 0f;
            interactionLayer.style.top = 0f;

            content.Add(scopeLayer);
            content.Add(edgeLayer);
            content.Add(nodeLayer);
            content.Add(interactionLayer);
            Add(content);

            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        /// <summary>
        /// Gets or sets the current canvas zoom factor.
        /// </summary>
        internal float Zoom
        {
            get => zoom;
            set
            {
                zoom = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
                ApplyTransform();
            }
        }

        /// <summary>
        /// Gets or sets the current canvas pan in panel coordinates.
        /// </summary>
        internal Vector2 Pan
        {
            get => pan;
            set
            {
                pan = value;
                ApplyTransform();
            }
        }

        /// <summary>
        /// Rebuilds native node cards and edge labels for a topology snapshot.
        /// </summary>
        /// <param name="topology">The topology to display.</param>
        internal void SetTopology(GraphTopology topology)
        {
            presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            edgeLayer.SetPresentation(presentation);
            RebuildScopeElements();
            nodeLayer.Clear();

            if (presentation == null)
            {
                return;
            }

            foreach (GraphPresentationItem item in presentation.Roots)
            {
                nodeLayer.Add(CreatePresentationElement(item, isMovable: true, parentPosition: Vector2.zero, shapeOverride: null));
            }

            UpdateContentBounds(presentation);
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Gets the current semantic presentation used by the canvas.
        /// </summary>
        internal GraphPresentation Presentation => presentation;

        /// <summary>Gets the USS-resolved paint values shared by this canvas and its painters.</summary>
        internal GraphCanvasAppearance Appearance => appearance;

        /// <summary>Gets the complete presentation bounds used by view framing.</summary>
        internal Rect PresentationBounds => CalculateBounds(presentation);

        /// <summary>Applies resolved custom styles and repaints without rebuilding graph data.</summary>
        internal void ResolveAppearance(ICustomStyle customStyle)
        {
            appearance.Resolve(customStyle);
            MarkDirtyRepaint();
            foreach (VisualElement element in content.Query<VisualElement>().ToList())
            {
                element.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Refreshes card selection without rebuilding the topology.
        /// </summary>
        /// <param name="selectedNode">The selected node instance.</param>
        internal void SetSelectedNode(TreeNode selectedNode)
        {
            edgeLayer.SetSelectedNode(selectedNode);

            foreach (GraphSequenceScopeElement scope in scopeLayer.Query<GraphSequenceScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphConditionScopeElement scope in scopeLayer.Query<GraphConditionScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphLoopScopeElement scope in scopeLayer.Query<GraphLoopScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphProbabilityScopeElement scope in scopeLayer.Query<GraphProbabilityScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphParallelScopeElement scope in scopeLayer.Query<GraphParallelScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphForEachScopeElement scope in scopeLayer.Query<GraphForEachScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphServiceScopeElement scope in interactionLayer.Query<GraphServiceScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphFlowCompletionElement completion in interactionLayer.Query<GraphFlowCompletionElement>().ToList())
            {
                completion.SetSelected(completion.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (VisualElement element in nodeLayer.Children())
            {
                if (element is GraphNodeElement node)
                {
                    node.SetSelected(node.Descriptor.Node == selectedNode);
                }
                else if (element is GraphConditionElement condition)
                {
                    condition.SetSelected(selectedNode);
                }
                else if (element is GraphContainerElement container)
                {
                    container.SetSelected(selectedNode);
                }
                else if (element is GraphReferenceProxyElement proxy)
                {
                    proxy.SetSelected(proxy.TargetNode == selectedNode);
                }
            }
        }

        /// <summary>
        /// Fits all nodes into the current viewport.
        /// </summary>
        internal void FitAll()
        {
            if (!HasValidGeometry || presentation == null || presentation.Roots.Count == 0)
            {
                return;
            }

            Rect bounds = CalculateBounds(presentation);
            float fitZoom = CalculateFitZoom(bounds, FramePadding, MaximumFitZoom);
            SetViewTransform(fitZoom, ViewportCenter - bounds.center * fitZoom);
        }

        /// <summary>
        /// Requests one Fit All operation after the canvas receives valid geometry.
        /// </summary>
        internal void RequestFitAllWhenGeometryIsValid()
        {
            fitAllWhenGeometryIsValid = true;
            TryApplyRequestedFit();
        }

        /// <summary>Requests a readable initial frame around the Head and its first two authored execution levels.</summary>
        internal void RequestInitialFrameWhenGeometryIsValid()
        {
            initialFrameWhenGeometryIsValid = true;
            TryApplyRequestedFit();
            schedule.Execute(TryApplyRequestedFit);
        }

        /// <summary>
        /// Frames the selected node in the viewport.
        /// </summary>
        internal void FrameSelected()
        {
            GraphPresentationItem selected = presentation?.Find(module.SelectedNode?.uuid ?? UUID.Empty);
            if (selected == null || !HasValidGeometry)
            {
                return;
            }

            Rect selectedBounds = GraphPresentationLayout.GetBounds(selected);
            GraphServiceScope serviceScope = presentation.FindServiceScope(selected.TargetUUID);
            if (serviceScope != null)
            {
                selectedBounds = serviceScope.Bounds;
            }
            float fitZoom = CalculateFitZoom(selectedBounds, FramePadding, MaximumFitZoom);
            float frameZoom = Mathf.Min(Mathf.Max(zoom, 0.75f), fitZoom);
            SetViewTransform(frameZoom, ViewportCenter - selectedBounds.center * frameZoom);
        }

        /// <summary>
        /// Re-centers the content transform after a layout change.
        /// </summary>
        internal void RefreshTransform()
        {
            edgeLayer.RefreshLabelPositions();
            ApplyTransform();
        }

        /// <summary>
        /// Updates a top-level presentation position while keeping nested items local to their container.
        /// </summary>
        /// <param name="descriptor">The moved source descriptor.</param>
        /// <param name="position">The new canvas position.</param>
        internal void UpdatePresentationPosition(GraphNodeDescriptor descriptor, Vector2 position)
        {
            presentation?.MoveRoot(descriptor?.UUID ?? UUID.Empty, position);
            RefreshPresentationGeometry();
        }

        /// <summary>Updates multiple moved roots before deriving shared scope geometry once.</summary>
        internal void UpdatePresentationPositions(IEnumerable<GraphNodeDescriptor> descriptors)
        {
            if (presentation != null && descriptors != null)
            {
                foreach (GraphNodeDescriptor descriptor in descriptors)
                {
                    presentation.MoveRoot(descriptor?.UUID ?? UUID.Empty, descriptor?.Position ?? Vector2.zero);
                }
            }

            RefreshPresentationGeometry();
        }

        private void RefreshPresentationGeometry()
        {
            GraphPresentationLayout.Layout(presentation);
            RebuildScopeElements();
            RefreshDerivedNodePositions();
            SetSelectedNode(module.SelectedNode);
            edgeLayer.RefreshLabelPositions();
            UpdateContentBounds(presentation);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 2)
            {
                return;
            }

            if (IsNodeTarget(evt.target))
            {
                return;
            }

            panning = true;
            panPointerId = evt.pointerId;
            panStartPointer = evt.position;
            panStart = pan;
            this.CapturePointer(panPointerId);
            evt.StopPropagation();
        }

        /// <summary>
        /// Determines whether an event target belongs to a graph node card.
        /// </summary>
        /// <param name="target">The UI Toolkit event target.</param>
        /// <returns>True when the target is the node card or one of its descendants.</returns>
        private static bool IsNodeTarget(IEventHandler target)
        {
            VisualElement element = target as VisualElement;
            while (element != null)
            {
                if (element is GraphNodeElement or GraphConditionElement or GraphContainerElement
                    or GraphReferenceProxyElement or GraphFlowCompletionElement or GraphServiceScopeElement
                    or GraphProbabilityPlaceholderElement or GraphDecisionPlaceholderElement)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!panning || evt.pointerId != panPointerId)
            {
                return;
            }

            pan = panStart + (Vector2)evt.position - panStartPointer;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != panPointerId)
            {
                return;
            }

            panning = false;
            this.ReleasePointer(evt.pointerId);
            panPointerId = -1;
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == panPointerId)
            {
                panning = false;
                this.ReleasePointer(evt.pointerId);
                panPointerId = -1;
            }
        }

        private void OnWheel(WheelEvent evt)
        {
            if (module.Topology == null || !HasValidGeometry || Mathf.Approximately(evt.delta.y, 0f))
            {
                return;
            }

            Vector2 viewportPoint = PanelToViewport(evt.mousePosition);
            Vector2 graphPoint = ViewportToGraph(viewportPoint);
            float wheelDelta = Mathf.Clamp(evt.delta.y, -20f, 20f);
            float targetZoom = Mathf.Clamp(
                zoom * Mathf.Exp(-wheelDelta * WheelZoomSensitivity),
                MinimumZoom,
                MaximumZoom);
            SetViewTransform(targetZoom, viewportPoint - graphPoint * targetZoom);
            evt.StopPropagation();
        }

        /// <summary>
        /// Converts a panel-space point to this viewport's local space.
        /// </summary>
        internal Vector2 PanelToViewport(Vector2 panelPoint) => this.WorldToLocal(panelPoint);

        /// <summary>
        /// Converts a viewport-local point to graph space using the current view transform.
        /// </summary>
        internal Vector2 ViewportToGraph(Vector2 viewportPoint) => (viewportPoint - pan) / zoom;

        /// <summary>
        /// Converts a graph-space point to viewport-local space using the current view transform.
        /// </summary>
        internal Vector2 GraphToViewport(Vector2 graphPoint) => graphPoint * zoom + pan;

        /// <summary>Gets whether panel attachment and layout are ready for coordinate conversion.</summary>
        private bool HasValidGeometry => panel != null
            && float.IsFinite(layout.width)
            && float.IsFinite(layout.height)
            && layout.width > 0f
            && layout.height > 0f;

        /// <summary>Gets the center of the current viewport in local coordinates.</summary>
        private Vector2 ViewportCenter => new(layout.width * 0.5f, layout.height * 0.5f);

        /// <summary>Calculates a bounded zoom that contains graph bounds inside this viewport.</summary>
        private float CalculateFitZoom(Rect bounds, float totalPadding, float maximumZoom)
        {
            float availableWidth = Mathf.Max(1f, layout.width - totalPadding);
            float availableHeight = Mathf.Max(1f, layout.height - totalPadding);
            float scaleX = availableWidth / Mathf.Max(1f, bounds.width);
            float scaleY = availableHeight / Mathf.Max(1f, bounds.height);
            return Mathf.Clamp(Mathf.Min(scaleX, scaleY), MinimumZoom, maximumZoom);
        }

        /// <summary>Applies one authoritative zoom and pan pair to the graph content.</summary>
        private void SetViewTransform(float value, Vector2 position)
        {
            zoom = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
            pan = position;
            ApplyTransform();
        }

        /// <summary>Consumes a pending initial fit after UI Toolkit resolves canvas geometry.</summary>
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            TryApplyRequestedFit();
        }

        /// <summary>Applies the pending initial fit once panel and geometry are valid.</summary>
        private void TryApplyRequestedFit()
        {
            if (!HasValidGeometry)
            {
                return;
            }

            if (initialFrameWhenGeometryIsValid)
            {
                initialFrameWhenGeometryIsValid = false;
                FrameInitialExecution();
                return;
            }

            if (!fitAllWhenGeometryIsValid)
            {
                return;
            }

            fitAllWhenGeometryIsValid = false;
            FitAll();
        }

        /// <summary>Frames only the initial readable execution context without treating an oversized graph as a thumbnail.</summary>
        private void FrameInitialExecution()
        {
            if (presentation == null || module.Topology?.Tree == null)
            {
                FitAll();
                return;
            }

            GraphPresentationItem head = presentation.Find(module.Topology.Tree.headNodeUUID);
            if (head == null)
            {
                FitAll();
                return;
            }

            // Initial navigation frames cards only. Full Flow bounds can contain distant END markers,
            // Body ranges, and free descendants that belong to a later navigation decision.
            Rect bounds = new(head.Position, head.Size);
            Queue<(GraphNodeDescriptor Node, int Depth)> queue = new();
            HashSet<UUID> visited = new();
            GraphNodeDescriptor headNode = module.Topology.FindNode(head.TargetUUID);
            queue.Enqueue((headNode, 0));
            visited.Add(headNode.UUID);
            while (queue.Count > 0)
            {
                (GraphNodeDescriptor node, int depth) = queue.Dequeue();
                if (depth >= 2)
                {
                    continue;
                }

                foreach (GraphEdgeDescriptor edge in module.Topology.Edges)
                {
                    if (edge.Source != node || edge.Target == null || edge.Kind != GraphEdgeKind.Child)
                    {
                        continue;
                    }

                    if (!visited.Add(edge.Target.UUID))
                    {
                        continue;
                    }

                    GraphPresentationItem target = presentation.Find(edge.Target.UUID);
                    if (target == null)
                    {
                        continue;
                    }

                    bounds = Union(bounds, new Rect(target.Position, target.Size));
                    queue.Enqueue((edge.Target, depth + 1));
                }
            }

            float initialZoom = Mathf.Max(MinimumInitialFrameZoom, CalculateFitZoom(bounds, FramePadding, MaximumFitZoom));
            SetViewTransform(initialZoom, ViewportCenter - bounds.center * initialZoom);
        }

        private static Rect Union(Rect left, Rect right)
        {
            return Rect.MinMaxRect(
                Mathf.Min(left.xMin, right.xMin),
                Mathf.Min(left.yMin, right.yMin),
                Mathf.Max(left.xMax, right.xMax),
                Mathf.Max(left.yMax, right.yMax));
        }

        private void ApplyTransform()
        {
            content.transform.position = new Vector3(pan.x, pan.y, 0f);
            content.transform.scale = new Vector3(zoom, zoom, 1f);
            edgeLayer.MarkDirtyRepaint();
            MarkDirtyRepaint();
        }

        private void UpdateContentBounds(GraphPresentation value)
        {
            Rect bounds = CalculateBounds(value);
            float width = Mathf.Max(2000f, bounds.xMax + 1000f);
            float height = Mathf.Max(1200f, bounds.yMax + 1000f);
            content.style.width = width;
            content.style.height = height;
            edgeLayer.style.width = width;
            edgeLayer.style.height = height;
            scopeLayer.style.width = width;
            scopeLayer.style.height = height;
            nodeLayer.style.width = width;
            nodeLayer.style.height = height;
            interactionLayer.style.width = width;
            interactionLayer.style.height = height;
        }

        private static Rect CalculateBounds(GraphPresentation value)
        {
            if (value == null || value.Roots.Count == 0)
            {
                return new Rect(Vector2.zero, GraphPresentationMetrics.NormalNodeSize);
            }

            Rect first = GraphPresentationLayout.GetBounds(value.Roots[0]);
            Vector2 min = first.min;
            Vector2 max = first.max;
            for (int i = 1; i < value.Roots.Count; i++)
            {
                Rect bounds = GraphPresentationLayout.GetBounds(value.Roots[i]);
                min = Vector2.Min(min, bounds.min);
                max = Vector2.Max(max, bounds.max);
            }

            foreach (GraphServiceScope scope in value.ServiceScopes)
            {
                min = Vector2.Min(min, scope.Bounds.min);
                max = Vector2.Max(max, scope.Bounds.max);
            }

            foreach (GraphFlowScope scope in value.CompletionScopes)
            {
                min = Vector2.Min(min, scope.Bounds.min);
                max = Vector2.Max(max, scope.Bounds.max);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void RebuildScopeElements()
        {
            scopeLayer.Clear();
            interactionLayer.Clear();
            if (presentation == null)
            {
                return;
            }

            foreach (GraphFlowScope scope in presentation.CompletionScopes)
            {
                if (scope is GraphSequenceScope sequenceScope)
                {
                    scopeLayer.Add(new GraphSequenceScopeElement(sequenceScope, appearance));
                }
                else if (scope is GraphConditionScope conditionScope)
                {
                    scopeLayer.Add(new GraphConditionScopeElement(conditionScope, appearance));
                }
                else if (scope is GraphLoopScope loopScope)
                {
                    scopeLayer.Add(new GraphLoopScopeElement(loopScope));
                }
                else if (scope is GraphProbabilityScope probabilityScope)
                {
                    scopeLayer.Add(new GraphProbabilityScopeElement(probabilityScope, appearance));
                }
                else if (scope is GraphParallelScope parallelScope)
                {
                    scopeLayer.Add(new GraphParallelScopeElement(parallelScope, appearance));
                }
                else if (scope is GraphForEachScope forEachScope)
                {
                    scopeLayer.Add(new GraphForEachScopeElement(forEachScope));
                }

                interactionLayer.Add(new GraphFlowCompletionElement(module, scope));
            }

            foreach (GraphServiceScope scope in presentation.ServiceScopes)
            {
                interactionLayer.Add(new GraphServiceScopeElement(module, scope));
            }
        }

        /// <summary>Refreshes positions of presentation-only cards after derived scope geometry changes.</summary>
        private void RefreshDerivedNodePositions()
        {
            foreach (GraphNodeElement node in nodeLayer.Query<GraphNodeElement>().ToList())
            {
                node.RefreshPosition();
            }

            foreach (GraphConditionElement condition in nodeLayer.Query<GraphConditionElement>().ToList())
            {
                condition.RefreshPosition();
            }

            foreach (GraphConditionPlaceholderElement placeholder in nodeLayer.Query<GraphConditionPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphLoopPlaceholderElement placeholder in nodeLayer.Query<GraphLoopPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphLoopJunctionElement junction in nodeLayer.Query<GraphLoopJunctionElement>().ToList())
            {
                junction.RefreshPosition();
            }

            foreach (GraphProbabilityPlaceholderElement placeholder in nodeLayer.Query<GraphProbabilityPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphDecisionPlaceholderElement placeholder in nodeLayer.Query<GraphDecisionPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphServicePlaceholderElement placeholder in nodeLayer.Query<GraphServicePlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphParallelPlaceholderElement placeholder in nodeLayer.Query<GraphParallelPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphForEachPlaceholderElement placeholder in nodeLayer.Query<GraphForEachPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphForEachJunctionElement junction in nodeLayer.Query<GraphForEachJunctionElement>().ToList())
            {
                junction.RefreshPosition();
            }
        }

        private VisualElement CreatePresentationElement(
            GraphPresentationItem item,
            bool isMovable,
            Vector2 parentPosition,
            GraphNodeShape? shapeOverride)
        {
            Vector2 localPosition = item.Position - parentPosition;
            switch (item.Kind)
            {
                case GraphPresentationKind.Condition:
                    return new GraphConditionElement(this, module, item, isMovable, localPosition, CreatePresentationElement);
                case GraphPresentationKind.ConditionPlaceholder:
                    return new GraphConditionPlaceholderElement(item, localPosition);
                case GraphPresentationKind.LoopPlaceholder:
                    return new GraphLoopPlaceholderElement(item, localPosition);
                case GraphPresentationKind.LoopJunction:
                    return new GraphLoopJunctionElement(item, localPosition);
                case GraphPresentationKind.ProbabilityPlaceholder:
                    return new GraphProbabilityPlaceholderElement(item, localPosition);
                case GraphPresentationKind.DecisionPlaceholder:
                    return new GraphDecisionPlaceholderElement(item, localPosition);
                case GraphPresentationKind.ParallelPlaceholder:
                    return new GraphParallelPlaceholderElement(item, localPosition);
                case GraphPresentationKind.ForEachPlaceholder:
                    return new GraphForEachPlaceholderElement(item, localPosition);
                case GraphPresentationKind.ForEachJunction:
                    return new GraphForEachJunctionElement(item, localPosition);
                case GraphPresentationKind.ServicePlaceholder:
                    return new GraphServicePlaceholderElement(item, localPosition);
                case GraphPresentationKind.ReferenceProxy:
                case GraphPresentationKind.Missing:
                    return new GraphReferenceProxyElement(this, module, item, localPosition);
                default:
                    return new GraphNodeElement(this, module, item.Node, isMovable, localPosition, shapeOverride);
            }
        }

        private void DrawBackground(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null || layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            Color gridColor = EditorGUIUtility.isProSkin ? appearance.GridDark : appearance.GridLight;
            const float grid = 24f;
            float scaledGrid = grid * zoom;
            if (scaledGrid < 8f)
            {
                return;
            }

            float startX = Mathf.Repeat(pan.x, scaledGrid);
            float startY = Mathf.Repeat(pan.y, scaledGrid);
            painter.strokeColor = gridColor;
            painter.lineWidth = appearance.GridLineWidth;
            for (float x = startX; x < layout.width; x += scaledGrid)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, layout.height));
                painter.Stroke();
            }

            for (float y = startY; y < layout.height; y += scaledGrid)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(layout.width, y));
                painter.Stroke();
            }
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            ResolveAppearance(evt.customStyle);
        }
    }

    /// <summary>
    /// Native node card used by <see cref="GraphCanvasElement"/>.
}
