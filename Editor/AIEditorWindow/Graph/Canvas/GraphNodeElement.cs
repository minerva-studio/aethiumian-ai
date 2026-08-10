using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
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
            typeLabel = new Label(GetKindLabel(canvas, descriptor, shapeOverride));
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

        /// <summary>Refreshes a movable root card after a grouped Service drag.</summary>
        internal void RefreshPosition()
        {
            if (movable)
            {
                style.left = Descriptor.Position.x;
                style.top = Descriptor.Position.y;
            }
        }

        private static string GetKindLabel(
            GraphCanvasElement canvas,
            GraphNodeDescriptor descriptor,
            GraphNodeShape? shapeOverride)
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

            if (descriptor.Node is ForEach)
            {
                return "FLOW  ·  FOREACH  ·  NEXT ITEM";
            }

            GraphProbabilityScope probabilityScope = canvas.Presentation?.Find(descriptor.UUID)?.ProbabilityScope;
            if (probabilityScope != null)
            {
                return $"BRANCH  ·  {descriptor.NodeType.Name.ToUpperInvariant()}  ·  {probabilityScope.Subtitle}";
            }

            if (canvas.Presentation?.Find(descriptor.UUID)?.DecisionScope != null)
            {
                return "BRANCH  ·  DECISION  ·  FIRST SUCCESS · LEFT TO RIGHT";
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
                ? canvas.Appearance.SelectedStroke
                : Descriptor.HasWarning
                    ? canvas.Appearance.WarningStroke
                    : GetStrokeColor();
            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.lineWidth = selected || Descriptor.IsHead
                ? canvas.Appearance.SelectedLineWidth
                : canvas.Appearance.NodeLineWidth;

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
            GraphCanvasAppearance value = canvas.Appearance;
            Color color;
            if (EditorGUIUtility.isProSkin)
            {
                color = shape switch
                {
                    GraphNodeShape.Flow => value.FlowFillDark,
                    GraphNodeShape.Branch => value.BranchFillDark,
                    GraphNodeShape.Service => value.ServiceFillDark,
                    _ => value.NormalFillDark,
                };
            }
            else
            {
                color = shape switch
                {
                    GraphNodeShape.Flow => value.FlowFillLight,
                    GraphNodeShape.Branch => value.BranchFillLight,
                    GraphNodeShape.Service => value.ServiceFillLight,
                    _ => value.NormalFillLight,
                };
            }

            if (!Descriptor.IsReachable)
            {
                color.a = 0.7f;
            }

            return color;
        }

        private Color GetStrokeColor()
        {
            GraphCanvasAppearance value = canvas.Appearance;
            return shape switch
            {
                GraphNodeShape.Flow => value.FlowStroke,
                GraphNodeShape.Branch => value.BranchStroke,
                GraphNodeShape.Service => value.ServiceStroke,
                _ => EditorGUIUtility.isProSkin
                    ? value.NormalStrokeDark
                    : value.NormalStrokeLight,
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
}
