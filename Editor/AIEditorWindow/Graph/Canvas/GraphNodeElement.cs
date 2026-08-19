using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;
using BooleanNode = Aethiumian.AI.Nodes.Boolean;

namespace Aethiumian.AI.Editor
{
    internal sealed class GraphNodeElement : VisualElement, IGraphMarqueeSelectable
    {
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly Label title;
        private readonly Label typeLabel;
        private readonly Label warningLabel;
        private bool selected;
        private readonly bool movable;
        private readonly GraphNodeShape shape;
        private readonly bool compact;
        private readonly GraphLeafVisualDescriptor leafVisual;
        private bool dragging;
        private int pointerId = -1;
        private Vector2 dragOffset;

        /// <summary>
        /// Initializes a node card.
        /// </summary>
        internal GraphNodeElement(GraphCanvasElement canvas, GraphEditorModule module, GraphNodeDescriptor descriptor)
            : this(canvas, module, descriptor, true, descriptor?.Position ?? Vector2.zero, null, null)
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
            GraphNodeShape? shapeOverride,
            GraphLeafVisualDescriptor leafVisual = null)
        {
            this.canvas = canvas;
            this.module = module;
            Descriptor = descriptor;
            this.movable = movable;
            this.leafVisual = leafVisual;
            shape = shapeOverride ?? descriptor.Shape;
            compact = descriptor.Node is Decorator or BooleanNode or Constant;
            name = $"ai-editor-graph-node-{descriptor.UUID}";
            AddToClassList("ai-editor-graph-node");
            AddToClassList($"ai-editor-graph-node-{shape.ToString().ToLowerInvariant()}");
            EnableInClassList("ai-editor-graph-node-compact", compact);
            if (descriptor.Node is Decorator)
            {
                AddToClassList("ai-editor-graph-node-decorator");
            }
            else if (descriptor.Node is BooleanNode)
            {
                AddToClassList("ai-editor-graph-node-boolean");
            }
            else if (descriptor.Node is Constant)
            {
                AddToClassList("ai-editor-graph-node-constant");
            }
            else if (descriptor.Node is Decision)
            {
                AddToClassList("ai-editor-graph-node-decision");
            }

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
            Vector2 size = leafVisual?.Size ?? GraphLayoutResolver.GetNodeSize(descriptor);
            style.width = size.x;
            style.height = size.y;
            generateVisualContent += DrawNodeShape;
            title = new Label(leafVisual?.Title ?? GetTitle(descriptor));
            title.AddToClassList("ai-editor-graph-node-title");
            string subtitle = compact ? string.Empty : GetKindLabel(canvas, descriptor, shapeOverride);
            string nodeTooltip = compact
                ? leafVisual?.Tooltip ?? GetCompactTooltip(descriptor)
                : GetNodeTooltip(descriptor, subtitle);
            title.tooltip = nodeTooltip;
            bool isDecoratorBadge = canvas.Presentation?.IsDecoratorBadge(canvas.Presentation.Find(descriptor.UUID)) == true;
            if (isDecoratorBadge)
            {
                title.tooltip = GetCompactTooltip(descriptor);
                title.AddToClassList("ai-editor-graph-decorator-badge-title");
            }
            typeLabel = string.IsNullOrEmpty(subtitle) ? null : new Label(subtitle);
            typeLabel?.AddToClassList("ai-editor-graph-node-type");
            Add(title);
            if (isDecoratorBadge)
            {
                Label grip = new("⋮⋮") { tooltip = "Drag to reorder decorators" };
                grip.AddToClassList("ai-editor-graph-decorator-reorder-grip");
                grip.AddManipulator(new DecoratorReorderManipulator(canvas, module, this));
                Add(grip);
            }
            if (typeLabel != null)
            {
                Add(typeLabel);
            }

            if (descriptor.Node is Decision)
            {
                Add(new GraphDecisionOrderStripElement(module, descriptor));
            }

            if (!string.IsNullOrEmpty(nodeTooltip))
            {
                tooltip = nodeTooltip;
            }

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
            this.AddManipulator(new ContextualMenuManipulator(canvas.PopulateAuthoredNodeContextMenu));
        }

        /// <summary>
        /// Gets the immutable descriptor represented by this card.
        /// </summary>
        internal GraphNodeDescriptor Descriptor { get; }

        /// <summary>Gets the authored node represented by this card.</summary>
        public TreeNode AuthoredNode => Descriptor?.Node;

