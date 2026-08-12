using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
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
    /// Displays a derived Loop count-check control point.
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

    /// <summary>Displays a non-runnable Parallel branch without creating a TreeNode.</summary>
    internal sealed class GraphParallelPlaceholderElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        internal GraphParallelPlaceholderElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphParallelPlaceholder placeholder = item.ParallelPlaceholder
                ?? throw new ArgumentException("A Parallel placeholder descriptor is required.", nameof(item));
            name = $"ai-editor-graph-parallel-placeholder-{placeholder.Index}";
            tooltip = placeholder.Tooltip;
            pickingMode = PickingMode.Ignore;
            AddToClassList("ai-editor-graph-parallel-placeholder");
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;
            AddLabel(placeholder.Title, "ai-editor-graph-parallel-placeholder-title");
            AddLabel(placeholder.Subtitle, "ai-editor-graph-parallel-placeholder-subtitle");
        }

        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }

        private void AddLabel(string text, string className)
        {
            Label label = new(text);
            label.AddToClassList(className);
            label.pickingMode = PickingMode.Ignore;
            Add(label);
        }
    }

    /// <summary>Displays one explicit ForEach diagnostic without creating a TreeNode.</summary>
    internal sealed class GraphForEachPlaceholderElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        internal GraphForEachPlaceholderElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphForEachPlaceholder placeholder = item.ForEachPlaceholder
                ?? throw new ArgumentException("A ForEach placeholder descriptor is required.", nameof(item));
            name = $"ai-editor-graph-foreach-placeholder-{placeholder.Kind.ToString().ToLowerInvariant()}";
            tooltip = placeholder.Tooltip;
            pickingMode = PickingMode.Ignore;
            AddToClassList("ai-editor-graph-foreach-placeholder");
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;
            AddLabel(placeholder.Title, "ai-editor-graph-foreach-placeholder-title");
            AddLabel(placeholder.Subtitle, "ai-editor-graph-foreach-placeholder-subtitle");
        }

        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }

        private void AddLabel(string text, string className)
        {
            Label label = new(text);
            label.AddToClassList(className);
            label.pickingMode = PickingMode.Ignore;
            Add(label);
        }
    }

    /// <summary>Displays the derived enumerable gate of a ForEach scope.</summary>
    internal sealed class GraphForEachJunctionElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        internal GraphForEachJunctionElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphForEachJunction junction = item.ForEachJunction
                ?? throw new ArgumentException("A ForEach junction descriptor is required.", nameof(item));
            name = "ai-editor-graph-foreach-enumerable-check";
            pickingMode = PickingMode.Ignore;
            AddToClassList("ai-editor-graph-foreach-junction");
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;
            AddLabel(junction.Title, "ai-editor-graph-foreach-junction-title");
            AddLabel(junction.Subtitle, "ai-editor-graph-foreach-junction-subtitle");
        }

        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }

        private void AddLabel(string text, string className)
        {
            Label label = new(text);
            label.AddToClassList(className);
            label.pickingMode = PickingMode.Ignore;
            Add(label);
        }
    }

    /// <summary>Displays a non-persistent Probability empty, missing, or no-options terminal.</summary>
    internal sealed class GraphProbabilityPlaceholderElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        /// <summary>Initializes one non-interactive Probability placeholder.</summary>
        internal GraphProbabilityPlaceholderElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphProbabilityPlaceholder placeholder = item.ProbabilityPlaceholder
                ?? throw new ArgumentException("A Probability placeholder item is required.", nameof(item));
            name = $"ai-editor-graph-probability-placeholder-{placeholder.Index}";
            tooltip = placeholder.Tooltip;
            pickingMode = PickingMode.Position;
            AddToClassList("ai-editor-graph-probability-placeholder");
            EnableInClassList("ai-editor-graph-probability-placeholder-invalid", placeholder.IsInvalidSelection);
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            Label title = new(placeholder.Title);
            title.AddToClassList("ai-editor-graph-probability-placeholder-title");
            title.pickingMode = PickingMode.Ignore;
            Add(title);

            Label subtitle = new(placeholder.Subtitle);
            subtitle.AddToClassList("ai-editor-graph-probability-placeholder-subtitle");
            subtitle.pickingMode = PickingMode.Ignore;
            Add(subtitle);
        }

        /// <summary>Refreshes the derived placeholder position after scope geometry changes.</summary>
        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }
    }

    /// <summary>Displays a normal empty Decision result or an invalid Error occurrence.</summary>
    internal sealed class GraphDecisionPlaceholderElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        /// <summary>Initializes one non-persistent Decision placeholder.</summary>
        internal GraphDecisionPlaceholderElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphDecisionPlaceholder placeholder = item.DecisionPlaceholder
                ?? throw new ArgumentException("The presentation item has no Decision placeholder.", nameof(item));
            name = $"ai-editor-graph-decision-placeholder-{placeholder.Index}";
            tooltip = placeholder.Tooltip;
            pickingMode = PickingMode.Position;
            AddToClassList("ai-editor-graph-decision-placeholder");
            EnableInClassList("ai-editor-graph-decision-placeholder-error", placeholder.IsError);
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            Label title = new(placeholder.Title);
            title.AddToClassList("ai-editor-graph-decision-placeholder-title");
            title.pickingMode = PickingMode.Ignore;
            Add(title);

            Label subtitle = new(placeholder.Subtitle);
            subtitle.AddToClassList("ai-editor-graph-decision-placeholder-subtitle");
            subtitle.pickingMode = PickingMode.Ignore;
            Add(subtitle);
        }

        /// <summary>Refreshes the derived placeholder position after scope geometry changes.</summary>
        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }
    }

    /// <summary>Displays one draggable editor-only tree boundary without creating a runtime node.</summary>
    internal sealed class GraphBoundaryElement : VisualElement
    {
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphPresentationItem item;
        private bool dragging;
        private bool moved;
        private bool selected;
        private int pointerId = -1;
        private Vector2 dragOffset;

        internal GraphBoundaryElement(
            GraphCanvasElement canvas,
            GraphEditorModule module,
            GraphPresentationItem item,
            Vector2 position)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            if (item.Kind is not (GraphPresentationKind.Entrance or GraphPresentationKind.Exit))
            {
                throw new ArgumentException("A graph boundary presentation item is required.", nameof(item));
            }

            bool entrance = item.Kind == GraphPresentationKind.Entrance;
            name = entrance ? "ai-editor-graph-entrance" : "ai-editor-graph-exit";
            AddToClassList("ai-editor-graph-boundary");
            AddToClassList(entrance ? "ai-editor-graph-boundary-entrance" : "ai-editor-graph-boundary-exit");
            bool connected = canvas.Presentation.Relations.Any(relation =>
                relation.Kind == GraphPresentationRelationKind.Entrance);
            EnableInClassList("ai-editor-graph-boundary-empty", entrance && !connected);
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;
            generateVisualContent += DrawBoundary;

            Label title = new(entrance ? "ENTRANCE" : "EXIT");
            title.AddToClassList("ai-editor-graph-boundary-title");
            title.pickingMode = PickingMode.Ignore;
            Add(title);
            Label subtitle = new(entrance && !connected ? "NO HEAD" : entrance ? "TREE HEAD" : "TREE COMPLETE");
            subtitle.AddToClassList("ai-editor-graph-boundary-subtitle");
            subtitle.pickingMode = PickingMode.Ignore;
            Add(subtitle);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel);
        }

        /// <summary>Gets the represented presentation-only boundary kind.</summary>
        internal GraphPresentationKind Kind => item.Kind;

        /// <summary>Updates the boundary's canvas-only selected state.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            EnableInClassList("ai-editor-graph-boundary-selected", value);
            MarkDirtyRepaint();
        }

        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }

        private void DrawBoundary(MeshGenerationContext context)
        {
            if (context.painter2D == null || contentRect.width < 1f || contentRect.height < 1f)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            bool entrance = item.Kind == GraphPresentationKind.Entrance;
            Color boundaryStroke = entrance ? canvas.Appearance.EntranceBoundary : canvas.Appearance.ExitBoundary;
            Color fill = boundaryStroke;
            fill.a = EditorGUIUtility.isProSkin ? 0.22f : 0.14f;

            painter.fillColor = fill;
            painter.strokeColor = selected ? canvas.Appearance.SelectedStroke : boundaryStroke;
            painter.lineWidth = selected ? canvas.Appearance.SelectedLineWidth : canvas.Appearance.NodeLineWidth;
            float width = contentRect.width;
            float height = contentRect.height;
            if (entrance)
            {
                DrawEntranceBadge(painter, width, height);
                return;
            }

            DrawExitDoubleRing(painter, width, height);
        }

        /// <summary>Draws the Entrance badge with its centered downward connection tip.</summary>
        private static void DrawEntranceBadge(Painter2D painter, float width, float height)
        {
            float corner = Mathf.Min(10f, height * 0.25f);
            float tipHeight = Mathf.Min(10f, height * 0.28f);
            float tipHalfWidth = Mathf.Min(14f, width * 0.14f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(corner, 0f));
            painter.LineTo(new Vector2(width - corner, 0f));
            painter.LineTo(new Vector2(width, corner));
            painter.LineTo(new Vector2(width, height - tipHeight));
            painter.LineTo(new Vector2(width * 0.5f + tipHalfWidth, height - tipHeight));
            painter.LineTo(new Vector2(width * 0.5f, height));
            painter.LineTo(new Vector2(width * 0.5f - tipHalfWidth, height - tipHeight));
            painter.LineTo(new Vector2(0f, height - tipHeight));
            painter.LineTo(new Vector2(0f, corner));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        /// <summary>Draws the Exit terminal as two concentric capsule rings.</summary>
        private static void DrawExitDoubleRing(Painter2D painter, float width, float height)
        {
            painter.BeginPath();
            TraceCapsule(painter, 0f, 0f, width, height);
            painter.Fill();
            painter.Stroke();

            float inset = Mathf.Min(5f, height * 0.18f);
            painter.BeginPath();
            TraceCapsule(painter, inset, inset, width - inset * 2f, height - inset * 2f);
            painter.Stroke();
        }

        /// <summary>Traces a capsule suitable for the Exit terminal's outer and inner rings.</summary>
        private static void TraceCapsule(Painter2D painter, float x, float y, float width, float height)
        {
            float radius = height * 0.5f;
            painter.MoveTo(new Vector2(x + radius, y));
            painter.LineTo(new Vector2(x + width - radius, y));
            painter.Arc(new Vector2(x + width - radius, y + radius), radius, Angle.Degrees(270f), Angle.Degrees(90f), ArcDirection.Clockwise);
            painter.LineTo(new Vector2(x + radius, y + height));
            painter.Arc(new Vector2(x + radius, y + radius), radius, Angle.Degrees(90f), Angle.Degrees(270f), ArcDirection.Clockwise);
            painter.ClosePath();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            canvas.Focus();
            canvas.SelectBoundary(item);
            if (item.Kind == GraphPresentationKind.Entrance)
            {
                module.SelectEntrance();
            }

            if (item.Kind == GraphPresentationKind.Entrance
                && canvas.Presentation.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.Entrance))
            {
                evt.StopPropagation();
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            dragOffset = (canvasPoint - canvas.Pan) / canvas.Zoom - item.Position;
            dragging = true;
            moved = false;
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
            moved |= (item.Position - position).sqrMagnitude > 0.01f;
            module.MoveBoundary(item, position);
            RefreshPosition();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId)
            {
                return;
            }

            FinishDrag(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == pointerId)
            {
                FinishDrag(evt.pointerId);
            }
        }

        private void FinishDrag(int releasedPointerId)
        {
            dragging = false;
            if (this.HasPointerCapture(releasedPointerId))
            {
                this.ReleasePointer(releasedPointerId);
            }

            pointerId = -1;
            if (moved)
            {
                module.CommitBoundaryMove();
            }
            moved = false;
        }
    }

    /// <summary>
    /// Displays the completion marker shared by composite Flow presentations.
    /// </summary>
}
