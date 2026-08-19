using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Owns the visible and draggable runtime order of one Decision collection.</summary>
    internal sealed class GraphDecisionOrderStripElement : VisualElement
    {
        private readonly GraphEditorModule module;
        private readonly UUID ownerUUID;
        private readonly List<VisualElement> optionElements = new();
        private readonly VisualElement insertionIndicator;
        private VisualElement draggedElement;
        private int draggedIndex = -1;
        private int destinationBoundary = -1;

        internal GraphDecisionOrderStripElement(GraphEditorModule module, GraphNodeDescriptor descriptor)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            ownerUUID = descriptor?.UUID ?? throw new ArgumentNullException(nameof(descriptor));
            name = $"ai-editor-graph-decision-order-{ownerUUID}";
            AddToClassList("ai-editor-graph-decision-order");

            Decision decision = descriptor.Node as Decision;
            NodeReference[] references = decision?.events ?? Array.Empty<NodeReference>();
            for (int index = 0; index < references.Length; index++)
            {
                VisualElement option = CreateOption(index, references[index]);
                optionElements.Add(option);
                Add(option);
            }

            insertionIndicator = new VisualElement
            {
                name = "ai-editor-graph-decision-insertion-indicator",
                pickingMode = PickingMode.Ignore,
            };
            insertionIndicator.AddToClassList("ai-editor-graph-decision-insertion-indicator");
            Add(insertionIndicator);
        }

        /// <summary>Gets one option output anchor in graph-local coordinates.</summary>
        internal static Vector2 GetOptionAnchor(GraphPresentationItem item, int index)
        {
            return item.Position + new Vector2(
                GraphPresentationMetrics.DecisionStripPadding
                    + GraphPresentationMetrics.DecisionPortAllowance
                    + index * GraphPresentationMetrics.DecisionOptionStride
                    + GraphPresentationMetrics.DecisionOptionWidth * 0.5f,
                item.Size.y);
        }

        /// <summary>Gets the prepend port anchor in graph-local coordinates.</summary>
        internal static Vector2 GetPrependAnchor(GraphPresentationItem item)
        {
            return item.Position + new Vector2(
                GraphPresentationMetrics.DecisionStripPadding
                    + GraphPresentationMetrics.DecisionPortAllowance * 0.5f,
                item.Size.y);
        }

        /// <summary>Gets the append output anchor in graph-local coordinates.</summary>
        internal static Vector2 GetAppendAnchor(GraphPresentationItem item, int count)
        {
            return item.Position + new Vector2(
                GraphPresentationMetrics.DecisionStripPadding
                    + GraphPresentationMetrics.DecisionPortAllowance
                    + count * GraphPresentationMetrics.DecisionOptionStride
                    + GraphPresentationMetrics.DecisionPortAllowance * 0.5f,
                item.Size.y);
        }

        private VisualElement CreateOption(int index, NodeReference reference)
        {
            VisualElement option = new() { name = $"ai-editor-graph-decision-option-{index}" };
            option.AddToClassList("ai-editor-graph-decision-option");
            string targetName = GetTargetName(reference);
            Label label = new($"{index + 1}  {targetName}") { tooltip = targetName };
            option.Add(label);
            option.AddManipulator(new OptionDragManipulator(this, index));
            return option;
        }

        /// <summary>Gets the nearest insertion boundary for a local horizontal pointer coordinate.</summary>
        internal static int GetInsertionBoundary(float localX, int optionCount)
        {
            return Mathf.Clamp(
                Mathf.RoundToInt(localX / GraphPresentationMetrics.DecisionOptionStride),
                0,
                optionCount);
        }

        /// <summary>Gets the indicator left edge centered on one real insertion boundary.</summary>
        internal static float GetInsertionIndicatorLeft(int boundaryIndex)
        {
            float gap = GraphPresentationMetrics.DecisionOptionStride - GraphPresentationMetrics.DecisionOptionWidth;
            float center = boundaryIndex == 0
                ? 0f
                : boundaryIndex * GraphPresentationMetrics.DecisionOptionStride - gap * 0.5f;
            return center - 1.5f;
        }

        /// <summary>Converts a pre-removal insertion boundary to a final collection index.</summary>
        internal static bool TryGetDestinationIndex(
            int sourceIndex,
            int boundaryIndex,
            int optionCount,
            out int destinationIndex)
        {
            destinationIndex = sourceIndex;
            if (sourceIndex < 0
                || sourceIndex >= optionCount
                || boundaryIndex < 0
                || boundaryIndex > optionCount
                || boundaryIndex == sourceIndex
                || boundaryIndex == sourceIndex + 1)
            {
                return false;
            }

            destinationIndex = boundaryIndex > sourceIndex ? boundaryIndex - 1 : boundaryIndex;
            return true;
        }

        private string GetTargetName(NodeReference reference)
        {
            if (reference == null || reference.UUID == UUID.Empty)
            {
                return "Empty";
            }

            return module.TopologyTree?.GetNode(reference.UUID)?.name ?? "Missing";
        }

        private void BeginDrag(int index, VisualElement option)
        {
            draggedIndex = index;
            destinationBoundary = index;
            draggedElement = option;
            option.AddToClassList("ai-editor-graph-decision-option-dragging");
            insertionIndicator.AddToClassList("ai-editor-graph-decision-insertion-indicator-visible");
            SetIndicator(index);
        }

        private void UpdateDrag(Vector2 panelPosition)
        {
            if (draggedElement == null) return;
            Vector2 local = this.WorldToLocal(panelPosition);
            draggedElement.style.translate = new StyleTranslate(new Translate(
                Mathf.Clamp(local.x - draggedElement.layout.center.x, -layout.width, layout.width), 0f));
            destinationBoundary = contentRect.Contains(local)
                ? GetInsertionBoundary(local.x, optionElements.Count)
                : -1;
            SetIndicator(destinationBoundary);
        }

        private void CompleteDrag(bool commit)
        {
            int source = draggedIndex;
            int boundary = destinationBoundary;
            if (draggedElement != null)
            {
                draggedElement.style.translate = new StyleTranslate(new Translate(0f, 0f));
                draggedElement.RemoveFromClassList("ai-editor-graph-decision-option-dragging");
            }

            insertionIndicator.RemoveFromClassList("ai-editor-graph-decision-insertion-indicator-visible");
            draggedElement = null;
            draggedIndex = -1;
            destinationBoundary = -1;
            if (commit
                && TryGetDestinationIndex(source, boundary, optionElements.Count, out int destination))
            {
                module.ReorderCollection(ownerUUID, nameof(Decision.events), source, destination);
            }
        }

        private void SetIndicator(int index)
        {
            insertionIndicator.style.display = index < 0 ? DisplayStyle.None : DisplayStyle.Flex;
            if (index >= 0)
            {
                insertionIndicator.style.left = GetInsertionIndicatorLeft(index);
            }
        }

        /// <summary>Handles one Decision option drag without leaking events to the parent node.</summary>
        private sealed class OptionDragManipulator : PointerManipulator
        {
            private readonly GraphDecisionOrderStripElement owner;
            private readonly int index;
            private int pointerId = -1;
            private bool dragging;

            internal OptionDragManipulator(GraphDecisionOrderStripElement owner, int index)
            {
                this.owner = owner;
                this.index = index;
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
                pointerId = evt.pointerId;
                dragging = true;
                target.focusable = true;
                target.Focus();
                target.CapturePointer(pointerId);
                owner.BeginDrag(index, target);
                evt.StopPropagation();
            }

            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId) return;
                owner.UpdateDrag(evt.position);
                evt.StopPropagation();
            }

            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId) return;
                dragging = false;
                target.ReleasePointer(pointerId);
                pointerId = -1;
                owner.CompleteDrag(commit: true);
                evt.StopPropagation();
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
                dragging = false;
                pointerId = -1;
                owner.CompleteDrag(commit: false);
                if (captured >= 0 && target.HasPointerCapture(captured)) target.ReleasePointer(captured);
            }
        }
    }
}
