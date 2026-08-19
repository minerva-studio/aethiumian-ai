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
    internal sealed class GraphEdgeLayerElement : VisualElement
    {
        private readonly GraphCanvasAppearance appearance;
        private GraphPresentation presentation;
        private IReadOnlyList<GraphPortDescriptor> ports = Array.Empty<GraphPortDescriptor>();
        private TreeNode selectedNode;
        private GraphPresentationRelation selectedRelation;
        private readonly List<GraphPresentationRelation> labeledRelations = new();
        private readonly List<Label> edgeLabels = new();

        /// <summary>
        /// Initializes an edge layer.
        /// </summary>
        internal GraphEdgeLayerElement(GraphCanvasAppearance appearance)
        {
            this.appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
            generateVisualContent += DrawEdges;
        }

        /// <summary>Gets the canvas-owned appearance used by this painter.</summary>
        internal GraphCanvasAppearance Appearance => appearance;

        /// <summary>Gets the currently selected authored edge.</summary>
        internal GraphPresentationRelation SelectedRelation => selectedRelation;

        /// <summary>
        /// Replaces the displayed topology.
        /// </summary>
        internal void SetTopology(GraphTopology topology)
        {
            GraphPresentation value = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(value);
            SetPresentation(value, Array.Empty<GraphPortDescriptor>());
        }

        /// <summary>
        /// Replaces the displayed semantic presentation.
        /// </summary>
        /// <param name="value">The semantic presentation to draw.</param>
        internal void SetPresentation(GraphPresentation value, IReadOnlyList<GraphPortDescriptor> valuePorts)
        {
            presentation = value;
            ports = valuePorts ?? Array.Empty<GraphPortDescriptor>();
            selectedRelation = null;
            Clear();
            labeledRelations.Clear();
            edgeLabels.Clear();
            if (presentation != null)
            {
                foreach (GraphPresentationRelation relation in presentation.Relations)
                {
                    if (!relation.Target.IsValid || string.IsNullOrEmpty(relation.Label) || IsAttachedInternal(relation))
                    {
                        continue;
                    }

                    Label label = new(GetDisplayLabel(relation));
                    label.AddToClassList("ai-editor-graph-edge-label");
                    label.EnableInClassList("ai-editor-graph-edge-label-disabled", relation.IsVisuallyDisabled);
                    label.pickingMode = PickingMode.Ignore;
                    GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out Vector2 to);
                    Vector2 labelPosition = GetLabelPosition(relation, from, to);

                    label.style.position = UIPosition.Absolute;
                    label.style.left = labelPosition.x;
                    label.style.top = labelPosition.y;
                    label.style.display = relation.IsVisibleFor(selectedNode)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                    Add(label);
                    labeledRelations.Add(relation);
                    edgeLabels.Add(label);
                }
            }

            MarkDirtyRepaint();
        }

        /// <summary>Updates contextual relation visibility from the window's authoritative selection.</summary>
        internal void SetSelectedNode(TreeNode value)
        {
            selectedNode = value;
            int count = Mathf.Min(labeledRelations.Count, edgeLabels.Count);
            for (int index = 0; index < count; index++)
            {
                edgeLabels[index].text = GetDisplayLabel(labeledRelations[index]);
                edgeLabels[index].style.display = labeledRelations[index].IsVisibleFor(selectedNode)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            MarkDirtyRepaint();
        }

        /// <summary>Gets the concise default label or selected-member Sequence semantics.</summary>
        private string GetDisplayLabel(GraphPresentationRelation relation)
        {
            bool sourceSelected = selectedNode != null && relation.Source.Item?.Node?.Node == selectedNode;
            if (!sourceSelected)
            {
                return relation.Label;
            }

            return relation.Kind switch
            {
                GraphPresentationRelationKind.SequenceNext => "True · Next",
                GraphPresentationRelationKind.SequenceSuccess => "True · Success",
                _ => relation.Label,
            };
        }

        /// <summary>
        /// Repositions edge labels after a node has moved in the canvas.
        /// </summary>
        internal void RefreshLabelPositions()
        {
            int count = Mathf.Min(labeledRelations.Count, edgeLabels.Count);
            for (int i = 0; i < count; i++)
            {
                GraphPresentationRelation relation = labeledRelations[i];
                Label label = edgeLabels[i];
                GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out Vector2 to);
                Vector2 labelPosition = GetLabelPosition(relation, from, to);

                label.style.left = labelPosition.x;
                label.style.top = labelPosition.y;
            }
        }

        /// <summary>Gets the rendered source anchor for one authored presentation relation.</summary>
        internal Vector2 GetSourceAnchor(GraphPresentationRelation relation)
        {
            if (relation == null)
            {
                return Vector2.zero;
            }

            GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out _);
            return from;
        }

        /// <summary>Gets the rendered target anchor for one presentation relation.</summary>
        internal Vector2 GetTargetAnchor(GraphPresentationRelation relation)
        {
            if (relation == null)
            {
                return Vector2.zero;
            }

            GetAnchors(relation, GetParallelOffset(relation), out _, out Vector2 to);
            return to;
        }

        /// <summary>Gets the ordinary source anchor for a semantic presentation endpoint.</summary>
        internal Vector2 GetSourceAnchor(GraphPresentationEndpoint endpoint)
        {
            Rect bounds = GetBounds(endpoint);
            return bounds.position + new Vector2(bounds.width * 0.5f, bounds.height);
        }

        /// <summary>Gets the source anchor used by one authored port, including unoccupied slots.</summary>
        internal Vector2 GetSourceAnchor(GraphPortDescriptor port)
        {
            if (port.AnchorKind == GraphPortAnchorKind.DecisionPrepend)
            {
                return GraphDecisionOrderStripElement.GetPrependAnchor(port.Source.Item);
            }

            if (port.AnchorKind == GraphPortAnchorKind.DecisionOption)
            {
                return GraphDecisionOrderStripElement.GetOptionAnchor(port.Source.Item, port.CollectionIndex);
            }

            if (port.AnchorKind == GraphPortAnchorKind.DecisionAppend)
            {
                int count = (port.Source.Item.Node.Node as Decision)?.events?.Length ?? 0;
                return GraphDecisionOrderStripElement.GetAppendAnchor(port.Source.Item, count);
            }

            Rect bounds = GetBounds(port.Source);
            if (port.AnchorKind == GraphPortAnchorKind.Service)
            {
                return GetServiceSource(port.Source, bounds);
            }

            if (port.AnchorKind == GraphPortAnchorKind.ConditionPredicate)
            {
                return bounds.position + new Vector2(bounds.width * 0.5f, GraphPresentationMetrics.ConditionHeader);
            }

            if (port.AnchorKind == GraphPortAnchorKind.ConditionTrue)
            {
                return bounds.position + new Vector2(bounds.width * 0.25f, bounds.height);
            }

            if (port.AnchorKind == GraphPortAnchorKind.ConditionFalse)
            {
                return bounds.position + new Vector2(bounds.width * 0.75f, bounds.height);
            }

            if (port.AnchorKind == GraphPortAnchorKind.DistributedOutput && port.OutputCount > 0)
            {
                return bounds.position + new Vector2(
                    bounds.width * (port.OutputIndex + 1f) / (port.OutputCount + 1f),
                    bounds.height);
            }

            if (port.Relation != null)
            {
                GetAnchors(
                    port.Relation,
                    GetParallelOffset(port.Relation),
                    out Vector2 relationSource,
                    out _,
                    overrideAuthoredSource: false);
                return relationSource;
            }

            return bounds.position + new Vector2(bounds.width * 0.5f, bounds.height);
        }

        /// <summary>Selects the nearest visible authored edge within the canvas-space tolerance.</summary>
        internal bool SelectAt(Vector2 point, float tolerance)
        {
            GraphPresentationRelation nearest = null;
            float nearestDistance = tolerance;
            if (presentation != null)
            {
                foreach (GraphPresentationRelation relation in presentation.Relations)
                {
                    bool selectable = relation.Origin != null
                        || relation.Role == GraphPresentationRelationRole.AuthoredTreeHead;
                    if (!selectable || !relation.Target.IsValid || !relation.IsVisibleFor(selectedNode)
                        || IsAttachedInternal(relation))
                    {
                        continue;
                    }

                    GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out Vector2 to);
                    float distance = DistanceToCurve(point, from, to);
                    if (distance <= nearestDistance)
                    {
                        nearest = relation;
                        nearestDistance = distance;
                    }
                }
            }

            bool changed = !ReferenceEquals(selectedRelation, nearest);
            selectedRelation = nearest;
            if (changed)
            {
                MarkDirtyRepaint();
            }

            return nearest != null;
        }

        /// <summary>Clears the presentation-only edge selection.</summary>
        internal void ClearEdgeSelection()
        {
            if (selectedRelation == null)
            {
                return;
            }

            selectedRelation = null;
            MarkDirtyRepaint();
        }

        private void DrawEdges(MeshGenerationContext context)
        {
            if (presentation == null || context.painter2D == null)
            {
                return;
            }

            Painter2D painter = context.painter2D;
            foreach (GraphPresentationRelation relation in presentation.Relations)
            {
                if (!relation.Target.IsValid || !relation.IsVisibleFor(selectedNode) || IsAttachedInternal(relation))
                {
                    continue;
                }

                GetAnchors(relation, GetParallelOffset(relation), out Vector2 from, out Vector2 to);

                if (ReferenceEquals(relation, selectedRelation))
                {
                    DrawCurve(painter, from, to, new Color(0.22f, 0.68f, 1f, 0.85f), appearance.AuthoredLineWidth + 5f, horizontal: false);
                }

                Color color = GetRenderedColor(relation);

                if (relation.Role == GraphPresentationRelationRole.DerivedCompletion)
                {
                    GraphDecisionScope decisionScope = GetOwningDecisionScope(relation);
                    if (relation.Kind == GraphPresentationRelationKind.DecisionSuccess && decisionScope != null)
                    {
                        DrawDecisionSuccess(painter, decisionScope, from, to, color);
                        continue;
                    }

                    GraphSequenceScope sequenceScope = GetOwningSequenceScope(relation);
                    if (relation.Kind == GraphPresentationRelationKind.SequenceFailure && sequenceScope != null)
                    {
                        DrawSequenceFailure(painter, sequenceScope, from, to, color);
                        continue;
                    }

                    GraphLoopScope loopScope = GetOwningLoopScope(relation);
                    if (relation.Kind == GraphPresentationRelationKind.LoopExit
                        && loopScope != null
                        && loopScope.Mode != Loop.LoopType.doWhile)
                    {
                        DrawLoopExit(painter, loopScope, from, to, color);
                        continue;
                    }

                    DrawPatternedCurve(
                        painter,
                        from,
                        to,
                        color,
                        appearance.DerivedLineWidth,
                        appearance.DerivedMarkLength,
                        appearance.DerivedGapLength);
                    DrawHollowArrowHead(painter, from, to, color);
                    continue;
                }

                if (relation.Role == GraphPresentationRelationRole.DerivedControl)
                {
                    if (relation.Kind == GraphPresentationRelationKind.LoopRepeat && to.y < from.y)
                    {
                        DrawLoopBack(painter, relation, from, to, color);
                        continue;
                    }

                    if (relation.Kind == GraphPresentationRelationKind.ForEachRepeat && to.y < from.y)
                    {
                        DrawForEachBack(painter, relation, from, to, color);
                        continue;
                    }

                    DrawPatternedCurve(
                        painter,
                        from,
                        to,
                        color,
                        appearance.DerivedLineWidth,
                        appearance.ControlMarkLength,
                        appearance.ControlGapLength);
                    DrawHollowArrowHead(painter, from, to, color);
                    continue;
                }

                if (relation.Role == GraphPresentationRelationRole.PlaceholderHint)
                {
                    DrawPatternedCurve(
                        painter,
                        from,
                        to,
                        color,
                        appearance.PlaceholderLineWidth,
                        appearance.PlaceholderMarkLength,
                        appearance.PlaceholderGapLength);
                    continue;
                }

                switch (relation.Kind)
                {
                    case GraphPresentationRelationKind.Entrance:
                    case GraphPresentationRelationKind.Structural:
                    case GraphPresentationRelationKind.SequenceStart:
                    case GraphPresentationRelationKind.SequenceNext:
                    case GraphPresentationRelationKind.AggregateStart:
                    case GraphPresentationRelationKind.AggregateNext:
                    case GraphPresentationRelationKind.FlowComplete:
                    case GraphPresentationRelationKind.DecisionBranch:
                    case GraphPresentationRelationKind.ProbabilityBranch:
                    case GraphPresentationRelationKind.ParallelBranch:
                    case GraphPresentationRelationKind.ParallelComplete:
                    case GraphPresentationRelationKind.ForEachCheck:
                    case GraphPresentationRelationKind.ForEachBody:
                    case GraphPresentationRelationKind.ForEachRepeat:
                    case GraphPresentationRelationKind.ForEachExit:
                    case GraphPresentationRelationKind.ConditionTrue:
                    case GraphPresentationRelationKind.ConditionFalse:
                    case GraphPresentationRelationKind.LoopCondition:
                    case GraphPresentationRelationKind.LoopBody:
                    case GraphPresentationRelationKind.LoopRepeat:
                    case GraphPresentationRelationKind.LoopExit:
                        DrawCurve(painter, from, to, color, appearance.AuthoredLineWidth, horizontal: false);
                        break;
                    case GraphPresentationRelationKind.Raw:
                        DrawDotted(
                            painter,
                            from,
                            to,
                            color,
                            appearance.AuthoredLineWidth,
                            appearance.PlaceholderMarkLength,
                            appearance.PlaceholderGapLength);
                        break;
                    default:
                        DrawDashed(
                            painter,
                            from,
                            to,
                            color,
                            appearance.AuthoredLineWidth,
                            appearance.DerivedMarkLength,
                            appearance.DerivedGapLength);
                        break;
                }

                DrawArrowHead(painter, from, to, color);
            }
        }

        /// <summary>Resolves the single color shared by one relation's curve, arrow, pattern, and label.</summary>
        internal Color GetRenderedColor(GraphPresentationRelation relation)
        {
            Color color = relation.Kind switch
            {
                GraphPresentationRelationKind.Entrance => appearance.EntranceBoundary,
                GraphPresentationRelationKind.Exit => appearance.ExitBoundary,
                GraphPresentationRelationKind.Service => appearance.ServiceEdge,
                GraphPresentationRelationKind.Raw => appearance.RawEdge,
                GraphPresentationRelationKind.SequenceStart
                    or GraphPresentationRelationKind.SequenceNext
                    or GraphPresentationRelationKind.SequenceFailure
                    or GraphPresentationRelationKind.SequenceSuccess
                    or GraphPresentationRelationKind.AggregateStart
                    or GraphPresentationRelationKind.AggregateNext
                    or GraphPresentationRelationKind.AggregateComplete
                    or GraphPresentationRelationKind.FlowComplete => appearance.FlowEdge,
                GraphPresentationRelationKind.DecisionBranch
                    or GraphPresentationRelationKind.DecisionSuccess
                    or GraphPresentationRelationKind.DecisionFailure
                    or GraphPresentationRelationKind.ConditionTrue
                    or GraphPresentationRelationKind.ConditionFalse => appearance.BranchEdge,
                GraphPresentationRelationKind.ProbabilityBranch => appearance.ProbabilityEdge,
                GraphPresentationRelationKind.ParallelBranch
                    or GraphPresentationRelationKind.ParallelComplete => appearance.ParallelEdge,
                GraphPresentationRelationKind.ForEachCheck
                    or GraphPresentationRelationKind.ForEachBody
                    or GraphPresentationRelationKind.ForEachRepeat
                    or GraphPresentationRelationKind.ForEachExit
                    or GraphPresentationRelationKind.LoopCondition
                    or GraphPresentationRelationKind.LoopBody
                    or GraphPresentationRelationKind.LoopRepeat
                    or GraphPresentationRelationKind.LoopExit => appearance.LoopEdge,
                _ => appearance.StructuralEdge,
            };

            if (relation.Kind is not (GraphPresentationRelationKind.Service
                or GraphPresentationRelationKind.Raw
                or GraphPresentationRelationKind.Entrance
                or GraphPresentationRelationKind.Exit)
                && (relation.Role is not GraphPresentationRelationRole.PlaceholderHint || relation.VisualOwner != null))
            {
                color = appearance.GetRelationColor(relation);
            }

            if (relation.IsVisuallyDisabled)
            {
                color.a *= appearance.DisabledAlpha;
            }

            return color;
        }

        /// <summary>Suppresses the zero-length visual curve that is represented by an attached decorator badge.</summary>
        private bool IsDecoratorInternal(GraphPresentationRelation relation)
        {
            GraphDecoratorStack stack = presentation?.FindDecoratorStack(relation?.Source.Item?.TargetUUID ?? UUID.Empty);
            return stack != null && stack.Badges.Contains(relation.Source.Item) && (ReferenceEquals(stack.Anchor, relation.Target.Item)
                || stack.Badges.Contains(relation.Target.Item));
        }

        /// <summary>Suppresses the internal edge represented by an Entrance attached directly above its Head card.</summary>
        private bool IsAttachedInternal(GraphPresentationRelation relation)
        {
            return IsDecoratorInternal(relation)
                || (relation?.Kind == GraphPresentationRelationKind.Entrance
                    && relation.Source.Item == presentation?.Entrance
                    && relation.Target.Item != null);
        }

        private float GetParallelOffset(GraphPresentationRelation relation)
        {
            if (presentation == null)
            {
                return 0f;
            }

            int occurrence = 0;
            foreach (GraphPresentationRelation candidate in presentation.Relations)
            {
                if (ReferenceEquals(candidate, relation))
                {
                    break;
                }

                if (candidate.Source == relation.Source && candidate.Target == relation.Target && candidate.Kind == relation.Kind)
                {
                    occurrence++;
                }
            }

            return occurrence * 7f;
        }

        private void GetAnchors(
            GraphPresentationRelation relation,
            float offset,
            out Vector2 from,
            out Vector2 to,
            bool overrideAuthoredSource = true)
        {
            Rect sourceBounds = GetBounds(relation.Source);
            Rect targetBounds = GetBounds(relation.Target);
            Vector2 sourceSize = sourceBounds.size;
            Vector2 targetSize = targetBounds.size;

            if (relation.Kind == GraphPresentationRelationKind.SequenceFailure)
            {
                sourceBounds = GetVisibleBounds(relation.Source);
                targetBounds = GetVisibleBounds(relation.Target);
                from = new Vector2(sourceBounds.xMax, sourceBounds.center.y + offset);
                to = new Vector2(targetBounds.xMax, targetBounds.center.y + offset);
                return;
            }

            GraphLoopScope sideRailLoop = GetOwningLoopScope(relation);
            if (relation.Kind == GraphPresentationRelationKind.LoopExit
                && sideRailLoop != null
                && sideRailLoop.Mode != Loop.LoopType.doWhile)
            {
                sourceBounds = GetVisibleBounds(relation.Source);
                targetBounds = GetVisibleBounds(relation.Target);
                from = new Vector2(sourceBounds.xMax, sourceBounds.center.y + offset);
                to = new Vector2(targetBounds.xMax, targetBounds.center.y + offset);
                return;
            }

            if (relation.Kind is GraphPresentationRelationKind.LoopRepeat
                or GraphPresentationRelationKind.ForEachRepeat)
            {
                sourceBounds = GetVisibleBounds(relation.Source);
                targetBounds = GetVisibleBounds(relation.Target);
                from = new Vector2(sourceBounds.xMin, sourceBounds.center.y + offset);
                to = new Vector2(targetBounds.xMin, targetBounds.center.y + offset);
                return;
            }

            if (relation.Kind == GraphPresentationRelationKind.Service)
            {
                from = GetServiceSource(relation.Source, sourceBounds) + new Vector2(0f, offset);
                to = targetBounds.position + new Vector2(0f, targetSize.y * 0.5f + offset);
                if (overrideAuthoredSource)
                {
                    OverrideAuthoredSource(relation, ref from);
                }
                return;
            }

            if (relation.Kind == GraphPresentationRelationKind.DecisionFailure)
            {
                from = sourceBounds.position + new Vector2(sourceSize.x, sourceSize.y * 0.5f + offset);
                to = targetBounds.position + new Vector2(0f, targetSize.y * 0.5f + offset);
                if (overrideAuthoredSource)
                {
                    OverrideAuthoredSource(relation, ref from);
                }
                return;
            }

            float sourceX = sourceSize.x * 0.5f;
            if (relation.Source.Anchor == GraphPresentationAnchorKind.Output
                && IsBranchingRelation(relation.Kind)
                && relation.Source.Item.Node != null
                && relation.Source.Item.Node.Shape is GraphNodeShape.Flow or GraphNodeShape.Branch)
            {
                GetStructuralOutputSlot(relation, out int index, out int count);
                sourceX = sourceSize.x * (index + 1f) / (count + 1f);
            }

            from = sourceBounds.position + new Vector2(sourceX + offset, sourceSize.y);
            GraphLoopScope loopScope = GetOwningLoopScope(relation);
            to = relation.Kind == GraphPresentationRelationKind.LoopExit
                && loopScope != null
                && loopScope.Mode != Loop.LoopType.doWhile
                ? targetBounds.position + new Vector2(targetSize.x, targetSize.y * 0.5f + offset)
                : targetBounds.position + new Vector2(targetSize.x * 0.5f + offset, 0f);
            if (overrideAuthoredSource)
            {
                OverrideAuthoredSource(relation, ref from);
            }
        }

        /// <summary>Gets the shared Service source, keeping compound Condition services in their header.</summary>
        private static Vector2 GetServiceSource(GraphPresentationEndpoint source, Rect bounds)
        {
            return source.Item?.ConditionScope != null
                ? bounds.position + new Vector2(bounds.width, GraphPresentationMetrics.ConditionHeader * 0.5f)
                : bounds.position + new Vector2(bounds.width, bounds.height * 0.5f);
        }

        /// <summary>Aligns authored edge sources with their field-level or ordered canvas port.</summary>
        private void OverrideAuthoredSource(GraphPresentationRelation relation, ref Vector2 from)
        {
            if (relation?.Role != GraphPresentationRelationRole.AuthoredReference || relation.Origin == null)
            {
                return;
            }

            GraphPortDescriptor port = ports.FirstOrDefault(candidate => candidate.ContainsOrigin(relation.Origin));
            if (port != null)
            {
                from = GetSourceAnchor(port);
            }
        }

        private static Rect GetBounds(GraphPresentationEndpoint endpoint)
        {
            if (endpoint.Anchor == GraphPresentationAnchorKind.FlowComplete)
            {
                GraphFlowScope scope = endpoint.Item.FlowScope;
                return new Rect(scope.CompletionPosition, scope.CompletionSize);
            }

            return new Rect(endpoint.Item.Position, endpoint.Item.Size);
        }

        /// <summary>Gets the complete visible bounds of an endpoint, including attached decorator badges.</summary>
        private Rect GetVisibleBounds(GraphPresentationEndpoint endpoint)
        {
            Rect bounds = GetBounds(endpoint);
            if (endpoint.Anchor == GraphPresentationAnchorKind.FlowComplete)
            {
                return bounds;
            }

            GraphDecoratorStack stack = presentation?.FindDecoratorStack(endpoint.Item?.TargetUUID ?? UUID.Empty);
            if (stack == null)
            {
                return bounds;
            }

            bounds = Encapsulate(bounds, stack.Anchor);
            foreach (GraphPresentationItem badge in stack.Badges)
            {
                bounds = Encapsulate(bounds, badge);
            }

            return bounds;
        }

        /// <summary>Expands a rectangle to contain one presentation item.</summary>
        private static Rect Encapsulate(Rect bounds, GraphPresentationItem item)
        {
            if (item == null)
            {
                return bounds;
            }

            Rect itemBounds = new(item.Position, item.Size);
            float xMin = Mathf.Min(bounds.xMin, itemBounds.xMin);
            float yMin = Mathf.Min(bounds.yMin, itemBounds.yMin);
            float xMax = Mathf.Max(bounds.xMax, itemBounds.xMax);
            float yMax = Mathf.Max(bounds.yMax, itemBounds.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void GetStructuralOutputSlot(GraphPresentationRelation relation, out int index, out int count)
        {
            index = 0;
            count = 0;
            if (presentation == null)
            {
                return;
            }

            foreach (GraphPresentationRelation candidate in presentation.Relations)
            {
                if (candidate.Source != relation.Source || !IsBranchingRelation(candidate.Kind) || !candidate.Target.IsValid
                    || !candidate.IsVisibleFor(selectedNode))
                {
                    continue;
                }

                if (ReferenceEquals(candidate, relation))
                {
                    index = count;
                }

                count++;
            }
        }

        private static bool IsBranchingRelation(GraphPresentationRelationKind kind)
        {
            return kind is GraphPresentationRelationKind.Structural
                or GraphPresentationRelationKind.SequenceStart
                or GraphPresentationRelationKind.SequenceNext
                or GraphPresentationRelationKind.AggregateStart
                or GraphPresentationRelationKind.AggregateNext
                or GraphPresentationRelationKind.DecisionBranch
                or GraphPresentationRelationKind.ProbabilityBranch
                or GraphPresentationRelationKind.ParallelBranch
                or GraphPresentationRelationKind.ForEachCheck
                or GraphPresentationRelationKind.ForEachBody
                or GraphPresentationRelationKind.ForEachRepeat
                or GraphPresentationRelationKind.ForEachExit
                or GraphPresentationRelationKind.ConditionTrue
                or GraphPresentationRelationKind.ConditionFalse
                or GraphPresentationRelationKind.LoopCondition
                or GraphPresentationRelationKind.LoopBody
                or GraphPresentationRelationKind.LoopRepeat
                or GraphPresentationRelationKind.LoopExit;
        }

        /// <summary>Positions a repeat label beside its side rail instead of across the Body.</summary>
        private Vector2 GetLabelPosition(GraphPresentationRelation relation, Vector2 from, Vector2 to)
        {
            if (relation.Kind == GraphPresentationRelationKind.DecisionSuccess)
            {
                GraphDecisionScope scope = GetOwningDecisionScope(relation);
                if (scope != null)
                {
                    return new Vector2(from.x + 4f, scope.SuccessRailY - 14f);
                }
            }

            if (relation.Kind == GraphPresentationRelationKind.SequenceFailure)
            {
                GraphSequenceScope scope = GetOwningSequenceScope(relation);
                if (scope != null)
                {
                    return new Vector2(scope.FailureRailX + 4f, from.y - 14f);
                }
            }

            if (relation.Kind == GraphPresentationRelationKind.LoopRepeat
                && relation.Role == GraphPresentationRelationRole.DerivedControl
                && to.y < from.y)
            {
                return new Vector2(GetLoopReturnRailX(relation, from, to) + 4f, (from.y + to.y) * 0.5f - 7f);
            }

            GraphLoopScope loopScope = GetOwningLoopScope(relation);
            if (relation.Kind == GraphPresentationRelationKind.LoopExit
                && relation.Role == GraphPresentationRelationRole.DerivedCompletion
                && loopScope != null
                && loopScope.Mode != Loop.LoopType.doWhile)
            {
                return new Vector2(loopScope.ExitRailX + 4f, (from.y + to.y) * 0.5f - 7f);
            }

            return (from + to) * 0.5f;
        }

        /// <summary>Draws a derived repeat path outside the lightweight Body frame.</summary>
        private void DrawLoopBack(
            Painter2D painter,
            GraphPresentationRelation relation,
            Vector2 from,
            Vector2 to,
            Color color)
        {
            float railX = GetLoopReturnRailX(relation, from, to);
            Vector2 lowerCorner = new(railX, from.y);
            Vector2 upperCorner = new(railX, to.y);
            DrawDashed(painter, from, lowerCorner, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, lowerCorner, upperCorner, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, upperCorner, to, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawHollowArrowHead(painter, upperCorner, to, color);
        }

        /// <summary>Draws the ForEach Next Item path outside its free Body frame.</summary>
        private void DrawForEachBack(
            Painter2D painter,
            GraphPresentationRelation relation,
            Vector2 from,
            Vector2 to,
            Color color)
        {
            GraphForEachScope scope = presentation?.Find(relation.TargetUUID)?.ForEachScope;
            float railX = scope == null ? Mathf.Min(from.x, to.x) - 28f : scope.ReturnRailX;
            Vector2 lowerCorner = new(railX, from.y);
            Vector2 upperCorner = new(railX, to.y);
            DrawDashed(painter, from, lowerCorner, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, lowerCorner, upperCorner, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, upperCorner, to, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawHollowArrowHead(painter, upperCorner, to, color);
        }

        /// <summary>Resolves a stable repeat rail outside its owning Loop Body frame.</summary>
        private float GetLoopReturnRailX(GraphPresentationRelation relation, Vector2 from, Vector2 to)
        {
            GraphLoopScope scope = GetOwningLoopScope(relation);
            return scope == null
                ? Mathf.Min(from.x, to.x) - 28f
                : scope.ReturnRailX;
        }

        /// <summary>Draws a derived Loop exit around the right edge of its Body frame.</summary>
        private void DrawLoopExit(
            Painter2D painter,
            GraphLoopScope scope,
            Vector2 from,
            Vector2 to,
            Color color)
        {
            Vector2 upperCorner = new(scope.ExitRailX, from.y);
            Vector2 lowerCorner = new(scope.ExitRailX, to.y);
            DrawDashed(painter, from, upperCorner, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, upperCorner, lowerCorner, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, lowerCorner, to, color, appearance.DerivedLineWidth, appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawHollowArrowHead(painter, lowerCorner, to, color);
        }

        /// <summary>Resolves the Loop scope that owns a derived relation.</summary>
        private GraphLoopScope GetOwningLoopScope(GraphPresentationRelation relation)
        {
            return presentation?.Find(relation.TargetUUID)?.LoopScope;
        }

        /// <summary>Draws one Decision return into the shared success merge rail.</summary>
        private void DrawDecisionSuccess(
            Painter2D painter,
            GraphDecisionScope scope,
            Vector2 from,
            Vector2 to,
            Color color)
        {
            Vector2 branchCorner = new(from.x, scope.SuccessRailY);
            Vector2 mergeCorner = new(to.x, scope.SuccessRailY);
            DrawDashed(painter, from, branchCorner, color, appearance.DerivedLineWidth,
                appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, branchCorner, mergeCorner, color, appearance.DerivedLineWidth,
                appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, mergeCorner, to, color, appearance.DerivedLineWidth,
                appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawHollowArrowHead(painter, mergeCorner, to, color);
        }

        /// <summary>Resolves the Decision scope that owns a derived completion relation.</summary>
        private GraphDecisionScope GetOwningDecisionScope(GraphPresentationRelation relation)
        {
            return presentation?.Find(relation.TargetUUID)?.DecisionScope;
        }

        /// <summary>Draws one Sequence failure into its shared short-circuit rail.</summary>
        private void DrawSequenceFailure(
            Painter2D painter,
            GraphSequenceScope scope,
            Vector2 from,
            Vector2 to,
            Color color)
        {
            Vector2 branchCorner = new(scope.FailureRailX, from.y);
            Vector2 mergeCorner = new(scope.FailureRailX, to.y);
            DrawDashed(painter, from, branchCorner, color, appearance.DerivedLineWidth,
                appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, branchCorner, mergeCorner, color, appearance.DerivedLineWidth,
                appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawDashed(painter, mergeCorner, to, color, appearance.DerivedLineWidth,
                appearance.DerivedMarkLength, appearance.DerivedGapLength);
            DrawHollowArrowHead(painter, mergeCorner, to, color);
        }

        /// <summary>Resolves the Sequence scope that owns one derived short-circuit relation.</summary>
        private GraphSequenceScope GetOwningSequenceScope(GraphPresentationRelation relation)
        {
            return presentation?.Find(relation.TargetUUID)?.SequenceScope;
        }

        private static void DrawCurve(Painter2D painter, Vector2 from, Vector2 to, Color color, float width, bool horizontal)
        {
            painter.strokeColor = color;
            painter.lineWidth = width;
            Vector2 firstControl;
            Vector2 secondControl;
            if (horizontal)
            {
                float distance = Mathf.Max(36f, Mathf.Abs(to.x - from.x) * 0.5f);
                firstControl = from + Vector2.right * distance;
                secondControl = to + Vector2.left * distance;
            }
            else
            {
                float distance = Mathf.Max(36f, Mathf.Abs(to.y - from.y) * 0.5f);
                firstControl = from + Vector2.up * distance;
                secondControl = to + Vector2.down * distance;
            }

            painter.BeginPath();
            painter.MoveTo(from);
            painter.BezierCurveTo(firstControl, secondControl, to);
            painter.Stroke();
        }

        /// <summary>Approximates pointer distance to the vertical cubic used by authored edges.</summary>
        private static float DistanceToCurve(Vector2 point, Vector2 from, Vector2 to)
        {
            float controlDistance = Mathf.Max(36f, Mathf.Abs(to.y - from.y) * 0.5f);
            Vector2 firstControl = from + Vector2.up * controlDistance;
            Vector2 secondControl = to + Vector2.down * controlDistance;
            const int segments = 24;
            float nearest = float.MaxValue;
            Vector2 previous = from;
            for (int index = 1; index <= segments; index++)
            {
                float t = index / (float)segments;
                float inverse = 1f - t;
                Vector2 current = inverse * inverse * inverse * from
                    + 3f * inverse * inverse * t * firstControl
                    + 3f * inverse * t * t * secondControl
                    + t * t * t * to;
                nearest = Mathf.Min(nearest, DistanceToSegment(point, previous, current));
                previous = current;
            }

            return nearest;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, start);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return Vector2.Distance(point, start + segment * t);
        }

        private static void DrawArrowHead(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = (to - from).normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector2 normal = new(-direction.y, direction.x);
            Vector2 basePoint = to - direction * 8f;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(to);
            painter.LineTo(basePoint + normal * 4f);
            painter.LineTo(basePoint - normal * 4f);
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>Draws an unfilled arrowhead for a derived relation.</summary>
        private void DrawHollowArrowHead(Painter2D painter, Vector2 from, Vector2 to, Color color)
        {
            Vector2 direction = (to - from).normalized;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            Vector2 normal = new(-direction.y, direction.x);
            Vector2 basePoint = to - direction * 8f;
            painter.strokeColor = color;
            painter.lineWidth = appearance.DerivedLineWidth;
            painter.BeginPath();
            painter.MoveTo(basePoint + normal * 4f);
            painter.LineTo(to);
            painter.LineTo(basePoint - normal * 4f);
            painter.Stroke();
        }

        /// <summary>Draws a sampled Bezier curve using a repeated mark-and-gap pattern.</summary>
        private static void DrawPatternedCurve(
            Painter2D painter,
            Vector2 from,
            Vector2 to,
            Color color,
            float width,
            float markLength,
            float gapLength)
        {
            float controlDistance = Mathf.Max(36f, Mathf.Abs(to.y - from.y) * 0.5f);
            Vector2 firstControl = from + Vector2.up * controlDistance;
            Vector2 secondControl = to + Vector2.down * controlDistance;
            const int sampleCount = 48;
            float patternLength = markLength + gapLength;
            float traversed = 0f;
            Vector2 previous = from;
            for (int sample = 1; sample <= sampleCount; sample++)
            {
                float t = sample / (float)sampleCount;
                Vector2 current = EvaluateBezier(from, firstControl, secondControl, to, t);
                float segmentLength = Vector2.Distance(previous, current);
                if (Mathf.Repeat(traversed + segmentLength * 0.5f, patternLength) < markLength)
                {
                    DrawSegment(painter, previous, current, color, width);
                }

                traversed += segmentLength;
                previous = current;
            }
        }

        /// <summary>Evaluates a cubic Bezier curve at the requested normalized position.</summary>
        private static Vector2 EvaluateBezier(
            Vector2 start,
            Vector2 firstControl,
            Vector2 secondControl,
            Vector2 end,
            float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * start
                + 3f * inverse * inverse * t * firstControl
                + 3f * inverse * t * t * secondControl
                + t * t * t * end;
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

        private static void DrawDashed(
            Painter2D painter,
            Vector2 from,
            Vector2 to,
            Color color,
            float width,
            float dash,
            float gap)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Vector2 direction = delta / length;
            for (float distance = 0f; distance < length; distance += dash + gap)
            {
                float endDistance = Mathf.Min(distance + dash, length);
                DrawSegment(painter, from + direction * distance, from + direction * endDistance, color, width);
            }
        }

        /// <summary>
        /// Draws a dotted edge for an optional raw reference.
        /// </summary>
        private static void DrawDotted(
            Painter2D painter,
            Vector2 from,
            Vector2 to,
            Color color,
            float width,
            float dotLength,
            float gap)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Vector2 direction = delta / length;
            for (float distance = 0f; distance < length; distance += dotLength + gap)
            {
                float endDistance = Mathf.Min(distance + dotLength, length);
                DrawSegment(painter, from + direction * distance, from + direction * endDistance, color, width);
            }
        }
    }
}
