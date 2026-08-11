using System;
using System.Collections.Generic;
using Aethiumian.AI.Nodes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Describes the canvas glyph used to distinguish authored port commands.</summary>
    internal enum GraphPortVisualShape
    {
        Solid,
        Ring,
        RingWithPlus,
    }

    /// <summary>Draws the canvas-only visual affordances for authored source and node input ports.</summary>
    internal sealed class GraphPortLayerElement : VisualElement
    {
        private const float Radius = 5f;
        private static readonly Color DefaultPortColor = new(0.2f, 0.75f, 1f, 0.95f);
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
            foreach (GraphPortDescriptor port in ports)
            {
                DrawSourcePort(painter, GetSourcePosition(port), GetVisualShape(port.Operation), GetSourceColor(port));
            }

            painter.fillColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
            HashSet<UUID> inputNodes = new();
            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                GraphPresentationItem item = presentation.Find(node.UUID);
                if (item?.Node == null || item.Parent != null || !inputNodes.Add(node.UUID))
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

        /// <summary>Finds a source port using a screen-stable hit radius converted to graph space.</summary>
        internal GraphPortDescriptor FindSourcePort(Vector2 graphPosition, float graphRadius)
        {
            float radiusSquared = graphRadius * graphRadius;
            GraphPortDescriptor closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (GraphPortDescriptor port in ports)
            {
                float distance = (GetSourcePosition(port) - graphPosition).sqrMagnitude;
                if (distance <= radiusSquared && distance < closestDistance)
                {
                    closest = port;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        /// <summary>Maps an authored mutation command to its distinct canvas affordance.</summary>
        internal static GraphPortVisualShape GetVisualShape(GraphPortOperation operation)
        {
            return operation switch
            {
                GraphPortOperation.Replace => GraphPortVisualShape.Solid,
                GraphPortOperation.Connect => GraphPortVisualShape.Ring,
                GraphPortOperation.Insert => GraphPortVisualShape.RingWithPlus,
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
            };
        }

        /// <summary>Gets the source-port color from its relation family without duplicating USS paint values.</summary>
        internal Color GetSourceColor(GraphPortDescriptor port)
        {
            return port.AnchorKind == GraphPortAnchorKind.Service
                ? edgeLayer?.Appearance.ServiceEdge ?? DefaultPortColor
                : DefaultPortColor;
        }

        /// <summary>Draws one source port without changing the UI hierarchy during repaint.</summary>
        private static void DrawSourcePort(Painter2D painter, Vector2 center, GraphPortVisualShape shape, Color color)
        {
            painter.BeginPath();
            painter.Arc(center, Radius, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
            if (shape == GraphPortVisualShape.Solid)
            {
                painter.fillColor = color;
                painter.Fill();
                return;
            }

            painter.strokeColor = color;
            painter.lineWidth = 2f;
            painter.Stroke();
            if (shape != GraphPortVisualShape.RingWithPlus)
            {
                return;
            }

            painter.strokeColor = color;
            painter.lineWidth = 1.5f;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.MoveTo(center + new Vector2(-2.5f, 0f));
            painter.LineTo(center + new Vector2(2.5f, 0f));
            painter.MoveTo(center + new Vector2(0f, -2.5f));
            painter.LineTo(center + new Vector2(0f, 2.5f));
            painter.Stroke();
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
