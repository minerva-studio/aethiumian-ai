using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Describes one real node card while an authored connection is being dragged.</summary>
    internal sealed class GraphConnectionTarget
    {
        internal GraphConnectionTarget(GraphPresentationItem item, bool compatible)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Compatible = compatible;
        }

        internal GraphPresentationItem Item { get; }
        internal bool Compatible { get; }
        internal Rect Bounds => new(Item.Position, Item.Size);
        internal Vector2 Anchor => GraphPortLayerElement.GetTargetPosition(Item);
    }

    /// <summary>Draws transient connection feedback without owning topology or mutation state.</summary>
    internal sealed class GraphConnectionPreviewElement : VisualElement
    {
        private static readonly Color PreviewColor = new(0.2f, 0.75f, 1f, 0.95f);
        private static readonly Color ValidColor = new(0.32f, 0.85f, 0.55f, 0.9f);
        private static readonly Color InvalidColor = new(0.95f, 0.35f, 0.4f, 0.35f);
        private IReadOnlyList<GraphConnectionTarget> targets = Array.Empty<GraphConnectionTarget>();
        private Vector2 source;
        private Vector2 pointer;
        private GraphConnectionTarget hovered;
        private bool visible;

        internal GraphConnectionPreviewElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawPreview;
        }

        internal bool IsVisible => visible;
        internal GraphConnectionTarget HoveredTarget => hovered;

        /// <summary>Starts transient feedback for a derived set of real node targets.</summary>
        internal void Show(Vector2 sourcePosition, IReadOnlyList<GraphConnectionTarget> valueTargets)
        {
            source = sourcePosition;
            pointer = sourcePosition;
            targets = valueTargets ?? Array.Empty<GraphConnectionTarget>();
            hovered = null;
            visible = true;
            MarkDirtyRepaint();
        }

        /// <summary>Updates the pointer and resolves the deepest compatible or incompatible card below it.</summary>
        internal GraphConnectionTarget UpdatePointer(Vector2 graphPosition)
        {
            pointer = graphPosition;
            hovered = FindTarget(targets, graphPosition);
            MarkDirtyRepaint();
            return hovered;
        }

        /// <summary>Clears all transient connection feedback.</summary>
        internal void Hide()
        {
            visible = false;
            hovered = null;
            targets = Array.Empty<GraphConnectionTarget>();
            MarkDirtyRepaint();
        }

        /// <summary>Finds the most specific real card at a graph-space position.</summary>
        internal static GraphConnectionTarget FindTarget(IReadOnlyList<GraphConnectionTarget> candidates, Vector2 position)
        {
            GraphConnectionTarget best = null;
            float bestArea = float.PositiveInfinity;
            foreach (GraphConnectionTarget candidate in candidates ?? Array.Empty<GraphConnectionTarget>())
            {
                if (!candidate.Bounds.Contains(position))
                {
                    continue;
                }

                float area = candidate.Bounds.width * candidate.Bounds.height;
                if (area < bestArea)
                {
                    best = candidate;
                    bestArea = area;
                }
            }

            return best;
        }

        private void DrawPreview(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (!visible || painter == null)
            {
                return;
            }

            foreach (GraphConnectionTarget target in targets)
            {
                painter.strokeColor = target == hovered
                    ? target.Compatible ? ValidColor : InvalidColor
                    : target.Compatible ? new Color(ValidColor.r, ValidColor.g, ValidColor.b, 0.24f) : InvalidColor;
                painter.lineWidth = target == hovered ? 2.5f : 1f;
                painter.BeginPath();
                painter.MoveTo(target.Bounds.min);
                painter.LineTo(new Vector2(target.Bounds.xMax, target.Bounds.yMin));
                painter.LineTo(target.Bounds.max);
                painter.LineTo(new Vector2(target.Bounds.xMin, target.Bounds.yMax));
                painter.ClosePath();
                painter.Stroke();
            }

            Vector2 destination = hovered?.Anchor ?? pointer;
            Color lineColor = hovered == null ? PreviewColor : hovered.Compatible ? ValidColor : InvalidColor;
            float bend = Mathf.Max(32f, Mathf.Abs(destination.y - source.y) * 0.35f);
            painter.strokeColor = lineColor;
            painter.lineWidth = 2f;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(source);
            painter.BezierCurveTo(source + Vector2.down * bend, destination + Vector2.up * bend, destination);
            painter.Stroke();

            if (hovered != null)
            {
                painter.fillColor = lineColor;
                painter.BeginPath();
                painter.Arc(destination, 6f, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                painter.Fill();
            }
        }
    }
}
