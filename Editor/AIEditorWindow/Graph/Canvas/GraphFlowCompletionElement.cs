using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
    internal sealed class GraphFlowCompletionElement : Label, IGraphGeometryElement, IGraphSelectionElement
    {
        private readonly GraphEditorModule module;

        /// <summary>
        /// Initializes one presentation-only Flow completion marker.
        /// </summary>
        /// <param name="module">The graph module that owns node selection.</param>
        /// <param name="scope">The derived Flow scope to display.</param>
        internal GraphFlowCompletionElement(GraphEditorModule module, GraphFlowScope scope)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));

            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            string displayName = scope.Owner.Node?.DisplayName ?? "Flow";
            text = $"END · {displayName}";
            name = $"ai-editor-graph-flow-end-{scope.Owner.TargetUUID}";
            tooltip = $"{displayName} completes here.";
            pickingMode = PickingMode.Position;
            AddToClassList("ai-editor-graph-flow-end");
            AddToClassList($"ai-editor-graph-flow-end-{GraphCanvasAppearance.GetFamily(scope.Owner.Node.Node).ToString().ToLowerInvariant()}");
            style.position = UIPosition.Absolute;
            style.left = scope.CompletionPosition.x;
            style.top = scope.CompletionPosition.y;
            style.width = scope.CompletionSize.x;
            style.height = scope.CompletionSize.y;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        /// <summary>Gets the derived scope represented by this marker.</summary>
        internal GraphFlowScope Scope { get; }

        /// <summary>Updates owner selection highlighting.</summary>
        internal void SetSelected(bool value)
        {
            EnableInClassList("ai-editor-graph-flow-end-selected", value);
        }

        /// <summary>Refreshes the retained marker geometry after its scope is re-laid out.</summary>
        internal void RefreshGeometry()
        {
            GraphElementGeometry.ApplyRect(this, new Rect(Scope.CompletionPosition, Scope.CompletionSize));
        }

        void IGraphGeometryElement.RefreshGeometry() => RefreshGeometry();

        void IGraphSelectionElement.RefreshSelection(GraphSelectionSnapshot selection)
        {
            SetSelected(Scope.Owner.Node != null && selection.Contains(Scope.Owner.TargetUUID));
        }

        /// <summary>Selects the owning Flow for a primary pointer press.</summary>
        /// <param name="evt">The pointer event received by this marker.</param>
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt == null || evt.button != 0 || Scope.Owner.Node?.Node == null)
            {
                return;
            }

            module.SelectNode(Scope.Owner.Node.Node);
            evt.StopImmediatePropagation();
        }
    }

    /// <summary>
    /// Draws graph relations with native UI Toolkit Painter2D content.
    /// </summary>
}
