using Aethiumian.AI.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Drives Decorator structural editing from the badge grip. The grip is the only drag
    /// entry point for attached Decorator badges; the badge body remains selection-only and
    /// the real child card remains the position drag surface.
    /// </summary>
    internal sealed class DecoratorStructureManipulator : PointerManipulator
    {
        private enum DropIntent
        {
            None,
            Reorder,
            Wrap,
            ExtractAndWrap,
            ExtractToFree,
            DetachEmptyToFree,
            MoveFreeStack,
            Invalid,
        }

        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphNodeElement badge;

        private readonly List<GraphNodeElement> stackElements = new();
        private int pointerId = -1;
        private int sourceIndex = -1;
        private int destinationBoundary = -1;
        private Vector2 startPosition;
        private bool dragging;
        private bool moved;
        private GraphDecoratorStack activeStack;
        private GraphNodeElement highlightedTarget;
        private GraphDecoratorPlaceholderElement draggedPlaceholder;
        private readonly Dictionary<GraphNodeElement, Rect> originalStackBounds = new();
        private DropIntent dropIntent;
        private UUID dropTargetUUID;
        private Vector2 grabOffsetGraph;
        private Vector2 draggedGraphPosition;
        private Vector2 originalBadgeGraphPosition;

        internal DecoratorStructureManipulator(
            GraphCanvasElement canvas,
            GraphEditorModule module,
            GraphNodeElement badge)
        {
            this.canvas = canvas;
            this.module = module;
            this.badge = badge;
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
            if (!CanStartManipulation(evt))
            {
                return;
            }

            module.SelectNode(badge.Descriptor.Node, evt.actionKey, evt.shiftKey);
            canvas.Focus();

            GraphDecoratorStack stack = canvas.Presentation?.FindDecoratorStack(badge.Descriptor.UUID);
            if (stack == null)
            {
                return;
            }

            activeStack = stack;
            HashSet<UUID> members = stack.Badges.Select(item => item.TargetUUID).ToHashSet();
            stackElements.Clear();
            stackElements.AddRange(canvas.Query<GraphNodeElement>().ToList()
                .Where(element => members.Contains(element.Descriptor.UUID))
                .OrderBy(element => element.worldBound.center.y));
            originalStackBounds.Clear();
            foreach (GraphNodeElement element in stackElements)
            {
                originalStackBounds[element] = element.worldBound;
            }
            sourceIndex = stackElements.IndexOf(badge);
            if (sourceIndex < 0 && stack.Badges.Count == 0)
            {
                sourceIndex = 0;
            }

            if (sourceIndex < 0)
            {
                activeStack = null;
                return;
            }

            pointerId = evt.pointerId;
            startPosition = evt.position;
            Vector2 pointerGraphPosition = canvas.PanelToGraph(evt.position);
            Vector2 badgeGraphPosition = canvas.PanelToGraph(badge.worldBound.position);
            grabOffsetGraph = pointerGraphPosition - badgeGraphPosition;
            originalBadgeGraphPosition = badgeGraphPosition;
            draggedGraphPosition = badgeGraphPosition;
            destinationBoundary = sourceIndex;
            dragging = true;
            moved = false;
            dropIntent = DropIntent.None;
            dropTargetUUID = UUID.Empty;
            target.focusable = true;
            target.Focus();
            target.CapturePointer(pointerId);
            UpdateInsertionIndicator(sourceIndex);
            evt.StopImmediatePropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
            {
                return;
            }

            if (!moved && ((Vector2)evt.position - startPosition).sqrMagnitude < 16f)
            {
                return;
            }

            if (!moved)
            {
                moved = true;
                draggedPlaceholder = canvas.FindDecoratorPlaceholder(badge.Descriptor.UUID);
                draggedPlaceholder?.SetDragHidden(true);
                canvas.SetDraggedDecoratorPorts(badge.Descriptor.UUID, true);
                badge.AddToClassList("ai-editor-graph-decorator-badge-dragging");
            }

            destinationBoundary = GetInsertionBoundary(evt.position);
            UpdateInsertionIndicator(destinationBoundary);

            highlightedTarget?.RemoveFromClassList("ai-editor-graph-drop-compatible");
            highlightedTarget?.RemoveFromClassList("ai-editor-graph-drop-invalid");
            highlightedTarget = null;

            Vector2 graphPosition = canvas.PanelToGraph(evt.position);
            GraphPresentationItem hover = canvas.FindDecoratorDropTarget(graphPosition, badge.Descriptor.UUID);
            dropTargetUUID = hover?.TargetUUID ?? UUID.Empty;
            Decorator decorator = badge.Descriptor.Node as Decorator;
            bool occupied = decorator?.node != null && decorator.node.UUID != UUID.Empty;
            if (destinationBoundary >= 0)
            {
                dropIntent = DropIntent.Reorder;
                draggedGraphPosition = canvas.PanelToGraph(evt.position) - grabOffsetGraph;
                draggedGraphPosition.x = originalBadgeGraphPosition.x;
            }
            else if (hover != null)
            {
                highlightedTarget = canvas.Query<GraphNodeElement>().ToList()
                    .FirstOrDefault(element => element.Descriptor.UUID == hover.TargetUUID);
                bool compatible = occupied
                    ? module.CanExtractAndWrapDecorator(badge.Descriptor.UUID, hover.TargetUUID)
                    : module.CanWrapDecorator(badge.Descriptor.UUID, hover.TargetUUID);
                dropIntent = compatible
                    ? (occupied ? DropIntent.ExtractAndWrap : DropIntent.Wrap)
                    : DropIntent.Invalid;
                draggedGraphPosition = GetTargetPreviewPosition(hover);
                highlightedTarget?.AddToClassList(compatible
                    ? "ai-editor-graph-drop-compatible"
                    : "ai-editor-graph-drop-invalid");
            }
            else
            {
                if (occupied)
                {
                    dropIntent = DropIntent.ExtractToFree;
                }
                else if (module.CanDetachEmptyDecoratorToFree(badge.Descriptor.UUID))
                {
                    dropIntent = DropIntent.DetachEmptyToFree;
                }
                else if (module.IsFreeEmptyDecorator(badge.Descriptor.UUID))
                {
                    dropIntent = DropIntent.MoveFreeStack;
                }
                else
                {
                    dropIntent = DropIntent.Invalid;
                }

                draggedGraphPosition = canvas.PanelToGraph(evt.position) - grabOffsetGraph;
            }

            MoveBadge(draggedGraphPosition);
            ApplyReorderPreview(destinationBoundary);

            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
            {
                return;
            }

            int boundary = destinationBoundary;
            bool didDrag = moved;
            DropIntent intent = dropIntent;
            UUID targetUUID = dropTargetUUID;
            Vector2 dropGraphPosition = draggedGraphPosition;

            GraphDecoratorPlaceholderElement placeholder = draggedPlaceholder;
            FinishVisuals(restorePlaceholder: false);
            target.ReleasePointer(evt.pointerId);

            if (!didDrag)
            {
                evt.StopImmediatePropagation();
                return;
            }

            int destination = boundary > sourceIndex ? boundary - 1 : boundary;
            bool committed = false;
            if (intent == DropIntent.Reorder)
            {
                if (boundary >= 0 && destination != sourceIndex)
                {
                    committed = module.MoveDecoratorBadge(badge.Descriptor.UUID, destination);
                }
            }
            else if (intent == DropIntent.Wrap)
            {
                committed = module.WrapDecorator(badge.Descriptor.UUID, targetUUID);
            }
            else if (intent == DropIntent.ExtractAndWrap)
            {
                committed = module.ExtractAndWrapDecorator(badge.Descriptor.UUID, targetUUID);
            }
            else if (intent == DropIntent.ExtractToFree)
            {
                committed = module.ExtractDecoratorToFree(badge.Descriptor.UUID, dropGraphPosition);
            }
            else if (intent == DropIntent.DetachEmptyToFree)
            {
                committed = module.DetachEmptyDecoratorToFree(badge.Descriptor.UUID, dropGraphPosition);
            }
            else if (intent == DropIntent.MoveFreeStack)
            {
                module.MoveNode(badge.Descriptor, dropGraphPosition);
                module.CommitNodeMove();
                committed = true;
            }

            if (!committed)
            {
                placeholder?.SetDragHidden(false);
            }

            evt.StopImmediatePropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
            {
                return;
            }

            Cancel();
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (dragging && evt.pointerId == pointerId)
            {
                Cancel();
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!dragging || evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            Cancel();
            evt.StopPropagation();
        }

        private void Cancel()
        {
            int captured = pointerId;
            FinishVisuals();
            if (captured >= 0 && target.HasPointerCapture(captured))
            {
                target.ReleasePointer(captured);
            }
        }

        private int GetInsertionBoundary(Vector2 panelPosition)
        {
            if (stackElements.Count == 0)
            {
                return -1;
            }

            float left = stackElements.Min(element => originalStackBounds[element].xMin) - 12f;
            float right = stackElements.Max(element => originalStackBounds[element].xMax) + 12f;
            if (panelPosition.x < left || panelPosition.x > right)
            {
                return -1;
            }

            float top = originalStackBounds[stackElements[0]].yMin - 12f;
            float bottom = originalStackBounds[stackElements[^1]].yMax + 12f;
            if (panelPosition.y < top || panelPosition.y > bottom)
            {
                return -1;
            }

            for (int index = 0; index < stackElements.Count; index++)
            {
                if (panelPosition.y < originalStackBounds[stackElements[index]].center.y)
                {
                    return index;
                }
            }

            return stackElements.Count;
        }

        private void UpdateInsertionIndicator(int boundary)
        {
            foreach (GraphNodeElement element in stackElements)
            {
                element.RemoveFromClassList("ai-editor-graph-decorator-insert-before");
                element.RemoveFromClassList("ai-editor-graph-decorator-insert-after");
            }

            if (boundary < 0 || stackElements.Count == 0)
            {
                return;
            }

            if (boundary == stackElements.Count)
            {
                stackElements[^1].AddToClassList("ai-editor-graph-decorator-insert-after");
            }
            else
            {
                stackElements[boundary].AddToClassList("ai-editor-graph-decorator-insert-before");
            }
        }

        private Vector2 GetTargetPreviewPosition(GraphPresentationItem target)
        {
            Vector2 badgeSize = GraphPresentationMetrics.DecoratorNodeSize;
            return new Vector2(
                target.Position.x + (target.Size.x - badgeSize.x) * 0.5f,
                target.Position.y - badgeSize.y);
        }

        /// <summary>Moves the real badge visually without changing its presentation position.</summary>
        private void MoveBadge(Vector2 graphPosition)
        {
            Vector2 delta = graphPosition - originalBadgeGraphPosition;
            badge.style.translate = new StyleTranslate(new Translate(delta.x, delta.y));
        }

        /// <summary>Temporarily opens a gap in the visible decorator order while reordering.</summary>
        private void ApplyReorderPreview(int boundary)
        {
            foreach (GraphNodeElement element in stackElements)
            {
                if (ReferenceEquals(element, badge)) continue;
                element.style.translate = new StyleTranslate(new Translate(0f, 0f));
            }

            if (boundary < 0 || boundary == sourceIndex)
            {
                return;
            }

            float shift = GraphPresentationMetrics.DecoratorNodeSize.y + 8f;
            if (sourceIndex < boundary)
            {
                for (int index = sourceIndex + 1; index < boundary && index < stackElements.Count; index++)
                {
                    stackElements[index].style.translate = new StyleTranslate(new Translate(0f, -shift));
                }
            }
            else
            {
                for (int index = boundary; index < sourceIndex; index++)
                {
                    stackElements[index].style.translate = new StyleTranslate(new Translate(0f, shift));
                }
            }
        }

        /// <summary>Restores transient drag visuals without changing presentation descriptors.</summary>
        private void FinishVisuals(bool restorePlaceholder = true)
        {
            dragging = false;
            moved = false;
            pointerId = -1;
            activeStack = null;
            dropIntent = DropIntent.None;
            dropTargetUUID = UUID.Empty;
            badge.RemoveFromClassList("ai-editor-graph-decorator-badge-dragging");
            badge.style.translate = new StyleTranslate(new Translate(0f, 0f));
            foreach (GraphNodeElement element in stackElements)
            {
                element.style.translate = new StyleTranslate(new Translate(0f, 0f));
            }

            if (restorePlaceholder)
            {
                draggedPlaceholder?.SetDragHidden(false);
            }
            draggedPlaceholder = null;
            canvas.SetDraggedDecoratorPorts(badge.Descriptor.UUID, false);
            originalStackBounds.Clear();

            UpdateInsertionIndicator(-1);
            highlightedTarget?.RemoveFromClassList("ai-editor-graph-drop-compatible");
            highlightedTarget?.RemoveFromClassList("ai-editor-graph-drop-invalid");
            highlightedTarget = null;
        }
    }
}