        /// <summary>Gets the complete card bounds used by box selection.</summary>
        public Rect MarqueeWorldBound => worldBound;

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
                Vector2 position = canvas.GetPresentationPosition(Descriptor);
                style.left = position.x;
                style.top = position.y;
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
                return parallel.mode.ToString().ToUpperInvariant();
            }

            if (descriptor.Node is Aggregate aggregate)
            {
                return aggregate.resultMode.ToString().ToUpperInvariant();
            }

            if (descriptor.Node is Loop loop)
            {
                return loop.loopType == Loop.LoopType.doWhile ? "DO WHILE" : "WHILE";
            }

            if (descriptor.Node is ForEach)
            {
                return string.Empty;
            }

            GraphProbabilityScope probabilityScope = canvas.Presentation?.Find(descriptor.UUID)?.ProbabilityScope;
            if (probabilityScope != null)
            {
                return descriptor.Node is PseudoProbability && !string.IsNullOrEmpty(probabilityScope.Subtitle)
                    ? probabilityScope.Subtitle
                    : string.Empty;
            }

            if (canvas.Presentation?.Find(descriptor.UUID)?.DecisionScope != null)
            {
                return string.Empty;
            }

            return value switch
            {
                _ => string.Empty,
            };
        }

        /// <summary>Returns the concise semantic text for compact decorator and leaf cards.</summary>
        private string GetTitle(GraphNodeDescriptor descriptor)
        {
            string semantic = descriptor.Node switch
            {
                Inverter => "NOT",
                Always always when always.returnValue.IsConstant => always.returnValue.Constant ? "ALWAYS T" : "ALWAYS F",
                Always => "ALWAYS VAR",
                Capture capture when capture.result == null || !capture.result.HasEditorReference => "CAPTURE → $MISSING",
                Capture capture => $"CAPTURE → ${module.TopologyTree?.GetVariableDescName(capture.result.UUID) ?? "MISSING"}",
                ResultChanged => "CHANGED",
                BooleanNode boolean when boolean.boolean == null || !boolean.boolean.HasEditorReference => "$MISSING",
                BooleanNode boolean => $"${module.TopologyTree?.GetVariableDescName(boolean.boolean.UUID) ?? "MISSING"}",
                Constant constant => constant.returnValue ? "TRUE" : "FALSE",
                _ => descriptor.DisplayName,
            };
            if (descriptor.Node is Decorator)
            {
                return semantic;
            }

            return descriptor.Node is BooleanNode or Constant
                && !string.Equals(descriptor.DisplayName, descriptor.NodeType.Name, StringComparison.Ordinal)
                ? descriptor.DisplayName
                : semantic;
        }

        /// <summary>Returns the full compact-node description used by the native tooltip.</summary>
        private string GetCompactTooltip(GraphNodeDescriptor descriptor)
        {
            return $"{descriptor.DisplayName}\n{GetTitle(descriptor)}";
        }

        /// <summary>Builds a full-name tooltip while preserving an optional semantic subtitle.</summary>
        private static string GetNodeTooltip(GraphNodeDescriptor descriptor, string subtitle)
        {
            if (descriptor == null || string.IsNullOrEmpty(descriptor.DisplayName))
            {
                return subtitle ?? string.Empty;
            }

            return string.IsNullOrEmpty(subtitle)
                ? descriptor.DisplayName
                : $"{descriptor.DisplayName}\n{subtitle}";
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
                case GraphNodeShape.Normal when leafVisual != null:
                    if (leafVisual.IsBoolean)
                    {
                        DrawCapsule(painter, width, height);
                    }
                    else
                    {
                        DrawLozenge(painter, width, height);
                    }
                    break;
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
                case GraphNodeShape.Branch when Descriptor.Node is Decision:
                    DrawPolygon(painter, new[]
                    {
                        new Vector2(18f, 0f),
                        new Vector2(width - 18f, 0f),
                        new Vector2(width, 18f),
                        new Vector2(width, height),
                        new Vector2(0f, height),
                        new Vector2(0f, 18f),
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

        }

        private Color GetFillColor()
        {
            GraphCanvasAppearance value = canvas.Appearance;
            if (leafVisual?.IsBoolean == true)
            {
                return EditorGUIUtility.isProSkin ? value.BooleanFillDark : value.BooleanFillLight;
            }

            if (leafVisual?.ConstantValue is bool constant)
            {
                return constant
                    ? EditorGUIUtility.isProSkin ? value.ConstantTrueFillDark : value.ConstantTrueFillLight
                    : EditorGUIUtility.isProSkin ? value.ConstantFalseFillDark : value.ConstantFalseFillLight;
            }

            Color color = value.GetFamilyFill(GraphCanvasAppearance.GetFamily(Descriptor.Node), EditorGUIUtility.isProSkin);

            if (!Descriptor.IsReachable)
            {
                color.a = 0.7f;
            }

            return color;
        }

        private Color GetStrokeColor()
        {
            GraphCanvasAppearance value = canvas.Appearance;
            if (leafVisual?.IsBoolean == true)
            {
                return value.BooleanStroke;
            }

            if (leafVisual?.ConstantValue is bool constant)
            {
                return constant ? value.ConstantTrueStroke : value.ConstantFalseStroke;
            }

            return value.GetFamilyStroke(GraphCanvasAppearance.GetFamily(Descriptor.Node));
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

        /// <summary>Draws the compact Constant value lozenge.</summary>
        private static void DrawLozenge(Painter2D painter, float width, float height)
        {
            float inset = Mathf.Min(8f, width * 0.18f);
            DrawPolygon(painter, new[]
            {
                new Vector2(inset, 0f),
                new Vector2(width - inset, 0f),
                new Vector2(width, height * 0.5f),
                new Vector2(width - inset, height),
                new Vector2(inset, height),
                new Vector2(0f, height * 0.5f),
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

        /// <summary>Reorders one decorator badge without leaking pointer events to node layout dragging.</summary>
        private sealed class DecoratorReorderManipulator : PointerManipulator
        {
            private readonly GraphCanvasElement canvas;
            private readonly GraphEditorModule module;
            private readonly GraphNodeElement card;
            private readonly List<GraphNodeElement> stackElements = new();
            private int pointerId = -1;
            private int sourceIndex = -1;
            private int destinationBoundary = -1;
            private Vector2 startPosition;
            private bool dragging;

            internal DecoratorReorderManipulator(GraphCanvasElement canvas, GraphEditorModule module, GraphNodeElement card)
            {
                this.canvas = canvas;
                this.module = module;
                this.card = card;
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp);
                target.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                target.RegisterCallback<KeyDownEvent>(OnKeyDown);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
                target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
                target.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            }

            private void OnPointerDown(PointerDownEvent evt)
            {
                if (!CanStartManipulation(evt)) return;
                GraphDecoratorStack stack = canvas.Presentation?.FindDecoratorStack(card.Descriptor.UUID);
                if (stack == null || stack.Badges.Count < 2) return;
                HashSet<UUID> members = stack.Badges.Select(item => item.TargetUUID).ToHashSet();
                stackElements.Clear();
                stackElements.AddRange(canvas.Query<GraphNodeElement>().ToList()
                    .Where(element => members.Contains(element.Descriptor.UUID))
                    .OrderBy(element => element.worldBound.center.y));
                sourceIndex = stackElements.IndexOf(card);
                if (sourceIndex < 0) return;

                pointerId = evt.pointerId;
                startPosition = evt.position;
                destinationBoundary = sourceIndex;
                dragging = true;
                target.focusable = true;
                target.Focus();
                target.CapturePointer(pointerId);
                card.AddToClassList("ai-editor-graph-decorator-badge-dragging");
                UpdateIndicator(sourceIndex);
                evt.StopPropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId) return;
                float delta = evt.position.y - startPosition.y;
                card.style.translate = new StyleTranslate(new Translate(0f, delta));
                destinationBoundary = GetInsertionBoundary(evt.position.y);
                UpdateIndicator(destinationBoundary);
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId) return;
                int boundary = destinationBoundary;
                FinishVisuals();
                target.ReleasePointer(evt.pointerId);
                int destination = boundary > sourceIndex ? boundary - 1 : boundary;
                if (boundary >= 0 && destination != sourceIndex)
                {
                    module.MoveDecoratorBadge(card.Descriptor.UUID, destination);
                }
                evt.StopPropagation();
            }

            private int GetInsertionBoundary(float panelY)
            {
                if (stackElements.Count == 0) return -1;
                float top = stackElements[0].worldBound.yMin - 12f;
                float bottom = stackElements[^1].worldBound.yMax + 12f;
                if (panelY < top || panelY > bottom) return -1;
                for (int index = 0; index < stackElements.Count; index++)
                {
                    if (panelY < stackElements[index].worldBound.center.y) return index;
                }
                return stackElements.Count;
            }

            private void UpdateIndicator(int boundary)
            {
                foreach (GraphNodeElement element in stackElements)
                {
                    element.RemoveFromClassList("ai-editor-graph-decorator-insert-before");
                    element.RemoveFromClassList("ai-editor-graph-decorator-insert-after");
                }
                if (boundary < 0 || stackElements.Count == 0) return;
                if (boundary == stackElements.Count)
                    stackElements[^1].AddToClassList("ai-editor-graph-decorator-insert-after");
                else
                    stackElements[boundary].AddToClassList("ai-editor-graph-decorator-insert-before");
            }

            private void OnPointerCancel(PointerCancelEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId) return;
                Cancel();
                evt.StopPropagation();
            }

            private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (dragging && evt.pointerId == pointerId) Cancel();
            }

            private void OnKeyDown(KeyDownEvent evt)
            {
                if (!dragging || evt.keyCode != KeyCode.Escape) return;
                Cancel();
                evt.StopPropagation();
            }

            private void Cancel()
            {
                int captured = pointerId;
                FinishVisuals();
                if (captured >= 0 && target.HasPointerCapture(captured)) target.ReleasePointer(captured);
            }

            private void FinishVisuals()
            {
                dragging = false;
                pointerId = -1;
                card.style.translate = new StyleTranslate(new Translate(0f, 0f));
                card.RemoveFromClassList("ai-editor-graph-decorator-badge-dragging");
                UpdateIndicator(-1);
            }
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            module.SelectNode(Descriptor.Node, evt.actionKey, evt.shiftKey);
            canvas.Focus();
            if (!module.IsNodeSelected(Descriptor.Node))
            {
                evt.StopPropagation();
                return;
            }
            if (!movable)
            {
                evt.StopPropagation();
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            dragOffset = (canvasPoint - canvas.Pan) / canvas.Zoom - canvas.GetPresentationPosition(Descriptor);
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
            RefreshPosition();
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
