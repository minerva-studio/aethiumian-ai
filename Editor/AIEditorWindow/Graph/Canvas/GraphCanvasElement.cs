using Aethiumian.AI.Nodes;
using Aethiumian.AI.Accessors;
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
    /// Exposes one authored node and its visible marquee-selection bounds.
    /// </summary>
    internal interface IGraphMarqueeSelectable
    {
        /// <summary>Gets the authored node represented by the visual element.</summary>
        TreeNode AuthoredNode { get; }

        /// <summary>Gets the panel-space bounds used by box selection.</summary>
        Rect MarqueeWorldBound { get; }
    }

    /// <summary>Describes one authored node that can participate in keyboard navigation.</summary>
    internal readonly struct GraphNavigationCandidate
    {
        internal GraphNavigationCandidate(UUID uuid, Rect bounds, int presentationOrder)
        {
            UUID = uuid;
            Bounds = bounds;
            PresentationOrder = presentationOrder;
        }

        internal UUID UUID { get; }
        internal Rect Bounds { get; }
        internal int PresentationOrder { get; }
    }

    /// <summary>
    /// Native UI Toolkit canvas for the graph topology.
    /// </summary>
    internal sealed class GraphCanvasElement : VisualElement
    {
        internal const float MinimumZoom = 0.05f;
        internal const float MaximumZoom = 2.5f;
        internal const float GridSpacing = 24f;
        private const float MaximumFitZoom = 1.5f;
        private const float MinimumInitialFrameZoom = 0.45f;
        private const float FramePadding = 48f;
        private const float WheelZoomSensitivity = 0.035f;
        private const float PortHitRadius = 10f;
        private const float ConnectionDragThreshold = 4f;
        private const float MarqueeDragThreshold = 3f;

        private readonly GraphEditorModule module;
        private readonly GraphCanvasAppearance appearance = new();
        private readonly VisualElement content;
        private readonly VisualElement backdropLayer;
        private readonly VisualElement scopeLayer;
        private readonly VisualElement groupLayer;
        private readonly GraphEdgeLayerElement edgeLayer;
        private readonly VisualElement nodeLayer;
        private readonly VisualElement interactionLayer;
        private readonly GraphPortLayerElement portLayer;
        private readonly GraphConnectionPreviewElement connectionPreview;
        private readonly VisualElement creationOverlay;
        private readonly VisualElement viewOptionsPanel;
        private readonly Button viewOptionsExpandButton;
        private readonly Button gridButton;
        private readonly Button snapButton;
        private readonly Button fitAllButton;
        private readonly Button frameSelectedButton;
        private readonly Button autoLayoutButton;
        private readonly Button serviceVisibilityButton;
        private readonly Button rawReferencesButton;
        private readonly Button inspectorButton;
        private GraphPresentation presentation;
        private GraphTopology topology;
        private bool panning;
        private int panPointerId = -1;
        private Vector2 panStartPointer;
        private Vector2 panStart;
        private float zoom = 1f;
        private Vector2 pan;
        private bool fitAllWhenGeometryIsValid;
        private bool initialFrameWhenGeometryIsValid;
        private GraphConnectionSource pendingConnectionSource;
        private int connectionPointerId = -1;
        private Vector2 connectionStartPointer;
        private bool draggingConnection;
        private GraphNodeCreationPalette creationPalette;
        private VisualElement renameOverlay;
        private VisualElement viewOptionsGroup;
        private bool overlayPointerActive;
        private int rightClickPortPointerId = -1;
        private GraphPresentationKind? selectedBoundaryKind;
        private readonly VisualElement selectionMarquee;
        private bool marqueeSelecting;
        private bool marqueeDragged;
        private bool marqueeAdditive;
        private int marqueePointerId = -1;
        private Vector2 marqueeStart;
        private Vector2 lastMouseGraphPosition;
        private bool gridVisible;

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

            groupLayer = new VisualElement { name = "ai-editor-graph-group-layer" };
            groupLayer.AddToClassList("ai-editor-graph-group-layer");
            groupLayer.pickingMode = PickingMode.Position;
            groupLayer.style.position = UIPosition.Absolute;
            groupLayer.style.left = 0f;
            groupLayer.style.top = 0f;

            nodeLayer = new VisualElement
            {
                name = "ai-editor-graph-node-layer",
            };
            nodeLayer.AddToClassList("ai-editor-graph-node-layer");
            // The layer spans the whole canvas; only its node descendants should participate in hit testing.
            nodeLayer.pickingMode = PickingMode.Ignore;
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
            content.Add(groupLayer);
            content.Add(edgeLayer);
            content.Add(nodeLayer);
            content.Add(interactionLayer);
            interactionLayer.Add(connectionPreview);
            content.Add(portLayer);
            Add(content);

            selectionMarquee = new VisualElement
            {
                name = "ai-editor-graph-selection-marquee",
                pickingMode = PickingMode.Ignore,
            };
            selectionMarquee.AddToClassList("ai-editor-graph-selection-marquee");
            selectionMarquee.style.position = UIPosition.Absolute;
            selectionMarquee.style.display = DisplayStyle.None;
            selectionMarquee.style.backgroundColor = new Color(0.18f, 0.52f, 0.85f, 0.16f);
            selectionMarquee.style.borderLeftColor = new Color(0.35f, 0.7f, 1f, 0.9f);
            selectionMarquee.style.borderRightColor = new Color(0.35f, 0.7f, 1f, 0.9f);
            selectionMarquee.style.borderTopColor = new Color(0.35f, 0.7f, 1f, 0.9f);
            selectionMarquee.style.borderBottomColor = new Color(0.35f, 0.7f, 1f, 0.9f);
            selectionMarquee.style.borderLeftWidth = 1f;
            selectionMarquee.style.borderRightWidth = 1f;
            selectionMarquee.style.borderTopWidth = 1f;
            selectionMarquee.style.borderBottomWidth = 1f;
            Add(selectionMarquee);

            creationOverlay = new VisualElement
            {
                name = "ai-editor-graph-creation-overlay",
            };
            creationOverlay.AddToClassList("ai-editor-graph-creation-overlay");
            creationOverlay.style.position = UIPosition.Absolute;
            creationOverlay.style.left = 0f;
            creationOverlay.style.top = 0f;
            creationOverlay.style.right = 0f;
            creationOverlay.style.bottom = 0f;
            creationOverlay.style.display = DisplayStyle.None;
            Add(creationOverlay);

            viewOptionsGroup = new VisualElement
            {
                name = "ai-editor-graph-view-options-group",
            };
            viewOptionsGroup.AddToClassList("ai-editor-graph-view-options-group");

            viewOptionsPanel = new VisualElement
            {
                name = "ai-editor-graph-view-options",
            };
            viewOptionsPanel.AddToClassList("ai-editor-graph-view-options");
            viewOptionsExpandButton = CreateViewToolButton(
                "ai-editor-graph-view-options-expand",
                "≡",
                null,
                "Show Graph view controls.");
            viewOptionsExpandButton.clicked += module.ToggleViewOptions;
            gridButton = CreateViewToolButton(
                "ai-editor-graph-view-options-grid",
                "▦",
                null,
                "Show or hide the graph grid.");
            gridButton.clicked += () => module.ShowGrid = !module.ShowGrid;
            snapButton = CreateViewToolButton(
                "ai-editor-graph-view-options-snap",
                "⌖",
                null,
                "Snap hand-dragged graph nodes and movable boundaries to the 24-unit grid.");
            snapButton.clicked += () => module.SnapToGrid = !module.SnapToGrid;
            fitAllButton = CreateViewToolButton(
                "ai-editor-graph-view-options-fit-all",
                null,
                "d_BoundsField",
                "Fit all graph content in the viewport.");
            fitAllButton.clicked += module.FitAll;
            frameSelectedButton = CreateViewToolButton(
                "ai-editor-graph-view-options-frame-selected",
                null,
                "d_RectTool",
                "Frame the selected graph nodes.");
            frameSelectedButton.clicked += module.FrameSelected;
            autoLayoutButton = CreateViewToolButton(
                "ai-editor-graph-view-options-auto-layout",
                null,
                "d_Refresh",
                "Generate and save a deterministic top-down layout.");
            autoLayoutButton.clicked += module.AutoLayout;
            serviceVisibilityButton = CreateViewToolButton(
                "ai-editor-graph-visibility-options-services",
                null,
                "d_VisibilityOff",
                "Show or hide all Service scopes.");
            serviceVisibilityButton.clicked += module.ToggleServiceVisibility;
            rawReferencesButton = CreateViewToolButton(
                "ai-editor-graph-visibility-options-raw-references",
                null,
                "d_Unlinked",
                "Show or hide Raw references.");
            rawReferencesButton.clicked += module.ToggleRawReferences;
            inspectorButton = CreateViewToolButton(
                "ai-editor-graph-view-options-inspector",
                null,
                "d_UnityEditor.InspectorWindow",
                "Show or hide the Graph Inspector.");
            inspectorButton.clicked += module.CollapseInspector;
            viewOptionsPanel.Add(viewOptionsExpandButton);
            viewOptionsPanel.Add(gridButton);
            viewOptionsPanel.Add(snapButton);
            viewOptionsPanel.Add(fitAllButton);
            viewOptionsPanel.Add(frameSelectedButton);
            viewOptionsPanel.Add(autoLayoutButton);
            viewOptionsPanel.Add(inspectorButton);

            VisualElement visibilityOptionsPanel = new()
            {
                name = "ai-editor-graph-visibility-options",
            };
            visibilityOptionsPanel.AddToClassList("ai-editor-graph-visibility-options");
            visibilityOptionsPanel.Add(serviceVisibilityButton);
            visibilityOptionsPanel.Add(rawReferencesButton);
            viewOptionsGroup.Add(viewOptionsPanel);
            viewOptionsGroup.Add(visibilityOptionsPanel);
            Add(viewOptionsGroup);
            gridVisible = module.ShowGrid;
            RefreshViewOptions();

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
        /// Gets the current semantic presentation used by the canvas.
        /// </summary>
        internal GraphPresentation Presentation => presentation;

        /// <summary>Gets the current canvas-only authored port handles.</summary>
        internal IReadOnlyList<GraphPortDescriptor> Ports => portLayer.Ports;

        /// <summary>Updates grid visibility without changing graph data or layout.</summary>
        internal void SetGridVisible(bool value)
        {
            if (gridVisible == value) return;
            gridVisible = value;
            RefreshViewOptions();
            MarkDirtyRepaint();
        }

        /// <summary>Creates one compact icon-style button for the floating Graph view toolbar.</summary>
        private static Button CreateViewToolButton(string name, string fallbackText, string iconName, string tooltip)
        {
            Button button = new()
            {
                name = name,
                text = fallbackText ?? string.Empty,
                tooltip = tooltip,
            };
            button.AddToClassList("ai-editor-graph-view-options-button");
            Texture icon = LoadViewToolIcon(iconName);
            // Unity's internal icon catalogue differs between Editor versions. Keep the
            // toolbar legible even when a specialized icon is absent in this version.
            if (icon != null)
            {
                Image image = new()
                {
                    image = icon,
                    pickingMode = PickingMode.Ignore,
                };
                image.AddToClassList("ai-editor-graph-view-options-icon");
                button.Add(image);
            }
            return button;
        }

        /// <summary>Loads one Unity editor icon and keeps the existing zoom fallback for missing icons.</summary>
        private static Texture LoadViewToolIcon(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;
            Texture icon = EditorGUIUtility.IconContent(iconName).image;
            return icon ?? EditorGUIUtility.IconContent("d_ViewToolZoom").image;
        }

        /// <summary>Replaces a floating-toolbar icon while preserving its centered Image element.</summary>
        private static void SetViewToolIcon(Button button, string iconName)
        {
            Image image = button?.Q<Image>();
            if (image != null)
            {
                image.image = LoadViewToolIcon(iconName);
            }
        }

        /// <summary>Synchronizes floating-toolbar visibility and selected visual state.</summary>
        internal void RefreshViewOptions()
        {
            viewOptionsPanel?.EnableInClassList("ai-editor-graph-view-options-expanded", module.ViewOptionsExpanded);
            gridButton?.EnableInClassList("ai-editor-graph-view-options-button-active", gridVisible);
            snapButton?.EnableInClassList("ai-editor-graph-view-options-button-active", module.SnapToGrid);
            serviceVisibilityButton?.EnableInClassList("ai-editor-graph-view-options-button-active", module.ShowServices);
            rawReferencesButton?.EnableInClassList("ai-editor-graph-view-options-button-active", module.ShowRawReferences);
            inspectorButton?.EnableInClassList("ai-editor-graph-view-options-button-active", module.InspectorVisible);
            SetViewToolIcon(serviceVisibilityButton, module.ShowServices ? "d_VisibilityOn" : "d_VisibilityOff");
            SetViewToolIcon(rawReferencesButton, module.ShowRawReferences ? "d_Linked" : "d_Unlinked");
        }

        /// <summary>Gets the USS-resolved paint values shared by this canvas and its painters.</summary>
        internal GraphCanvasAppearance Appearance => appearance;

        #region Selection

        /// <summary>
        /// Refreshes card selection without rebuilding the topology.
        /// </summary>
        /// <param name="selectedNode">The selected node instance.</param>
        internal void SetSelectedNode(TreeNode selectedNode)
        {
            SetSelectedNodes(selectedNode == null ? Array.Empty<UUID>() : new[] { selectedNode.uuid });
        }

        /// <summary>Refreshes authored-node selection without rebuilding topology.</summary>
        internal void SetSelectedNodes(IReadOnlyCollection<UUID> selectedUUIDs)
        {
            HashSet<UUID> selected = selectedUUIDs?.ToHashSet() ?? new HashSet<UUID>();
            if (selected.Count > 0)
            {
                selectedBoundaryKind = null;
            }

            TreeNode contextualNode = selected.Count == 1 ? module.TopologyTree?.GetNode(selected.First()) : null;
            edgeLayer.SetSelectedNode(contextualNode);

            foreach (GraphSequenceScopeElement scope in scopeLayer.Query<GraphSequenceScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node != null && selected.Contains(scope.Scope.Owner.TargetUUID));
            }

            foreach (GraphConditionScopeElement scope in scopeLayer.Query<GraphConditionScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node != null && selected.Contains(scope.Scope.Owner.TargetUUID));
            }

            foreach (GraphLoopScopeElement scope in scopeLayer.Query<GraphLoopScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node != null && selected.Contains(scope.Scope.Owner.TargetUUID));
            }

            foreach (GraphProbabilityScopeElement scope in scopeLayer.Query<GraphProbabilityScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node != null && selected.Contains(scope.Scope.Owner.TargetUUID));
            }

            foreach (GraphParallelScopeElement scope in scopeLayer.Query<GraphParallelScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node != null && selected.Contains(scope.Scope.Owner.TargetUUID));
            }

            foreach (GraphForEachScopeElement scope in scopeLayer.Query<GraphForEachScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node != null && selected.Contains(scope.Scope.Owner.TargetUUID));
            }

            foreach (GraphServiceScopeElement scope in interactionLayer.Query<GraphServiceScopeElement>().ToList())
            {
                scope.SetSelected(scope.Scope.Owner.Node != null && selected.Contains(scope.Scope.Owner.TargetUUID));
            }

            foreach (GraphFlowCompletionElement completion in interactionLayer.Query<GraphFlowCompletionElement>().ToList())
            {
                completion.SetSelected(completion.Scope.Owner.Node != null && selected.Contains(completion.Scope.Owner.TargetUUID));
            }

            foreach (VisualElement element in nodeLayer.Children())
            {
                if (element is GraphNodeElement node)
                {
                    node.SetSelected(selected.Contains(node.Descriptor.UUID));
                }
                else if (element is GraphConditionElement condition)
                {
                    condition.SetSelected(selected);
                }
                else if (element is GraphContainerElement container)
                {
                    container.SetSelected(selected);
                }
                else if (element is GraphReferenceProxyElement proxy)
                {
                    proxy.SetSelected(proxy.TargetNode != null && selected.Contains(proxy.TargetNode.uuid));
                }
                else if (element is GraphBoundaryElement boundary)
                {
                    boundary.SetSelected(boundary.Kind == selectedBoundaryKind);
                }
            }

            SetSelectedGroup(module.SelectedGroupUUID);
        }

        /// <summary>Updates the selected visual state for one persisted graph group.</summary>
        /// <param name="groupUUID">The selected group UUID, or <see cref="UUID.Empty"/> to clear it.</param>
        internal void SetSelectedGroup(UUID groupUUID)
        {
            foreach (GraphGroupElement group in groupLayer.Query<GraphGroupElement>().ToList())
            {
                group.SetSelected(group.UUID == groupUUID);
            }
        }

        /// <summary>Updates the visibility mode of every derived Service scope without rebuilding topology.</summary>
        internal void SetServiceVisibility(bool showAllServices)
        {
            foreach (GraphServiceScopeElement scope in interactionLayer.Query<GraphServiceScopeElement>().ToList())
            {
                scope.SetServicesVisible(showAllServices);
            }
        }

        /// <summary>Selects one presentation-only boundary without creating an authored node selection.</summary>
        internal void SelectBoundary(GraphPresentationItem boundary)
        {
            module.SetGraphSelection(Array.Empty<TreeNode>());
            selectedBoundaryKind = boundary?.Kind;
            foreach (GraphBoundaryElement element in nodeLayer.Query<GraphBoundaryElement>().ToList())
            {
                element.SetSelected(element.Kind == selectedBoundaryKind);
            }

            edgeLayer.ClearEdgeSelection();
        }

        #endregion

        #region Viewport Transform

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
            IReadOnlyList<TreeNode> selectedNodes = module.SelectedNodes;
            if (selectedNodes.Count == 0 || !HasValidGeometry)
            {
                return;
            }

            Rect? selectedBounds = null;
            foreach (TreeNode node in selectedNodes)
            {
                GraphPresentationItem selected = presentation?.Find(node.uuid);
                if (selected == null) continue;
                Rect bounds = GraphPresentationLayout.GetBounds(selected);
                GraphServiceScope serviceScope = presentation.FindServiceScope(selected.TargetUUID);
                if (selectedNodes.Count == 1 && serviceScope != null) bounds = serviceScope.Bounds;
                selectedBounds = selectedBounds.HasValue
                    ? Rect.MinMaxRect(
                        Mathf.Min(selectedBounds.Value.xMin, bounds.xMin),
                        Mathf.Min(selectedBounds.Value.yMin, bounds.yMin),
                        Mathf.Max(selectedBounds.Value.xMax, bounds.xMax),
                        Mathf.Max(selectedBounds.Value.yMax, bounds.yMax))
                    : bounds;
            }

            if (!selectedBounds.HasValue) return;
            float fitZoom = CalculateFitZoom(selectedBounds.Value, FramePadding, MaximumFitZoom);
            float frameZoom = Mathf.Min(Mathf.Max(zoom, 0.75f), fitZoom);
            SetViewTransform(frameZoom, ViewportCenter - selectedBounds.Value.center * frameZoom);
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
            return presentation == null ? descriptor : presentation.ResolveMovableRoot(descriptor?.UUID ?? UUID.Empty);
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
        /// <summary>Updates multiple moved roots before deriving shared scope geometry once.</summary>
        internal void UpdatePresentationPositions(IEnumerable<GraphNodeDescriptor> descriptors, bool preserveGroupElements = false)
        {
            if (presentation != null && descriptors != null)
            {
                foreach (GraphNodeDescriptor descriptor in descriptors)
                {
                    if (descriptor == null) continue;
                    // Presentation items may retain a distinct descriptor snapshot; synchronize the
                    // canonical authored position before MoveRoot/Layout derives compound geometry.
                    GraphPresentationItem item = presentation.Find(descriptor.UUID);
                    if (item?.Node != null) item.Node.Position = descriptor.Position;
                    presentation.MoveRoot(descriptor.UUID, descriptor.Position);
                }
            }

            GraphPresentationLayout.Layout(presentation);
            RefreshPresentationGeometryCore(preserveGroupElements);
        }

        #endregion

        #region Pointer Interaction

        private void OnWheel(WheelEvent evt)
        {
            if (IsViewOptionsTarget(evt.target))
            {
                return;
            }

            if (creationPalette != null || renameOverlay != null)
            {
                evt.StopPropagation();
                return;
            }

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

        private void OnPointerDown(PointerDownEvent evt)
        {
            lastMouseGraphPosition = content.WorldToLocal(evt.position);
            if (IsViewOptionsTarget(evt.target))
            {
                return;
            }
            if (creationPalette != null || renameOverlay != null)
            {
                overlayPointerActive = IsOverlayTarget(evt.target);
                if (!overlayPointerActive)
                {
                    CloseCreationPalette();
                    evt.StopPropagation();
                }

                return;
            }

            if (evt.button != 0 && evt.button != 1 && evt.button != 2)
            {
                return;
            }

            if (evt.button == 1)
            {
                rightClickPortPointerId = -1;

                // Select the authored target during capture so the node is selected before
                // UITK opens the contextual menu on pointer release.
                TreeNode authoredNode = ResolveAuthoredNode(evt.target);
                if (authoredNode != null)
                {
                    if (!module.IsNodeSelected(authoredNode)) module.SelectNode(authoredNode);
                    Focus();
                    edgeLayer.ClearEdgeSelection();
                    return;
                }
            }

            // Match Shader Graph navigation: middle drag or Alt + left drag pans
            // from any canvas target without starting selection, node, or port gestures.
            if (evt.button == 2 || evt.button == 0 && evt.altKey)
            {
                panning = true;
                panPointerId = evt.pointerId;
                panStartPointer = evt.position;
                panStart = pan;
                this.CapturePointer(panPointerId);
                evt.StopPropagation();
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
            if (evt.button == 1 && portLayer.FindConnectionSource(graphPoint, PortHitRadius / zoom) != null)
            {
                // Ports are painter-only, so preserve their hit result until the matching release.
                rightClickPortPointerId = evt.pointerId;
                return;
            }

            bool selectedEdge = edgeLayer.SelectAt(graphPoint, 8f / zoom);
            if (selectedEdge)
            {
                module.SetGraphSelection(Array.Empty<TreeNode>());
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

            if (evt.button == 0)
            {
                marqueeSelecting = true;
                marqueeDragged = false;
                marqueeAdditive = evt.shiftKey || evt.actionKey;
                marqueePointerId = evt.pointerId;
                marqueeStart = PanelToViewport(evt.position);
                this.CapturePointer(marqueePointerId);
            }
            else
            {
                return;
            }
            evt.StopPropagation();
        }

        /// <summary>Disconnects the selected authored edge from keyboard commands.</summary>
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (creationPalette != null || renameOverlay != null || IsTextEditingTarget(evt.target))
            {
                return;
            }

            if (evt.keyCode == KeyCode.Escape && pendingConnectionSource != null)
            {
                CancelConnectionDrag();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Escape && edgeLayer.SelectedRelation != null)
            {
                edgeLayer.ClearEdgeSelection();
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Escape && module.SelectedNodes.Count > 0)
            {
                module.SetGraphSelection(Array.Empty<TreeNode>());
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.Escape && module.SelectedGroupUUID != UUID.Empty)
            {
                module.SetGraphSelection(Array.Empty<TreeNode>());
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F && module.SelectedNodes.Count > 0)
            {
                module.FrameSelected();
                evt.StopPropagation();
                return;
            }

            GraphNavigationDirection? direction = evt.keyCode switch
            {
                KeyCode.LeftArrow => GraphNavigationDirection.Left,
                KeyCode.RightArrow => GraphNavigationDirection.Right,
                KeyCode.UpArrow => GraphNavigationDirection.Up,
                KeyCode.DownArrow => GraphNavigationDirection.Down,
                _ => null,
            };
            if (direction.HasValue)
            {
                module.NavigateSelection(direction.Value, evt.shiftKey);
                evt.StopPropagation();
                return;
            }

            TreeNode selectedNode = module.SelectedNode;
            bool commandModifier = evt.ctrlKey || evt.commandKey;
            if (commandModifier && evt.keyCode == KeyCode.C && module.SelectedNodes.Count > 0)
            {
                module.CopySelectedNodes();
                evt.StopPropagation();
                return;
            }

            if (commandModifier && evt.keyCode == KeyCode.V && module.PasteGraphSelection(lastMouseGraphPosition))
            {
                evt.StopPropagation();
                return;
            }

            if (commandModifier && evt.keyCode == KeyCode.D && module.SelectedNodes.Count > 0)
            {
                if (module.DuplicateSelectedNodes()) evt.StopPropagation();
                return;
            }

            if (evt.keyCode == KeyCode.F2 && module.SelectedGroupUUID != UUID.Empty)
            {
                GraphGroupElement selectedGroup = groupLayer.Query<GraphGroupElement>()
                    .ToList()
                    .FirstOrDefault(group => group.UUID == module.SelectedGroupUUID);
                if (selectedGroup != null)
                {
                    selectedGroup.BeginRename();
                    evt.StopPropagation();
                }
                return;
            }

            if (evt.keyCode == KeyCode.F2 && selectedNode != null)
            {
                ShowRenameOverlay(selectedNode);
                evt.StopPropagation();
                return;
            }

            GraphPresentationRelation selectedRelation = edgeLayer.SelectedRelation;
            if (evt.keyCode is KeyCode.Delete or KeyCode.Backspace
                && module.SelectedNodes.Count == 0
                && module.SelectedGroupUUID != UUID.Empty)
            {
                if (module.Ungroup(module.SelectedGroupUUID)) evt.StopPropagation();
                return;
            }

            if (evt.keyCode is not (KeyCode.Delete or KeyCode.Backspace) || selectedRelation == null)
            {
                if (evt.keyCode is KeyCode.Delete or KeyCode.Backspace && module.SelectedNodes.Count > 0)
                {
                    if (module.DeleteSelectedNodes())
                        evt.StopPropagation();
                }
                return;
            }

            bool disconnected = selectedRelation.Role == GraphPresentationRelationRole.AuthoredTreeHead
                ? module.DisconnectEntrance()
                : selectedRelation.IsEditableReference && module.Disconnect(selectedRelation.Origin);
            if (disconnected)
            {
                evt.StopPropagation();
            }
        }

        /// <summary>Dispatches a keyboard event through the production handler for package tests.</summary>
        /// <param name="keyCode">The logical key to dispatch.</param>
        /// <param name="modifiers">The modifier state carried by the event.</param>
        /// <param name="eventTarget">The element that owns the keyboard event.</param>
        /// <returns>True when the production handler stopped propagation.</returns>
        /// <summary>Adds the edge-specific disconnect command to the canvas context menu.</summary>
        private void OnContextualMenuPopulate(ContextualMenuPopulateEvent evt)
        {
            if (creationPalette != null || renameOverlay != null)
            {
                evt.StopPropagation();
                return;
            }

            GraphPresentationRelation relation = edgeLayer.SelectedRelation;
            if (relation?.Role == GraphPresentationRelationRole.AuthoredTreeHead || relation?.IsEditableReference == true)
            {
                PopulateEdgeCommandMenu(evt.menu, relation);
                return;
            }

        }

        #endregion

        #region Context Menus And Overlays

        /// <summary>Populates the system-level node command menu for an authored graph node.</summary>
        internal void PopulateAuthoredNodeContextMenu(ContextualMenuPopulateEvent evt)
        {
            TreeNode node = ResolveAuthoredNode(evt.target);
            if (node == null) return;
            if (!module.IsNodeSelected(node)) module.SelectNode(node);
            PopulateNodeCommandMenu(evt.menu, node);
            evt.StopPropagation();
        }

        /// <summary>Fills the native Graph dropdown through the shared command registrar.</summary>
        internal void PopulateNodeCommandMenu(DropdownMenu menu, TreeNode node)
        {
            if (module.SelectedNodes.Count > 1 && module.IsNodeSelected(node))
            {
                menu.AppendAction("Align/Left", _ => module.AlignSelectedNodes(GraphSelectionAlignment.Left), _ => module.CanAlignSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Align/Center", _ => module.AlignSelectedNodes(GraphSelectionAlignment.Center), _ => module.CanAlignSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Align/Right", _ => module.AlignSelectedNodes(GraphSelectionAlignment.Right), _ => module.CanAlignSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Align/Top", _ => module.AlignSelectedNodes(GraphSelectionAlignment.Top), _ => module.CanAlignSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Align/Middle", _ => module.AlignSelectedNodes(GraphSelectionAlignment.Middle), _ => module.CanAlignSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Align/Bottom", _ => module.AlignSelectedNodes(GraphSelectionAlignment.Bottom), _ => module.CanAlignSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Distribute/Horizontal", _ => module.DistributeSelectedNodes(GraphSelectionDistribution.Horizontal), _ => module.CanDistributeSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Distribute/Vertical", _ => module.DistributeSelectedNodes(GraphSelectionDistribution.Vertical), _ => module.CanDistributeSelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendSeparator();
                menu.AppendAction("Copy Selection", _ => module.CopySelectedNodes());
                menu.AppendAction("Duplicate Selection", _ => module.DuplicateSelectedNodes());
                menu.AppendAction("Group Selection", _ => module.GroupSelection());
                menu.AppendAction("Tidy Selection", _ => module.TidySelection(), _ => module.CanTidySelection
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Delete Selection", _ => module.DeleteSelectedNodes());
                return;
            }

            NodeCommandMenuRegistrar.Register(
                new DropdownNodeCommandMenu(menu),
                module.TreeModule,
                node,
                new GraphNodeCommandHandler(module, this));
            menu.AppendSeparator();
            menu.AppendAction("Set as Head", _ => module.SetHead(node), _ => module.CanSetHead(node)
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled);
        }

        /// <summary>Fills the native Graph dropdown for one selected authored edge relation.</summary>
        /// <param name="menu">The UI Toolkit menu that receives the commands.</param>
        /// <param name="relation">The selected semantic relation.</param>
        internal void PopulateEdgeCommandMenu(DropdownMenu menu, GraphPresentationRelation relation)
        {
            if (menu == null || relation == null)
            {
                return;
            }

            if (relation.Role == GraphPresentationRelationRole.AuthoredTreeHead)
            {
                menu.AppendAction("Disconnect", _ => module.DisconnectEntrance());
                return;
            }

            if (!relation.IsEditableReference)
            {
                return;
            }

            GraphEdgeDescriptor edge = relation.Role == GraphPresentationRelationRole.AuthoredReference
                && !relation.IsMissingTarget
                && module.CanReorder(relation.Origin)
                ? relation.Origin
                : null;
            if (edge != null)
            {
                int count = module.GetCollectionCount(edge);
                int index = edge.CollectionIndex;
                menu.AppendAction("Move First", _ => module.Reorder(edge, 0), _ => index > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Move Earlier", _ => module.Reorder(edge, index - 1), _ => index > 0
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Move Later", _ => module.Reorder(edge, index + 1), _ => index < count - 1
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendAction("Move Last", _ => module.Reorder(edge, count - 1), _ => index < count - 1
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled);
                menu.AppendSeparator();
            }

            menu.AppendAction("Disconnect", _ => module.Disconnect(relation.Origin));
        }

        /// <summary>Returns whether a keyboard event originated from an editable text control.</summary>
        private static bool IsTextEditingTarget(IEventHandler target)
        {
            VisualElement element = target as VisualElement;
            while (element != null)
            {
                if (element is TextField || element.ClassListContains("unity-base-text-field")) return true;
                element = element.parent;
            }
            return false;
        }

        /// <summary>Resolves only real authored node visuals; proxies and presentation placeholders return null.</summary>
        private static TreeNode ResolveAuthoredNode(IEventHandler target)
        {
            VisualElement element = target as VisualElement;
            while (element != null)
            {
                if (element is GraphNodeElement node)
                    return node.Descriptor?.Node;
                if (element is GraphConditionElement condition)
                    return condition.AuthoredNode;
                if (element is GraphContainerElement container)
                    return container.AuthoredNode;
                element = element.parent;
            }

            return null;
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
                    or GraphBoundaryElement or GraphReferenceProxyElement or GraphFlowCompletionElement or GraphServiceScopeElement
                    or GraphProbabilityPlaceholderElement or GraphDecisionPlaceholderElement or GraphGroupElement)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        private bool IsOverlayTarget(IEventHandler target)
        {
            VisualElement element = target as VisualElement;
            while (element != null)
            {
                if (element == creationPalette || element == renameOverlay)
                {
                    return true;
                }

                element = element.parent;
            }

            return false;
        }

        /// <summary>Gets whether one event belongs to the floating Graph view controls.</summary>
        private bool IsViewOptionsTarget(IEventHandler target)
        {
            VisualElement element = target as VisualElement;
            while (element != null)
            {
                if (element == viewOptionsGroup) return true;
                element = element.parent;
            }

            return false;
        }

        #endregion

        #region Connection Drag

        private void OnPointerMove(PointerMoveEvent evt)
        {
            lastMouseGraphPosition = content.WorldToLocal(evt.position);
            if (evt.pointerId == connectionPointerId)
            {
                UpdateConnectionDrag(evt);
                return;
            }

            if (marqueeSelecting && evt.pointerId == marqueePointerId)
            {
                Vector2 current = PanelToViewport(evt.position);
                marqueeDragged |= (current - marqueeStart).sqrMagnitude >= MarqueeDragThreshold * MarqueeDragThreshold;
                if (marqueeDragged)
                {
                    Rect rect = Rect.MinMaxRect(
                        Mathf.Min(marqueeStart.x, current.x), Mathf.Min(marqueeStart.y, current.y),
                        Mathf.Max(marqueeStart.x, current.x), Mathf.Max(marqueeStart.y, current.y));
                    selectionMarquee.style.display = DisplayStyle.Flex;
                    selectionMarquee.style.left = rect.xMin;
                    selectionMarquee.style.top = rect.yMin;
                    selectionMarquee.style.width = rect.width;
                    selectionMarquee.style.height = rect.height;
                }
                evt.StopPropagation();
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
            overlayPointerActive = false;

            if (evt.pointerId == connectionPointerId)
            {
                GraphConnectionSource source = pendingConnectionSource;
                GraphConnectionTarget target = draggingConnection ? connectionPreview.HoveredTarget : null;
                bool createAtDrop = draggingConnection && target == null;
                Vector2 graphPosition = content.WorldToLocal(evt.position);
                Vector2 viewportPosition = PanelToViewport(evt.position);
                CancelConnectionDrag();
                if (target?.Compatible == true)
                {
                    if (source.IsEntrance)
                    {
                        module.AssignEntrance(target.Item.TargetUUID);
                    }
                    else
                    {
                        module.Assign(source.AuthoredPort, target.Item.TargetUUID);
                    }
                }
                else if (target != null)
                {
                    module.ShowConnectionRejectedNotification();
                }
                else if (createAtDrop)
                {
                    ShowCreationPalette(
                        graphPosition,
                        viewportPosition,
                        source.AuthoredPort,
                        createAsEntranceHead: source.IsEntrance);
                }

                evt.StopPropagation();
                return;
            }

            if (evt.pointerId == marqueePointerId)
            {
                Vector2 current = PanelToViewport(evt.position);
                if (marqueeDragged)
                {
                    CompleteMarqueeSelection(marqueeStart, current, marqueeAdditive);
                }
                else if (!marqueeAdditive)
                {
                    module.SetGraphSelection(Array.Empty<TreeNode>());
                }

                selectionMarquee.style.display = DisplayStyle.None;
                marqueeSelecting = false;
                marqueePointerId = -1;
                this.ReleasePointer(evt.pointerId);
                edgeLayer.ClearEdgeSelection();
                evt.StopPropagation();
                return;
            }

            bool rightClickStartedOnPort = evt.button == 1 && evt.pointerId == rightClickPortPointerId;
            if (evt.button == 1)
            {
                rightClickPortPointerId = -1;
            }

            if (evt.button == 1
                && !rightClickStartedOnPort
                && !IsNodeTarget(evt.target)
                && edgeLayer.SelectedRelation == null)
            {
                Vector2 viewportPosition = PanelToViewport(evt.position);
                ShowCreationPalette(ViewportToGraph(viewportPosition), viewportPosition, null);
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
            overlayPointerActive = false;

            if (evt.pointerId == rightClickPortPointerId)
            {
                rightClickPortPointerId = -1;
            }

            if (evt.pointerId == connectionPointerId)
            {
                CancelConnectionDrag();
                return;
            }

            if (evt.pointerId == marqueePointerId)
            {
                selectionMarquee.style.display = DisplayStyle.None;
                marqueeSelecting = false;
                marqueePointerId = -1;
                this.ReleasePointer(evt.pointerId);
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
            GraphConnectionSource source = portLayer.FindConnectionSource(graphPosition, PortHitRadius / zoom);
            if (source == null)
            {
                return false;
            }

            pendingConnectionSource = source;
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
                Vector2 sourcePosition = pendingConnectionSource.IsEntrance
                    ? portLayer.GetSourcePosition(pendingConnectionSource.EntrancePort)
                    : portLayer.GetSourcePosition(pendingConnectionSource.AuthoredPort);
                connectionPreview.Show(sourcePosition, BuildConnectionTargets(pendingConnectionSource));
            }

            if (draggingConnection)
            {
                connectionPreview.UpdatePointer(content.WorldToLocal(evt.position));
            }

            evt.StopPropagation();
        }

        private IReadOnlyList<GraphConnectionTarget> BuildConnectionTargets(GraphConnectionSource source)
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

                bool compatible = source.IsEntrance
                    ? module.CanAssignEntrance(node.UUID)
                    : module.CanAssign(source.AuthoredPort, node.UUID);
                targets.Add(new GraphConnectionTarget(item, compatible));
            }

            return targets;
        }

        private void CancelConnectionDrag()
        {
            int pointerId = connectionPointerId;
            pendingConnectionSource = null;
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
            if (evt.pointerId == panPointerId)
            {
                panning = false;
                panPointerId = -1;
            }
            if (evt.pointerId == marqueePointerId)
            {
                selectionMarquee.style.display = DisplayStyle.None;
                marqueeSelecting = false;
                marqueePointerId = -1;
            }
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (creationPalette != null || renameOverlay != null)
            {
                if (overlayPointerActive || IsOverlayTarget(evt.relatedTarget))
                {
                    return;
                }

                // UITK may report a transient null related target while a palette child
                // transfers focus between its search field, list, and navigation buttons.
                if (evt.relatedTarget == null)
                {
                    if (!overlayPointerActive)
                    {
                        CloseCreationPalette();
                    }
                    return;
                }

                CloseCreationPalette();
                evt.StopPropagation();
                return;
            }

            CloseCreationPalette();
            if (pendingConnectionSource != null)
            {
                CancelConnectionDrag();
            }
        }

        #endregion

        #region Palette And Rename Overlays

        /// <summary>Closes the transient node-creation palette without mutating the tree.</summary>
        internal void CloseCreationPalette()
        {
            overlayPointerActive = false;
            creationPalette = null;
            renameOverlay = null;
            creationOverlay.Clear();
            creationOverlay.style.display = DisplayStyle.None;
        }

        /// <summary>Opens the native UI Toolkit creation palette for a canvas location.</summary>
        private void ShowCreationPalette(
            Vector2 graphPosition,
            Vector2 viewportPosition,
            GraphPortDescriptor port,
            bool createAsEntranceHead = false)
        {
            if (module.Topology == null || !HasValidGeometry)
            {
                return;
            }

            CloseCreationPalette();
            NodeCreationMenuContext context = port?.AnchorKind == GraphPortAnchorKind.Service
                ? NodeCreationMenuContext.Services
                : NodeCreationMenuContext.Nodes;
            creationPalette = new GraphNodeCreationPalette(
                context,
                type =>
                {
                    CloseCreationPalette();
                    if (createAsEntranceHead)
                    {
                        module.CreateEntranceNode(type, graphPosition);
                    }
                    else
                    {
                        module.CreateNode(type, graphPosition, port);
                    }
                },
                CloseCreationPalette);
            creationOverlay.style.display = DisplayStyle.Flex;
            creationOverlay.Add(creationPalette);
            creationPalette.ShowAt(viewportPosition, new Vector2(layout.width, layout.height));
        }

        internal void ShowRenameOverlay(TreeNode node)
        {
            if (node == null || module.Topology == null) return;
            CloseCreationPalette();
            VisualElement row = new();
            row.AddToClassList("ai-editor-graph-node-rename-overlay");
            TextField field = new() { value = node.name };
            row.Add(field);
            row.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                {
                    if (module.RenameNode(node, field.value)) CloseCreationPalette();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    CloseCreationPalette();
                    evt.StopPropagation();
                }
            });
            renameOverlay = row;
            creationOverlay.style.display = DisplayStyle.Flex;
            creationOverlay.Add(row);
            schedule.Execute(() =>
            {
                PositionRenameOverlay(row, node);
                field.Focus();
                field.SelectAll();
            });
        }

        /// <summary>Positions the rename field over the rendered node card.</summary>
        /// <param name="overlay">The rename overlay to position.</param>
        /// <param name="node">The authored node being renamed.</param>
        private void PositionRenameOverlay(VisualElement overlay, TreeNode node)
        {
            GraphNodeElement nodeElement = nodeLayer
                .Query<GraphNodeElement>()
                .ToList()
                .FirstOrDefault(element => element.Descriptor?.Node == node);
            if (nodeElement == null)
            {
                return;
            }

            // worldBound already includes the current graph pan and zoom; convert it
            // back to the overlay's local coordinate space before assigning position.
            Rect bounds = nodeElement.worldBound;
            Vector2 localPosition = creationOverlay.WorldToLocal(bounds.position);
            overlay.style.left = localPosition.x;
            overlay.style.top = localPosition.y;
            overlay.style.width = bounds.width;
            overlay.style.height = bounds.height;
        }

        #endregion

        #region Coordinate Conversion

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

        /// <summary>Gets the current viewport center in graph coordinates.</summary>
        internal Vector2 ViewportCenterGraph => ViewportToGraph(ViewportCenter);

        /// <summary>Returns the stable snapshot of authored root nodes eligible for keyboard navigation.</summary>
        internal IReadOnlyList<GraphNavigationCandidate> GetNavigableCandidates()
        {
            List<GraphNavigationCandidate> result = new();
            if (presentation == null)
            {
                return result;
            }

            for (int index = 0; index < presentation.Roots.Count; index++)
            {
                GraphPresentationItem item = presentation.Roots[index];
                if (item?.Node == null || !item.IsRoot)
                {
                    continue;
                }

                result.Add(new GraphNavigationCandidate(
                    item.TargetUUID,
                    GraphPresentationLayout.GetBounds(item),
                    index));
            }

            return result;
        }

        /// <summary>Reveals a graph rectangle with minimal pan while preserving the current zoom.</summary>
        /// <param name="graphBounds">The graph-space rectangle to reveal.</param>
        /// <param name="padding">The viewport-space inset to preserve.</param>
        internal bool RevealGraphBounds(Rect graphBounds, float padding = 24f)
        {
            if (!HasValidGeometry || graphBounds.width <= 0f || graphBounds.height <= 0f)
            {
                return false;
            }

            Rect viewportBounds = Rect.MinMaxRect(
                GraphToViewport(graphBounds.min).x,
                GraphToViewport(graphBounds.min).y,
                GraphToViewport(graphBounds.max).x,
                GraphToViewport(graphBounds.max).y);
            float availableWidth = Mathf.Max(1f, layout.width - padding * 2f);
            float availableHeight = Mathf.Max(1f, layout.height - padding * 2f);
            Vector2 nextPan = pan;

            if (viewportBounds.width > availableWidth)
            {
                nextPan.x += ViewportCenter.x - viewportBounds.center.x;
            }
            else if (viewportBounds.xMin < padding)
            {
                nextPan.x += padding - viewportBounds.xMin;
            }
            else if (viewportBounds.xMax > layout.width - padding)
            {
                nextPan.x -= viewportBounds.xMax - (layout.width - padding);
            }

            if (viewportBounds.height > availableHeight)
            {
                nextPan.y += ViewportCenter.y - viewportBounds.center.y;
            }
            else if (viewportBounds.yMin < padding)
            {
                nextPan.y += padding - viewportBounds.yMin;
            }
            else if (viewportBounds.yMax > layout.height - padding)
            {
                nextPan.y -= viewportBounds.yMax - (layout.height - padding);
            }

            if ((nextPan - pan).sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            SetViewTransform(zoom, nextPan);
            return true;
        }

        /// <summary>Completes one box-selection query using viewport-local coordinates.</summary>
        /// <param name="start">The first viewport-local corner.</param>
        /// <param name="end">The opposite viewport-local corner.</param>
        /// <param name="additive">Whether matches are added to the existing Graph selection.</param>
        internal void CompleteMarqueeSelection(Vector2 start, Vector2 end, bool additive)
        {
            Rect viewportRect = Rect.MinMaxRect(
                Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
            List<TreeNode> matches = new();
            HashSet<UUID> seen = new();
            foreach (VisualElement element in nodeLayer.Query<VisualElement>().ToList())
            {
                if (element is not IGraphMarqueeSelectable selectable || selectable.AuthoredNode == null)
                {
                    continue;
                }

                TreeNode node = selectable.AuthoredNode;
                Rect viewportBounds = this.WorldToLocal(selectable.MarqueeWorldBound);
                if (viewportRect.Overlaps(viewportBounds) && seen.Add(node.uuid))
                {
                    matches.Add(node);
                }
            }

            module.SetGraphSelection(matches, additive);
        }

        #endregion

        /// <summary>Gets whether panel attachment and layout are ready for coordinate conversion.</summary>
        private bool HasValidGeometry => panel != null
            && float.IsFinite(layout.width)
            && float.IsFinite(layout.height)
            && layout.width > 0f
            && layout.height > 0f;

        /// <summary>Gets the center of the current viewport in local coordinates.</summary>
        private Vector2 ViewportCenter => new(layout.width * 0.5f, layout.height * 0.5f);

        #region Layout And Geometry

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
            groupLayer.style.width = width;
            groupLayer.style.height = height;
            scopeLayer.style.width = width;
            scopeLayer.style.height = height;
            nodeLayer.style.width = width;
            nodeLayer.style.height = height;
            interactionLayer.style.width = width;
            interactionLayer.style.height = height;
        }

        #endregion

        #region Presentation

        /// <summary>
        /// Rebuilds native node cards and edge labels for a topology snapshot.
        /// </summary>
        /// <param name="topology">The topology to display.</param>
        internal void SetTopology(GraphTopology topology)
        {
            CancelConnectionDrag();
            if (topology != null && ReferenceEquals(this.topology, topology) && presentation != null)
            {
                foreach (GraphNodeDescriptor descriptor in topology.Nodes)
                {
                    GraphPresentationItem item = presentation.Find(descriptor.UUID);
                    if (item != null)
                    {
                        item.Position = descriptor.Position;
                    }
                }

                GraphPresentationLayout.Layout(presentation);
                RefreshPresentationGeometryCore();
                edgeLayer.MarkDirtyRepaint();
                MarkDirtyRepaint();
                return;
            }

            this.topology = topology;
            presentation = GraphPresentationBuilder.Build(topology);
            GraphPresentationLayout.Layout(presentation);
            IReadOnlyList<GraphPortDescriptor> ports = GraphPortDescriptorBuilder.Build(
                topology,
                presentation,
                module.ShowRawReferences);
            edgeLayer.SetPresentation(presentation, ports);
            GraphPresentationRelation entranceRelation = presentation.Relations.FirstOrDefault(relation =>
                relation.Kind == GraphPresentationRelationKind.Entrance);
            GraphEntrancePortDescriptor entrancePort = presentation.Entrance == null
                ? null
                : new GraphEntrancePortDescriptor(presentation.Entrance, entranceRelation);
            portLayer.SetPorts(topology, presentation, edgeLayer, ports, entrancePort);
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

            RebuildGroupElements();

            UpdateContentBounds(presentation);
            MarkDirtyRepaint();
        }

        /// <summary>Rebuilds persisted annotation frames from current authored visual bounds.</summary>
        private void RebuildGroupElements()
        {
            groupLayer.Clear();
            GraphLayoutData layout = module.TopologyTree?.GraphLayout;
            if (layout == null || presentation == null) return;
            const float padding = 18f;
            const float titleHeight = 24f;
            foreach (GraphGroupLayoutEntry group in layout.Groups)
            {
                Rect? bounds = null;
                HashSet<UUID> members = group.Members.ToHashSet();
                foreach (UUID member in group.Members)
                {
                    GraphPresentationItem item = presentation.Find(member);
                    if (item == null) continue;
                    Rect itemBounds = GraphPresentationLayout.GetBounds(item);
                    bounds = bounds.HasValue ? Union(bounds.Value, itemBounds) : itemBounds;
                }
                foreach (GraphFlowScope scope in presentation.CompletionScopes)
                {
                    // Global exits and unrelated scopes must not enlarge an authored group.
                    if (!members.Contains(scope.Owner.TargetUUID)) continue;
                    bounds = bounds.HasValue ? Union(bounds.Value, scope.Bounds) : scope.Bounds;
                }
                if (!bounds.HasValue) continue;
                Rect frame = bounds.Value;
                frame.xMin -= padding; frame.xMax += padding;
                frame.yMin -= padding + titleHeight; frame.yMax += padding;
                groupLayer.Add(new GraphGroupElement(module, group, frame));
            }
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

            SetServiceVisibility(module.ShowServices);

            interactionLayer.Add(connectionPreview);
        }

        /// <summary>Refreshes positions of presentation-only cards after derived scope geometry changes.</summary>
        private void RefreshDerivedNodePositions()
        {
            foreach (GraphBoundaryElement boundary in nodeLayer.Query<GraphBoundaryElement>().ToList())
            {
                boundary.RefreshPosition();
            }

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

        internal void RefreshPresentationGeometry()
        {
            GraphPresentationLayout.Layout(presentation);
            RefreshPresentationGeometryCore();
        }

        /// <summary>Refreshes derived canvas geometry after the semantic presentation has been laid out.</summary>
        private void RefreshPresentationGeometryCore(bool preserveGroupElements = false)
        {
            RebuildScopeElements();
            if (!preserveGroupElements) RebuildGroupElements();
            RefreshDerivedNodePositions();
            SetSelectedNodes(module.SelectedNodes.Select(node => node.uuid).ToArray());
            edgeLayer.RefreshLabelPositions();
            portLayer.MarkDirtyRepaint();
            UpdateContentBounds(presentation);
        }

        /// <summary>Translates the existing group frame during a drag without replacing its captured title bar.</summary>
        /// <param name="groupUUID">The dragged group UUID.</param>
        /// <param name="delta">The graph-space drag delta.</param>
        internal void TranslateGroupElement(UUID groupUUID, Vector2 delta)
        {
            GraphGroupElement group = groupLayer.Query<GraphGroupElement>().ToList().FirstOrDefault(item => item.UUID == groupUUID);
            if (group == null) return;
            group.style.left = group.resolvedStyle.left + delta.x;
            group.style.top = group.resolvedStyle.top + delta.y;
        }

        #endregion

        #region Custom Drawing

        private VisualElement CreatePresentationElement(
            GraphPresentationItem item,
            bool isMovable,
            Vector2 parentPosition,
            GraphNodeShape? shapeOverride)
        {
            Vector2 localPosition = item.Position - parentPosition;
            switch (item.Kind)
            {
                case GraphPresentationKind.Entrance:
                case GraphPresentationKind.Exit:
                    return new GraphBoundaryElement(this, module, item, localPosition);
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
            if (!gridVisible || painter == null || layout.width <= 0f || layout.height <= 0f)
            {
                return;
            }

            Color gridColor = EditorGUIUtility.isProSkin ? appearance.GridDark : appearance.GridLight;
            float scaledGrid = GridSpacing * zoom;
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

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            ResolveAppearance(evt.customStyle);
        }

        #endregion
    }

    /// <summary>
    /// Native node card used by <see cref="GraphCanvasElement"/>.
}
