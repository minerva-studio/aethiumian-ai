using System;
using System.Collections.Generic;
using Aethiumian.AI.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Draws the canvas-only visual affordances for authored source and node input ports.</summary>
    internal sealed class GraphPortLayerElement : VisualElement
    {
        private const float Radius = 5f;
        private IReadOnlyList<GraphPortDescriptor> ports = Array.Empty<GraphPortDescriptor>();
        private GraphTopology topology;
        private GraphPresentation presentation;
        private GraphEdgeLayerElement edgeLayer;

        internal GraphPortLayerElement()
        {
            generateVisualContent += DrawPorts;
        }

        internal IReadOnlyList<GraphPortDescriptor> Ports => ports;

        internal void SetPorts(
            GraphTopology sourceTopology,
            GraphPresentation value,
            GraphEdgeLayerElement edges,
            IReadOnlyList<GraphPortDescriptor> valuePorts)
        {
            topology = sourceTopology;
            presentation = value;
            edgeLayer = edges;
            ports = valuePorts ?? Array.Empty<GraphPortDescriptor>();
            MarkDirtyRepaint();
        }

        private void DrawPorts(MeshGenerationContext context)
        {
            if (presentation == null || context.painter2D == null)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            painter.fillColor = new Color(0.2f, 0.75f, 1f, 0.95f);
            foreach (GraphPortDescriptor port in ports)
            {
                Vector2 center = GetSourcePosition(port);
                painter.BeginPath();
                painter.Arc(center, Radius, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                painter.Fill();
            }

            painter.fillColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
            HashSet<UUID> inputNodes = new();
            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                GraphPresentationItem item = presentation.Find(node.UUID);
                if (item?.Node == null || !inputNodes.Add(node.UUID))
                {
                    continue;
                }

                Vector2 center = GetTargetPosition(item);
                painter.BeginPath();
                painter.Arc(center, Radius, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                painter.Fill();
            }
        }

        internal Vector2 GetSourcePosition(GraphPortDescriptor port)
        {
            return edgeLayer?.GetSourceAnchor(port) ?? Vector2.zero;
        }

        /// <summary>Gets the target anchor for one real node card.</summary>
        internal static Vector2 GetTargetPosition(GraphPresentationItem item)
        {
            Rect bounds = new(item.Position, item.Size);
            return item.Node.Node is Service
                ? bounds.position + new Vector2(0f, bounds.height * 0.5f)
                : bounds.position + new Vector2(bounds.width * 0.5f, 0f);
        }
    }
}
