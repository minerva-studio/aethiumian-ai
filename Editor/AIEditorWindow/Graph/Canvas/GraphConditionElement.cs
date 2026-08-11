using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
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
            title.tooltip = item.Node?.DisplayName ?? "Condition";
            title.AddToClassList("ai-editor-graph-condition-title");
            Add(title);
            bool hasPredicate = item.ConditionScope?.PredicateRoot?.Node != null;
            Condition condition = item.Node?.Node as Condition;
            bool hasMissingPredicate = !hasPredicate && condition?.condition?.UUID != UUID.Empty;
            Label check = new(hasPredicate ? "CHECK" : hasMissingPredicate ? "CHECK  ·  MISSING" : "CHECK  ·  +")
            {
                tooltip = hasPredicate
                    ? "Condition predicate"
                    : hasMissingPredicate ? "Condition predicate reference is missing." : "Connect a condition predicate.",
            };
            check.AddToClassList("ai-editor-graph-condition-check");
            EnableInClassList("ai-editor-graph-condition-check-missing", hasMissingPredicate);
            Add(check);
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

            foreach (GraphPresentationItem predicate in item.ConditionScope?.PredicateRoots ?? Array.Empty<GraphPresentationItem>())
            {
                Add(createElement(predicate, false, item.Position, null));
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

        /// <summary>Refreshes a movable Condition root after a grouped Service drag.</summary>
        internal void RefreshPosition()
        {
            if (movable && item.Node != null)
            {
                style.left = item.Node.Position.x;
                style.top = item.Node.Position.y;
            }
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

            GraphCanvasAppearance appearance = canvas.Appearance;
            Color stroke = selected ? appearance.SelectedStroke : appearance.GetFamilyStroke(GraphVisualFamily.Condition);
            painter.strokeColor = stroke;
            painter.lineWidth = selected ? appearance.SelectedLineWidth : appearance.ScopeLineWidth;
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
            painter.Stroke();
            if (item.ConditionScope?.PredicateRoot != null)
            {
                GraphPresentationItem predicate = item.ConditionScope.PredicateRoot;
                Vector2 from = new(layout.width * 0.5f, GraphPresentationMetrics.ConditionHeader);
                Vector2 to = new(
                    predicate.Position.x - item.Position.x + predicate.Size.x * 0.5f,
                    predicate.Position.y - item.Position.y);
                painter.strokeColor = stroke;
                painter.lineWidth = appearance.ScopeLineWidth;
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
}
