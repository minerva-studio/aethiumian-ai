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
        private readonly GraphEditorModule module;
        private readonly VisualElement content;
        private readonly GraphEdgeLayerElement edgeLayer;
        private readonly VisualElement nodeLayer;
        private GraphPresentation presentation;
        private bool panning;
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStart;
        private float zoom = 1f;
        private Vector2 pan;

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

            nodeLayer = new VisualElement
            {
                name = "ai-editor-graph-node-layer",
            };
            nodeLayer.AddToClassList("ai-editor-graph-node-layer");
            nodeLayer.style.position = UIPosition.Absolute;
            nodeLayer.style.left = 0f;
            nodeLayer.style.top = 0f;

            content.Add(edgeLayer);
            content.Add(nodeLayer);
            Add(content);

            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            RegisterCallback<WheelEvent>(OnWheel);
        }

        /// <summary>
        /// Gets or sets the current canvas zoom factor.
        /// </summary>
        internal float Zoom
        {
            get => zoom;
            set
            {
                zoom = Mathf.Clamp(value, 0.25f, 2.5f);
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
            edgeLayer.SetPresentation(topology, presentation);
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

        /// <summary>
        /// Refreshes card selection without rebuilding the topology.
        /// </summary>
        /// <param name="selectedNode">The selected node instance.</param>
        internal void SetSelectedNode(TreeNode selectedNode)
        {
            foreach (VisualElement element in nodeLayer.Children())
            {
                if (element is GraphNodeElement node)
                {
                    node.SetSelected(node.Descriptor.Node == selectedNode);
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
            if (presentation == null || presentation.Roots.Count == 0 || layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            Rect bounds = CalculateBounds(presentation);
            float availableWidth = Mathf.Max(1f, layout.width - 32f);
            float availableHeight = Mathf.Max(1f, layout.height - 32f);
            float scaleX = availableWidth / Mathf.Max(1f, bounds.width);
            float scaleY = availableHeight / Mathf.Max(1f, bounds.height);
            zoom = Mathf.Clamp(Mathf.Min(scaleX, scaleY), 0.25f, 1.5f);
            Vector2 center = bounds.center;
            pan = new Vector2(layout.width * 0.5f, layout.height * 0.5f) - center * zoom;
            ApplyTransform();
        }

        /// <summary>
        /// Frames the selected node in the viewport.
        /// </summary>
        internal void FrameSelected()
        {
            GraphPresentationItem selected = presentation?.Find(module.SelectedNode?.uuid ?? UUID.Empty);
            if (selected == null || layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            zoom = Mathf.Clamp(Mathf.Max(zoom, 0.75f), 0.25f, 2.5f);
            pan = new Vector2(layout.width * 0.5f, layout.height * 0.5f)
                - (selected.Position + selected.Size * 0.5f) * zoom;
            ApplyTransform();
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
            edgeLayer.RefreshLabelPositions();
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
                if (element is GraphNodeElement or GraphContainerElement or GraphReferenceProxyElement)
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
            if (module.Topology == null)
            {
                return;
            }

            float oldZoom = zoom;
            float direction = evt.delta.y > 0f ? 0.9f : 1.1f;
            zoom = Mathf.Clamp(zoom * direction, 0.25f, 2.5f);
            Vector2 pointer = this.WorldToLocal(evt.mousePosition);
            pan = pointer - (pointer - pan) * (zoom / oldZoom);
            ApplyTransform();
            evt.StopPropagation();
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
            nodeLayer.style.width = width;
            nodeLayer.style.height = height;
        }

        private static Rect CalculateBounds(GraphPresentation value)
        {
            if (value == null || value.Roots.Count == 0)
            {
                return new Rect(0f, 0f, 220f, 82f);
            }

            Rect first = GetPresentationBounds(value.Roots[0]);
            Vector2 min = first.min;
            Vector2 max = first.max;
            for (int i = 1; i < value.Roots.Count; i++)
            {
                Rect bounds = GetPresentationBounds(value.Roots[i]);
                min = Vector2.Min(min, bounds.min);
                max = Vector2.Max(max, bounds.max);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect GetPresentationBounds(GraphPresentationItem item)
        {
            return new Rect(item.Position, item.Size);
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
                case GraphPresentationKind.Sequence:
                case GraphPresentationKind.Decision:
                case GraphPresentationKind.Condition:
                    return new GraphContainerElement(this, module, item, isMovable, localPosition, CreatePresentationElement);
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
            GraphTopology topology = module.Topology;
            if (topology == null)
            {
                return 0;
            }

            int count = 0;
            foreach (GraphEdgeDescriptor edge in topology.Edges)
            {
                if (edge.Source == Descriptor && edge.Kind == GraphEdgeKind.Child && edge.Target != null)
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
    /// Native container element for Sequence, Decision and Condition presentations.
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
    /// Draws graph edges with native UI Toolkit Painter2D content.
    /// </summary>
    internal sealed class GraphEdgeLayerElement : VisualElement
    {
        private GraphTopology topology;
        private GraphPresentation presentation;
        private readonly List<GraphEdgeDescriptor> labeledEdges = new();
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
            SetPresentation(topology, null);
        }

        /// <summary>
        /// Replaces the displayed topology and filters edges owned by semantic containers.
        /// </summary>
        /// <param name="topology">The authoritative topology.</param>
        /// <param name="presentation">The semantic presentation, if available.</param>
        internal void SetPresentation(GraphTopology topology, GraphPresentation presentation)
        {
            this.topology = topology;
            this.presentation = presentation;
            Clear();
            labeledEdges.Clear();
            edgeLabels.Clear();
            if (topology != null)
            {
                IReadOnlyList<GraphEdgeDescriptor> edges = presentation?.ExternalEdges ?? topology.Edges;
                foreach (GraphEdgeDescriptor edge in edges)
                {
                    if (edge.Target == null)
                    {
                        continue;
                    }

                    Label label = new(edge.Label);
                    label.AddToClassList("ai-editor-graph-edge-label");
                    label.pickingMode = PickingMode.Ignore;
                    GetAnchors(edge, GetParallelOffset(edge), out Vector2 from, out Vector2 to);

                    label.style.position = UIPosition.Absolute;
                    label.style.left = (from.x + to.x) * 0.5f;
                    label.style.top = (from.y + to.y) * 0.5f;
                    Add(label);
                    labeledEdges.Add(edge);
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
            int count = Mathf.Min(labeledEdges.Count, edgeLabels.Count);
            for (int i = 0; i < count; i++)
            {
                GraphEdgeDescriptor edge = labeledEdges[i];
                Label label = edgeLabels[i];
                GetAnchors(edge, GetParallelOffset(edge), out Vector2 from, out Vector2 to);

                label.style.left = (from.x + to.x) * 0.5f;
                label.style.top = (from.y + to.y) * 0.5f;
            }
        }

        private void DrawEdges(MeshGenerationContext context)
        {
            if (topology == null || context.painter2D == null)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            IReadOnlyList<GraphEdgeDescriptor> edges = presentation?.ExternalEdges ?? topology.Edges;
            foreach (GraphEdgeDescriptor edge in edges)
            {
                if (edge.Target == null)
                {
                    continue;
                }

                GetAnchors(edge, GetParallelOffset(edge), out Vector2 from, out Vector2 to);

                Color color = edge.Kind switch
                {
                    GraphEdgeKind.Service => new Color(0.95f, 0.72f, 0.25f),
                    GraphEdgeKind.Raw => new Color(0.55f, 0.65f, 0.9f),
                    _ => new Color(0.72f, 0.72f, 0.72f),
                };

                switch (edge.Kind)
                {
                    case GraphEdgeKind.Child:
                        DrawCurve(painter, from, to, color, 2f, horizontal: false);
                        break;
                    case GraphEdgeKind.Raw:
                        DrawDotted(painter, from, to, color, 2f);
                        break;
                    default:
                        DrawDashed(painter, from, to, color, 2f);
                        break;
                }

                DrawArrowHead(painter, from, to, color);
            }
        }

        private float GetParallelOffset(GraphEdgeDescriptor edge)
        {
            if (topology == null)
            {
                return 0f;
            }

            int occurrence = 0;
            IReadOnlyList<GraphEdgeDescriptor> edges = presentation?.ExternalEdges ?? topology.Edges;
            foreach (GraphEdgeDescriptor candidate in edges)
            {
                if (ReferenceEquals(candidate, edge))
                {
                    break;
                }

                if (candidate.Source == edge.Source && candidate.Target == edge.Target && candidate.Kind == edge.Kind)
                {
                    occurrence++;
                }
            }

            return occurrence * 7f;
        }

        private void GetAnchors(GraphEdgeDescriptor edge, float offset, out Vector2 from, out Vector2 to)
        {
            Rect sourceBounds = GetBounds(edge.Source);
            Rect targetBounds = GetBounds(edge.Target);
            Vector2 sourceSize = sourceBounds.size;
            Vector2 targetSize = targetBounds.size;
            if (edge.Kind == GraphEdgeKind.Service)
            {
                from = sourceBounds.position + new Vector2(sourceSize.x, sourceSize.y * 0.5f + offset);
                to = targetBounds.position + new Vector2(0f, targetSize.y * 0.5f + offset);
                return;
            }

            float sourceX = sourceSize.x * 0.5f;
            if (edge.Kind == GraphEdgeKind.Child
                && edge.Source.Shape is GraphNodeShape.Flow or GraphNodeShape.Branch)
            {
                GetStructuralOutputSlot(edge, out int index, out int count);
                sourceX = sourceSize.x * (index + 1f) / (count + 1f);
            }

            from = sourceBounds.position + new Vector2(sourceX + offset, sourceSize.y);
            to = targetBounds.position + new Vector2(targetSize.x * 0.5f + offset, 0f);
        }

        private Rect GetBounds(GraphNodeDescriptor descriptor)
        {
            GraphPresentationItem item = presentation?.Find(descriptor.UUID);
            return item != null
                ? new Rect(item.Position, item.Size)
                : new Rect(descriptor.Position, GraphLayoutResolver.GetNodeSize(descriptor));
        }

        private void GetStructuralOutputSlot(GraphEdgeDescriptor edge, out int index, out int count)
        {
            index = 0;
            count = 0;
            if (topology == null)
            {
                return;
            }

            IReadOnlyList<GraphEdgeDescriptor> edges = presentation?.ExternalEdges ?? topology.Edges;
            foreach (GraphEdgeDescriptor candidate in edges)
            {
                if (candidate.Source != edge.Source || candidate.Kind != GraphEdgeKind.Child || candidate.Target == null)
                {
                    continue;
                }

                if (ReferenceEquals(candidate, edge))
                {
                    index = count;
                }

                count++;
            }
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
