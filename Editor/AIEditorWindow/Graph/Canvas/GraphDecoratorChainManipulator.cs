using Aethiumian.AI.Nodes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Moves a free empty Decorator chain from its presentation-only placeholder.</summary>
    internal sealed class GraphDecoratorChainManipulator : PointerManipulator
    {
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphDecoratorPlaceholderElement placeholder;
        private readonly Dictionary<GraphNodeDescriptor, Vector2> originalPositions = new();
        private int pointerId = -1;
        private Vector2 startPosition;
        private Vector2 grabOffsetGraph;
        private Vector2 anchorToPlaceholderOffset;
        private GraphNodeDescriptor anchorDescriptor;
        private bool dragging;

        /// <summary>Initializes the chain-level pointer state machine for a free placeholder.</summary>
        internal GraphDecoratorChainManipulator(
            GraphCanvasElement canvas,
            GraphEditorModule module,
            GraphDecoratorPlaceholderElement placeholder)
        {
            this.canvas = canvas;
            this.module = module;
            this.placeholder = placeholder;
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

            GraphDecoratorStack stack = ResolveFreeStack();
            if (stack == null || stack.Badges.Count == 0)
            {
                return;
            }

            List<TreeNode> chain = stack.Badges
                .Select(badge => badge.Node?.Node)
                .Where(node => node != null)
                .ToList();
            if (chain.Count == 0)
            {
                return;
            }

            ApplyChainSelection(chain, evt.actionKey, evt.shiftKey);
            anchorDescriptor = stack.Badges[0].Node;
            if (anchorDescriptor == null)
            {
                return;
            }

            if (!module.IsNodeSelected(anchorDescriptor.Node))
            {
                return;
            }

            originalPositions.Clear();
            IReadOnlyCollection<GraphNodeDescriptor> seeds = module.SelectedNodes
                .Select(node => module.Topology?.FindNode(node.uuid))
                .Where(descriptor => descriptor != null)
                .ToArray();
            foreach (GraphNodeDescriptor descriptor in module.CollectMoveSet(seeds, module.MoveMode))
            {
                originalPositions[descriptor] = descriptor.Position;
            }

            pointerId = evt.pointerId;
            startPosition = evt.position;
            Vector2 pointerGraphPosition = canvas.PanelToGraph(evt.position);
            Vector2 anchorGraphPosition = canvas.Presentation?.Find(anchorDescriptor.UUID)?.Position
                ?? anchorDescriptor.Position;
            anchorToPlaceholderOffset = anchorGraphPosition - placeholder.PresentationItem.Position;
            grabOffsetGraph = pointerGraphPosition - placeholder.PresentationItem.Position;
            dragging = false;
            // Escape is handled by this manipulator, so the captured placeholder must own
            // keyboard focus rather than forwarding it to the canvas.
            target.Focus();
            target.CapturePointer(pointerId);
            evt.StopImmediatePropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointerId || anchorDescriptor == null)
            {
                return;
            }

            if (!dragging)
            {
                if (((Vector2)evt.position - startPosition).sqrMagnitude < 16f)
                {
                    return;
                }

                dragging = true;
                placeholder.AddToClassList("ai-editor-graph-decorator-placeholder-dragging");
            }

            Vector2 placeholderPosition = canvas.PanelToGraph(evt.position) - grabOffsetGraph;
            module.MoveNode(anchorDescriptor, placeholderPosition + anchorToPlaceholderOffset);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId)
            {
                return;
            }

            FinishDrag(commit: dragging);
            evt.StopImmediatePropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId != pointerId)
            {
                return;
            }

            FinishDrag(commit: false);
            evt.StopPropagation();
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == pointerId)
            {
                FinishDrag(commit: false);
            }
        }

        /// <summary>Cancels a captured chain drag when the user presses Escape.</summary>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || pointerId < 0)
            {
                return;
            }

            FinishDrag(commit: false);
            evt.StopPropagation();
        }

        /// <summary>Commits one graph move or restores all captured positions without writing Undo.</summary>
        private void FinishDrag(bool commit)
        {
            bool wasDragging = dragging;
            dragging = false;
            placeholder.RemoveFromClassList("ai-editor-graph-decorator-placeholder-dragging");
            if (commit && wasDragging)
            {
                module.CommitNodeMove();
            }
            else if (wasDragging)
            {
                foreach (KeyValuePair<GraphNodeDescriptor, Vector2> pair in originalPositions)
                {
                    pair.Key.Position = pair.Value;
                }

                canvas.UpdatePresentationPositions(originalPositions.Keys, preserveGroupElements: true);
                module.CancelNodeMove();
            }

            int capturedPointer = pointerId;
            pointerId = -1;
            anchorDescriptor = null;
            originalPositions.Clear();
            if (capturedPointer >= 0 && target.HasPointerCapture(capturedPointer))
            {
                target.ReleasePointer(capturedPointer);
            }
        }

        /// <summary>Resolves the free decorator stack owned by this presentation-only placeholder.</summary>
        private GraphDecoratorStack ResolveFreeStack()
        {
            GraphPresentation presentation = canvas.Presentation;
            GraphDecoratorStack stack = presentation?.FindDecoratorStack(placeholder.DecoratorUUID);
            return stack?.Anchor.DecoratorPlaceholder != null
                && stack.Badges.Count > 0
                && presentation.Roots.Contains(stack.Badges[0])
                ? stack
                : null;
        }

        /// <summary>Applies normal, additive, or toggle selection semantics to the whole chain.</summary>
        private void ApplyChainSelection(IReadOnlyList<TreeNode> chain, bool toggle, bool additive)
        {
            List<TreeNode> existing = module.SelectedNodes.ToList();
            HashSet<UUID> chainUUIDs = chain.Select(node => node.uuid).ToHashSet();
            if (toggle)
            {
                bool allSelected = chain.All(module.IsNodeSelected);
                if (allSelected)
                {
                    existing.RemoveAll(node => chainUUIDs.Contains(node.uuid));
                }
                else
                {
                    existing.AddRange(chain.Where(node => !existing.Any(selected => selected.uuid == node.uuid)));
                }
            }
            else if (additive)
            {
                existing.AddRange(chain.Where(node => !existing.Any(selected => selected.uuid == node.uuid)));
            }
            else
            {
                existing = chain.ToList();
            }

            module.SetGraphSelection(existing);
        }
    }
}
