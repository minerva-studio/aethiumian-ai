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
        private const float FramePadding = 48f;
        private const float WheelZoomSensitivity = 0.035f;

        private readonly GraphEditorModule module;
        private readonly VisualElement content;
        private readonly VisualElement scopeLayer;
        private readonly GraphEdgeLayerElement edgeLayer;
        private readonly VisualElement nodeLayer;
        private GraphPresentation presentation;
        private bool panning;
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStart;
        private float zoom = 1f;
        private Vector2 pan;
        private bool fitAllWhenGeometryIsValid;

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

            edgeLayer = new GraphEdgeLayerElement
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

            content.Add(scopeLayer);
            content.Add(edgeLayer);
            content.Add(nodeLayer);
            Add(content);

            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
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

        /// <summary>Gets the complete presentation bounds used by view framing.</summary>
        internal Rect PresentationBounds => CalculateBounds(presentation);

        /// <summary>
        /// Refreshes card selection without rebuilding the topology.
        /// </summary>
        /// <param name="selectedNode">The selected node instance.</param>
        internal void SetSelectedNode(TreeNode selectedNode)
        {
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

            foreach (GraphFlowCompletionElement completion in scopeLayer.Query<GraphFlowCompletionElement>().ToList())
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
            GraphPresentationLayout.Layout(presentation);
            RebuildScopeElements();
            RefreshDerivedNodePositions();
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
                if (element is GraphNodeElement or GraphConditionElement or GraphContainerElement or GraphReferenceProxyElement)
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
            if (!fitAllWhenGeometryIsValid || !HasValidGeometry)
            {
                return;
            }

            fitAllWhenGeometryIsValid = false;
            FitAll();
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

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void RebuildScopeElements()
        {
            scopeLayer.Clear();
            if (presentation == null)
            {
                return;
            }

            foreach (GraphFlowScope scope in presentation.CompletionScopes)
            {
                if (scope is GraphSequenceScope sequenceScope)
                {
                    scopeLayer.Add(new GraphSequenceScopeElement(sequenceScope));
                }
                else if (scope is GraphConditionScope conditionScope)
                {
                    scopeLayer.Add(new GraphConditionScopeElement(conditionScope));
                }
                else if (scope is GraphLoopScope loopScope)
                {
                    scopeLayer.Add(new GraphLoopScopeElement(loopScope));
                }

                scopeLayer.Add(new GraphFlowCompletionElement(scope));
            }
        }

        /// <summary>Refreshes positions of presentation-only cards after derived scope geometry changes.</summary>
        private void RefreshDerivedNodePositions()
        {
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

            Color gridColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.045f)
                : new Color(0f, 0f, 0f, 0.055f);
            const float grid = 24f;
            float scaledGrid = grid * zoom;
            if (scaledGrid < 8f)
            {
                return;
            }

            float startX = Mathf.Repeat(pan.x, scaledGrid);
            float startY = Mathf.Repeat(pan.y, scaledGrid);
            painter.strokeColor = gridColor;
            painter.lineWidth = 1f;
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
    }

    /// <summary>
    /// Native node card used by <see cref="GraphCanvasElement"/>.
    /// </summary>
    internal sealed class GraphNodeElement : VisualElement
    {
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly Label title;
        private readonly Label typeLabel;
        private readonly Label warningLabel;
        private bool selected;
        private readonly bool movable;
        private readonly GraphNodeShape shape;
        private bool dragging;
        private int pointerId = -1;
        private Vector2 dragOffset;

        /// <summary>
        /// Initializes a node card.
        /// </summary>
        internal GraphNodeElement(GraphCanvasElement canvas, GraphEditorModule module, GraphNodeDescriptor descriptor)
            : this(canvas, module, descriptor, true, descriptor?.Position ?? Vector2.zero, null)
        {
        }

        /// <summary>
        /// Initializes a node card at a presentation-local position.
        /// </summary>
        /// <param name="canvas">The owning canvas.</param>
        /// <param name="module">The owning graph module.</param>
        /// <param name="descriptor">The source node descriptor.</param>
        /// <param name="movable">Whether this card may move the top-level layout.</param>
        /// <param name="position">The position relative to the parent presentation item.</param>
        /// <param name="shapeOverride">An optional presentation shape override.</param>
        internal GraphNodeElement(
            GraphCanvasElement canvas,
            GraphEditorModule module,
            GraphNodeDescriptor descriptor,
            bool movable,
            Vector2 position,
            GraphNodeShape? shapeOverride)
        {
            this.canvas = canvas;
            this.module = module;
            Descriptor = descriptor;
            this.movable = movable;
            shape = shapeOverride ?? descriptor.Shape;
            name = $"ai-editor-graph-node-{descriptor.UUID}";
            AddToClassList("ai-editor-graph-node");
            AddToClassList($"ai-editor-graph-node-{shape.ToString().ToLowerInvariant()}");
            if (descriptor.IsHead)
            {
                AddToClassList("ai-editor-graph-node-head");
            }

            if (descriptor.IsReachable)
            {
                AddToClassList("ai-editor-graph-node-reachable");
            }
            else
            {
                AddToClassList("ai-editor-graph-node-unreachable");
            }

            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            Vector2 size = GraphLayoutResolver.GetNodeSize(descriptor);
            style.width = size.x;
            style.height = size.y;
            generateVisualContent += DrawNodeShape;
            title = new Label(descriptor.DisplayName);
            title.AddToClassList("ai-editor-graph-node-title");
            typeLabel = new Label(GetKindLabel(descriptor, shapeOverride));
            typeLabel.AddToClassList("ai-editor-graph-node-type");
            Add(title);
            Add(typeLabel);

            if (descriptor.HasWarning)
            {
                tooltip = descriptor.Warning;
                warningLabel = new Label("!");
                warningLabel.tooltip = descriptor.Warning;
                warningLabel.AddToClassList("ai-editor-graph-node-warning");
                Add(warningLabel);
            }

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        /// <summary>
        /// Gets the immutable descriptor represented by this card.
        /// </summary>
        internal GraphNodeDescriptor Descriptor { get; }

        /// <summary>
        /// Updates the selected visual state.
        /// </summary>
        internal void SetSelected(bool selected)
        {
            this.selected = selected;
            EnableInClassList("ai-editor-graph-node-selected", selected);
            MarkDirtyRepaint();
        }

        private static string GetKindLabel(GraphNodeDescriptor descriptor, GraphNodeShape? shapeOverride)
        {
            GraphNodeShape value = shapeOverride ?? descriptor.Shape;
            if (descriptor.Node is Parallel parallel)
            {
                return $"FLOW  ·  PARALLEL  ·  {parallel.mode.ToString().ToUpperInvariant()}";
            }

            if (descriptor.Node is Loop loop)
            {
                return $"FLOW  ·  LOOP  ·  {loop.loopType.ToString().ToUpperInvariant()}";
            }

            return value switch
            {
                GraphNodeShape.Flow => $"FLOW  ·  {descriptor.NodeType.Name.ToUpperInvariant()}",
                GraphNodeShape.Branch => $"BRANCH  ·  {descriptor.NodeType.Name.ToUpperInvariant()}",
                GraphNodeShape.Service => $"SERVICE  ·  {descriptor.NodeType.Name.ToUpperInvariant()}",
                _ => descriptor.NodeType.Name,
            };
        }

        private void DrawNodeShape(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            float width = layout.width;
            float height = layout.height;
            Color fill = GetFillColor();
            Color stroke = selected
                ? new Color(0.25f, 0.62f, 1f, 1f)
                : Descriptor.HasWarning
                    ? new Color(1f, 0.48f, 0.25f, 0.95f)
                    : GetStrokeColor();
            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.lineWidth = selected || Descriptor.IsHead ? 2.5f : 1.5f;

            switch (shape)
            {
                case GraphNodeShape.Flow:
                    DrawPolygon(painter, new[]
                    {
                        new Vector2(12f, 0f),
                        new Vector2(width - 12f, 0f),
                        new Vector2(width, height * 0.5f),
                        new Vector2(width - 12f, height),
                        new Vector2(12f, height),
                        new Vector2(0f, height * 0.5f),
                    });
                    break;
                case GraphNodeShape.Branch:
                    DrawPolygon(painter, new[]
                    {
                        new Vector2(width * 0.5f, 0f),
                        new Vector2(width, height * 0.5f),
                        new Vector2(width * 0.5f, height),
                        new Vector2(0f, height * 0.5f),
                    });
                    break;
                case GraphNodeShape.Service:
                    DrawCapsule(painter, width, height);
                    break;
                default:
                    DrawChamferedCard(painter, width, height);
                    break;
            }

            DrawPort(painter, shape == GraphNodeShape.Service
                ? new Vector2(0f, height * 0.5f)
                : new Vector2(width * 0.5f, 0f), stroke);
            if (shape == GraphNodeShape.Service)
            {
                DrawPort(painter, new Vector2(width, height * 0.5f), stroke);
            }
            else
            {
                int structuralOutputCount = GetStructuralOutputCount();
                if (shape is GraphNodeShape.Flow or GraphNodeShape.Branch && structuralOutputCount > 0)
                {
                    for (int i = 0; i < structuralOutputCount; i++)
                    {
                        DrawPort(painter, new Vector2(width * (i + 1f) / (structuralOutputCount + 1f), height), stroke);
                    }
                }
                else
                {
                    DrawPort(painter, new Vector2(width * 0.5f, height), stroke);
                }
            }
        }

        private int GetStructuralOutputCount()
        {
            GraphPresentation presentation = canvas.Presentation;
            if (presentation == null)
            {
                return 0;
            }

            int count = 0;
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (relation.Source.Item?.Node == Descriptor
                    && relation.Source.Anchor == GraphPresentationAnchorKind.Output
                    && relation.Target.IsValid
                    && relation.Kind is (GraphPresentationRelationKind.Structural
                        or GraphPresentationRelationKind.SequenceStart
                        or GraphPresentationRelationKind.DecisionBranch
                        or GraphPresentationRelationKind.ProbabilityBranch
                        or GraphPresentationRelationKind.ParallelBranch
                        or GraphPresentationRelationKind.ConditionTrue
                        or GraphPresentationRelationKind.ConditionFalse))
                {
                    count++;
                }
            }

            return count;
        }

        private Color GetFillColor()
        {
            float alpha = Descriptor.IsReachable ? 0.98f : 0.7f;
            if (EditorGUIUtility.isProSkin)
            {
                return shape switch
                {
                    GraphNodeShape.Flow => new Color(0.12f, 0.24f, 0.31f, alpha),
                    GraphNodeShape.Branch => new Color(0.25f, 0.18f, 0.31f, alpha),
                    GraphNodeShape.Service => new Color(0.30f, 0.23f, 0.10f, alpha),
                    _ => new Color(0.16f, 0.17f, 0.19f, alpha),
                };
            }

            return shape switch
            {
                GraphNodeShape.Flow => new Color(0.72f, 0.86f, 0.91f, alpha),
                GraphNodeShape.Branch => new Color(0.85f, 0.78f, 0.91f, alpha),
                GraphNodeShape.Service => new Color(0.93f, 0.86f, 0.68f, alpha),
                _ => new Color(0.82f, 0.83f, 0.85f, alpha),
            };
        }

        private Color GetStrokeColor()
        {
            return shape switch
            {
                GraphNodeShape.Flow => new Color(0.25f, 0.67f, 0.82f, 0.95f),
                GraphNodeShape.Branch => new Color(0.68f, 0.45f, 0.86f, 0.95f),
                GraphNodeShape.Service => new Color(0.91f, 0.66f, 0.21f, 0.95f),
                _ => EditorGUIUtility.isProSkin
                    ? new Color(0.62f, 0.65f, 0.7f, 0.9f)
                    : new Color(0.32f, 0.35f, 0.4f, 0.9f),
            };
        }

        private static void DrawChamferedCard(Painter2D painter, float width, float height)
        {
            DrawPolygon(painter, new[]
            {
                new Vector2(6f, 0f),
                new Vector2(width - 6f, 0f),
                new Vector2(width, 6f),
                new Vector2(width, height - 6f),
                new Vector2(width - 6f, height),
                new Vector2(6f, height),
                new Vector2(0f, height - 6f),
                new Vector2(0f, 6f),
            });
        }

        private static void DrawCapsule(Painter2D painter, float width, float height)
        {
            float radius = height * 0.5f;
            const float kappa = 0.5522848f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(radius, 0f));
            painter.LineTo(new Vector2(width - radius, 0f));
            painter.BezierCurveTo(
                new Vector2(width - radius + radius * kappa, 0f),
                new Vector2(width, radius - radius * kappa),
                new Vector2(width, radius));
            painter.BezierCurveTo(
                new Vector2(width, radius + radius * kappa),
                new Vector2(width - radius + radius * kappa, height),
                new Vector2(width - radius, height));
            painter.LineTo(new Vector2(radius, height));
            painter.BezierCurveTo(
                new Vector2(radius - radius * kappa, height),
                new Vector2(0f, radius + radius * kappa),
                new Vector2(0f, radius));
            painter.BezierCurveTo(
                new Vector2(0f, radius - radius * kappa),
                new Vector2(radius - radius * kappa, 0f),
                new Vector2(radius, 0f));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        private static void DrawPolygon(Painter2D painter, IReadOnlyList<Vector2> points)
        {
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (int i = 1; i < points.Count; i++)
            {
                painter.LineTo(points[i]);
            }

            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        private static void DrawPort(Painter2D painter, Vector2 center, Color color)
        {
            const float radius = 4f;
            painter.fillColor = color;
            DrawPolygon(painter, new[]
            {
                center + new Vector2(0f, -radius),
                center + new Vector2(radius, 0f),
                center + new Vector2(0f, radius),
                center + new Vector2(-radius, 0f),
            });
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            module.SelectNode(Descriptor.Node);
            if (!movable)
            {
                evt.StopPropagation();
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            dragOffset = (canvasPoint - canvas.Pan) / canvas.Zoom - Descriptor.Position;
            dragging = true;
            pointerId = evt.pointerId;
            this.CapturePointer(pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
            {
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            Vector2 position = (canvasPoint - canvas.Pan) / canvas.Zoom - dragOffset;
            module.MoveNode(Descriptor, position);
            style.left = position.x;
            style.top = position.y;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId)
            {
                return;
            }

            dragging = false;
            this.ReleasePointer(evt.pointerId);
            pointerId = -1;
            module.CommitNodeMove();
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == pointerId)
            {
                dragging = false;
                this.ReleasePointer(evt.pointerId);
                pointerId = -1;
                module.CommitNodeMove();
            }
        }
    }

    /// <summary>
    /// Displays a Condition shell with its predicate embedded as the only child.
    /// True and false targets remain ordinary top-level graph nodes.
    /// </summary>
    internal sealed class GraphConditionElement : VisualElement
    {
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphPresentationItem item;
        private readonly bool movable;
        private bool selected;
        private bool dragging;
        private int pointerId = -1;
        private Vector2 dragOffset;

        /// <summary>Initializes a Condition compound element.</summary>
        internal GraphConditionElement(
            GraphCanvasElement canvas,
            GraphEditorModule module,
            GraphPresentationItem item,
            bool movable,
            Vector2 position,
            Func<GraphPresentationItem, bool, Vector2, GraphNodeShape?, VisualElement> createElement)
        {
            this.canvas = canvas;
            this.module = module;
            this.item = item;
            this.movable = movable;
            name = $"ai-editor-graph-condition-{item.TargetUUID}";
            AddToClassList("ai-editor-graph-condition");
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            Label title = new(item.Node?.DisplayName ?? "Condition");
            title.AddToClassList("ai-editor-graph-condition-title");
            Add(title);
            Label typeLabel = new("CONDITION  ·  TRUE / FALSE");
            typeLabel.AddToClassList("ai-editor-graph-condition-type");
            Add(typeLabel);
            if (item.Node?.HasWarning == true)
            {
                tooltip = item.Node.Warning;
                Label warning = new("!")
                {
                    tooltip = item.Node.Warning,
                };
                warning.AddToClassList("ai-editor-graph-node-warning");
                Add(warning);
            }

            if (item.Slots.Count > 0 && item.Slots[0].Content?.Node != null)
            {
                Add(createElement(item.Slots[0].Content, false, item.Position, null));
            }

            generateVisualContent += DrawShell;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        /// <summary>Updates shell and predicate selection state.</summary>
        internal void SetSelected(TreeNode node)
        {
            bool shellSelected = item.Node?.Node == node;
            selected = shellSelected;
            EnableInClassList("ai-editor-graph-condition-selected", shellSelected);
            foreach (VisualElement child in Children())
            {
                if (child is GraphNodeElement predicate)
                {
                    predicate.SetSelected(predicate.Descriptor.Node == node);
                }
            }

            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || item.Node == null)
            {
                return;
            }

            module.SelectNode(item.Node.Node);
            if (!movable)
            {
                evt.StopPropagation();
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            dragOffset = (canvasPoint - canvas.Pan) / canvas.Zoom - item.Position;
            dragging = true;
            pointerId = evt.pointerId;
            this.CapturePointer(pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
            {
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            Vector2 position = (canvasPoint - canvas.Pan) / canvas.Zoom - dragOffset;
            module.MoveNode(item.Node, position);
            style.left = position.x;
            style.top = position.y;
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId)
            {
                return;
            }

            dragging = false;
            this.ReleasePointer(evt.pointerId);
            pointerId = -1;
            module.CommitNodeMove();
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == pointerId)
            {
                dragging = false;
                this.ReleasePointer(evt.pointerId);
                pointerId = -1;
                module.CommitNodeMove();
            }
        }

        private void DrawShell(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            Color stroke = selected
                ? new Color(0.25f, 0.62f, 1f)
                : new Color(0.68f, 0.45f, 0.86f, 0.8f);
            painter.fillColor = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.10f, 0.16f, 0.7f)
                : new Color(0.88f, 0.84f, 0.92f, 0.7f);
            painter.strokeColor = stroke;
            painter.lineWidth = selected ? 2.5f : 1.25f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(8f, 0f));
            painter.LineTo(new Vector2(layout.width - 8f, 0f));
            painter.LineTo(new Vector2(layout.width, 8f));
            painter.LineTo(new Vector2(layout.width, layout.height - 8f));
            painter.LineTo(new Vector2(layout.width - 8f, layout.height));
            painter.LineTo(new Vector2(8f, layout.height));
            painter.LineTo(new Vector2(0f, layout.height - 8f));
            painter.LineTo(new Vector2(0f, 8f));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
            if (item.Slots.Count > 0 && item.Slots[0].Content != null)
            {
                GraphPresentationItem predicate = item.Slots[0].Content;
                Vector2 from = new(layout.width * 0.5f, 28f);
                Vector2 to = new(
                    predicate.Position.x - item.Position.x + predicate.Size.x * 0.5f,
                    predicate.Position.y - item.Position.y);
                painter.strokeColor = stroke;
                painter.lineWidth = 1.25f;
                painter.BeginPath();
                painter.MoveTo(from);
                painter.LineTo(to);
                painter.Stroke();
            }
        }
    }

    /// <summary>
    /// Native container element retained for compatibility with older editor tests.
    /// New presentations only create GraphConditionElement for compound nodes.
    /// </summary>
    internal sealed class GraphContainerElement : VisualElement
    {
        private const float PlaceholderHeight = 52f;
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphPresentationItem item;
        private readonly bool movable;
        private readonly Label title;
        private readonly Label typeLabel;
        private readonly List<VisualElement> selectableChildren = new();
        private readonly Func<GraphPresentationItem, bool, Vector2, GraphNodeShape?, VisualElement> createElement;
        private bool dragging;
        private int pointerId = -1;
        private Vector2 dragOffset;

        /// <summary>
        /// Initializes a semantic Flow container.
        /// </summary>
        internal GraphContainerElement(
            GraphCanvasElement canvas,
            GraphEditorModule module,
            GraphPresentationItem item,
            bool movable,
            Vector2 position,
            Func<GraphPresentationItem, bool, Vector2, GraphNodeShape?, VisualElement> createElement)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            this.movable = movable;
            this.createElement = createElement ?? throw new ArgumentNullException(nameof(createElement));

            name = $"ai-editor-graph-container-{item.Node?.UUID ?? item.TargetUUID}";
            AddToClassList("ai-editor-graph-container");
            AddToClassList($"ai-editor-graph-container-{item.Kind.ToString().ToLowerInvariant()}");
            if (item.Node?.IsHead == true)
            {
                AddToClassList("ai-editor-graph-container-head");
            }

            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            generateVisualContent += DrawContainer;
            title = new Label(item.Node?.DisplayName ?? "Flow");
            title.AddToClassList("ai-editor-graph-container-title");
            typeLabel = new Label(GetTypeLabel(item));
            typeLabel.AddToClassList("ai-editor-graph-container-type");

            VisualElement header = new()
            {
                name = "ai-editor-graph-container-header",
            };
            header.AddToClassList("ai-editor-graph-container-header");
            header.style.position = UIPosition.Absolute;
            header.style.left = 0f;
            header.style.top = 0f;
            header.style.width = item.Size.x;
            header.style.height = 48f;
            header.Add(title);
            header.Add(typeLabel);
            header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            header.RegisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
            header.RegisterCallback<PointerUpEvent>(OnHeaderPointerUp);
            header.RegisterCallback<PointerCancelEvent>(OnHeaderPointerCancel);
            Add(header);

            BuildSlots();
        }

        /// <summary>Updates selection for this container and all nested presentations.</summary>
        internal void SetSelected(TreeNode selectedNode)
        {
            EnableInClassList("ai-editor-graph-container-selected", item.Node?.Node == selectedNode);
            foreach (VisualElement child in selectableChildren)
            {
                if (child is GraphNodeElement card)
                {
                    card.SetSelected(card.Descriptor.Node == selectedNode);
                }
                else if (child is GraphContainerElement container)
                {
                    container.SetSelected(selectedNode);
                }
                else if (child is GraphReferenceProxyElement proxy)
                {
                    proxy.SetSelected(proxy.TargetNode == selectedNode);
                }
            }

            MarkDirtyRepaint();
        }

        private void BuildSlots()
        {
            foreach (GraphPresentationSlot slot in item.Slots)
            {
                if (item.Kind is GraphPresentationKind.Sequence or GraphPresentationKind.Decision)
                {
                    Vector2 slotPosition = item.Position + new Vector2(0f, slot.Content.Position.y - item.Position.y);
                    AddSlot(slot, slotPosition, null);
                }
                else
                {
                    GraphNodeShape? shapeOverride = slot.Label == "Condition" ? GraphNodeShape.Branch : null;
                    Vector2 slotPosition = item.Position + new Vector2(slot.Content.Position.x - item.Position.x, slot.Content.Position.y - item.Position.y);
                    AddSlot(slot, slotPosition, shapeOverride);
                }
            }
        }

        private void AddSlot(GraphPresentationSlot slot, Vector2 slotPosition, GraphNodeShape? shapeOverride)
        {
            GraphSlotElement slotElement = new(
                slot,
                item.Position,
                slotPosition,
                createElement,
                shapeOverride);
            Add(slotElement);
            if (slotElement.ContentElement != null)
            {
                selectableChildren.Add(slotElement.ContentElement);
            }
        }

        private static string GetTypeLabel(GraphPresentationItem value)
        {
            return value.Kind switch
            {
                GraphPresentationKind.Sequence => "FLOW  ·  SEQUENCE  ·  RUN ALL",
                GraphPresentationKind.Decision => "FLOW  ·  DECISION  ·  PRIORITY",
                GraphPresentationKind.Condition => "FLOW  ·  CONDITION  ·  TRUE / FALSE",
                _ => "FLOW",
            };
        }

        private void DrawContainer(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            float width = layout.width;
            float height = layout.height;
            Color fill = EditorGUIUtility.isProSkin
                ? new Color(0.10f, 0.12f, 0.15f, 0.96f)
                : new Color(0.88f, 0.90f, 0.93f, 0.96f);
            Color stroke = ClassListContains("ai-editor-graph-container-selected")
                ? new Color(0.25f, 0.62f, 1f)
                : item.Warning != null
                    ? new Color(1f, 0.48f, 0.25f)
                    : item.Kind == GraphPresentationKind.Condition
                        ? new Color(0.68f, 0.45f, 0.86f)
                        : new Color(0.25f, 0.67f, 0.82f);

            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.lineWidth = ClassListContains("ai-editor-graph-container-selected") ? 2.5f : 1.5f;
            DrawRoundedRect(painter, new Rect(0f, 0f, width, height), 8f);

            if (item.Kind is GraphPresentationKind.Sequence or GraphPresentationKind.Decision)
            {
                float x = 24f;
                float startY = 48f;
                float endY = height - 16f;
                DrawSegment(painter, new Vector2(x, startY), new Vector2(x, endY), stroke, 1.5f);
                foreach (GraphPresentationSlot slot in item.Slots)
                {
                    float y = slot.Content.Position.y - item.Position.y + Mathf.Min(PlaceholderHeight, slot.Content.Size.y) * 0.5f;
                    DrawSegment(painter, new Vector2(x, y), new Vector2(slot.Content.Position.x - item.Position.x - 8f, y), stroke, 1.5f);
                }
            }
            else
            {
                GraphPresentationItem predicate = GetSlotContent("Condition");
                GraphPresentationItem trueItem = GetSlotContent("True");
                GraphPresentationItem falseItem = GetSlotContent("False");
                Vector2 predicateBottom = predicate.Position - item.Position + new Vector2(predicate.Size.x * 0.5f, predicate.Size.y);
                Vector2 branchY = new(item.Size.x * 0.5f, trueItem.Position.y - item.Position.y - 8f);
                DrawSegment(painter, predicateBottom, branchY, stroke, 1.5f);
                DrawSegment(painter, branchY, new Vector2(trueItem.Position.x - item.Position.x + trueItem.Size.x * 0.5f, branchY.y), stroke, 1.5f);
                DrawSegment(painter, branchY, new Vector2(falseItem.Position.x - item.Position.x + falseItem.Size.x * 0.5f, branchY.y), stroke, 1.5f);
                DrawSegment(painter, new Vector2(trueItem.Position.x - item.Position.x + trueItem.Size.x * 0.5f, branchY.y), new Vector2(trueItem.Position.x - item.Position.x + trueItem.Size.x * 0.5f, trueItem.Position.y - item.Position.y), stroke, 1.5f);
                DrawSegment(painter, new Vector2(falseItem.Position.x - item.Position.x + falseItem.Size.x * 0.5f, branchY.y), new Vector2(falseItem.Position.x - item.Position.x + falseItem.Size.x * 0.5f, falseItem.Position.y - item.Position.y), stroke, 1.5f);
            }
        }

        private GraphPresentationItem GetSlotContent(string label)
        {
            foreach (GraphPresentationSlot slot in item.Slots)
            {
                if (slot.Label == label)
                {
                    return slot.Content;
                }
            }

            return new GraphPresentationItem(GraphPresentationKind.Missing, null, UUID.Empty, label + " is empty");
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || item.Node == null)
            {
                return;
            }

            module.SelectNode(item.Node.Node);
            if (!movable)
            {
                evt.StopPropagation();
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            dragOffset = (canvasPoint - canvas.Pan) / canvas.Zoom - item.Position;
            dragging = true;
            pointerId = evt.pointerId;
            ((VisualElement)evt.currentTarget).CapturePointer(pointerId);
            evt.StopPropagation();
        }

        private void OnHeaderPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
            {
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            Vector2 position = (canvasPoint - canvas.Pan) / canvas.Zoom - dragOffset;
            module.MoveNode(item.Node, position);
            style.left = position.x;
            style.top = position.y;
            evt.StopPropagation();
        }

        private void OnHeaderPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId)
            {
                return;
            }

            dragging = false;
            ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
            pointerId = -1;
            module.CommitNodeMove();
            evt.StopPropagation();
        }

        private void OnHeaderPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == pointerId)
            {
                dragging = false;
                ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
                pointerId = -1;
                module.CommitNodeMove();
            }
        }

        private static void DrawRoundedRect(Painter2D painter, Rect rect, float radius)
        {
            radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax - radius, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin + radius));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - radius));
            painter.LineTo(new Vector2(rect.xMax - radius, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin + radius, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax - radius));
            painter.LineTo(new Vector2(rect.xMin, rect.yMin + radius));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        private static void DrawSegment(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }
    }

    /// <summary>
    /// Displays a named container slot and its recursively built content.
    /// </summary>
    internal sealed class GraphSlotElement : VisualElement
    {
        internal GraphSlotElement(
            GraphPresentationSlot slot,
            Vector2 parentPosition,
            Vector2 slotPosition,
            Func<GraphPresentationItem, bool, Vector2, GraphNodeShape?, VisualElement> createElement,
            GraphNodeShape? shapeOverride)
        {
            name = $"ai-editor-graph-slot-{slot.Label}";
            AddToClassList("ai-editor-graph-slot");
            style.position = UIPosition.Absolute;
            style.left = slotPosition.x - parentPosition.x;
            style.top = slotPosition.y - parentPosition.y;
            style.width = Mathf.Max(300f, slot.Content?.Size.x ?? 220f) + 72f;
            style.height = Mathf.Max(52f, slot.Content?.Size.y ?? 52f);

            Label label = new(slot.Label);
            label.AddToClassList("ai-editor-graph-slot-label");
            label.style.position = UIPosition.Absolute;
            bool stackedLabel = shapeOverride.HasValue || slot.Label is "True" or "False";
            label.style.left = stackedLabel ? 0f : 12f;
            label.style.top = stackedLabel ? -16f : 17f;
            Add(label);

            if (slot.Content != null)
            {
                ContentElement = createElement(slot.Content, false, slotPosition, shapeOverride);
                Add(ContentElement);
            }
        }

        /// <summary>Gets the nested element displayed by this slot.</summary>
        internal VisualElement ContentElement { get; }
    }

    /// <summary>
    /// Displays a missing or repeated reference without creating a second editable node.
    /// </summary>
    internal sealed class GraphReferenceProxyElement : VisualElement
    {
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphPresentationItem item;
        private readonly Label title;
        private bool selected;

        internal GraphReferenceProxyElement(GraphCanvasElement canvas, GraphEditorModule module, GraphPresentationItem item, Vector2 position)
        {
            this.canvas = canvas;
            this.module = module;
            this.item = item;
            name = $"ai-editor-graph-reference-{item.TargetUUID}";
            AddToClassList("ai-editor-graph-reference");
            AddToClassList(item.Kind == GraphPresentationKind.Missing ? "ai-editor-graph-reference-missing" : "ai-editor-graph-reference-proxy");
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            title = new Label(item.Node?.DisplayName ?? item.Warning ?? "Missing reference");
            title.AddToClassList("ai-editor-graph-reference-title");
            Add(title);
            tooltip = item.Warning;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        /// <summary>Gets the actual node selected by this proxy, if one exists.</summary>
        internal TreeNode TargetNode => item.Node?.Node;

        /// <summary>Updates the visual selection state.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            EnableInClassList("ai-editor-graph-reference-selected", value);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 && TargetNode != null)
            {
                module.SelectNode(TargetNode);
                evt.StopPropagation();
            }
        }
    }

    /// <summary>
    /// Draws a derived free-Sequence scope rail.
    /// </summary>
    internal sealed class GraphSequenceScopeElement : VisualElement
    {
        private bool selected;

        /// <summary>
        /// Initializes one non-interactive Sequence scope overlay.
        /// </summary>
        /// <param name="scope">The derived scope to display.</param>
        internal GraphSequenceScopeElement(GraphSequenceScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            name = $"ai-editor-graph-sequence-scope-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-sequence-scope");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Bounds.x;
            style.top = scope.Bounds.y;
            style.width = Mathf.Max(1f, scope.Bounds.width);
            style.height = Mathf.Max(1f, scope.Bounds.height);
            generateVisualContent += DrawScope;
        }

        /// <summary>Gets the derived scope represented by this overlay.</summary>
        internal GraphSequenceScope Scope { get; }

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            EnableInClassList("ai-editor-graph-sequence-scope-selected", value);
            MarkDirtyRepaint();
        }

        private void DrawScope(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            Color color = selected
                ? new Color(0.25f, 0.62f, 1f, 0.9f)
                : new Color(0.25f, 0.72f, 0.92f, 0.42f);
            float railX = Scope.RailX - Scope.Bounds.x;
            float startY = Scope.RailStartY - Scope.Bounds.y;
            float endY = Scope.RailEndY - Scope.Bounds.y;
            float ownerX = Scope.Owner.Position.x - Scope.Bounds.x;
            float completionX = Scope.CompletionPosition.x - Scope.Bounds.x;

            painter.strokeColor = color;
            painter.lineWidth = selected ? 2f : 1.25f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(ownerX, startY));
            painter.LineTo(new Vector2(railX, startY));
            painter.LineTo(new Vector2(railX, endY));
            painter.LineTo(new Vector2(completionX, endY));
            painter.Stroke();
        }
    }

    /// <summary>
    /// Draws the non-interactive bracket that identifies one Condition branch scope.
    /// </summary>
    internal sealed class GraphConditionScopeElement : VisualElement
    {
        private bool selected;

        /// <summary>Initializes one derived Condition scope bracket.</summary>
        internal GraphConditionScopeElement(GraphConditionScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            name = $"ai-editor-graph-condition-scope-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-condition-scope");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Bounds.x;
            style.top = scope.Bounds.y;
            style.width = Mathf.Max(1f, scope.Bounds.width);
            style.height = Mathf.Max(1f, scope.Bounds.height);
            generateVisualContent += DrawBracket;
        }

        /// <summary>Gets the derived scope represented by this overlay.</summary>
        internal GraphConditionScope Scope { get; }

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            EnableInClassList("ai-editor-graph-condition-scope-selected", value);
            MarkDirtyRepaint();
        }

        /// <summary>Draws low-emphasis range brackets without connection arrows.</summary>
        private void DrawBracket(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            Color color = selected
                ? new Color(0.72f, 0.48f, 0.92f, 0.95f)
                : new Color(0.72f, 0.48f, 0.92f, 0.38f);
            float left = Scope.LeftX - Scope.Bounds.x;
            float right = Scope.RightX - Scope.Bounds.x;
            float top = Scope.BracketTopY - Scope.Bounds.y;
            float bottom = Scope.BracketBottomY - Scope.Bounds.y;
            const float tick = 12f;
            painter.strokeColor = color;
            painter.lineWidth = selected ? 2f : 1.25f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(left + tick, top));
            painter.LineTo(new Vector2(left, top));
            painter.LineTo(new Vector2(left, bottom));
            painter.LineTo(new Vector2(left + tick, bottom));
            painter.MoveTo(new Vector2(right - tick, top));
            painter.LineTo(new Vector2(right, top));
            painter.LineTo(new Vector2(right, bottom));
            painter.LineTo(new Vector2(right - tick, bottom));
            painter.Stroke();
        }
    }

    /// <summary>
    /// Draws the non-interactive bracket that identifies one Loop body and repeat scope.
    /// </summary>
    internal sealed class GraphLoopScopeElement : VisualElement
    {
        private bool selected;

        /// <summary>Initializes one derived Loop scope bracket.</summary>
        internal GraphLoopScopeElement(GraphLoopScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            name = $"ai-editor-graph-loop-scope-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-loop-scope");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Bounds.x;
            style.top = scope.Bounds.y;
            style.width = Mathf.Max(1f, scope.Bounds.width);
            style.height = Mathf.Max(1f, scope.Bounds.height);
            generateVisualContent += DrawBracket;

            Label label = new("BODY / REPEAT");
            label.AddToClassList("ai-editor-graph-loop-scope-label");
            label.pickingMode = PickingMode.Ignore;
            label.style.position = UIPosition.Absolute;
            label.style.left = scope.LeftX - scope.Bounds.x + 8f;
            label.style.top = scope.BracketTopY - scope.Bounds.y - 1f;
            Add(label);
        }

        /// <summary>Gets the derived scope represented by this overlay.</summary>
        internal GraphLoopScope Scope { get; }

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            EnableInClassList("ai-editor-graph-loop-scope-selected", value);
            MarkDirtyRepaint();
        }

        /// <summary>Draws low-emphasis body brackets without connection arrows.</summary>
        private void DrawBracket(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            Color color = selected
                ? new Color(0.28f, 0.82f, 0.72f, 0.95f)
                : new Color(0.28f, 0.82f, 0.72f, 0.38f);
            float left = Scope.LeftX - Scope.Bounds.x;
            float right = Scope.RightX - Scope.Bounds.x;
            float top = Scope.BracketTopY - Scope.Bounds.y;
            float bottom = Scope.BracketBottomY - Scope.Bounds.y;
            const float tick = 12f;
            painter.strokeColor = color;
            painter.lineWidth = selected ? 2f : 1.25f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(left + tick, top));
            painter.LineTo(new Vector2(left, top));
            painter.LineTo(new Vector2(left, bottom));
            painter.LineTo(new Vector2(left + tick, bottom));
            painter.MoveTo(new Vector2(right - tick, top));
            painter.LineTo(new Vector2(right, top));
            painter.LineTo(new Vector2(right, bottom));
            painter.LineTo(new Vector2(right - tick, bottom));
            painter.Stroke();
        }
    }

    /// <summary>
    /// Displays an empty or unresolved Condition branch without creating an editable TreeNode.
    /// </summary>
    internal sealed class GraphConditionPlaceholderElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        /// <summary>Initializes one non-interactive Condition branch placeholder.</summary>
        internal GraphConditionPlaceholderElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphConditionPlaceholder placeholder = item.Placeholder
                ?? throw new ArgumentException("A Condition placeholder descriptor is required.", nameof(item));
            name = $"ai-editor-graph-condition-placeholder-{placeholder.Branch.ToString().ToLowerInvariant()}";
            AddToClassList("ai-editor-graph-condition-placeholder");
            EnableInClassList("ai-editor-graph-condition-placeholder-missing", placeholder.IsMissing);
            pickingMode = PickingMode.Ignore;
            tooltip = placeholder.Tooltip;
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            Label title = new(placeholder.Title);
            title.AddToClassList("ai-editor-graph-condition-placeholder-title");
            Add(title);
            Label subtitle = new(placeholder.Subtitle);
            subtitle.AddToClassList("ai-editor-graph-condition-placeholder-subtitle");
            Add(subtitle);
        }

        /// <summary>Repositions this derived element from its presentation item.</summary>
        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }
    }

    /// <summary>
    /// Displays an empty or unresolved Loop condition or body occurrence.
    /// </summary>
    internal sealed class GraphLoopPlaceholderElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        /// <summary>Initializes one non-interactive Loop placeholder.</summary>
        internal GraphLoopPlaceholderElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphLoopPlaceholder placeholder = item.LoopPlaceholder
                ?? throw new ArgumentException("A Loop placeholder descriptor is required.", nameof(item));
            name = $"ai-editor-graph-loop-placeholder-{placeholder.Part.ToString().ToLowerInvariant()}";
            AddToClassList("ai-editor-graph-loop-placeholder");
            EnableInClassList("ai-editor-graph-loop-placeholder-missing", placeholder.IsMissing);
            pickingMode = PickingMode.Ignore;
            tooltip = placeholder.Tooltip;
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            Label title = new(placeholder.Title);
            title.AddToClassList("ai-editor-graph-loop-placeholder-title");
            Add(title);
            Label subtitle = new(placeholder.Subtitle);
            subtitle.AddToClassList("ai-editor-graph-loop-placeholder-subtitle");
            Add(subtitle);
        }

        /// <summary>Repositions this derived element from its presentation item.</summary>
        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }
    }

    /// <summary>
    /// Displays a derived Loop count-check or repeat control point.
    /// </summary>
    internal sealed class GraphLoopJunctionElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        /// <summary>Initializes one non-interactive Loop control point.</summary>
        internal GraphLoopJunctionElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphLoopJunction junction = item.LoopJunction
                ?? throw new ArgumentException("A Loop junction descriptor is required.", nameof(item));
            name = $"ai-editor-graph-loop-junction-{junction.Kind.ToString().ToLowerInvariant()}";
            AddToClassList("ai-editor-graph-loop-junction");
            AddToClassList($"ai-editor-graph-loop-junction-{junction.Kind.ToString().ToLowerInvariant()}");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            Label title = new(junction.Title);
            title.AddToClassList("ai-editor-graph-loop-junction-title");
            Add(title);
            if (!string.IsNullOrEmpty(junction.Subtitle))
            {
                Label subtitle = new(junction.Subtitle);
                subtitle.AddToClassList("ai-editor-graph-loop-junction-subtitle");
                Add(subtitle);
            }
        }

        /// <summary>Repositions this derived element from its presentation item.</summary>
        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }
    }

    /// <summary>
    /// Displays the non-interactive completion marker shared by composite Flow presentations.
    /// </summary>
    internal sealed class GraphFlowCompletionElement : Label
    {
        /// <summary>
        /// Initializes one presentation-only Flow completion marker.
        /// </summary>
        /// <param name="scope">The derived Flow scope to display.</param>
        internal GraphFlowCompletionElement(GraphFlowScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            string displayName = scope.Owner.Node?.DisplayName ?? "Flow";
            text = $"END · {displayName}";
            name = $"ai-editor-graph-flow-end-{scope.Owner.TargetUUID}";
            tooltip = $"{displayName} completes here.";
            pickingMode = PickingMode.Ignore;
            AddToClassList("ai-editor-graph-flow-end");
            style.position = UIPosition.Absolute;
            style.left = scope.CompletionPosition.x;
            style.top = scope.CompletionPosition.y;
            style.width = scope.CompletionSize.x;
            style.height = scope.CompletionSize.y;
        }

        /// <summary>Gets the derived scope represented by this marker.</summary>
        internal GraphFlowScope Scope { get; }

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            EnableInClassList("ai-editor-graph-flow-end-selected", value);
        }
    }

    /// <summary>
    /// Draws graph relations with native UI Toolkit Painter2D content.
    /// </summary>
    internal sealed class GraphEdgeLayerElement : VisualElement
    {
        private GraphPresentation presentation;
        private readonly List<GraphPresentationRelation> labeledRelations = new();
        private readonly List<Label> edgeLabels = new();

        /// <summary>
        /// Initializes an edge layer.
        /// </summary>
        internal GraphEdgeLayerElement()
        {
            generateVisualContent += DrawEdges;
        }

        /// <summary>
        /// Replaces the displayed topology.
        /// </summary>
        internal void SetTopology(GraphTopology topology)
        {
            GraphPresentation value = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(value);
            SetPresentation(value);
        }

        /// <summary>
        /// Replaces the displayed semantic presentation.
        /// </summary>
        /// <param name="value">The semantic presentation to draw.</param>
        internal void SetPresentation(GraphPresentation value)
        {
            presentation = value;
            Clear();
            labeledRelations.Clear();
            edgeLabels.Clear();
            if (presentation != null)
            {
                foreach (GraphPresentationRelation relation in presentation.Relations)
                {
                    if (!relation.Target.IsValid || string.IsNullOrEmpty(relation.Label))
                    {
                        continue;
                    }

                    Label label = new(relation.Label);
                    label.AddToClassList("ai-editor-graph-edge-label");
                    label.pickingMode = PickingMode.Ignore;
                    GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out Vector2 to);

                    label.style.position = UIPosition.Absolute;
                    label.style.left = (from.x + to.x) * 0.5f;
                    label.style.top = (from.y + to.y) * 0.5f;
                    Add(label);
                    labeledRelations.Add(relation);
                    edgeLabels.Add(label);
                }
            }

            MarkDirtyRepaint();
        }

        /// <summary>
        /// Repositions edge labels after a node has moved in the canvas.
        /// </summary>
        internal void RefreshLabelPositions()
        {
            int count = Mathf.Min(labeledRelations.Count, edgeLabels.Count);
            for (int i = 0; i < count; i++)
            {
                GraphPresentationRelation relation = labeledRelations[i];
                Label label = edgeLabels[i];
                GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out Vector2 to);

                label.style.left = (from.x + to.x) * 0.5f;
                label.style.top = (from.y + to.y) * 0.5f;
            }
        }

        private void DrawEdges(MeshGenerationContext context)
        {
            if (presentation == null || context.painter2D == null)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (!relation.Target.IsValid)
                {
                    continue;
                }

                GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out Vector2 to);

                Color color = relation.Kind switch
                {
                    GraphPresentationRelationKind.Service => new Color(0.95f, 0.72f, 0.25f),
                    GraphPresentationRelationKind.Raw => new Color(0.55f, 0.65f, 0.9f),
                    GraphPresentationRelationKind.SequenceStart
                        or GraphPresentationRelationKind.SequenceNext
                        or GraphPresentationRelationKind.FlowComplete => new Color(0.25f, 0.72f, 0.92f),
                    GraphPresentationRelationKind.DecisionBranch
                        or GraphPresentationRelationKind.ConditionTrue
                        or GraphPresentationRelationKind.ConditionFalse => new Color(0.72f, 0.48f, 0.92f),
                    GraphPresentationRelationKind.ProbabilityBranch => new Color(0.95f, 0.72f, 0.25f),
                    GraphPresentationRelationKind.ParallelBranch => new Color(0.35f, 0.66f, 0.95f),
                    GraphPresentationRelationKind.LoopCondition
                        or GraphPresentationRelationKind.LoopBody
                        or GraphPresentationRelationKind.LoopRepeat
                        or GraphPresentationRelationKind.LoopExit => new Color(0.28f, 0.82f, 0.72f),
                    _ => new Color(0.72f, 0.72f, 0.72f),
                };

                if (relation.Role == GraphPresentationRelationRole.DerivedCompletion)
                {
                    DrawPatternedCurve(painter, from, to, color, 1.25f, 8f, 5f);
                    DrawHollowArrowHead(painter, from, to, color);
                    continue;
                }

                if (relation.Role == GraphPresentationRelationRole.DerivedControl)
                {
                    if (relation.Kind == GraphPresentationRelationKind.LoopRepeat && to.y < from.y)
                    {
                        DrawLoopBack(painter, from, to, color);
                        continue;
                    }

                    DrawPatternedCurve(painter, from, to, color, 1.25f, 4f, 4f);
                    DrawHollowArrowHead(painter, from, to, color);
                    continue;
                }

                if (relation.Role == GraphPresentationRelationRole.PlaceholderHint)
                {
                    DrawPatternedCurve(painter, from, to, color, 1f, 2f, 6f);
                    continue;
                }

                switch (relation.Kind)
                {
                    case GraphPresentationRelationKind.Structural:
                    case GraphPresentationRelationKind.SequenceStart:
                    case GraphPresentationRelationKind.SequenceNext:
                    case GraphPresentationRelationKind.FlowComplete:
                    case GraphPresentationRelationKind.DecisionBranch:
                    case GraphPresentationRelationKind.ProbabilityBranch:
                    case GraphPresentationRelationKind.ParallelBranch:
                    case GraphPresentationRelationKind.ConditionTrue:
                    case GraphPresentationRelationKind.ConditionFalse:
                    case GraphPresentationRelationKind.LoopCondition:
                    case GraphPresentationRelationKind.LoopBody:
                    case GraphPresentationRelationKind.LoopRepeat:
                    case GraphPresentationRelationKind.LoopExit:
                        DrawCurve(painter, from, to, color, 2f, horizontal: false);
                        break;
                    case GraphPresentationRelationKind.Raw:
                        DrawDotted(painter, from, to, color, 2f);
                        break;
                    default:
                        DrawDashed(painter, from, to, color, 2f);
                        break;
                }

                DrawArrowHead(painter, from, to, color);
            }
        }

        private float GetParallelOffset(GraphPresentationRelation relation)
        {
            if (presentation == null)
            {
                return 0f;
            }

            int occurrence = 0;
            foreach (GraphPresentationRelation candidate in presentation.Relations)
            {
                if (ReferenceEquals(candidate, relation))
                {
                    break;
                }

                if (candidate.Source == relation.Source && candidate.Target == relation.Target && candidate.Kind == relation.Kind)
                {
                    occurrence++;
                }
            }

            return occurrence * 7f;
        }

        private void GetAnchors(GraphPresentationRelation relation, float offset, out Vector2 from, out Vector2 to)
        {
            Rect sourceBounds = GetBounds(relation.Source);
            Rect targetBounds = GetBounds(relation.Target);
            Vector2 sourceSize = sourceBounds.size;
            Vector2 targetSize = targetBounds.size;
            if (relation.Kind == GraphPresentationRelationKind.Service)
            {
                from = sourceBounds.position + new Vector2(sourceSize.x, sourceSize.y * 0.5f + offset);
                to = targetBounds.position + new Vector2(0f, targetSize.y * 0.5f + offset);
                return;
            }

            float sourceX = sourceSize.x * 0.5f;
            if (relation.Source.Anchor == GraphPresentationAnchorKind.Output
                && IsBranchingRelation(relation.Kind)
                && relation.Source.Item.Node != null
                && relation.Source.Item.Node.Shape is GraphNodeShape.Flow or GraphNodeShape.Branch)
            {
                GetStructuralOutputSlot(relation, out int index, out int count);
                sourceX = sourceSize.x * (index + 1f) / (count + 1f);
            }

            from = sourceBounds.position + new Vector2(sourceX + offset, sourceSize.y);
            to = targetBounds.position + new Vector2(targetSize.x * 0.5f + offset, 0f);
        }

        private static Rect GetBounds(GraphPresentationEndpoint endpoint)
        {
            if (endpoint.Anchor == GraphPresentationAnchorKind.FlowComplete)
            {
                GraphFlowScope scope = endpoint.Item.FlowScope;
                return new Rect(scope.CompletionPosition, scope.CompletionSize);
            }

            return new Rect(endpoint.Item.Position, endpoint.Item.Size);
        }

        private void GetStructuralOutputSlot(GraphPresentationRelation relation, out int index, out int count)
        {
            index = 0;
            count = 0;
            if (presentation == null)
            {
                return;
            }

            foreach (GraphPresentationRelation candidate in presentation.Relations)
            {
                if (candidate.Source != relation.Source || !IsBranchingRelation(candidate.Kind) || !candidate.Target.IsValid)
                {
                    continue;
                }

                if (ReferenceEquals(candidate, relation))
                {
                    index = count;
                }

                count++;
            }
        }

        private static bool IsBranchingRelation(GraphPresentationRelationKind kind)
        {
            return kind is GraphPresentationRelationKind.Structural
                or GraphPresentationRelationKind.SequenceStart
                or GraphPresentationRelationKind.SequenceNext
                or GraphPresentationRelationKind.DecisionBranch
                or GraphPresentationRelationKind.ProbabilityBranch
                or GraphPresentationRelationKind.ParallelBranch
                or GraphPresentationRelationKind.ConditionTrue
                or GraphPresentationRelationKind.ConditionFalse
                or GraphPresentationRelationKind.LoopCondition
                or GraphPresentationRelationKind.LoopBody
                or GraphPresentationRelationKind.LoopRepeat
                or GraphPresentationRelationKind.LoopExit;
        }

        /// <summary>Draws a derived repeat path outside the body lane.</summary>
        private static void DrawLoopBack(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            float railX = Mathf.Min(from.x, to.x) - 28f;
            Vector2 lowerCorner = new(railX, from.y);
            Vector2 upperCorner = new(railX, to.y);
            DrawDashed(painter, from, lowerCorner, color, 1.25f);
            DrawDashed(painter, lowerCorner, upperCorner, color, 1.25f);
            DrawDashed(painter, upperCorner, to, color, 1.25f);
            DrawHollowArrowHead(painter, upperCorner, to, color);
        }

        private static void DrawCurve(Painter2D painter, Vector2 from, Vector2 to, Color color, float width, bool horizontal)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            Vector2 firstControl;
            Vector2 secondControl;
            if (horizontal)
            {
                float distance = Mathf.Max(36f, Mathf.Abs(to.x - from.x) * 0.5f);
                firstControl = from + Vector2.right * distance;
                secondControl = to + Vector2.left * distance;
            }
            else
            {
                float distance = Mathf.Max(36f, Mathf.Abs(to.y - from.y) * 0.5f);
                firstControl = from + Vector2.up * distance;
                secondControl = to + Vector2.down * distance;
            }

            painter.BeginPath();
            painter.MoveTo(from);
            painter.BezierCurveTo(firstControl, secondControl, to);
            painter.Stroke();
        }

        private static void DrawArrowHead(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = (to - from).normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector2 normal = new(-direction.y, direction.x);
            Vector2 basePoint = to - direction * 8f;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(to);
            painter.LineTo(basePoint + normal * 4f);
            painter.LineTo(basePoint - normal * 4f);
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>Draws an unfilled arrowhead for a derived relation.</summary>
        private static void DrawHollowArrowHead(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = (to - from).normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector2 normal = new(-direction.y, direction.x);
            Vector2 basePoint = to - direction * 8f;
            painter.strokeColor = color;
            painter.lineWidth = 1.25f;
            painter.BeginPath();
            painter.MoveTo(basePoint + normal * 4f);
            painter.LineTo(to);
            painter.LineTo(basePoint - normal * 4f);
            painter.Stroke();
        }

        /// <summary>Draws a sampled Bezier curve using a repeated mark-and-gap pattern.</summary>
        private static void DrawPatternedCurve(
            Painter2D painter,
            Vector2 from,
            Vector2 to,
            Color color,
            float width,
            float markLength,
            float gapLength)
        {
            float controlDistance = Mathf.Max(36f, Mathf.Abs(to.y - from.y) * 0.5f);
            Vector2 firstControl = from + Vector2.up * controlDistance;
            Vector2 secondControl = to + Vector2.down * controlDistance;
            const int sampleCount = 48;
            float patternLength = markLength + gapLength;
            float traversed = 0f;
            Vector2 previous = from;
            for (int sample = 1; sample <= sampleCount; sample++)
            {
                float t = sample / (float)sampleCount;
                Vector2 current = EvaluateBezier(from, firstControl, secondControl, to, t);
                float segmentLength = Vector2.Distance(previous, current);
                if (Mathf.Repeat(traversed + segmentLength * 0.5f, patternLength) < markLength)
                {
                    DrawSegment(painter, previous, current, color, width);
                }

                traversed += segmentLength;
                previous = current;
            }
        }

        /// <summary>Evaluates a cubic Bezier curve at the requested normalized position.</summary>
        private static Vector2 EvaluateBezier(
            Vector2 start,
            Vector2 firstControl,
            Vector2 secondControl,
            Vector2 end,
            float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * start
                + 3f * inverse * inverse * t * firstControl
                + 3f * inverse * t * t * secondControl
                + t * t * t * end;
        }

        private static void DrawSegment(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static void DrawDashed(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Vector2 direction = delta / length;
            const float dash = 8f;
            const float gap = 5f;
            for (float distance = 0f; distance < length; distance += dash + gap)
            {
                float endDistance = Mathf.Min(distance + dash, length);
                DrawSegment(painter, from + direction * distance, from + direction * endDistance, color, width);
            }
        }

        /// <summary>
        /// Draws a dotted edge for an optional raw reference.
        /// </summary>
        private static void DrawDotted(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Vector2 direction = delta / length;
            const float dotLength = 2f;
            const float gap = 6f;
            for (float distance = 0f; distance < length; distance += dotLength + gap)
            {
                float endDistance = Mathf.Min(distance + dotLength, length);
                DrawSegment(painter, from + direction * distance, from + direction * endDistance, color, width);
            }
        }
    }
}
