using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UIPosition = UnityEngine.UIElements.Position;

namespace Aethiumian.AI.Editor
{
    internal sealed class GraphReferenceProxyElement : VisualElement, IGraphGeometryElement, IGraphSelectionElement
    {
        private readonly GraphCanvasElement canvas;
        private readonly GraphEditorModule module;
        private readonly GraphPresentationItem item;
        private readonly Label title;
        private bool selected;

        internal GraphReferenceProxyElement(GraphCanvasElement canvas, GraphEditorModule module, GraphPresentationItem item, Vector2 position)
        {
            this.canvas = canvas;
            this.module = module;
            this.item = item;
            name = $"ai-editor-graph-reference-{item.TargetUUID}";
            AddToClassList("ai-editor-graph-reference");
            AddToClassList(item.Kind == GraphPresentationKind.Missing ? "ai-editor-graph-reference-missing" : "ai-editor-graph-reference-proxy");
            style.position = UIPosition.Absolute;
            style.left = position.x;
            style.top = position.y;
            style.width = item.Size.x;
            style.height = item.Size.y;

            title = new Label(item.Node?.DisplayName ?? item.Warning ?? "Missing reference");
            title.AddToClassList("ai-editor-graph-reference-title");
            Add(title);
            tooltip = item.Warning;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        /// <summary>Gets the actual node selected by this proxy, if one exists.</summary>
        internal TreeNode TargetNode => item.Node?.Node;

        /// <summary>Updates the visual selection state.</summary>
        internal void SetSelected(bool value)
        {
            selected = value;
            EnableInClassList("ai-editor-graph-reference-selected", value);
        }

        void IGraphGeometryElement.RefreshGeometry()
        {
            GraphElementGeometry.ApplyRect(this, new Rect(item.Position, item.Size));
        }

        void IGraphSelectionElement.RefreshSelection(GraphSelectionSnapshot selection)
        {
            SetSelected(TargetNode != null && selection.Contains(TargetNode.uuid));
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0 && TargetNode != null)
            {
                module.SelectNode(TargetNode);
                evt.StopPropagation();
            }
        }
    }

    /// <summary>
    /// Draws a lightweight, non-interactive boundary around one Service structural subtree.
    /// </summary>
}
