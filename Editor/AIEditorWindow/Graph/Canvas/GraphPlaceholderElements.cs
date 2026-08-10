using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Displays the completion marker shared by composite Flow presentations.
    /// </summary>
}
