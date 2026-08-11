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
    /// <summary>
    /// Native UI Toolkit canvas for the graph topology.
    /// </summary>
    internal sealed class GraphCanvasElement : VisualElement
    {
        internal const float MinimumZoom = 0.05f;
        internal const float MaximumZoom = 2.5f;
        private const float MaximumFitZoom = 1.5f;
        private const float MinimumInitialFrameZoom = 0.45f;
        private const float FramePadding = 48f;
        private const float WheelZoomSensitivity = 0.035f;
        private const float PortHitRadius = 10f;
        private const float ConnectionDragThreshold = 4f;

        private readonly GraphEditorModule module;
        private readonly GraphCanvasAppearance appearance = new();
        private readonly VisualElement content;
        private readonly VisualElement backdropLayer;
        private readonly VisualElement scopeLayer;
        private readonly GraphEdgeLayerElement edgeLayer;
        private readonly VisualElement nodeLayer;
        private readonly VisualElement interactionLayer;
        private readonly GraphPortLayerElement portLayer;
        private readonly GraphConnectionPreviewElement connectionPreview;
        private GraphPresentation presentation;
        private bool panning;
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStart;
        private float zoom = 1f;
        private Vector2 pan;
        private bool fitAllWhenGeometryIsValid;
        private bool initialFrameWhenGeometryIsValid;
        private GraphPortDescriptor pendingConnectionPort;
        private int connectionPointerId = -1;
        private Vector2 connectionStartPointer;
        private bool draggingConnection;

        /// <summary>
        /// Initializes a graph canvas owned by a graph editor module.
        /// </summary>
        /// <param name="module">The owning graph module.</param>
        internal GraphCanvasElement(GraphEditorModule module)
        {
            this.module = module ?? throw new ArgumentNullException(nameof(module));
            name = "ai-editor-graph-canvas";
            AddToClassList("ai-editor-graph-canvas");
            style.flexGrow = 1f;
            style.position = UIPosition.Relative;
            style.overflow = Overflow.Hidden;
            focusable = true;
            generateVisualContent += DrawBackground;

            content = new VisualElement
            {
                name = "ai-editor-graph-content",
            };
            content.AddToClassList("ai-editor-graph-content");
            content.style.position = UIPosition.Absolute;
            content.style.left = 0f;
            content.style.top = 0f;
            content.style.width = 1f;
            content.style.height = 1f;

            backdropLayer = new VisualElement
            {
                name = "ai-editor-graph-backdrop-layer",
            };
            backdropLayer.AddToClassList("ai-editor-graph-backdrop-layer");
            backdropLayer.pickingMode = PickingMode.Ignore;
            backdropLayer.style.position = UIPosition.Absolute;
            backdropLayer.style.left = 0f;
            backdropLayer.style.top = 0f;

            edgeLayer = new GraphEdgeLayerElement(appearance)
            {
                name = "ai-editor-graph-edge-layer",
            };
            edgeLayer.AddToClassList("ai-editor-graph-edge-layer");
            edgeLayer.pickingMode = PickingMode.Ignore;
            edgeLayer.style.position = UIPosition.Absolute;
            edgeLayer.style.left = 0f;
            edgeLayer.style.top = 0f;

            scopeLayer = new VisualElement
            {
                name = "ai-editor-graph-scope-layer",
            };
            scopeLayer.AddToClassList("ai-editor-graph-scope-layer");
            scopeLayer.pickingMode = PickingMode.Ignore;
            scopeLayer.style.position = UIPosition.Absolute;
            scopeLayer.style.left = 0f;
            scopeLayer.style.top = 0f;

            nodeLayer = new VisualElement
            {
                name = "ai-editor-graph-node-layer",
            };
            nodeLayer.AddToClassList("ai-editor-graph-node-layer");
            nodeLayer.style.position = UIPosition.Absolute;
            nodeLayer.style.left = 0f;
            nodeLayer.style.top = 0f;

            interactionLayer = new VisualElement
            {
                name = "ai-editor-graph-interaction-layer",
            };
            interactionLayer.AddToClassList("ai-editor-graph-interaction-layer");
            interactionLayer.pickingMode = PickingMode.Ignore;
            interactionLayer.style.position = UIPosition.Absolute;
            interactionLayer.style.left = 0f;
            interactionLayer.style.top = 0f;

            portLayer = new GraphPortLayerElement
            {
                name = "ai-editor-graph-port-layer",
            };
            portLayer.AddToClassList("ai-editor-graph-port-layer");
            portLayer.pickingMode = PickingMode.Ignore;
            portLayer.style.position = UIPosition.Absolute;
            portLayer.style.left = 0f;
            portLayer.style.top = 0f;

            connectionPreview = new GraphConnectionPreviewElement
            {
                name = "ai-editor-graph-connection-preview",
            };
            connectionPreview.style.position = UIPosition.Absolute;
            connectionPreview.style.left = 0f;
            connectionPreview.style.top = 0f;

            content.Add(backdropLayer);
            content.Add(scopeLayer);
            content.Add(edgeLayer);
            content.Add(nodeLayer);
            content.Add(interactionLayer);
            interactionLayer.Add(connectionPreview);
            content.Add(portLayer);
            Add(content);

            RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            RegisterCallback<FocusOutEvent>(OnFocusOut);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextualMenuPopulate);
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        /// <summary>
        /// Gets or sets the current canvas zoom factor.
        /// </summary>
        internal float Zoom
        {
            get => zoom;
            set
            {
                zoom = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
                ApplyTransform();
            }
        }

        /// <summary>
        /// Gets or sets the current canvas pan in panel coordinates.
        /// </summary>
        internal Vector2 Pan
        {
            get => pan;
            set
            {
                pan = value;
                ApplyTransform();
            }
        }

        /// <summary>
        /// Rebuilds native node cards and edge labels for a topology snapshot.
        /// </summary>
        /// <param name="topology">The topology to display.</param>
        internal void SetTopology(GraphTopology topology)
        {
            CancelConnectionDrag();
            presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(
                topology,
                presentation,
                module.ShowRawReferences);
            edgeLayer.SetPresentation(presentation, ports);
            portLayer.SetPorts(topology, presentation, edgeLayer, ports);
            RebuildScopeElements();
            nodeLayer.Clear();

            if (presentation == null)
            {
                return;
            }

            foreach (GraphPresentationItem item in presentation.Roots)
            {
                nodeLayer.Add(CreatePresentationElement(item, isMovable: true, parentPosition: Vector2.zero, shapeOverride: null));
            }

            UpdateContentBounds(presentation);
            MarkDirtyRepaint();
        }

        /// <summary>
        /// Gets the current semantic presentation used by the canvas.
        /// </summary>
        internal GraphPresentation Presentation => presentation;

        /// <summary>Gets the current canvas-only authored port handles.</summary>
        internal IReadOnlyList<GraphPortDescriptor> Ports => portLayer.Ports;

        /// <summary>Gets whether a source-port connection preview is active.</summary>
        internal bool IsDraggingConnection => draggingConnection;

        /// <summary>Gets the USS-resolved paint values shared by this canvas and its painters.</summary>
        internal GraphCanvasAppearance Appearance => appearance;

        /// <summary>Gets the complete presentation bounds used by view framing.</summary>
        internal Rect PresentationBounds => CalculateBounds(presentation);

        /// <summary>Applies resolved custom styles and repaints without rebuilding graph data.</summary>
        internal void ResolveAppearance(ICustomStyle customStyle)
        {
            appearance.Resolve(customStyle);
            MarkDirtyRepaint();
            foreach (VisualElement element in content.Query<VisualElement>().ToList())
            {
                element.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Refreshes card selection without rebuilding the topology.
        /// </summary>
        /// <param name="selectedNode">The selected node instance.</param>
        internal void SetSelectedNode(TreeNode selectedNode)
        {
            edgeLayer.SetSelectedNode(selectedNode);

            foreach (GraphSequenceScopeElement scope in scopeLayer.Query<GraphSequenceScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphConditionScopeElement scope in scopeLayer.Query<GraphConditionScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphLoopScopeElement scope in scopeLayer.Query<GraphLoopScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphProbabilityScopeElement scope in scopeLayer.Query<GraphProbabilityScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphParallelScopeElement scope in scopeLayer.Query<GraphParallelScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphForEachScopeElement scope in scopeLayer.Query<GraphForEachScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphServiceScopeElement scope in interactionLayer.Query<GraphServiceScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (GraphFlowCompletionElement completion in interactionLayer.Query<GraphFlowCompletionElement>().ToList())
            {
                completion.SetSelected(completion.Scope.Owner.Node?.Node == selectedNode);
            }

            foreach (VisualElement element in nodeLayer.Children())
            {
                if (element is GraphNodeElement node)
                {
                    node.SetSelected(node.Descriptor.Node == selectedNode);
                }
                else if (element is GraphConditionElement condition)
                {
                    condition.SetSelected(selectedNode);
                }
                else if (element is GraphContainerElement container)
                {
                    container.SetSelected(selectedNode);
                }
                else if (element is GraphReferenceProxyElement proxy)
                {
                    proxy.SetSelected(proxy.TargetNode == selectedNode);
                }
            }
        }

        /// <summary>
        /// Fits all nodes into the current viewport.
        /// </summary>
        internal void FitAll()
        {
            if (!HasValidGeometry || presentation == null || presentation.Roots.Count == 0)
            {
                return;
            }

            Rect bounds = CalculateBounds(presentation);
            float fitZoom = CalculateFitZoom(bounds, FramePadding, MaximumFitZoom);
            SetViewTransform(fitZoom, ViewportCenter - bounds.center * fitZoom);
        }

        /// <summary>
        /// Requests one Fit All operation after the canvas receives valid geometry.
        /// </summary>
        internal void RequestFitAllWhenGeometryIsValid()
        {
            fitAllWhenGeometryIsValid = true;
            TryApplyRequestedFit();
        }

        /// <summary>Requests a readable initial frame around the Head and its first two authored execution levels.</summary>
        internal void RequestInitialFrameWhenGeometryIsValid()
        {
            initialFrameWhenGeometryIsValid = true;
            TryApplyRequestedFit();
            schedule.Execute(TryApplyRequestedFit);
        }

        /// <summary>
        /// Frames the selected node in the viewport.
        /// </summary>
        internal void FrameSelected()
        {
            GraphPresentationItem selected = presentation?.Find(module.SelectedNode?.uuid ?? UUID.Empty);
            if (selected == null || !HasValidGeometry)
            {
                return;
            }

            Rect selectedBounds = GraphPresentationLayout.GetBounds(selected);
            GraphServiceScope serviceScope = presentation.FindServiceScope(selected.TargetUUID);
            if (serviceScope != null)
            {
                selectedBounds = serviceScope.Bounds;
            }
            float fitZoom = CalculateFitZoom(selectedBounds, FramePadding, MaximumFitZoom);
            float frameZoom = Mathf.Min(Mathf.Max(zoom, 0.75f), fitZoom);
            SetViewTransform(frameZoom, ViewportCenter - selectedBounds.center * frameZoom);
        }

        /// <summary>
        /// Re-centers the content transform after a layout change.
        /// </summary>
        internal void RefreshTransform()
        {
            edgeLayer.RefreshLabelPositions();
            ApplyTransform();
        }

        /// <summary>Gets the current derived canvas position of a real descriptor.</summary>
        internal Vector2 GetPresentationPosition(GraphNodeDescriptor descriptor)
        {
            return presentation?.Find(descriptor?.UUID ?? UUID.Empty)?.Position ?? descriptor?.Position ?? Vector2.zero;
        }

        /// <summary>Resolves a dragged decorator to the single real child that owns persisted placement.</summary>
        internal GraphNodeDescriptor GetMoveAnchor(GraphNodeDescriptor descriptor)
        {
            return presentation?.FindDecoratorStack(descriptor?.UUID ?? UUID.Empty)?.Anchor.Node ?? descriptor;
        }

        /// <summary>Translates a badge drag destination into the attached child card destination.</summary>
        internal Vector2 GetMoveAnchorPosition(GraphNodeDescriptor descriptor, Vector2 position)
        {
            GraphDecoratorStack stack = presentation?.FindDecoratorStack(descriptor?.UUID ?? UUID.Empty);
            GraphPresentationItem item = presentation?.Find(descriptor?.UUID ?? UUID.Empty);
            return stack == null || item == null ? position : position + stack.Anchor.Position - item.Position;
        }

        /// <summary>
        /// Updates a top-level presentation position while keeping nested items local to their container.
        /// </summary>
        /// <param name="descriptor">The moved source descriptor.</param>
        /// <param name="position">The new canvas position.</param>
        internal void UpdatePresentationPosition(GraphNodeDescriptor descriptor, Vector2 position)
        {
            presentation?.MoveRoot(descriptor?.UUID ?? UUID.Empty, position);
            RefreshPresentationGeometry();
        }

        /// <summary>Updates multiple moved roots before deriving shared scope geometry once.</summary>
        internal void UpdatePresentationPositions(IEnumerable<GraphNodeDescriptor> descriptors)
        {
            if (presentation != null && descriptors != null)
            {
                foreach (GraphNodeDescriptor descriptor in descriptors)
                {
                    presentation.MoveRoot(descriptor?.UUID ?? UUID.Empty, descriptor?.Position ?? Vector2.zero);
                }
            }

            RefreshPresentationGeometry();
        }

        private void RefreshPresentationGeometry()
        {
            GraphPresentationLayout.Layout(presentation);
            RebuildScopeElements();
            RefreshDerivedNodePositions();
            SetSelectedNode(module.SelectedNode);
            edgeLayer.RefreshLabelPositions();
            portLayer.MarkDirtyRepaint();
            UpdateContentBounds(presentation);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 && evt.button != 1 && evt.button != 2)
            {
                return;
            }

            if (evt.button == 0 && TryBeginConnection(evt))
            {
                return;
            }

            if (IsNodeTarget(evt.target))
            {
                return;
            }

            Vector2 graphPoint = content.WorldToLocal(evt.position);
            bool selectedEdge = edgeLayer.SelectAt(graphPoint, 8f / zoom);
            if (selectedEdge)
            {
                Focus();
                if (evt.button is 0 or 1)
                {
                    evt.StopPropagation();
                }

                return;
            }

            edgeLayer.ClearEdgeSelection();
            if (evt.button == 1)
            {
                return;
            }

            panning = true;
            panPointerId = evt.pointerId;
            panStartPointer = evt.position;
            panStart = pan;
            this.CapturePointer(panPointerId);
            evt.StopPropagation();
        }

        /// <summary>Disconnects the selected authored edge from keyboard commands.</summary>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && pendingConnectionPort != null)
            {
                CancelConnectionDrag();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode is not (KeyCode.Delete or KeyCode.Backspace) || edgeLayer.SelectedRelation?.Origin == null)
            {
                return;
            }

            if (module.Disconnect(edgeLayer.SelectedRelation.Origin))
            {
                evt.StopPropagation();
            }
        }

        /// <summary>Adds the edge-specific disconnect command to the canvas context menu.</summary>
        private void OnContextualMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            GraphPresentationRelation relation = edgeLayer.SelectedRelation;
            if (relation?.Origin == null)
            {
                return;
            }

            evt.menu.AppendAction("Disconnect", _ => module.Disconnect(relation.Origin));
        }

        /// <summary>
        /// Determines whether an event target belongs to a graph node card.
        /// </summary>
        /// <param name="target">The UI Toolkit event target.</param>
        /// <returns>True when the target is the node card or one of its descendants.</returns>
        private static bool IsNodeTarget(IEventHandler target)
        {
            VisualElement element = target as VisualElement;
            while (element != null)
            {
                if (element is GraphNodeElement or GraphConditionElement or GraphContainerElement
                    or GraphReferenceProxyElement or GraphFlowCompletionElement or GraphServiceScopeElement
                    or GraphProbabilityPlaceholderElement or GraphDecisionPlaceholderElement)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId == connectionPointerId)
            {
                UpdateConnectionDrag(evt);
                return;
            }

            if (!panning || evt.pointerId != panPointerId)
            {
                return;
            }

            pan = panStart + (Vector2)evt.position - panStartPointer;
            ApplyTransform();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId == connectionPointerId)
            {
                GraphPortDescriptor port = pendingConnectionPort;
                GraphConnectionTarget target = draggingConnection ? connectionPreview.HoveredTarget : null;
                CancelConnectionDrag();
                if (target?.Compatible == true)
                {
                    module.Assign(port, target.Item.TargetUUID);
                }

                evt.StopPropagation();
                return;
            }

            if (evt.pointerId != panPointerId)
            {
                return;
            }

            panning = false;
            this.ReleasePointer(evt.pointerId);
            panPointerId = -1;
            evt.StopPropagation();
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == connectionPointerId)
            {
                CancelConnectionDrag();
                return;
            }

            if (evt.pointerId == panPointerId)
            {
                panning = false;
                this.ReleasePointer(evt.pointerId);
                panPointerId = -1;
            }
        }

        private bool TryBeginConnection(PointerDownEvent evt)
        {
            Vector2 graphPosition = content.WorldToLocal(evt.position);
            GraphPortDescriptor port = portLayer.FindSourcePort(graphPosition, PortHitRadius / zoom);
            if (port == null)
            {
                return false;
            }

            pendingConnectionPort = port;
            connectionPointerId = evt.pointerId;
            connectionStartPointer = evt.position;
            draggingConnection = false;
            Focus();
            this.CapturePointer(connectionPointerId);
            evt.StopPropagation();
            return true;
        }

        private void UpdateConnectionDrag(PointerMoveEvent evt)
        {
            if (!draggingConnection && Vector2.Distance(connectionStartPointer, evt.position) >= ConnectionDragThreshold)
            {
                draggingConnection = true;
                connectionPreview.Show(portLayer.GetSourcePosition(pendingConnectionPort), BuildConnectionTargets(pendingConnectionPort));
            }

            if (draggingConnection)
            {
                connectionPreview.UpdatePointer(content.WorldToLocal(evt.position));
            }

            evt.StopPropagation();
        }

        private IReadOnlyList<GraphConnectionTarget> BuildConnectionTargets(GraphPortDescriptor port)
        {
            List<GraphConnectionTarget> targets = new();
            if (module.Topology == null || presentation == null)
            {
                return targets;
            }

            foreach (GraphNodeDescriptor node in module.Topology.Nodes)
            {
                GraphPresentationItem item = presentation.Find(node.UUID);
                if (item?.Node == null)
                {
                    continue;
                }

                targets.Add(new GraphConnectionTarget(item, module.CanAssign(port, node.UUID).Succeeded));
            }

            return targets;
        }

        private void CancelConnectionDrag()
        {
            int pointerId = connectionPointerId;
            pendingConnectionPort = null;
            connectionPointerId = -1;
            draggingConnection = false;
            connectionPreview.Hide();
            if (pointerId >= 0 && this.HasPointerCapture(pointerId))
            {
                this.ReleasePointer(pointerId);
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == connectionPointerId)
            {
                CancelConnectionDrag();
            }
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (pendingConnectionPort != null)
            {
                CancelConnectionDrag();
            }
        }

        private void OnWheel(WheelEvent evt)
        {
            if (module.Topology == null || !HasValidGeometry || Mathf.Approximately(evt.delta.y, 0f))
            {
                return;
            }

            Vector2 viewportPoint = PanelToViewport(evt.mousePosition);
            Vector2 graphPoint = ViewportToGraph(viewportPoint);
            float wheelDelta = Mathf.Clamp(evt.delta.y, -20f, 20f);
            float targetZoom = Mathf.Clamp(
                zoom * Mathf.Exp(-wheelDelta * WheelZoomSensitivity),
                MinimumZoom,
                MaximumZoom);
            SetViewTransform(targetZoom, viewportPoint - graphPoint * targetZoom);
            evt.StopPropagation();
        }

        /// <summary>
        /// Converts a panel-space point to this viewport's local space.
        /// </summary>
        internal Vector2 PanelToViewport(Vector2 panelPoint) => this.WorldToLocal(panelPoint);

        /// <summary>
        /// Converts a viewport-local point to graph space using the current view transform.
        /// </summary>
        internal Vector2 ViewportToGraph(Vector2 viewportPoint) => (viewportPoint - pan) / zoom;

        /// <summary>
        /// Converts a graph-space point to viewport-local space using the current view transform.
        /// </summary>
        internal Vector2 GraphToViewport(Vector2 graphPoint) => graphPoint * zoom + pan;

        /// <summary>Gets whether panel attachment and layout are ready for coordinate conversion.</summary>
        private bool HasValidGeometry => panel != null
            && float.IsFinite(layout.width)
            && float.IsFinite(layout.height)
            && layout.width > 0f
            && layout.height > 0f;

        /// <summary>Gets the center of the current viewport in local coordinates.</summary>
        private Vector2 ViewportCenter => new(layout.width * 0.5f, layout.height * 0.5f);

        /// <summary>Calculates a bounded zoom that contains graph bounds inside this viewport.</summary>
        private float CalculateFitZoom(Rect bounds, float totalPadding, float maximumZoom)
        {
            float availableWidth = Mathf.Max(1f, layout.width - totalPadding);
            float availableHeight = Mathf.Max(1f, layout.height - totalPadding);
            float scaleX = availableWidth / Mathf.Max(1f, bounds.width);
            float scaleY = availableHeight / Mathf.Max(1f, bounds.height);
            return Mathf.Clamp(Mathf.Min(scaleX, scaleY), MinimumZoom, maximumZoom);
        }

        /// <summary>Applies one authoritative zoom and pan pair to the graph content.</summary>
        private void SetViewTransform(float value, Vector2 position)
        {
            zoom = Mathf.Clamp(value, MinimumZoom, MaximumZoom);
            pan = position;
            ApplyTransform();
        }

        /// <summary>Consumes a pending initial fit after UI Toolkit resolves canvas geometry.</summary>
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            TryApplyRequestedFit();
        }

        /// <summary>Applies the pending initial fit once panel and geometry are valid.</summary>
        private void TryApplyRequestedFit()
        {
            if (!HasValidGeometry)
            {
                return;
            }

            if (initialFrameWhenGeometryIsValid)
            {
                initialFrameWhenGeometryIsValid = false;
                FrameInitialExecution();
                return;
            }

            if (!fitAllWhenGeometryIsValid)
            {
                return;
            }

            fitAllWhenGeometryIsValid = false;
            FitAll();
        }

        /// <summary>Frames only the initial readable execution context without treating an oversized graph as a thumbnail.</summary>
        private void FrameInitialExecution()
        {
            if (presentation == null || module.Topology?.Tree == null)
            {
                FitAll();
                return;
            }

            GraphPresentationItem head = presentation.Find(module.Topology.Tree.headNodeUUID);
            if (head == null)
            {
                FitAll();
                return;
            }

            // Initial navigation frames cards only. Full Flow bounds can contain distant END markers,
            // Body ranges, and free descendants that belong to a later navigation decision.
            Rect bounds = new(head.Position, head.Size);
            Queue<(GraphNodeDescriptor Node, int Depth)> queue = new();
            HashSet<UUID> visited = new();
            GraphNodeDescriptor headNode = module.Topology.FindNode(head.TargetUUID);
            queue.Enqueue((headNode, 0));
            visited.Add(headNode.UUID);
            while (queue.Count > 0)
            {
                (GraphNodeDescriptor node, int depth) = queue.Dequeue();
                if (depth >= 2)
                {
                    continue;
                }

                foreach (GraphEdgeDescriptor edge in module.Topology.Edges)
                {
                    if (edge.Source != node || edge.Target == null || edge.Kind != GraphEdgeKind.Child)
                    {
                        continue;
                    }

                    if (!visited.Add(edge.Target.UUID))
                    {
                        continue;
                    }

                    GraphPresentationItem target = presentation.Find(edge.Target.UUID);
                    if (target == null)
                    {
                        continue;
                    }

                    bounds = Union(bounds, new Rect(target.Position, target.Size));
                    queue.Enqueue((edge.Target, depth + 1));
                }
            }

            float initialZoom = Mathf.Max(MinimumInitialFrameZoom, CalculateFitZoom(bounds, FramePadding, MaximumFitZoom));
            SetViewTransform(initialZoom, ViewportCenter - bounds.center * initialZoom);
        }

        private static Rect Union(Rect left, Rect right)
        {
            return Rect.MinMaxRect(
                Mathf.Min(left.xMin, right.xMin),
                Mathf.Min(left.yMin, right.yMin),
                Mathf.Max(left.xMax, right.xMax),
                Mathf.Max(left.yMax, right.yMax));
        }

        private void ApplyTransform()
        {
            content.transform.position = new Vector3(pan.x, pan.y, 0f);
            content.transform.scale = new Vector3(zoom, zoom, 1f);
            edgeLayer.MarkDirtyRepaint();
            MarkDirtyRepaint();
        }

        private void UpdateContentBounds(GraphPresentation value)
        {
            Rect bounds = CalculateBounds(value);
            float width = Mathf.Max(2000f, bounds.xMax + 1000f);
            float height = Mathf.Max(1200f, bounds.yMax + 1000f);
            content.style.width = width;
            content.style.height = height;
            backdropLayer.style.width = width;
            backdropLayer.style.height = height;
            edgeLayer.style.width = width;
            edgeLayer.style.height = height;
            scopeLayer.style.width = width;
            scopeLayer.style.height = height;
            nodeLayer.style.width = width;
            nodeLayer.style.height = height;
            interactionLayer.style.width = width;
            interactionLayer.style.height = height;
        }

        private static Rect CalculateBounds(GraphPresentation value)
        {
            if (value == null || value.Roots.Count == 0)
            {
                return new Rect(Vector2.zero, GraphPresentationMetrics.NormalNodeSize);
            }

            Rect first = GraphPresentationLayout.GetBounds(value.Roots[0]);
            Vector2 min = first.min;
            Vector2 max = first.max;
            for (int i = 1; i < value.Roots.Count; i++)
            {
                Rect bounds = GraphPresentationLayout.GetBounds(value.Roots[i]);
                min = Vector2.Min(min, bounds.min);
                max = Vector2.Max(max, bounds.max);
            }

            foreach (GraphServiceScope scope in value.ServiceScopes)
            {
                min = Vector2.Min(min, scope.Bounds.min);
                max = Vector2.Max(max, scope.Bounds.max);
            }

            foreach (GraphFlowScope scope in value.CompletionScopes)
            {
                min = Vector2.Min(min, scope.Bounds.min);
                max = Vector2.Max(max, scope.Bounds.max);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void RebuildScopeElements()
        {
            backdropLayer.Clear();
            scopeLayer.Clear();
            interactionLayer.Clear();
            if (presentation == null)
            {
                interactionLayer.Add(connectionPreview);
                return;
            }

            foreach (GraphFlowScope scope in presentation.CompletionScopes)
            {
                if (scope is GraphSequenceScope sequenceScope)
                {
                    scopeLayer.Add(new GraphSequenceScopeElement(sequenceScope, appearance));
                }
                else if (scope is GraphConditionScope conditionScope)
                {
                    backdropLayer.Add(new GraphConditionBackdropElement(conditionScope, appearance));
                    scopeLayer.Add(new GraphConditionScopeElement(conditionScope, appearance));
                }
                else if (scope is GraphLoopScope loopScope)
                {
                    scopeLayer.Add(new GraphLoopScopeElement(loopScope));
                }
                else if (scope is GraphProbabilityScope probabilityScope)
                {
                    scopeLayer.Add(new GraphProbabilityScopeElement(probabilityScope, appearance));
                }
                else if (scope is GraphParallelScope parallelScope)
                {
                    scopeLayer.Add(new GraphParallelScopeElement(parallelScope, appearance));
                }
                else if (scope is GraphForEachScope forEachScope)
                {
                    scopeLayer.Add(new GraphForEachScopeElement(forEachScope));
                }

                interactionLayer.Add(new GraphFlowCompletionElement(module, scope));
            }

            foreach (GraphServiceScope scope in presentation.ServiceScopes)
            {
                interactionLayer.Add(new GraphServiceScopeElement(module, scope));
            }

            interactionLayer.Add(connectionPreview);
        }

        /// <summary>Refreshes positions of presentation-only cards after derived scope geometry changes.</summary>
        private void RefreshDerivedNodePositions()
        {
            foreach (GraphNodeElement node in nodeLayer.Query<GraphNodeElement>().ToList())
            {
                node.RefreshPosition();
            }

            foreach (GraphConditionElement condition in nodeLayer.Query<GraphConditionElement>().ToList())
            {
                condition.RefreshPosition();
            }

            foreach (GraphConditionPlaceholderElement placeholder in nodeLayer.Query<GraphConditionPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphLoopPlaceholderElement placeholder in nodeLayer.Query<GraphLoopPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphLoopJunctionElement junction in nodeLayer.Query<GraphLoopJunctionElement>().ToList())
            {
                junction.RefreshPosition();
            }

            foreach (GraphProbabilityPlaceholderElement placeholder in nodeLayer.Query<GraphProbabilityPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphDecisionPlaceholderElement placeholder in nodeLayer.Query<GraphDecisionPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphServicePlaceholderElement placeholder in nodeLayer.Query<GraphServicePlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphParallelPlaceholderElement placeholder in nodeLayer.Query<GraphParallelPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphForEachPlaceholderElement placeholder in nodeLayer.Query<GraphForEachPlaceholderElement>().ToList())
            {
                placeholder.RefreshPosition();
            }

            foreach (GraphForEachJunctionElement junction in nodeLayer.Query<GraphForEachJunctionElement>().ToList())
            {
                junction.RefreshPosition();
            }
        }

        private VisualElement CreatePresentationElement(
            GraphPresentationItem item,
            bool isMovable,
            Vector2 parentPosition,
            GraphNodeShape? shapeOverride)
        {
            Vector2 localPosition = item.Position - parentPosition;
            switch (item.Kind)
            {
                case GraphPresentationKind.Condition:
                    return new GraphConditionElement(this, module, item, isMovable, localPosition, CreatePresentationElement);
                case GraphPresentationKind.ConditionPlaceholder:
                    return new GraphConditionPlaceholderElement(item, localPosition);
                case GraphPresentationKind.LoopPlaceholder:
                    return new GraphLoopPlaceholderElement(item, localPosition);
                case GraphPresentationKind.LoopJunction:
                    return new GraphLoopJunctionElement(item, localPosition);
                case GraphPresentationKind.ProbabilityPlaceholder:
                    return new GraphProbabilityPlaceholderElement(item, localPosition);
                case GraphPresentationKind.DecisionPlaceholder:
                    return new GraphDecisionPlaceholderElement(item, localPosition);
                case GraphPresentationKind.ParallelPlaceholder:
                    return new GraphParallelPlaceholderElement(item, localPosition);
                case GraphPresentationKind.ForEachPlaceholder:
                    return new GraphForEachPlaceholderElement(item, localPosition);
                case GraphPresentationKind.ForEachJunction:
                    return new GraphForEachJunctionElement(item, localPosition);
                case GraphPresentationKind.ServicePlaceholder:
                    return new GraphServicePlaceholderElement(item, localPosition);
                case GraphPresentationKind.ReferenceProxy:
                case GraphPresentationKind.Missing:
                    return new GraphReferenceProxyElement(this, module, item, localPosition);
                default:
                    GraphNodeElement node = new(this, module, item.Node, isMovable, localPosition, shapeOverride, item.LeafVisual);
                    if (presentation?.FindDecoratorStack(item.TargetUUID)?.Badges.Contains(item) == true)
                    {
                        node.style.width = item.Size.x;
                        node.style.height = item.Size.y;
                        node.AddToClassList("ai-editor-graph-decorator-badge");
                    }

                    return node;
            }
        }

        private void DrawBackground(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            if (painter == null || layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            Color gridColor = EditorGUIUtility.isProSkin ? appearance.GridDark : appearance.GridLight;
            const float grid = 24f;
            float scaledGrid = grid * zoom;
            if (scaledGrid < 8f)
            {
                return;
            }

            float startX = Mathf.Repeat(pan.x, scaledGrid);
            float startY = Mathf.Repeat(pan.y, scaledGrid);
            painter.strokeColor = gridColor;
            painter.lineWidth = appearance.GridLineWidth;
            for (float x = startX; x < layout.width; x += scaledGrid)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, layout.height));
                painter.Stroke();
            }

            for (float y = startY; y < layout.height; y += scaledGrid)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(layout.width, y));
                painter.Stroke();
            }
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            ResolveAppearance(evt.customStyle);
        }
    }

    /// <summary>
    /// Native node card used by <see cref="GraphCanvasElement"/>.
}
