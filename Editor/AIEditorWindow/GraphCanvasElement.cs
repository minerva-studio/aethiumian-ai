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
            edgeLayer.SetTopology(topology);
            nodeLayer.Clear();

            if (topology == null)
            {
                return;
            }

            foreach (GraphNodeDescriptor descriptor in topology.Nodes)
            {
                GraphNodeElement nodeElement = new(this, module, descriptor);
                nodeElement.style.left = descriptor.Position.x;
                nodeElement.style.top = descriptor.Position.y;
                nodeLayer.Add(nodeElement);
            }

            UpdateContentBounds(topology);
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Refreshes card selection without rebuilding the topology.
        /// </summary>
        /// <param name="selectedNode">The selected node instance.</param>
        internal void SetSelectedNode(TreeNode selectedNode)
        {
            foreach (GraphNodeElement node in nodeLayer.Children())
            {
                node.SetSelected(node.Descriptor.Node == selectedNode);
            }
        }

        /// <summary>
        /// Fits all nodes into the current viewport.
        /// </summary>
        internal void FitAll()
        {
            GraphTopology topology = module.Topology;
            if (topology == null || topology.Nodes.Count == 0 || layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            Rect bounds = CalculateBounds(topology);
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
            GraphNodeDescriptor selected = module.Topology?.FindNode(module.SelectedNode?.uuid ?? UUID.Empty);
            if (selected == null || layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            zoom = Mathf.Clamp(Mathf.Max(zoom, 0.75f), 0.25f, 2.5f);
            pan = new Vector2(layout.width * 0.5f, layout.height * 0.5f)
                - (selected.Position + GraphLayoutResolver.GetNodeSize(selected) * 0.5f) * zoom;
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
                if (element is GraphNodeElement)
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

        private void UpdateContentBounds(GraphTopology topology)
        {
            Rect bounds = CalculateBounds(topology);
            float width = Mathf.Max(2000f, bounds.xMax + 1000f);
            float height = Mathf.Max(1200f, bounds.yMax + 1000f);
            content.style.width = width;
            content.style.height = height;
            edgeLayer.style.width = width;
            edgeLayer.style.height = height;
            nodeLayer.style.width = width;
            nodeLayer.style.height = height;
        }

        private static Rect CalculateBounds(GraphTopology topology)
        {
            if (topology == null || topology.Nodes.Count == 0)
            {
                return new Rect(0f, 0f, 220f, 82f);
            }

            Vector2 min = topology.Nodes[0].Position;
            Vector2 max = min + GraphLayoutResolver.GetNodeSize(topology.Nodes[0]);
            for (int i = 1; i < topology.Nodes.Count; i++)
            {
                Vector2 position = topology.Nodes[i].Position;
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position + GraphLayoutResolver.GetNodeSize(topology.Nodes[i]));
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
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
        private bool dragging;
        private int pointerId = -1;
        private Vector2 dragOffset;

        /// <summary>
        /// Initializes a node card.
        /// </summary>
        internal GraphNodeElement(GraphCanvasElement canvas, GraphEditorModule module, GraphNodeDescriptor descriptor)
        {
            this.canvas = canvas;
            this.module = module;
            Descriptor = descriptor;
            name = $"ai-editor-graph-node-{descriptor.UUID}";
            AddToClassList("ai-editor-graph-node");
            AddToClassList($"ai-editor-graph-node-{descriptor.Shape.ToString().ToLowerInvariant()}");
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
            Vector2 size = GraphLayoutResolver.GetNodeSize(descriptor);
            style.width = size.x;
            style.height = size.y;
            generateVisualContent += DrawNodeShape;
            title = new Label(descriptor.DisplayName);
            title.AddToClassList("ai-editor-graph-node-title");
            typeLabel = new Label(GetKindLabel(descriptor));
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

        private static string GetKindLabel(GraphNodeDescriptor descriptor)
        {
            return descriptor.Shape switch
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

            switch (Descriptor.Shape)
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

            DrawPort(painter, Descriptor.Shape == GraphNodeShape.Service
                ? new Vector2(0f, height * 0.5f)
                : new Vector2(width * 0.5f, 0f), stroke);
            if (Descriptor.Shape == GraphNodeShape.Service)
            {
                DrawPort(painter, new Vector2(width, height * 0.5f), stroke);
            }
            else
            {
                int structuralOutputCount = GetStructuralOutputCount();
                if (Descriptor.Shape is GraphNodeShape.Flow or GraphNodeShape.Branch && structuralOutputCount > 0)
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
                return Descriptor.Shape switch
                {
                    GraphNodeShape.Flow => new Color(0.12f, 0.24f, 0.31f, alpha),
                    GraphNodeShape.Branch => new Color(0.25f, 0.18f, 0.31f, alpha),
                    GraphNodeShape.Service => new Color(0.30f, 0.23f, 0.10f, alpha),
                    _ => new Color(0.16f, 0.17f, 0.19f, alpha),
                };
            }

            return Descriptor.Shape switch
            {
                GraphNodeShape.Flow => new Color(0.72f, 0.86f, 0.91f, alpha),
                GraphNodeShape.Branch => new Color(0.85f, 0.78f, 0.91f, alpha),
                GraphNodeShape.Service => new Color(0.93f, 0.86f, 0.68f, alpha),
                _ => new Color(0.82f, 0.83f, 0.85f, alpha),
            };
        }

        private Color GetStrokeColor()
        {
            return Descriptor.Shape switch
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

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            dragOffset = (canvasPoint - canvas.Pan) / canvas.Zoom - Descriptor.Position;
            dragging = true;
            pointerId = evt.pointerId;
            this.CapturePointer(pointerId);
            module.SelectNode(Descriptor.Node);
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
    /// Draws graph edges with native UI Toolkit Painter2D content.
    /// </summary>
    internal sealed class GraphEdgeLayerElement : VisualElement
    {
        private GraphTopology topology;
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
            this.topology = topology;
            Clear();
            labeledEdges.Clear();
            edgeLabels.Clear();
            if (topology != null)
            {
                foreach (GraphEdgeDescriptor edge in topology.Edges)
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
            foreach (GraphEdgeDescriptor edge in topology.Edges)
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
            foreach (GraphEdgeDescriptor candidate in topology.Edges)
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
            Vector2 sourceSize = GraphLayoutResolver.GetNodeSize(edge.Source);
            Vector2 targetSize = GraphLayoutResolver.GetNodeSize(edge.Target);
            if (edge.Kind == GraphEdgeKind.Service)
            {
                from = edge.Source.Position + new Vector2(sourceSize.x, sourceSize.y * 0.5f + offset);
                to = edge.Target.Position + new Vector2(0f, targetSize.y * 0.5f + offset);
                return;
            }

            float sourceX = sourceSize.x * 0.5f;
            if (edge.Kind == GraphEdgeKind.Child
                && edge.Source.Shape is GraphNodeShape.Flow or GraphNodeShape.Branch)
            {
                GetStructuralOutputSlot(edge, out int index, out int count);
                sourceX = sourceSize.x * (index + 1f) / (count + 1f);
            }

            from = edge.Source.Position + new Vector2(sourceX + offset, sourceSize.y);
            to = edge.Target.Position + new Vector2(targetSize.x * 0.5f + offset, 0f);
        }

        private void GetStructuralOutputSlot(GraphEdgeDescriptor edge, out int index, out int count)
        {
            index = 0;
            count = 0;
            if (topology == null)
            {
                return;
            }

            foreach (GraphEdgeDescriptor candidate in topology.Edges)
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
