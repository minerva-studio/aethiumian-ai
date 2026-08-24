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
    internal sealed class GraphContainerElement : VisualElement, IGraphMarqueeSelectable, IGraphGeometryElement, IGraphSelectionElement
    {
        private const float HeaderHeight = 48f;
        private const float PlaceholderHeight = 52f;
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphPresentationItem item;
        private readonly bool movable;
        private readonly Label title;
        private readonly Label typeLabel;
        private readonly List<VisualElement> selectableChildren = new();
        private readonly Func<GraphPresentationItem, bool, Vector2, GraphNodeShape?, VisualElement> createElement;
        private bool dragging;
        private int pointerId = -1;
        private Vector2 dragOffset;

        /// <summary>Gets the authored node represented by this compatibility container.</summary>
        public TreeNode AuthoredNode => item.Node?.Node;

        /// <summary>Gets the container header bounds used by box selection.</summary>
        public Rect MarqueeWorldBound
        {
            get
            {
                Rect bounds = worldBound;
                float localHeight = layout.height;
                bounds.height = localHeight > 0f
                    ? bounds.height * Mathf.Clamp01(HeaderHeight / localHeight)
                    : 0f;
                return bounds;
            }
        }

        /// <summary>
        /// Initializes a semantic Flow container.
        /// </summary>
        internal GraphContainerElement(
            GraphCanvasElement canvas,
            GraphEditorModule module,
            GraphPresentationItem item,
            bool movable,
            Vector2 position,
            Func<GraphPresentationItem, bool, Vector2, GraphNodeShape?, VisualElement> createElement)
        {
            this.canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            this.item = item ?? throw new ArgumentNullException(nameof(item));
            this.movable = movable;
            this.createElement = createElement ?? throw new ArgumentNullException(nameof(createElement));

            name = $"ai-editor-graph-container-{item.Node?.UUID ?? item.TargetUUID}";
            usageHints |= UsageHints.DynamicTransform;
            AddToClassList("ai-editor-graph-container");
            AddToClassList($"ai-editor-graph-container-{item.Kind.ToString().ToLowerInvariant()}");
            if (item.Node?.IsHead == true)
            {
                AddToClassList("ai-editor-graph-container-head");
            }

            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            generateVisualContent += DrawContainer;
            title = new Label(item.Node?.DisplayName ?? "Flow");
            title.AddToClassList("ai-editor-graph-container-title");
            typeLabel = new Label(GetTypeLabel(item));
            typeLabel.AddToClassList("ai-editor-graph-container-type");

            VisualElement header = new()
            {
                name = "ai-editor-graph-container-header",
            };
            header.AddToClassList("ai-editor-graph-container-header");
            header.style.position = UIPosition.Absolute;
            header.style.left = 0f;
            header.style.top = 0f;
            header.style.width = item.Size.x;
            header.style.height = HeaderHeight;
            header.Add(title);
            header.Add(typeLabel);
            header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            header.RegisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
            header.RegisterCallback<PointerUpEvent>(OnHeaderPointerUp);
            header.RegisterCallback<PointerCancelEvent>(OnHeaderPointerCancel);
            header.RegisterCallback<PointerCaptureOutEvent>(OnHeaderPointerCaptureOut);
            header.AddManipulator(new ContextualMenuManipulator(canvas.PopulateAuthoredNodeContextMenu));
            Add(header);

            BuildSlots();
        }

        /// <summary>Updates selection for this container and all nested presentations.</summary>
        internal void SetSelected(TreeNode selectedNode)
        {
            SetSelected(selectedNode == null ? new HashSet<UUID>() : new HashSet<UUID> { selectedNode.uuid });
        }

        /// <summary>Updates this container and nested cards from the Graph selection set.</summary>
        internal void SetSelected(IReadOnlyCollection<UUID> selectedUUIDs)
        {
            EnableInClassList("ai-editor-graph-container-selected", item.Node != null && selectedUUIDs.Contains(item.Node.UUID));
            foreach (VisualElement child in selectableChildren)
            {
                if (child is GraphNodeElement card)
                {
                    card.SetSelected(selectedUUIDs.Contains(card.Descriptor.UUID));
                }
                else if (child is GraphContainerElement container)
                {
                    container.SetSelected(selectedUUIDs);
                }
                else if (child is GraphReferenceProxyElement proxy)
                {
                    proxy.SetSelected(proxy.TargetNode != null && selectedUUIDs.Contains(proxy.TargetNode.uuid));
                }
            }

            MarkDirtyRepaint();
        }

        /// <summary>Refreshes a movable compatibility container after a grouped drag.</summary>
        internal void RefreshPosition()
        {
            if (movable && item.Node != null)
            {
                style.left = item.Node.Position.x;
                style.top = item.Node.Position.y;
            }
        }

        void IGraphGeometryElement.RefreshGeometry() => RefreshPosition();

        void IGraphSelectionElement.RefreshSelection(GraphSelectionSnapshot selection)
        {
            SetSelected(selection.SelectedUUIDs);
        }

        private void BuildSlots()
        {
            foreach (GraphPresentationSlot slot in item.Slots)
            {
                if (item.Kind is GraphPresentationKind.Sequence or GraphPresentationKind.Decision)
                {
                    Vector2 slotPosition = item.Position + new Vector2(0f, slot.Content.Position.y - item.Position.y);
                    AddSlot(slot, slotPosition, null);
                }
                else
                {
                    GraphNodeShape? shapeOverride = slot.Label == "Condition" ? GraphNodeShape.Branch : null;
                    Vector2 slotPosition = item.Position + new Vector2(slot.Content.Position.x - item.Position.x, slot.Content.Position.y - item.Position.y);
                    AddSlot(slot, slotPosition, shapeOverride);
                }
            }
        }

        private void AddSlot(GraphPresentationSlot slot, Vector2 slotPosition, GraphNodeShape? shapeOverride)
        {
            GraphSlotElement slotElement = new(
                slot,
                item.Position,
                slotPosition,
                createElement,
                shapeOverride);
            Add(slotElement);
            if (slotElement.ContentElement != null)
            {
                selectableChildren.Add(slotElement.ContentElement);
            }
        }

        private static string GetTypeLabel(GraphPresentationItem value)
        {
            return value.Kind switch
            {
                GraphPresentationKind.Sequence => "FLOW  ·  SEQUENCE  ·  RUN ALL",
                GraphPresentationKind.Decision => "FLOW  ·  DECISION  ·  PRIORITY",
                GraphPresentationKind.Condition => "FLOW  ·  CONDITION  ·  TRUE / FALSE",
                _ => "FLOW",
            };
        }

        private void DrawContainer(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null)
            {
                return;
            }

            float width = layout.width;
            float height = layout.height;
            GraphCanvasAppearance appearance = canvas.Appearance;
            Color fill = EditorGUIUtility.isProSkin
                ? appearance.CompoundFillDark
                : appearance.CompoundFillLight;
            Color stroke = ClassListContains("ai-editor-graph-container-selected")
                ? appearance.SelectedStroke
                : item.Warning != null
                    ? appearance.WarningStroke
                    : item.Kind == GraphPresentationKind.Condition
                        ? appearance.BranchStroke
                        : appearance.FlowStroke;

            painter.fillColor = fill;
            painter.strokeColor = stroke;
            painter.lineWidth = ClassListContains("ai-editor-graph-container-selected")
                ? appearance.SelectedLineWidth
                : appearance.NodeLineWidth;
            DrawRoundedRect(painter, new Rect(0f, 0f, width, height), 8f);

            if (item.Kind is GraphPresentationKind.Sequence or GraphPresentationKind.Decision)
            {
                float x = 24f;
                float startY = 48f;
                float endY = height - 16f;
                DrawSegment(painter, new Vector2(x, startY), new Vector2(x, endY), stroke, appearance.NodeLineWidth);
                foreach (GraphPresentationSlot slot in item.Slots)
                {
                    float y = slot.Content.Position.y - item.Position.y + Mathf.Min(PlaceholderHeight, slot.Content.Size.y) * 0.5f;
                    DrawSegment(painter, new Vector2(x, y), new Vector2(slot.Content.Position.x - item.Position.x - 8f, y), stroke, appearance.NodeLineWidth);
                }
            }
            else
            {
                GraphPresentationItem predicate = GetSlotContent("Condition");
                GraphPresentationItem trueItem = GetSlotContent("True");
                GraphPresentationItem falseItem = GetSlotContent("False");
                Vector2 predicateBottom = predicate.Position - item.Position + new Vector2(predicate.Size.x * 0.5f, predicate.Size.y);
                Vector2 branchY = new(item.Size.x * 0.5f, trueItem.Position.y - item.Position.y - 8f);
                DrawSegment(painter, predicateBottom, branchY, stroke, appearance.NodeLineWidth);
                DrawSegment(painter, branchY, new Vector2(trueItem.Position.x - item.Position.x + trueItem.Size.x * 0.5f, branchY.y), stroke, appearance.NodeLineWidth);
                DrawSegment(painter, branchY, new Vector2(falseItem.Position.x - item.Position.x + falseItem.Size.x * 0.5f, branchY.y), stroke, appearance.NodeLineWidth);
                DrawSegment(painter, new Vector2(trueItem.Position.x - item.Position.x + trueItem.Size.x * 0.5f, branchY.y), new Vector2(trueItem.Position.x - item.Position.x + trueItem.Size.x * 0.5f, trueItem.Position.y - item.Position.y), stroke, appearance.NodeLineWidth);
                DrawSegment(painter, new Vector2(falseItem.Position.x - item.Position.x + falseItem.Size.x * 0.5f, branchY.y), new Vector2(falseItem.Position.x - item.Position.x + falseItem.Size.x * 0.5f, falseItem.Position.y - item.Position.y), stroke, appearance.NodeLineWidth);
            }
        }

        private GraphPresentationItem GetSlotContent(string label)
        {
            foreach (GraphPresentationSlot slot in item.Slots)
            {
                if (slot.Label == label)
                {
                    return slot.Content;
                }
            }

            return GraphPresentationItem.CreateMissing(label + " is empty");
        }

        private void OnHeaderPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || item.Node == null)
            {
                return;
            }

            module.SelectNode(item.Node.Node, evt.actionKey, evt.shiftKey);
            if (!module.IsNodeSelected(item.Node.Node))
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
            dragOffset = (canvasPoint - canvas.Pan) / canvas.Zoom - item.Position;
            dragging = true;
            pointerId = evt.pointerId;
            ((VisualElement)evt.currentTarget).CapturePointer(pointerId);
            evt.StopPropagation();
        }

        private void OnHeaderPointerMove(PointerMoveEvent evt)
        {
            if (!dragging || evt.pointerId != pointerId)
            {
                return;
            }

            Vector2 canvasPoint = canvas.WorldToLocal(evt.position);
            Vector2 position = (canvasPoint - canvas.Pan) / canvas.Zoom - dragOffset;
            module.MoveNode(item.Node, position);
            RefreshPosition();
            evt.StopPropagation();
        }

        private void OnHeaderPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != pointerId)
            {
                return;
            }

            dragging = false;
            ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
            pointerId = -1;
            module.CommitNodeMove();
            evt.StopPropagation();
        }

        private void OnHeaderPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == pointerId)
            {
                dragging = false;
                ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
                pointerId = -1;
                module.CommitNodeMove();
            }
        }

        /// <summary>Commits a captured container drag if pointer capture is lost.</summary>
        private void OnHeaderPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId != pointerId) return;
            dragging = false;
            pointerId = -1;
            module.CommitNodeMove();
        }

        private static void DrawRoundedRect(Painter2D painter, Rect rect, float radius)
        {
            radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin + radius, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax - radius, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin + radius));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - radius));
            painter.LineTo(new Vector2(rect.xMax - radius, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin + radius, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax - radius));
            painter.LineTo(new Vector2(rect.xMin, rect.yMin + radius));
            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        private static void DrawSegment(Painter2D painter, Vector2 from, Vector2 to, Color color, float width)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }
    }

    /// <summary>
    /// Displays a named container slot and its recursively built content.
    /// </summary>
    internal sealed class GraphSlotElement : VisualElement
    {
        internal GraphSlotElement(
            GraphPresentationSlot slot,
            Vector2 parentPosition,
            Vector2 slotPosition,
            Func<GraphPresentationItem, bool, Vector2, GraphNodeShape?, VisualElement> createElement,
            GraphNodeShape? shapeOverride)
        {
            name = $"ai-editor-graph-slot-{slot.Label}";
            AddToClassList("ai-editor-graph-slot");
            style.position = UIPosition.Absolute;
            style.left = slotPosition.x - parentPosition.x;
            style.top = slotPosition.y - parentPosition.y;
            style.width = Mathf.Max(300f, slot.Content?.Size.x ?? 220f) + 72f;
            style.height = Mathf.Max(52f, slot.Content?.Size.y ?? 52f);

            Label label = new(slot.Label);
            label.AddToClassList("ai-editor-graph-slot-label");
            label.style.position = UIPosition.Absolute;
            bool stackedLabel = shapeOverride.HasValue || slot.Label is "True" or "False";
            label.style.left = stackedLabel ? 0f : 12f;
            label.style.top = stackedLabel ? -16f : 17f;
            Add(label);

            if (slot.Content != null)
            {
                ContentElement = createElement(slot.Content, false, slotPosition, shapeOverride);
                Add(ContentElement);
            }
        }

        /// <summary>Gets the nested element displayed by this slot.</summary>
        internal VisualElement ContentElement { get; }
    }

    /// <summary>
    /// Displays a missing or repeated reference without creating a second editable node.
    /// </summary>
}
