using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
    /// <summary>Draws the Condition fill beneath all global edge and node layers.</summary>
    internal sealed class GraphConditionBackdropElement : VisualElement
    {
        private readonly GraphConditionScope scope;
        private readonly GraphCanvasAppearance appearance;

        internal GraphConditionBackdropElement(GraphConditionScope scope, GraphCanvasAppearance appearance)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Owner.Position.x;
            style.top = scope.Owner.Position.y;
            style.width = scope.Owner.Size.x;
            style.height = scope.Owner.Size.y;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            painter.fillColor = appearance.GetFamilyFill(GraphVisualFamily.Condition, EditorGUIUtility.isProSkin);
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
        }
    }

    internal sealed class GraphServiceScopeElement : VisualElement
    {
        /// <summary>Initializes one derived Service scope frame.</summary>
        internal GraphServiceScopeElement(GraphEditorModule module, GraphServiceScope scope)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            name = $"ai-editor-graph-service-scope-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-service-scope");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Bounds.x;
            style.top = scope.Bounds.y;
            style.width = Mathf.Max(1f, scope.Bounds.width);
            style.height = Mathf.Max(1f, scope.Bounds.height);
            style.display = DisplayStyle.None;

            string shared = scope.AdditionalHostCount > 0 ? $"  ·  SHARED +{scope.AdditionalHostCount}" : string.Empty;
            VisualElement header = new();
            header.AddToClassList("ai-editor-graph-service-scope-header");
            header.pickingMode = PickingMode.Position;
            Label label = new($"SERVICE  ·  {scope.Owner.Node?.DisplayName}{shared}");
            label.AddToClassList("ai-editor-graph-service-scope-title");
            label.pickingMode = PickingMode.Ignore;
            Button follow = new(() => module.ToggleServiceFollowParent(scope.Owner.TargetUUID))
            {
                text = module.GetServiceFollowParent(scope.Owner.TargetUUID) ? "●" : "○",
                tooltip = "Follow the first-placement host when it moves.",
            };
            follow.AddToClassList("ai-editor-graph-service-follow");
            header.Add(label);
            header.Add(follow);
            header.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 && scope.Owner.Node != null)
                {
                    module.SelectNode(scope.Owner.Node.Node);
                    evt.StopPropagation();
                }
            });
            Add(header);
        }

        /// <summary>Gets the derived scope represented by this frame.</summary>
        internal GraphServiceScope Scope { get; }

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("ai-editor-graph-service-scope-selected", value);
        }
    }

    /// <summary>
    /// Displays an unresolved authored Service slot without creating a TreeNode.
    /// </summary>
    internal sealed class GraphServicePlaceholderElement : VisualElement
    {
        private readonly GraphPresentationItem item;

        /// <summary>Initializes one missing Service placeholder.</summary>
        internal GraphServicePlaceholderElement(GraphPresentationItem item, Vector2 position)
        {
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            GraphServicePlaceholder placeholder = item.ServicePlaceholder
                ?? throw new ArgumentException("A Service placeholder descriptor is required.", nameof(item));
            name = $"ai-editor-graph-service-placeholder-{placeholder.Label}";
            AddToClassList("ai-editor-graph-service-placeholder");
            pickingMode = PickingMode.Ignore;
            tooltip = placeholder.Tooltip;
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            Label title = new(placeholder.Title);
            title.AddToClassList("ai-editor-graph-service-placeholder-title");
            Add(title);
        }

        /// <summary>Repositions this derived placeholder from presentation geometry.</summary>
        internal void RefreshPosition()
        {
            style.left = item.Position.x;
            style.top = item.Position.y;
        }
    }

    /// <summary>
    /// Draws a derived free-Sequence scope rail.
    /// </summary>
    internal sealed class GraphSequenceScopeElement : VisualElement
    {
        private readonly GraphCanvasAppearance appearance;
        private bool selected;

        /// <summary>
        /// Initializes one non-interactive Sequence scope overlay.
        /// </summary>
        /// <param name="scope">The derived scope to display.</param>
        internal GraphSequenceScopeElement(GraphSequenceScope scope, GraphCanvasAppearance appearance)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
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

        /// <summary>Gets the canvas-owned appearance used by this painter.</summary>
        internal GraphCanvasAppearance Appearance => appearance;

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

            Color color = selected ? appearance.SequenceScopeSelected : appearance.SequenceScope;
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
        private readonly GraphCanvasAppearance appearance;
        private bool selected;

        /// <summary>Initializes one derived Condition scope bracket.</summary>
        internal GraphConditionScopeElement(GraphConditionScope scope, GraphCanvasAppearance appearance)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            name = $"ai-editor-graph-condition-scope-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-condition-scope");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Bounds.x;
            style.top = scope.Bounds.y;
            style.width = Mathf.Max(1f, scope.Bounds.width);
            style.height = Mathf.Max(1f, scope.Bounds.height);
            style.display = DisplayStyle.None;
            generateVisualContent += DrawBracket;
        }

        /// <summary>Gets the derived scope represented by this overlay.</summary>
        internal GraphConditionScope Scope { get; }

        /// <summary>Gets the canvas-owned appearance used by this painter.</summary>
        internal GraphCanvasAppearance Appearance => appearance;

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
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

            Color color = selected ? appearance.ConditionScopeSelected : appearance.ConditionScope;
            float left = Scope.LeftX - Scope.Bounds.x;
            float right = Scope.RightX - Scope.Bounds.x;
            float top = Scope.BracketTopY - Scope.Bounds.y;
            float bottom = Scope.BracketBottomY - Scope.Bounds.y;
            const float tick = 12f;
            painter.strokeColor = color;
            painter.lineWidth = selected ? appearance.SelectedScopeLineWidth : appearance.ScopeLineWidth;
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
    /// Draws the non-interactive fan boundary that groups freely arranged Probability candidates.
    /// </summary>
    internal sealed class GraphProbabilityScopeElement : VisualElement
    {
        private readonly GraphCanvasAppearance appearance;
        private bool selected;

        /// <summary>Initializes one derived Probability candidate fan.</summary>
        internal GraphProbabilityScopeElement(GraphProbabilityScope scope, GraphCanvasAppearance appearance)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            name = $"ai-editor-graph-probability-scope-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-probability-scope");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Bounds.x;
            style.top = scope.Bounds.y;
            style.width = Mathf.Max(1f, scope.Bounds.width);
            style.height = Mathf.Max(1f, scope.Bounds.height);
            style.display = DisplayStyle.None;
            generateVisualContent += DrawFan;
        }

        /// <summary>Gets the derived scope represented by this overlay.</summary>
        internal GraphProbabilityScope Scope { get; }

        /// <summary>Gets the canvas-owned appearance used by this painter.</summary>
        internal GraphCanvasAppearance Appearance => appearance;

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("ai-editor-graph-probability-scope-selected", value);
            MarkDirtyRepaint();
        }

        /// <summary>Draws a low-emphasis bracket without editable arrowheads.</summary>
        private void DrawFan(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            Color color = selected ? appearance.ProbabilityScopeSelected : appearance.ProbabilityScope;
            float left = Scope.LeftX - Scope.Bounds.x;
            float right = Scope.RightX - Scope.Bounds.x;
            float top = Scope.FanTopY - Scope.Bounds.y;
            float bottom = Scope.FanBottomY - Scope.Bounds.y;
            const float tick = 12f;
            painter.strokeColor = color;
            painter.lineWidth = selected ? appearance.SelectedScopeLineWidth : appearance.ScopeLineWidth;
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
    /// Draws the non-interactive frame that identifies one Loop Body.
    /// </summary>
    internal sealed class GraphLoopScopeElement : VisualElement
    {
        /// <summary>Initializes one derived Loop Body frame.</summary>
        internal GraphLoopScopeElement(GraphLoopScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            name = $"ai-editor-graph-loop-body-frame-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-loop-body-frame");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.BodyFrameBounds.x;
            style.top = scope.BodyFrameBounds.y;
            style.width = Mathf.Max(1f, scope.BodyFrameBounds.width);
            style.height = Mathf.Max(1f, scope.BodyFrameBounds.height);
            style.display = DisplayStyle.None;

            Label label = new("BODY");
            label.AddToClassList("ai-editor-graph-loop-body-frame-label");
            label.pickingMode = PickingMode.Ignore;
            Add(label);
        }

        /// <summary>Gets the derived scope represented by this overlay.</summary>
        internal GraphLoopScope Scope { get; }

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("ai-editor-graph-loop-body-frame-selected", value);
        }
    }

    /// <summary>Draws the selected-only fork and synchronization guide for a Parallel Flow.</summary>
    internal sealed class GraphParallelScopeElement : VisualElement
    {
        internal GraphParallelScopeElement(GraphParallelScope scope, GraphCanvasAppearance appearance)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            name = $"ai-editor-graph-parallel-scope-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-parallel-scope");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.Bounds.x;
            style.top = scope.Bounds.y;
            style.width = Mathf.Max(1f, scope.Bounds.width);
            style.height = Mathf.Max(1f, scope.Bounds.height);
            style.display = DisplayStyle.None;
            joinLabel = new Label();
            joinLabel.name = "parallel-join-label";
            joinLabel.pickingMode = PickingMode.Ignore;
            joinLabel.AddToClassList("ai-editor-graph-parallel-join-label");
            Add(joinLabel);
            generateVisualContent += DrawScope;
        }

        private readonly GraphCanvasAppearance appearance;
        private readonly Label joinLabel;
        internal GraphParallelScope Scope { get; }

        internal void SetSelected(bool value)
        {
            style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("ai-editor-graph-parallel-scope-selected", value);
        }

        private void DrawScope(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            Rect bounds = Scope.Bounds;
            float forkY = Scope.ForkY - bounds.yMin;
            float joinY = Scope.JoinY - bounds.yMin;
            float centerX = Scope.Owner.Position.x + Scope.Owner.Size.x * 0.5f - bounds.xMin;
            Color color = appearance.ParallelEdge;
            painter.strokeColor = color;
            painter.lineWidth = appearance.ScopeLineWidth;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, forkY));
            painter.LineTo(new Vector2(layout.width, forkY));
            painter.MoveTo(new Vector2(0f, joinY));
            painter.LineTo(new Vector2(layout.width, joinY));
            painter.Stroke();

            joinLabel.text = $"{Scope.JoinTitle} · {Scope.JoinSubtitle}";
            joinLabel.style.left = Mathf.Clamp(centerX - 72f, 0f, Mathf.Max(0f, layout.width - 144f));
            joinLabel.style.top = joinY - 16f;
        }
    }

    /// <summary>Draws the selected-only Body frame for a ForEach Flow.</summary>
    internal sealed class GraphForEachScopeElement : VisualElement
    {
        internal GraphForEachScopeElement(GraphForEachScope scope)
        {
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            name = $"ai-editor-graph-foreach-body-frame-{scope.Owner.TargetUUID}";
            AddToClassList("ai-editor-graph-foreach-body-frame");
            pickingMode = PickingMode.Ignore;
            style.position = UIPosition.Absolute;
            style.left = scope.BodyFrameBounds.x;
            style.top = scope.BodyFrameBounds.y;
            style.width = Mathf.Max(1f, scope.BodyFrameBounds.width);
            style.height = Mathf.Max(1f, scope.BodyFrameBounds.height);
            style.display = DisplayStyle.None;

            Label label = new("BODY");
            label.AddToClassList("ai-editor-graph-loop-body-frame-label");
            label.pickingMode = PickingMode.Ignore;
            Add(label);
        }

        internal GraphForEachScope Scope { get; }

        internal void SetSelected(bool value)
        {
            style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("ai-editor-graph-foreach-body-frame-selected", value);
        }
    }

    /// <summary>
    /// Displays an empty or unresolved Condition branch without creating an editable TreeNode.
    /// </summary>
}
