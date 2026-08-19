using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Editor-only annotation frame drawn behind authored graph nodes.</summary>
    internal sealed class GraphGroupElement : VisualElement, IGraphSelectionElement
    {
        private readonly GraphEditorModule module;
        private readonly GraphGroupLayoutEntry group;
        private TextField renameEditor;
        private Label titleLabel;
        private bool cancelRename;

        /// <summary>Creates a frame for one persisted group and its derived visual bounds.</summary>
        internal GraphGroupElement(GraphEditorModule module, GraphGroupLayoutEntry group, Rect bounds)
        {
            this.module = module;
            this.group = group;
            name = $"ai-editor-graph-group-{group.UUID}";
            AddToClassList("ai-editor-graph-group");
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.left = bounds.xMin;
            style.top = bounds.yMin;
            style.width = bounds.width;
            style.height = bounds.height;
            style.backgroundColor = group.Color;
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = group.Color;
            style.borderBottomColor = group.Color;
            style.borderLeftColor = group.Color;
            style.borderRightColor = group.Color;
            style.borderTopLeftRadius = 10f;
            style.borderTopRightRadius = 10f;
            style.borderBottomLeftRadius = 10f;
            style.borderBottomRightRadius = 10f;

            VisualElement titleBar = new() { name = "title-bar" };
            titleBar.AddToClassList("ai-editor-graph-group-title");
            titleBar.style.paddingLeft = 10f;
            titleBar.style.paddingRight = 10f;
            titleBar.style.paddingTop = 4f;
            titleBar.style.paddingBottom = 4f;
            titleLabel = new Label(group.Title ?? "Group");
            renameEditor = new TextField { name = "rename", value = group.Title ?? "Group" };
            renameEditor.style.display = DisplayStyle.None;
            titleBar.Add(titleLabel);
            titleBar.Add(renameEditor);
            Add(titleBar);
            titleBar.AddManipulator(new GroupDragManipulator(this));
            titleBar.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.clickCount < 2) return;
                BeginRename();
                evt.StopPropagation();
            });
            renameEditor.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (!cancelRename) module.RenameGroup(group.UUID, renameEditor.value);
                cancelRename = false;
                renameEditor.style.display = DisplayStyle.None;
                titleLabel.style.display = DisplayStyle.Flex;
            });
            renameEditor.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    cancelRename = true;
                    renameEditor.Blur();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    renameEditor.Blur();
                    evt.StopPropagation();
                }
            });
            RegisterCallback<PointerDownEvent>(OnBodyPointerDown);
            this.AddManipulator(new ContextualMenuManipulator(OnContextMenu));
        }

        /// <summary>Gets the persisted group represented by this visual frame.</summary>
        internal UUID UUID => group.UUID;

        /// <summary>Applies the selected border state without changing the authored group data.</summary>
        /// <param name="value">Whether this frame is selected.</param>
        internal void SetSelected(bool value)
        {
            EnableInClassList("ai-editor-graph-group-selected", value);
            Color border = value
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(group.Color.r, group.Color.g, group.Color.b, 0.9f);
            style.borderTopColor = border;
            style.borderBottomColor = border;
            style.borderLeftColor = border;
            style.borderRightColor = border;
        }

        /// <summary>Applies the group entry from the shared canvas selection snapshot.</summary>
        /// <param name="selection">The current authored graph selection state.</param>
        public void RefreshSelection(GraphSelectionSnapshot selection)
        {
            SetSelected(selection != null && selection.GroupUUID == group.UUID);
        }

        /// <summary>Selects the frame from its visible body without initiating a title drag.</summary>
        private void OnBodyPointerDown(PointerDownEvent evt)
        {
            // Title descendants own rename and drag gestures. The body may resolve to a retained
            // child of the group frame, so accept any non-title descendant rather than only this.
            if (evt.button != 0 || evt.clickCount > 1 || IsTitleDescendant(evt.target as VisualElement)) return;
            module.SelectGroup(group.UUID);
            evt.StopPropagation();
        }

        /// <summary>Gets whether a picked group descendant belongs to the title interaction surface.</summary>
        private bool IsTitleDescendant(VisualElement element)
        {
            for (VisualElement current = element; current != null && current != this; current = current.parent)
            {
                if (current.name == "title-bar") return true;
            }

            return false;
        }

        private void OnContextMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", _ => BeginRename(), _ => DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Add Selected", _ => module.AddSelectedToGroup(group.UUID),
                _ => module.SelectedNodes.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Remove Selected", _ => module.RemoveSelectedFromGroup(group.UUID),
                _ => module.SelectedNodes.Count > 0 ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Ungroup", _ => module.Ungroup(group.UUID),
                _ => DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Tidy Members", _ => module.TidyGroup(group.UUID),
                _ => module.CanTidyGroup(group.UUID)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
            evt.menu.AppendAction("Color/Blue", _ => module.SetGroupColor(group.UUID, new Color(0.25f, 0.55f, 0.9f, 0.22f)),
                _ => DropdownMenuAction.Status.Normal);
            evt.menu.AppendAction("Color/Green", _ => module.SetGroupColor(group.UUID, new Color(0.25f, 0.8f, 0.4f, 0.22f)),
                _ => DropdownMenuAction.Status.Normal);
            evt.StopPropagation();
        }

        /// <summary>Opens the explicit title editor without starting a drag.</summary>
        /// <summary>Begins editing this group's title using the existing rename editor.</summary>
        internal void BeginRename()
        {
            cancelRename = false;
            renameEditor.value = group.Title ?? "Group";
            renameEditor.style.display = DisplayStyle.Flex;
            titleLabel.style.display = DisplayStyle.None;
            renameEditor.Focus();
            renameEditor.SelectAll();
        }

        /// <summary>Owns the pointer lifecycle for dragging a group from any title-bar descendant.</summary>
        private sealed class GroupDragManipulator : PointerManipulator
        {
            private readonly GraphGroupElement owner;
            private bool dragging;
            private int pointerId = -1;
            private Vector2 lastCanvasPosition;

            /// <summary>Creates a primary-button drag manipulator for one group title bar.</summary>
            /// <param name="owner">The group whose layout is moved.</param>
            internal GroupDragManipulator(GraphGroupElement owner)
            {
                this.owner = owner;
                activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            }

            /// <inheritdoc />
            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
                target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut, TrickleDown.TrickleDown);
            }

            /// <inheritdoc />
            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
                target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut, TrickleDown.TrickleDown);
            }

            /// <summary>Starts a drag only for an explicit primary-button pointer event.</summary>
            private void OnPointerDown(PointerDownEvent evt)
            {
                // Double-click is reserved for the title rename action.
                if (dragging || evt.clickCount >= 2 || evt.button != (int)MouseButton.LeftMouse && evt.button != 0) return;
                if (owner.renameEditor.resolvedStyle.display != DisplayStyle.None
                    || owner.renameEditor.panel?.focusController?.focusedElement == owner.renameEditor) return;
                owner.module.SelectGroup(owner.group.UUID);
                dragging = true;
                pointerId = evt.pointerId;
                lastCanvasPosition = owner.module.Canvas.WorldToLocal(evt.position);
                target.CapturePointer(pointerId);
                evt.StopPropagation();
            }

            /// <summary>Applies each captured pointer delta in graph space.</summary>
            private void OnPointerMove(PointerMoveEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId || !target.HasPointerCapture(evt.pointerId)) return;
                Vector2 currentCanvasPosition = owner.module.Canvas.WorldToLocal(evt.position);
                Vector2 delta = currentCanvasPosition - lastCanvasPosition;
                lastCanvasPosition = currentCanvasPosition;
                owner.module.MoveGroup(owner.group.UUID, delta / owner.module.Canvas.Zoom);
                evt.StopPropagation();
            }

            /// <summary>Stops the matching primary-button drag and commits one layout edit.</summary>
            private void OnPointerUp(PointerUpEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId || evt.button != (int)MouseButton.LeftMouse && evt.button != 0) return;
                StopDrag();
                evt.StopPropagation();
            }

            /// <summary>Commits the current layout when the active pointer leaves capture unexpectedly.</summary>
            private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId) return;
                dragging = false;
                pointerId = -1;
                owner.module.CommitGroupMove();
                evt.StopPropagation();
            }

            /// <summary>Ends the matching pointer drag on cancellation.</summary>
            private void OnPointerCancel(PointerCancelEvent evt)
            {
                if (!dragging || evt.pointerId != pointerId) return;
                StopDrag();
                evt.StopPropagation();
            }

            /// <summary>Releases capture before committing so capture-out cannot commit twice.</summary>
            private void StopDrag()
            {
                int activePointerId = pointerId;
                dragging = false;
                pointerId = -1;
                if (target.HasPointerCapture(activePointerId)) target.ReleasePointer(activePointerId);
                owner.module.CommitGroupMove();
            }
        }
    }
}
