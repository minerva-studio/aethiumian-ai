using Aethiumian.AI.Nodes;
using Aethiumian.AI.Accessors;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Alignment edge or axis used by the Graph multi-selection commands.</summary>
    internal enum GraphSelectionAlignment
    {
        Left,
        Center,
        Right,
        Top,
        Middle,
        Bottom,
    }

    /// <summary>Axis used by Graph multi-selection distribution commands.</summary>
    internal enum GraphSelectionDistribution
    {
        Horizontal,
        Vertical,
    }

    /// <summary>Direction used by transient Graph keyboard navigation.</summary>
    internal enum GraphNavigationDirection
    {
        Left,
        Right,
        Up,
        Down,
    }

    /// <summary>Determines which authored nodes follow one hand-dragged graph card.</summary>
    internal enum GraphMoveMode
    {
        /// <summary>Recursively moves explicit seeds and their structural Child descendants.</summary>
        Structure,

        /// <summary>Moves only the explicitly selected descriptors.</summary>
        Single,
    }

    /// <summary>Summarizes the user-visible consequences of deleting one graph node.</summary>
    internal readonly struct GraphNodeDeleteImpact
    {
        internal GraphNodeDeleteImpact(bool isHead, UUID parentUUID, int structuralIncoming, int serviceIncoming, int rawIncoming, int childCount)
        {
            IsHead = isHead;
            ParentUUID = parentUUID;
            StructuralIncoming = structuralIncoming;
            ServiceIncoming = serviceIncoming;
            RawIncoming = rawIncoming;
            DirectStructuralChildCount = childCount;
        }

        internal bool IsHead { get; }
        internal UUID ParentUUID { get; }
        internal int StructuralIncoming { get; }
        internal int ServiceIncoming { get; }
        internal int RawIncoming { get; }
        internal int DirectStructuralChildCount { get; }
    }

    /// <summary>
    /// Coordinates graph topology, layout persistence, selection, and the graph inspector.
    /// </summary>
    internal sealed class GraphEditorModule : AIEditorWindowModule
    {
        private const float InspectorMinWidth = 220f;
        private const float InspectorMaxWidth = 620f;

        private VisualElement host;
        private VisualElement body;
        private VisualElement inspector;
        private VisualElement splitter;
        private IMGUIContainer inspectorContainer;
        private GraphCanvasElement canvas;
        private GraphTopology topology;
        private Vector2 inspectorScrollPosition;
        private NodeDrawHandler nodeDrawer;
        private float inspectorWidth = 300f;
        private bool inspectorCollapsed;
        private bool resizingInspector;
        private int resizePointerId = -1;
        private float resizeStartX;
        private float resizeStartWidth;
        private bool nodeMoved;
        private bool groupMoved;
        private bool showRawReferences;
        private bool showServices;
        private bool showGrid = true;
        private bool snapToGrid;
        private GraphMoveMode moveMode = GraphMoveMode.Structure;
        private bool viewOptionsExpanded;
        private BehaviourTreeData topologyTree;
        private BehaviourTreeData framedTree;
        private Vector2 viewPan;
        private float viewZoom = 1f;
        private readonly List<UUID> selectedNodeUUIDs = new();
        private UUID selectedGroupUUID;
        private UUID navigationAnchorUUID;
        private bool synchronizingWindowSelection;

        /// <summary>
        /// Initializes a module for the owning editor window.
        /// </summary>
        /// <param name="editorWindow">The owner window.</param>
        internal GraphEditorModule(AIEditorWindow editorWindow)
        {
            Initialize(editorWindow);
            LoadViewState();
        }

        internal TreeNode SelectedNode { get => editorWindow ? editorWindow.SelectedNode : null; set { if (editorWindow) editorWindow.SelectedNode = value; } }
        /// <summary>
        /// Gets the latest topology snapshot.
        /// </summary>
        internal GraphTopology Topology => topology;

        /// <summary>Gets the tree that owns the current topology snapshot.</summary>
        internal BehaviourTreeData TopologyTree => topologyTree;

        /// <summary>
        /// Gets the graph canvas, or null before attachment.
        /// </summary>
        internal GraphCanvasElement Canvas => canvas;

        /// <summary>Gets whether optional raw references are included in the current graph snapshot.</summary>
        internal bool ShowRawReferences => showRawReferences;

        /// <summary>
        /// Gets whether the floating Graph view options toolbar is expanded.
        /// </summary>
        internal bool ViewOptionsExpanded => viewOptionsExpanded;

        /// <summary>Gets or sets whether all derived Service scopes are visible in the Graph view.</summary>
        internal bool ShowServices
        {
            get => showServices;
            set
            {
                if (showServices == value) return;
                showServices = value;
                SaveViewState();
                canvas?.SetServiceVisibility(value);
                canvas?.RefreshViewOptions();
            }
        }

        /// <summary>Gets or sets whether the current Graph view draws its navigation grid.</summary>
        internal bool ShowGrid
        {
            get => showGrid;
            set
            {
                if (showGrid == value) return;
                showGrid = value;
                SaveViewState();
                canvas?.SetGridVisible(value);
            }
        }

        /// <summary>Gets or sets whether hand-dragged Graph nodes and movable boundaries snap to the navigation grid.</summary>
        internal bool SnapToGrid
        {
            get => snapToGrid;
            set
            {
                if (snapToGrid == value) return;
                snapToGrid = value;
                SaveViewState();
                canvas?.RefreshViewOptions();
            }
        }

        /// <summary>Gets or sets whether a graph drag recursively moves structural descendants or only the explicit selection.</summary>
        internal GraphMoveMode MoveMode
        {
            get => moveMode;
            set
            {
                if (moveMode == value) return;
                moveMode = value;
                SaveViewState();
                canvas?.RefreshViewOptions();
            }
        }

        /// <summary>
        /// Toggles the floating Graph view options toolbar and stores the state on the owning window.
        /// </summary>
        internal void ToggleViewOptions()
        {
            viewOptionsExpanded = !viewOptionsExpanded;
            SaveViewState();
            canvas?.RefreshViewOptions();
        }

        /// <summary>
        /// Loads Graph sidebar state from the owning editor window.
        /// </summary>
        private void LoadViewState()
        {
            GraphSidebarState state = editorWindow.GraphSidebarState;
            viewOptionsExpanded = state.viewOptionsExpanded;
            showGrid = state.showGrid;
            snapToGrid = state.snapToGrid;
            moveMode = state.moveMode;
            showServices = state.showServices;
            showRawReferences = state.showRawReferences;
            inspectorCollapsed = state.inspectorCollapsed;
        }

        /// <summary>
        /// Stores the current Graph sidebar state on the owning editor window.
        /// </summary>
        private void SaveViewState()
        {
            GraphSidebarState state = editorWindow.GraphSidebarState;
            state.viewOptionsExpanded = viewOptionsExpanded;
            state.showGrid = showGrid;
            state.snapToGrid = snapToGrid;
            state.moveMode = moveMode;
            state.showServices = showServices;
            state.showRawReferences = showRawReferences;
            state.inspectorCollapsed = inspectorCollapsed;
        }

        /// <summary>
        /// Gets the single inspector IMGUI container.
        /// </summary>
        internal IMGUIContainer InspectorContainer => inspectorContainer;

        /// <summary>Gets the ordered authored-node selection owned by the Graph page.</summary>
        internal IReadOnlyList<TreeNode> SelectedNodes => selectedNodeUUIDs
            .Select(uuid => tree?.GetNode(uuid))
            .Where(node => node != null)
            .ToArray();

        /// <summary>Gets whether the Graph selection contains the authored node.</summary>
        internal bool IsNodeSelected(TreeNode node) => node != null && selectedNodeUUIDs.Contains(node.uuid);

        /// <summary>Gets the currently selected authored graph group.</summary>
        internal UUID SelectedGroupUUID => selectedGroupUUID;

        #region Attachment And View State

        /// <summary>
        /// Mounts the native graph controls into the UXML graph host.
        /// </summary>
        /// <param name="graphHost">The declared graph host element.</param>
        internal void Attach(VisualElement graphHost)
        {
            if (canvas != null)
            {
                viewPan = canvas.Pan;
                viewZoom = canvas.Zoom;
            }

            host = graphHost ?? throw new ArgumentNullException(nameof(graphHost));
            body = RequireElement<VisualElement>(host, "ai-editor-graph-body");
            VisualElement canvasHost = RequireElement<VisualElement>(body, "ai-editor-graph-canvas-host");
            splitter = RequireElement<VisualElement>(body, "ai-editor-graph-inspector-splitter");
            inspector = RequireElement<VisualElement>(body, "ai-editor-graph-inspector");
            VisualElement inspectorContentHost = RequireElement<VisualElement>(inspector, "ai-editor-graph-inspector-content-host");

            canvas = new GraphCanvasElement(this);
            canvas.Pan = viewPan;
            canvas.Zoom = viewZoom;
            canvasHost.Clear();
            canvasHost.Add(canvas);

            splitter.UnregisterCallback<PointerDownEvent>(BeginResize);
            splitter.UnregisterCallback<PointerMoveEvent>(ResizeInspector);
            splitter.UnregisterCallback<PointerUpEvent>(EndResize);
            splitter.UnregisterCallback<PointerCancelEvent>(EndResize);
            splitter.RegisterCallback<PointerDownEvent>(BeginResize);
            splitter.RegisterCallback<PointerMoveEvent>(ResizeInspector);
            splitter.RegisterCallback<PointerUpEvent>(EndResize);
            splitter.RegisterCallback<PointerCancelEvent>(EndResize);
            inspector.style.width = inspectorWidth;
            inspectorContainer = new IMGUIContainer(DrawInspector)
            {
                name = "ai-editor-graph-inspector-imgui",
            };
            inspectorContainer.AddToClassList("ai-editor-graph-inspector-imgui");
            inspectorContentHost.Clear();
            inspectorContentHost.Add(inspectorContainer);
            RebuildTopology();
        }

        /// <summary>Resolves one required UXML element and reports a configuration error when it is absent.</summary>
        private static T RequireElement<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root?.Q<T>(name);
            return element ?? throw new InvalidOperationException(
                $"AI Editor Graph UXML element '{name}' is missing or is not a {typeof(T).Name}.");
        }

        /// <summary>
        /// Rebuilds topology and reapplies positions without writing the asset.
        /// </summary>
        internal void RebuildTopology()
        {
            RebuildTopology(null);
        }

        /// <summary>Rebuilds topology while preserving one-shot in-memory positions for an edit command.</summary>
        /// <param name="preservedPositions">Positions captured before a command changed topology semantics.</param>
        private void RebuildTopology(
            IReadOnlyDictionary<UUID, Vector2> preservedPositions,
            IReadOnlyDictionary<UUID, Vector2> positionOverrides = null)
        {
            if (host == null || !editorWindow)
            {
                return;
            }

            navigationAnchorUUID = UUID.Empty;
            canvas?.CloseCreationPalette();
            topologyTree = tree;
            topology = GraphTopologyBuilder.Build(tree, showRawReferences);
            GraphLayoutResolver.Resolve(tree, topology);
            if (preservedPositions != null)
            {
                foreach (GraphNodeDescriptor node in topology.Nodes)
                {
                    if (preservedPositions.TryGetValue(node.UUID, out Vector2 position))
                    {
                        node.Position = position;
                    }
                }
            }

            if (positionOverrides != null)
            {
                foreach (GraphNodeDescriptor node in topology.Nodes)
                {
                    if (positionOverrides.TryGetValue(node.UUID, out Vector2 position))
                    {
                        node.Position = position;
                    }
                }

                // The tree mutation already owns the current Undo transaction. Persist the
                // final handoff in that same transaction so a later rebuild cannot resurrect
                // the stale pre-mutation coordinates.
                tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
                EditorUtility.SetDirty(tree);
            }
            else if (preservedPositions != null)
            {
                // Structural edits retain the visible positions and must persist them with
                // the tree mutation, rather than only keeping them in the transient snapshot.
                tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
                EditorUtility.SetDirty(tree);
            }

            canvas?.SetTopology(topology);
            PruneSelection();
            if (selectedNodeUUIDs.Count == 0 && SelectedNode is not null and not EditorHeadNode
                && tree?.GetNode(SelectedNode.uuid) == SelectedNode)
            {
                selectedNodeUUIDs.Add(SelectedNode.uuid);
            }
            canvas?.SetSelectedNodes(selectedNodeUUIDs);
            UpdateInspectorVisibility();
            inspectorContainer?.MarkDirtyRepaint();

            RequestInitialFrameForVisibleTree();
        }

        /// <summary>Captures the current resolved Graph positions without materializing them into asset layout data.</summary>
        /// <returns>The current node positions, or an empty map before the Graph has been built.</returns>
        private Dictionary<UUID, Vector2> CaptureTopologyPositions()
        {
            if (topology == null)
            {
                return new Dictionary<UUID, Vector2>();
            }

            Dictionary<UUID, Vector2> positions = new(topology.Nodes.Count);
            foreach (GraphNodeDescriptor node in topology.Nodes)
            {
                GraphPresentationItem item = canvas?.Presentation?.Find(node.UUID);
                positions[node.UUID] = item?.Position ?? node.Position;
            }

            return positions;
        }

        /// <summary>
        /// Synchronizes lightweight view state without rebuilding the topology snapshot.
        /// </summary>
        internal void UpdateView()
        {
            if (host == null || !editorWindow)
            {
                return;
            }

            if (topologyTree != tree || topology == null)
            {
                RebuildTopology();
                return;
            }

            PruneSelection();
            canvas?.SetSelectedNodes(selectedNodeUUIDs);
            UpdateInspectorVisibility();
            inspectorContainer?.MarkDirtyRepaint();
            RequestInitialFrameForVisibleTree();
        }

        /// <summary>Requests the first tree frame only after the Graph page becomes visible.</summary>
        private void RequestInitialFrameForVisibleTree()
        {
            if (tree == null || framedTree == tree || host?.style.display.value != DisplayStyle.Flex)
            {
                return;
            }

            framedTree = tree;
            canvas?.RequestInitialFrameWhenGeometryIsValid();
        }

        private void UpdateInspectorVisibility()
        {
            inspector?.SetEnabled(!inspectorCollapsed);
            if (inspector != null)
            {
                inspector.style.display = inspectorCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (splitter != null)
            {
                splitter.style.display = inspectorCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            canvas?.RefreshViewOptions();
        }

        #endregion

        /// <summary>Gets the Nodes page owner for shared clipboard command semantics.</summary>
        internal TreeNodeModule TreeModule => editorWindow ? editorWindow.TreeModule : null;

        /// <summary>Gets the shared page-neutral node command service.</summary>
        internal NodeEditorCommandService NodeCommands => editorWindow ? editorWindow.NodeCommands : null;

        #region Selection

        /// <summary>
        /// Selects a node through the window's authoritative TreeNodeModule.
        /// </summary>
        /// <param name="node">The node selected in the canvas.</param>
        internal void SelectNode(TreeNode node)
        {
            SetGraphSelection(node == null ? Array.Empty<TreeNode>() : new[] { node });
        }

        /// <summary>Applies one node pointer gesture to the Graph-only selection.</summary>
        internal void SelectNode(TreeNode node, bool toggle, bool additive)
        {
            if (!editorWindow || node == null || tree?.GetNode(node.uuid) != node)
            {
                return;
            }

            List<TreeNode> next = SelectedNodes.ToList();
            int existing = next.FindIndex(item => item.uuid == node.uuid);
            if (toggle)
            {
                if (existing >= 0) next.RemoveAt(existing);
                else next.Add(node);
            }
            else if (additive)
            {
                if (existing < 0) next.Add(node);
            }
            else if (existing < 0)
            {
                next.Clear();
                next.Add(node);
            }

            SetGraphSelection(next);
        }

        /// <summary>Replaces or extends the Graph selection with authored nodes.</summary>
        internal void SetGraphSelection(IEnumerable<TreeNode> nodes, bool additive = false)
        {
            if (!editorWindow)
            {
                return;
            }

            navigationAnchorUUID = UUID.Empty;

            List<UUID> next = additive ? new List<UUID>(selectedNodeUUIDs) : new List<UUID>();
            foreach (TreeNode node in nodes ?? Enumerable.Empty<TreeNode>())
            {
                if (node != null && tree?.GetNode(node.uuid) == node && !next.Contains(node.uuid))
                {
                    next.Add(node.uuid);
                }
            }

            selectedNodeUUIDs.Clear();
            selectedNodeUUIDs.AddRange(next);
            TreeNode windowNode = selectedNodeUUIDs.Count == 1 ? tree.GetNode(selectedNodeUUIDs[0]) : null;
            synchronizingWindowSelection = true;
            try
            {
                editorWindow.SelectedNode = windowNode;
            }
            finally
            {
                synchronizingWindowSelection = false;
            }

            canvas?.SetSelectedNodes(selectedNodeUUIDs);
            if (selectedNodeUUIDs.Count > 0 || !additive)
            {
                selectedGroupUUID = UUID.Empty;
                canvas?.SetSelectedGroup(UUID.Empty);
            }
            inspectorContainer?.MarkDirtyRepaint();
            editorWindow.Repaint();
        }

        /// <summary>Selects one authored graph group and clears authored node selection.</summary>
        /// <param name="groupUUID">The group UUID to select.</param>
        internal void SelectGroup(UUID groupUUID)
        {
            if (groupUUID == UUID.Empty || tree?.GraphLayout?.Groups.All(group => group.UUID != groupUUID) != false)
            {
                return;
            }

            selectedGroupUUID = groupUUID;
            SetGraphSelection(Array.Empty<TreeNode>());
            selectedGroupUUID = groupUUID;
            canvas?.SetSelectedGroup(groupUUID);
        }

        /// <summary>Removes selection entries that no longer belong to the active tree.</summary>
        private void PruneSelection()
        {
            selectedNodeUUIDs.RemoveAll(uuid => tree?.GetNode(uuid) == null);
            if (navigationAnchorUUID != UUID.Empty && tree?.GetNode(navigationAnchorUUID) == null)
            {
                navigationAnchorUUID = UUID.Empty;
            }
        }

        /// <summary>Selects the legacy editor-only Head placeholder so its dedicated Inspector is shown.</summary>
        internal void SelectEntrance()
        {
            if (editorWindow?.TreeModule != null)
            {
                editorWindow.SelectedNode = editorWindow.TreeModule.EditorHeadNode;
            }
        }

        /// <summary>
        /// Updates graph selection visuals when another editor page selects a node.
        /// </summary>
        /// <param name="node">The newly selected node.</param>
        internal void OnSelectionChanged(TreeNode node)
        {
            if (!editorWindow)
            {
                return;
            }

            if (!synchronizingWindowSelection)
            {
                selectedNodeUUIDs.Clear();
                navigationAnchorUUID = UUID.Empty;
                if (node != null && node is not EditorHeadNode && tree?.GetNode(node.uuid) == node)
                {
                    selectedNodeUUIDs.Add(node.uuid);
                }
            }

            canvas?.SetSelectedNodes(selectedNodeUUIDs);
            inspectorContainer?.MarkDirtyRepaint();
            editorWindow.Repaint();
        }

        #endregion

        #region Keyboard Navigation

        /// <summary>Moves the transient Graph selection to a spatially adjacent authored node.</summary>
        /// <param name="direction">The direction in graph space.</param>
        /// <param name="extend">Whether the target is appended to the current selection.</param>
        /// <returns><c>true</c> when the key was handled by the Graph navigation layer.</returns>
        internal bool NavigateSelection(GraphNavigationDirection direction, bool extend)
        {
            if (!editorWindow || canvas == null)
            {
                return false;
            }

            IReadOnlyList<GraphNavigationCandidate> candidates = canvas.GetNavigableCandidates();
            if (candidates.Count == 0)
            {
                return false;
            }

            GraphNavigationCandidate? current = FindNavigationAnchor(candidates);
            GraphNavigationCandidate target;
            if (!current.HasValue)
            {
                target = candidates
                    .OrderBy(candidate => (candidate.Bounds.center - canvas.ViewportCenterGraph).sqrMagnitude)
                    .ThenBy(candidate => candidate.PresentationOrder)
                    .First();
            }
            else
            {
                target = FindDirectionalCandidate(current.Value, candidates, direction);
                if (target.UUID == UUID.Empty)
                {
                    return true;
                }
            }

            TreeNode targetNode = tree?.GetNode(target.UUID);
            if (targetNode == null)
            {
                navigationAnchorUUID = UUID.Empty;
                return true;
            }

            List<TreeNode> next = extend ? SelectedNodes.ToList() : new List<TreeNode>();
            if (!next.Any(node => node.uuid == target.UUID))
            {
                next.Add(targetNode);
            }

            SetGraphSelection(next);
            navigationAnchorUUID = target.UUID;
            canvas.RevealGraphBounds(target.Bounds);
            return true;
        }

        /// <summary>Resolves the current navigation anchor or the last navigable selected node.</summary>
        private GraphNavigationCandidate? FindNavigationAnchor(IReadOnlyList<GraphNavigationCandidate> candidates)
        {
            if (navigationAnchorUUID != UUID.Empty)
            {
                GraphNavigationCandidate anchored = candidates.FirstOrDefault(candidate => candidate.UUID == navigationAnchorUUID);
                if (anchored.UUID != UUID.Empty)
                {
                    return anchored;
                }
            }

            for (int index = selectedNodeUUIDs.Count - 1; index >= 0; index--)
            {
                GraphNavigationCandidate selected = candidates.FirstOrDefault(candidate => candidate.UUID == selectedNodeUUIDs[index]);
                if (selected.UUID != UUID.Empty)
                {
                    return selected;
                }
            }

            return null;
        }

        /// <summary>Finds the best candidate in one direction using visual-center distance scoring.</summary>
        private static GraphNavigationCandidate FindDirectionalCandidate(
            GraphNavigationCandidate current,
            IReadOnlyList<GraphNavigationCandidate> candidates,
            GraphNavigationDirection direction)
        {
            Vector2 axis = direction switch
            {
                GraphNavigationDirection.Left => Vector2.left,
                GraphNavigationDirection.Right => Vector2.right,
                GraphNavigationDirection.Up => Vector2.down,
                _ => Vector2.up,
            };
            Vector2 lateralAxis = new(-axis.y, axis.x);
            Vector2 origin = current.Bounds.center;
            GraphNavigationCandidate best = default;
            float bestScore = float.PositiveInfinity;
            float bestDistance = float.PositiveInfinity;
            foreach (GraphNavigationCandidate candidate in candidates)
            {
                if (candidate.UUID == current.UUID)
                {
                    continue;
                }

                Vector2 delta = candidate.Bounds.center - origin;
                float axialDistance = Vector2.Dot(delta, axis);
                if (axialDistance <= 0f)
                {
                    continue;
                }

                float lateralDistance = Mathf.Abs(Vector2.Dot(delta, lateralAxis));
                float score = axialDistance + lateralDistance * 2f;
                float straightDistance = delta.sqrMagnitude;
                if (score < bestScore
                    || (Mathf.Approximately(score, bestScore) && straightDistance < bestDistance)
                    || (Mathf.Approximately(score, bestScore) && Mathf.Approximately(straightDistance, bestDistance)
                        && candidate.PresentationOrder < best.PresentationOrder))
                {
                    best = candidate;
                    bestScore = score;
                    bestDistance = straightDistance;
                }
            }

            return best;
        }

        #endregion

        #region Node Lifecycle Commands

        /// <summary>Checks whether an authored node can become the Graph tree head.</summary>
        /// <param name="node">The candidate authored node.</param>
        /// <returns><c>true</c> when the candidate belongs to this tree, is not a Service, and is not already Head.</returns>
        internal bool CanSetHead(TreeNode node)
        {
            return editorWindow
                && tree
                && node != null
                && node is not Service
                && tree.GetNode(node.uuid) == node
                && tree.CanSetHead(node.uuid, allowMoveExisting: false);
        }

        /// <summary>Sets the authored Graph tree head without changing parents, references, or layout.</summary>
        /// <param name="node">The authored node to make Head.</param>
        /// <returns><c>true</c> when the head changed and the Graph was rebuilt.</returns>
        internal bool SetHead(TreeNode node)
        {
            if (!CanSetHead(node))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TrySetHead(node.uuid, "Set tree Head"))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Checks whether the editor-only Entrance can target an authored node.</summary>
        /// <param name="targetUUID">The candidate authored node UUID.</param>
        /// <returns>The validation result without writing serialized data or Undo state.</returns>
        internal bool CanAssignEntrance(UUID targetUUID)
        {
            if (!editorWindow || !tree)
            {
                return false;
            }

            TreeNode target = tree.GetNode(targetUUID);
            return target != null && target is not Service && tree.CanSetHead(targetUUID, allowMoveExisting: true);
        }

        /// <summary>Assigns the editor-only Entrance to one authored non-Service node.</summary>
        /// <param name="targetUUID">The authored node UUID selected by the Entrance gesture.</param>
        /// <returns><c>true</c> when the Head changed and the Graph was rebuilt.</returns>
        internal bool AssignEntrance(UUID targetUUID)
        {
            if (!CanAssignEntrance(targetUUID))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryMoveToHead(targetUUID, "Set tree Head"))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Clears the Head represented by the editor-only Entrance relation.</summary>
        /// <returns><c>true</c> when the Head was cleared and the Graph was rebuilt.</returns>
        internal bool DisconnectEntrance()
        {
            if (!editorWindow || !tree || tree.headNodeUUID == UUID.Empty)
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TrySetHead(UUID.Empty, "Disconnect tree Entrance"))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Renames an authored node as one undoable graph command.</summary>
        internal bool RenameNode(TreeNode node, string value)
        {
            string name = value?.Trim();
            if (!editorWindow || !tree || node == null || tree.GetNode(node.uuid) != node
                || string.IsNullOrEmpty(name) || node.name == name)
            {
                return false;
            }

            Undo.RecordObject(tree, $"Rename {node.name}");
            node.name = name;
            tree.SerializedObject.Update();
            tree.RegenerateTable();
            EditorUtility.SetDirty(tree);
            SelectNode(node);
            RebuildTopology();
            return true;
        }

        /// <summary>Copies a node through the shared editor command service.</summary>
        internal void CopyNode(TreeNode node, bool includeSubtree) => NodeCommands?.Copy(node, includeSubtree);

        /// <summary>Copies the current authored Graph selection and its relative layout.</summary>
        internal bool CopySelectedNodes()
        {
            IReadOnlyList<TreeNode> nodes = SelectedNodes;
            if (nodes.Count == 0 || topology == null) return false;
            List<Vector2> positions = nodes.Select(node => topology.FindNode(node.uuid)?.Position ?? Vector2.zero).ToList();
            editorWindow.Clipboard.WriteGraphSelectionWithLayout(nodes, positions, tree, tree.GraphLayout);
            return editorWindow.Clipboard.IsGraphSelection;
        }

        /// <summary>Creates one non-nested annotation frame around the current multi-selection.</summary>
        /// <returns><c>true</c> when a group was authored.</returns>
        internal bool GroupSelection()
        {
            if (!editorWindow || !tree || SelectedNodes.Count < 2) return false;
            GraphLayoutData layout = tree.GraphLayout ?? GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>());
            HashSet<UUID> members = SelectedNodes.Select(node => node.uuid).ToHashSet();
            Undo.RegisterCompleteObjectUndo(tree, "Group AI graph selection");
            RemoveMembersFromOtherGroups(layout, members);
            layout.AddGroup("Group", new Color(0.25f, 0.55f, 0.9f, 0.22f), members);
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, layout);
            EditorUtility.SetDirty(tree);
            canvas?.RefreshPresentationGeometry();
            return true;
        }

        /// <summary>Removes one annotation frame while retaining its authored members.</summary>
        /// <param name="groupUUID">The group UUID.</param>
        internal bool Ungroup(UUID groupUUID)
        {
            if (!(tree?.GraphLayout?.Groups.Any(group => group.UUID == groupUUID) == true)) return false;
            Undo.RegisterCompleteObjectUndo(tree, "Ungroup AI graph selection");
            tree.GraphLayout.RemoveGroup(groupUUID);
            if (selectedGroupUUID == groupUUID)
            {
                selectedGroupUUID = UUID.Empty;
            }
            EditorUtility.SetDirty(tree);
            canvas?.RefreshPresentationGeometry();
            return true;
        }

        /// <summary>Renames one annotation frame in the editor-only layout.</summary>
        /// <param name="groupUUID">The group UUID.</param><param name="title">The new title.</param>
        internal bool RenameGroup(UUID groupUUID, string title)
        {
            if (string.IsNullOrWhiteSpace(title) || tree?.GraphLayout == null) return false;
            GraphGroupLayoutEntry group = tree.GraphLayout.Groups.FirstOrDefault(item => item.UUID == groupUUID);
            if (group.UUID == UUID.Empty || string.Equals(group.Title, title.Trim(), StringComparison.Ordinal)) return false;
            Undo.RegisterCompleteObjectUndo(tree, "Rename AI graph group");
            tree.GraphLayout.ReplaceGroup(group.WithTitle(title.Trim()));
            EditorUtility.SetDirty(tree);
            canvas?.RefreshPresentationGeometry();
            return true;
        }

        /// <summary>Changes one annotation frame preset color.</summary>
        /// <param name="groupUUID">The group UUID.</param><param name="color">The preset color.</param>
        internal bool SetGroupColor(UUID groupUUID, Color color)
        {
            if (tree?.GraphLayout == null) return false;
            GraphGroupLayoutEntry group = tree.GraphLayout.Groups.FirstOrDefault(item => item.UUID == groupUUID);
            if (group.UUID == UUID.Empty || group.Color == color) return false;
            Undo.RegisterCompleteObjectUndo(tree, "Change AI graph group color");
            tree.GraphLayout.ReplaceGroup(group.WithColor(color));
            EditorUtility.SetDirty(tree);
            canvas?.RefreshPresentationGeometry();
            return true;
        }

        /// <summary>Adds the current selected authored nodes to an existing group.</summary>
        /// <param name="groupUUID">The group UUID.</param>
        internal bool AddSelectedToGroup(UUID groupUUID)
        {
            if (tree?.GraphLayout == null || SelectedNodes.Count == 0) return false;
            GraphGroupLayoutEntry group = tree.GraphLayout.Groups.FirstOrDefault(item => item.UUID == groupUUID);
            if (group.UUID == UUID.Empty) return false;
            HashSet<UUID> selected = SelectedNodes.Select(node => node.uuid).ToHashSet();
            bool alreadyInTarget = selected.All(group.Members.Contains)
                && !tree.GraphLayout.Groups.Any(item => item.UUID != groupUUID && item.Members.Any(selected.Contains));
            if (alreadyInTarget) return false;
            Undo.RegisterCompleteObjectUndo(tree, "Add AI graph nodes to group");
            RemoveMembersFromOtherGroups(tree.GraphLayout, selected, groupUUID);
            tree.GraphLayout.ReplaceGroup(new GraphGroupLayoutEntry(group.UUID, group.Title, group.Color,
                group.Members.Concat(selected)));
            EditorUtility.SetDirty(tree);
            canvas?.RefreshPresentationGeometry();
            return true;
        }

        /// <summary>Removes the current selected authored nodes from an existing group.</summary>
        /// <param name="groupUUID">The group UUID.</param>
        internal bool RemoveSelectedFromGroup(UUID groupUUID)
        {
            if (tree?.GraphLayout == null || SelectedNodes.Count == 0) return false;
            GraphGroupLayoutEntry group = tree.GraphLayout.Groups.FirstOrDefault(item => item.UUID == groupUUID);
            if (group.UUID == UUID.Empty) return false;
            HashSet<UUID> selected = SelectedNodes.Select(node => node.uuid).ToHashSet();
            if (!group.Members.Any(selected.Contains)) return false;
            Undo.RegisterCompleteObjectUndo(tree, "Remove AI graph nodes from group");
            tree.GraphLayout.ReplaceGroup(new GraphGroupLayoutEntry(group.UUID, group.Title, group.Color,
                group.Members.Where(member => !selected.Contains(member))));
            if (tree.GraphLayout.Groups.FirstOrDefault(item => item.UUID == groupUUID).Members.Count == 0)
                tree.GraphLayout.RemoveGroup(groupUUID);
            EditorUtility.SetDirty(tree);
            canvas?.RefreshPresentationGeometry();
            return true;
        }

        /// <summary>Moves a group and all current members by one graph-space delta.</summary>
        /// <param name="groupUUID">The group UUID.</param><param name="delta">The graph-space delta.</param>
        internal bool MoveGroup(UUID groupUUID, Vector2 delta)
        {
            GraphGroupLayoutEntry group = tree?.GraphLayout?.Groups.FirstOrDefault(item => item.UUID == groupUUID) ?? default;
            if (group.UUID == UUID.Empty || topology == null) return false;
            if (delta.sqrMagnitude <= 0.0001f) return false;
            groupMoved = true;
            HashSet<UUID> moved = new();
            foreach (UUID member in group.Members)
            {
                GraphNodeDescriptor node = topology.FindNode(member);
                if (node == null) continue;
                GraphNodeDescriptor anchor = canvas == null ? node : canvas.GetMoveAnchor(node);
                if (anchor == null) continue;
                foreach (GraphNodeDescriptor affected in GetMoveGroup(anchor))
                {
                    if (moved.Add(affected.UUID)) affected.Position += delta;
                }
            }
            canvas?.UpdatePresentationPositions(topology.Nodes, preserveGroupElements: true);
            canvas?.TranslateGroupElement(groupUUID, delta);
            return true;
        }

        /// <summary>Commits one completed group drag as one undoable layout write.</summary>
        internal void CommitGroupMove()
        {
            if (!editorWindow || !tree || topology == null || !groupMoved) { groupMoved = false; return; }
            Undo.RegisterCompleteObjectUndo(tree, "Move AI graph group");
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
            EditorUtility.SetDirty(tree);
            groupMoved = false;
            canvas?.RefreshPresentationGeometry();
        }

        /// <summary>Removes selected members from all other groups without changing other metadata.</summary>
        /// <param name="layout">The mutable layout.</param><param name="members">Members to move.</param>
        /// <param name="keepGroupUUID">Optional destination group to retain.</param>
        private static void RemoveMembersFromOtherGroups(GraphLayoutData layout, ISet<UUID> members, UUID keepGroupUUID = default)
        {
            foreach (GraphGroupLayoutEntry existing in layout.Groups.ToList())
            {
                if (existing.UUID == keepGroupUUID || !existing.Members.Any(members.Contains)) continue;
                GraphGroupLayoutEntry updated = new(existing.UUID, existing.Title, existing.Color,
                    existing.Members.Where(member => !members.Contains(member)));
                if (updated.Members.Count == 0) layout.RemoveGroup(existing.UUID);
                else layout.ReplaceGroup(updated);
            }
        }

        /// <summary>Pastes a detached Graph selection centered at the requested graph position.</summary>
        internal bool PasteGraphSelection(Vector2 center)
        {
            if (!editorWindow || !tree || !editorWindow.Clipboard.TryGetGraphSelection(
                    out List<TreeNode> nodes, out List<Vector2> positions, out List<GraphGroupLayoutEntry> groups))
            {
                return false;
            }

            foreach (TreeNode node in nodes) node.name = tree.GenerateNewNodeName(node.name);
            Vector2 sourceCenter = GetPositionBoundsCenter(positions);
            Vector2 delta = center - sourceCenter;
            Dictionary<UUID, Vector2> graphPositions = nodes
                .Select((node, index) => (node.uuid, Position: positions[index] + delta))
                .ToDictionary(item => item.uuid, item => item.Position);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Paste AI graph selection");
            Undo.RegisterCompleteObjectUndo(tree, "Paste AI graph selection");
            if (!tree.TryAddNodes(nodes, "Paste AI graph selection", graphPositions, recordUndo: false))
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return false;
            }

            RebuildTopology();
            if (groups != null && groups.Count > 0)
            {
                GraphLayoutData layout = tree.GraphLayout ?? GraphLayoutData.Create(Array.Empty<GraphLayoutEntry>());
                foreach (GraphGroupLayoutEntry group in groups) layout.AddGroup(group.Title, group.Color, group.Members);
                tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, layout);
            }
            SetGraphSelection(nodes);
            RebuildTopology();
            Undo.CollapseUndoOperations(undoGroup);
            return true;
        }

        /// <summary>Duplicates the current Graph selection as one detached, offset subgraph transaction.</summary>
        internal bool DuplicateSelectedNodes()
        {
            IReadOnlyList<TreeNode> nodes = SelectedNodes;
            if (nodes.Count == 0 || topology == null || !CopySelectedNodes()) return false;
            Vector2 center = GetPositionBoundsCenter(nodes
                .Select(node => topology.FindNode(node.uuid)?.Position ?? Vector2.zero));
            return PasteGraphSelection(center + new Vector2(30f, 30f));
        }

        /// <summary>Gets the center of an axis-aligned position set.</summary>
        private static Vector2 GetPositionBoundsCenter(IEnumerable<Vector2> source)
        {
            Vector2[] positions = source?.ToArray() ?? Array.Empty<Vector2>();
            if (positions.Length == 0) return Vector2.zero;
            float minX = positions.Min(position => position.x);
            float minY = positions.Min(position => position.y);
            float maxX = positions.Max(position => position.x);
            float maxY = positions.Max(position => position.y);
            return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        /// <summary>Duplicates a node and rebuilds the visible graph after its transaction commits.</summary>
        internal bool DuplicateNode(TreeNode node)
        {
            Vector2 position = topology?.FindNode(node?.uuid ?? UUID.Empty)?.Position ?? Vector2.zero;
            TreeNode duplicate = NodeCommands?.Duplicate(node, position + new Vector2(30f, 30f));
            if (duplicate == null)
            {
                return false;
            }

            SelectNode(duplicate);
            RebuildTopology();
            return true;
        }

        /// <summary>Pastes compatible values while retaining the target node identity.</summary>
        internal bool PasteValue(TreeNode node)
        {
            if (NodeCommands?.PasteValue(node) != true)
            {
                return false;
            }

            tree.SerializedObject.Update();
            tree.RegenerateTable();
            SelectNode(node);
            RebuildTopology();
            return true;
        }

        /// <summary>Pastes clipboard structure into one single-reference slot.</summary>
        internal bool PasteTo(TreeNode owner, INodeReferenceSingleSlot slot)
        {
            TreeNode pasted = NodeCommands?.PasteTo(owner, slot, GetPastePosition(owner));
            if (pasted == null)
            {
                if (NodeCommands?.CanPasteStructure == true)
                    ShowConnectionRejectedNotification();
                return false;
            }
            SelectNode(pasted);
            RebuildTopology();
            return true;
        }

        /// <summary>Pastes clipboard structure into one list-reference slot position.</summary>
        internal bool PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index)
        {
            TreeNode pasted = NodeCommands?.PasteAt(owner, slot, index, GetPastePosition(owner));
            if (pasted == null)
            {
                if (NodeCommands?.CanPasteStructure == true)
                    ShowConnectionRejectedNotification();
                return false;
            }
            SelectNode(pasted);
            RebuildTopology();
            return true;
        }

        /// <summary>Gets a stable initial position for one node pasted under an authored owner.</summary>
        private Vector2 GetPastePosition(TreeNode owner)
        {
            return (topology?.FindNode(owner?.uuid ?? UUID.Empty)?.Position ?? Vector2.zero)
                + new Vector2(30f, 30f);
        }

        /// <summary>Builds the delete confirmation data directly from the current topology.</summary>
        internal bool TryAnalyzeDelete(UUID targetUUID, out GraphNodeDeleteImpact impact)
        {
            TreeNode target = tree?.GetNode(targetUUID);
            if (target == null)
            {
                impact = default;
                return false;
            }

            NodeTopologySnapshot snapshot = NodeTopologySnapshot.Create(tree.EditorNodes);
            IReadOnlyList<NodeReferenceOccurrence> incoming = snapshot.GetIncoming(target);
            int structural = incoming.Count(occurrence => occurrence.Kind == NodeOwnershipKind.Structural);
            int services = incoming.Count(occurrence => occurrence.Kind == NodeOwnershipKind.Service);
            UUID parentUUID = incoming.FirstOrDefault(occurrence => occurrence.Kind == NodeOwnershipKind.Structural).Owner?.uuid ?? UUID.Empty;
            int children = target.GetChildrenReference().Count(reference => reference?.UUID != UUID.Empty);
            impact = new GraphNodeDeleteImpact(
                tree.Head == target,
                parentUUID,
                structural,
                services,
                snapshot.GetRawIncomingCount(target),
                children);
            return true;
        }

        /// <summary>Confirms and atomically deletes one authored node and all incoming references.</summary>
        internal bool TryDeleteNode(TreeNode node)
        {
            if (!editorWindow || !tree || node == null || tree.GetNode(node.uuid) != node)
                return false;

            if (!TryAnalyzeDelete(node.uuid, out GraphNodeDeleteImpact impact))
                return false;

            string message = $"Delete '{node.name}'?\n\n"
                + $"Structural references: {impact.StructuralIncoming}\n"
                + $"Service references: {impact.ServiceIncoming}\n"
                + $"Raw references: {impact.RawIncoming}\n\n"
                + $"Direct child nodes kept detached: {impact.DirectStructuralChildCount}.";
            if (!EditorUtility.DisplayDialog("Delete Graph Node", message, "Delete", "Cancel"))
                return false;

            return CommitDeleteNode(node, impact);
        }

        /// <summary>Confirms and deletes the complete Graph selection as one Undo transaction.</summary>
        internal bool DeleteSelectedNodes()
        {
            IReadOnlyList<TreeNode> nodes = SelectedNodes;
            if (nodes.Count == 0) return false;
            if (nodes.Count == 1) return TryDeleteNode(nodes[0]);

            HashSet<UUID> selected = nodes.Select(node => node.uuid).ToHashSet();
            int structural = 0;
            int services = 0;
            int raw = 0;
            int detachedChildren = 0;
            bool removesHead = false;
            foreach (TreeNode node in nodes)
            {
                if (!TryAnalyzeDelete(node.uuid, out GraphNodeDeleteImpact impact)) return false;
                structural += impact.StructuralIncoming;
                services += impact.ServiceIncoming;
                raw += impact.RawIncoming;
                removesHead |= impact.IsHead;
                detachedChildren += node.GetChildrenReference().Count(reference =>
                    reference?.UUID != UUID.Empty && !selected.Contains(reference.UUID));
            }

            string message = $"Delete {nodes.Count} selected graph nodes?\n\n"
                + $"Structural references: {structural}\nService references: {services}\nRaw references: {raw}\n"
                + $"Tree Head removed: {(removesHead ? "Yes" : "No")}\n\n"
                + $"Direct child nodes kept detached: {detachedChildren}.";
            if (!EditorUtility.DisplayDialog("Delete Graph Nodes", message, "Delete", "Cancel")) return false;

            return CommitDeleteSelectedNodes(nodes);
        }

        /// <summary>Commits an already-confirmed multi-node deletion as one Undo transaction.</summary>
        internal bool CommitDeleteSelectedNodes(IReadOnlyList<TreeNode> nodes)
        {
            if (!editorWindow || !tree || nodes == null || nodes.Count == 0
                || nodes.Any(node => node == null || tree.GetNode(node.uuid) != node))
            {
                return false;
            }

            HashSet<UUID> removed = nodes.Select(node => node.uuid).ToHashSet();
            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryDeleteNodesWithDecoratorUnwrap(removed, $"Delete {nodes.Count} AI graph nodes"))
            {
                return false;
            }

            SetGraphSelection(Array.Empty<TreeNode>());
            RebuildTopology(positions);
            return true;
        }

        /// <summary>Commits an already-confirmed graph deletion without opening UI.</summary>
        internal bool CommitDeleteNode(TreeNode node, GraphNodeDeleteImpact impact)
        {
            if (!editorWindow || !tree || node == null || tree.GetNode(node.uuid) != node)
                return false;

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryDeleteNodesWithDecoratorUnwrap(
                    new HashSet<UUID> { node.uuid },
                    $"Delete AI graph node {node.name}"))
            {
                return false;
            }

            SelectNode(impact.ParentUUID == UUID.Empty ? null : tree.GetNode(impact.ParentUUID));
            RebuildTopology(positions);
            return true;
        }

        /// <summary>
        /// Creates one authored node at a graph position and optionally assigns it to a source port.
        /// </summary>
        /// <param name="nodeType">The concrete node type selected by the graph palette.</param>
        /// <param name="position">The persistent graph-space position for the new node.</param>
        /// <param name="port">An optional authored port that receives the new node.</param>
        /// <returns>True only when the complete creation command committed.</returns>
        internal bool CreateNode(Type nodeType, Vector2 position, GraphPortDescriptor port = null)
        {
            return CreateNode(nodeType, position, port, setAsEntranceHead: false);
        }

        /// <summary>Creates one ordinary authored node and atomically assigns it as the tree Head.</summary>
        /// <param name="nodeType">The concrete non-Service node type selected from the Entrance palette.</param>
        /// <param name="position">The persistent graph-space position for the new node.</param>
        /// <returns>True only when node creation and Head assignment both committed.</returns>
        internal bool CreateEntranceNode(Type nodeType, Vector2 position)
        {
            return CreateNode(nodeType, position, null, setAsEntranceHead: true);
        }

        /// <summary>Executes the shared node-creation transaction for regular ports and the editor-only Entrance.</summary>
        private bool CreateNode(Type nodeType, Vector2 position, GraphPortDescriptor port, bool setAsEntranceHead)
        {
            if (!editorWindow || !tree || !NodeMenuCache.IsCreatableNodeType(nodeType))
            {
                return false;
            }

            bool requiresService = port?.AnchorKind == GraphPortAnchorKind.Service;
            if (port != null && typeof(Service).IsAssignableFrom(nodeType) != requiresService)
            {
                return false;
            }

            if (setAsEntranceHead && typeof(Service).IsAssignableFrom(nodeType))
            {
                return false;
            }

            string undoName = setAsEntranceHead
                ? "Create and set tree Head"
                : port == null ? "Create AI graph node" : "Create and connect AI graph node";
            try
            {
                TreeNode node = NodeFactory.Create(nodeType);
                node.name = tree.GenerateNewNodeName(NodeMenuCache.Shared.GetDisplayName(nodeType));
                Dictionary<UUID, Vector2> graphPositions = new() { [node.uuid] = position };
                IReadOnlyList<TreeNode> addedNodes = new[] { node };
                bool committed = setAsEntranceHead
                    ? tree.TryAddAndSetHead(addedNodes, node.uuid, undoName, graphPositions)
                    : port == null
                        ? tree.TryAddNodes(addedNodes, undoName, graphPositions)
                        : (port.Operation == GraphPortOperation.Wrap || port.Operation == GraphPortOperation.Replace)
                            && node is Decorator && port.Address.FieldName != nameof(Decorator.node)
                            ? tree.TryAddAndWrapReference(port.Address,
                                addedNodes, node.uuid, undoName, graphPositions)
                        : port.Operation == GraphPortOperation.Insert
                            ? tree.TryAddAndInsertReference(
                                port.Address, addedNodes, node.uuid, undoName, graphPositions)
                            : tree.TryAddAndSetReference(
                                port.Address, addedNodes, node.uuid, undoName, graphPositions);
                if (!committed)
                {
                    if (setAsEntranceHead || port != null)
                    {
                        ShowConnectionRejectedNotification();
                    }

                    return false;
                }

                SelectNode(node);
                RebuildTopology();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                return false;
            }
        }

        #endregion

        #region Port Commands

        /// <summary>Checks whether a topology edge identifies a movable authored collection occurrence.</summary>
        /// <param name="edge">The authoritative topology edge selected by the Graph.</param>
        /// <returns><c>true</c> when the edge has a real collection occurrence and target.</returns>
        internal bool CanReorder(GraphEdgeDescriptor edge)
        {
            return editorWindow
                && tree
                && edge != null
                && edge.Target != null
                && edge.Kind != GraphEdgeKind.Raw
                && edge.Reference.IsCollection
                && edge.Source?.Node != null
                && tree.GetNode(edge.Source.UUID) == edge.Source.Node;
        }

        /// <summary>Gets the current authored occurrence count for one collection edge.</summary>
        /// <param name="edge">The authoritative topology edge.</param>
        /// <returns>The collection size, or zero when the edge is not reorderable.</returns>
        internal int GetCollectionCount(GraphEdgeDescriptor edge)
        {
            if (!CanReorder(edge) || topology == null)
            {
                return 0;
            }

            return topology.Edges.Count(candidate => candidate.Source.UUID == edge.Source.UUID
                && candidate.Reference.Address.FieldName == edge.Reference.Address.FieldName
                && candidate.Reference.IsCollection);
        }

        /// <summary>Moves one authored collection occurrence and rebuilds the Graph once.</summary>
        /// <param name="edge">The authoritative topology edge identifying the occurrence.</param>
        /// <param name="destinationIndex">The destination collection index.</param>
        /// <returns><c>true</c> when the existing topology service committed the move.</returns>
        internal bool Reorder(GraphEdgeDescriptor edge, int destinationIndex)
        {
            if (!CanReorder(edge))
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryReorderReference(
                    edge.Reference.Address,
                    destinationIndex,
                    $"Reorder {edge.Reference.Address.FieldName}"))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Checks an authored port assignment without creating Undo state or dirtying the tree.</summary>
        internal bool CanAssign(GraphPortDescriptor port, UUID targetUUID)
        {
            if (!editorWindow || !tree || port == null)
            {
                return false;
            }

            return port.Operation switch
            {
                GraphPortOperation.Connect => tree.CanConnectReference(
                    port.Address,
                    targetUUID,
                    allowMoveExisting: !port.IsRaw),
                GraphPortOperation.Replace => tree.CanRedirectReferenceChain(port.Address, targetUUID)
                    || tree.CanReplaceReference(
                        port.Address,
                        targetUUID,
                        allowMoveExisting: !port.IsRaw),
                GraphPortOperation.Wrap => tree.CanWrapDecoratorChild(port.Address.OwnerUUID, targetUUID),
                GraphPortOperation.Insert => tree.CanRedirectReferenceChain(port.Address, targetUUID)
                    || tree.CanInsertReference(
                        port.Address,
                        targetUUID,
                        allowMoveExisting: !port.IsRaw
                            || port.Address.FieldName == nameof(ServiceHostNode.services)),
                _ => false,
            };
        }

        /// <summary>Executes one authored port command and rebuilds the graph only after a successful mutation.</summary>
        /// <remarks>Existing cards retain their current in-memory positions so connecting an edge does not interrupt editing.</remarks>
        internal bool Assign(GraphPortDescriptor port, UUID targetUUID)
        {
            if (!editorWindow || !tree || port == null)
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!TryAssign(port, targetUUID))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Moves one authored collection occurrence when no concrete target edge exists.</summary>
        internal bool ReorderCollection(
            UUID ownerUUID,
            string fieldName,
            int sourceIndex,
            int destinationIndex)
        {
            if (!editorWindow || !tree || sourceIndex < 0 || destinationIndex < 0)
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryReorderReference(
                    new NodeReferenceAddress(ownerUUID, fieldName, sourceIndex),
                    destinationIndex,
                    $"Reorder {fieldName}"))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Reorders one compact decorator stack through its tree-owned atomic mutation.</summary>
        internal bool ReorderDecoratorStack(IReadOnlyList<UUID> orderedDecorators)
        {
            if (!editorWindow || !tree || orderedDecorators == null || orderedDecorators.Count < 2)
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            GraphDecoratorStack stack = canvas?.Presentation?.FindDecoratorStack(orderedDecorators[0]);
            Dictionary<UUID, Vector2> overrides = null;
            if (stack?.Anchor.DecoratorPlaceholder != null && stack.Badges.Count > 0
                && stack.Badges[0].TargetUUID != orderedDecorators[0]
                && positions.TryGetValue(stack.Badges[0].TargetUUID, out Vector2 freeStackPosition))
            {
                // In a free stack the outer badge owns the persisted placement. Hand that
                // placement to the new outer badge before rebuilding the derived stack.
                overrides = new Dictionary<UUID, Vector2> { [orderedDecorators[0]] = freeStackPosition };
            }
            if (!tree.TryReorderDecoratorStack(orderedDecorators, "Reorder decorators"))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions, overrides);
            return true;
        }

        /// <summary>Wraps an existing target through the tree-owned Decorator transaction.</summary>
        internal bool WrapDecorator(UUID decoratorUUID, UUID targetUUID)
        {
            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryWrapDecoratorChild(decoratorUUID, targetUUID, "Wrap Decorator")) return false;
            RebuildTopology(positions);
            return true;
        }

        /// <summary>Checks Decorator wrapping without mutating tree state.</summary>
        internal bool CanWrapDecorator(UUID decoratorUUID, UUID targetUUID)
        {
            return editorWindow && tree && tree.CanWrapDecoratorChild(decoratorUUID, targetUUID);
        }

        /// <summary>Checks extraction and wrapping without mutating tree state.</summary>
        internal bool CanExtractAndWrapDecorator(UUID decoratorUUID, UUID targetUUID)
        {
            return editorWindow && tree && tree.CanExtractDecoratorAndWrapTarget(decoratorUUID, targetUUID);
        }

        /// <summary>Checks whether an empty Decorator can be detached from its current structural owner.</summary>
        internal bool CanDetachEmptyDecoratorToFree(UUID decoratorUUID)
        {
            return editorWindow && tree && tree.CanDetachEmptyDecoratorToFree(decoratorUUID);
        }

        /// <summary>Gets whether an empty Decorator is already a free graph root.</summary>
        internal bool IsFreeEmptyDecorator(UUID decoratorUUID)
        {
            return editorWindow && tree && tree.IsFreeEmptyDecorator(decoratorUUID);
        }

        /// <summary>Extracts a Decorator while preserving the current graph positions.</summary>
        internal bool ExtractDecoratorToFree(UUID decoratorUUID, Vector2 dropGraphPosition)
        {
            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            Dictionary<UUID, Vector2> overrides = new() { [decoratorUUID] = dropGraphPosition };
            if (!tree.TryExtractDecoratorToFree(decoratorUUID, "Extract Decorator")) return false;
            RebuildTopology(positions, overrides);
            return true;
        }

        /// <summary>Detaches an empty Decorator and persists its explicit free graph position.</summary>
        internal bool DetachEmptyDecoratorToFree(UUID decoratorUUID, Vector2 dropGraphPosition)
        {
            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            Dictionary<UUID, Vector2> overrides = new() { [decoratorUUID] = dropGraphPosition };
            if (!tree.TryDetachEmptyDecoratorToFree(decoratorUUID, "Detach empty Decorator")) return false;
            RebuildTopology(positions, overrides);
            return true;
        }

        /// <summary>Extracts a Decorator and wraps another target in one tree transaction.</summary>
        internal bool ExtractAndWrapDecorator(UUID decoratorUUID, UUID targetUUID)
        {
            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryExtractDecoratorAndWrapTarget(decoratorUUID, targetUUID, "Extract and Wrap Decorator")) return false;
            RebuildTopology(positions);
            return true;
        }

        /// <summary>Checks whether one selected decorator block can move together and wrap a target.</summary>
        internal bool CanExtractDecoratorBlockAndWrapTarget(IReadOnlyList<UUID> decoratorUUIDs, UUID targetUUID)
        {
            return editorWindow && tree
                && tree.CanExtractDecoratorBlockAndWrapTarget(decoratorUUIDs, targetUUID);
        }

        /// <summary>Moves one selected decorator block into a target occurrence as one topology transaction.</summary>
        internal bool ExtractDecoratorBlockAndWrapTarget(IReadOnlyList<UUID> decoratorUUIDs, UUID targetUUID)
        {
            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            if (!tree.TryExtractDecoratorBlockAndWrapTarget(decoratorUUIDs, targetUUID, "Move Decorator block"))
            {
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Moves one decorator badge within its currently visible compact stack.</summary>
        internal bool MoveDecoratorBadge(UUID decoratorUUID, int destinationIndex)
        {
            GraphDecoratorStack stack = canvas?.Presentation?.FindDecoratorStack(decoratorUUID);
            if (stack == null) return false;
            List<UUID> ordered = stack.Badges.Select(badge => badge.TargetUUID).ToList();
            int sourceIndex = ordered.IndexOf(decoratorUUID);
            if (sourceIndex < 0 || destinationIndex < 0 || destinationIndex >= ordered.Count || sourceIndex == destinationIndex)
                return false;
            ordered.RemoveAt(sourceIndex);
            ordered.Insert(destinationIndex, decoratorUUID);
            return ReorderDecoratorStack(ordered);
        }

        /// <summary>Moves one selected decorator block as a contiguous ordered segment of its current stack.</summary>
        internal bool MoveDecoratorBadgeBlock(IReadOnlyList<UUID> decoratorUUIDs, int destinationBoundary)
        {
            if (decoratorUUIDs == null || decoratorUUIDs.Count < 2)
            {
                return false;
            }

            GraphDecoratorStack stack = canvas?.Presentation?.FindDecoratorStack(decoratorUUIDs[0]);
            if (stack == null)
            {
                return false;
            }

            List<UUID> ordered = stack.Badges.Select(badge => badge.TargetUUID).ToList();
            HashSet<UUID> selected = decoratorUUIDs.ToHashSet();
            if (selected.Count != decoratorUUIDs.Count || !selected.All(ordered.Contains)
                || destinationBoundary < 0 || destinationBoundary > ordered.Count)
            {
                return false;
            }

            List<UUID> block = ordered.Where(selected.Contains).ToList();
            int removedBeforeBoundary = ordered.Take(destinationBoundary).Count(selected.Contains);
            List<UUID> reordered = ordered.Where(uuid => !selected.Contains(uuid)).ToList();
            int destination = Mathf.Clamp(destinationBoundary - removedBeforeBoundary, 0, reordered.Count);
            reordered.InsertRange(destination, block);
            if (ordered.SequenceEqual(reordered))
            {
                return false;
            }

            return ReorderDecoratorStack(reordered);
        }

        /// <summary>Moves a free decorator stack through its outer placement owner from any dragged badge position.</summary>
        internal void MoveFreeDecoratorStack(UUID draggedDecoratorUUID, Vector2 draggedBadgePosition)
        {
            GraphDecoratorStack stack = canvas?.Presentation?.FindDecoratorStack(draggedDecoratorUUID);
            if (stack?.Anchor.DecoratorPlaceholder == null || stack.Badges.Count == 0)
            {
                return;
            }

            GraphNodeDescriptor outer = stack.Badges[0].Node;
            GraphPresentationItem dragged = canvas.Presentation?.Find(draggedDecoratorUUID);
            GraphPresentationItem outerItem = canvas.Presentation?.Find(outer?.UUID ?? UUID.Empty);
            if (outer == null || dragged == null || outerItem == null)
            {
                return;
            }

            MoveNode(outer, draggedBadgePosition + outerItem.Position - dragged.Position);
        }

        /// <summary>Runs one authored port mutation without rebuilding the graph presentation.</summary>
        private bool TryAssign(GraphPortDescriptor port, UUID targetUUID)
        {
            if (!editorWindow || !tree || port == null)
            {
                return false;
            }

            if (port.Operation is GraphPortOperation.Replace or GraphPortOperation.Insert
                && tree.CanRedirectReferenceChain(port.Address, targetUUID))
            {
                return tree.TryRedirectReferenceChain(
                    port.Address,
                    targetUUID,
                    $"Redirect {port.Address.FieldName}");
            }

            return port.Operation switch
            {
                GraphPortOperation.Connect => tree.TryConnectReference(
                    port.Address,
                    targetUUID,
                    $"Connect {port.Address.FieldName}",
                    allowMoveExisting: !port.IsRaw),
                GraphPortOperation.Replace => tree.TryReplaceReference(
                    port.Address,
                    targetUUID,
                    $"Replace {port.Address.FieldName}",
                    allowMoveExisting: !port.IsRaw),
                GraphPortOperation.Wrap => tree.TryWrapDecoratorChild(
                    port.Address.OwnerUUID,
                    targetUUID,
                    "Wrap Decorator child"),
                GraphPortOperation.Insert => tree.TryInsertReference(
                    port.Address,
                    targetUUID,
                    !port.IsRaw || port.Address.FieldName == nameof(ServiceHostNode.services),
                    port.Address.FieldName == nameof(ServiceHostNode.services)
                        ? "Move Service"
                        : $"Insert {port.Address.FieldName}"),
                _ => false,
            };
        }

        /// <summary>Disconnects one selected authored edge through the topology mutation owner.</summary>
        /// <remarks>Existing cards retain their current in-memory positions so disconnecting an edge does not interrupt editing.</remarks>
        internal bool Disconnect(GraphEdgeDescriptor edge)
        {
            if (!editorWindow || !tree || edge == null)
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            string undoName = edge.Reference.IsCollection
                ? $"Remove {edge.Reference.Address.FieldName}"
                : $"Disconnect {edge.Reference.Address.FieldName}";
            if (!tree.TryRemoveReference(edge.Reference, undoName))
            {
                ShowConnectionRejectedNotification();
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Disconnects the single child slot of a decorator while retaining both nodes.</summary>
        internal bool DisconnectDecoratorChild(Decorator decorator)
        {
            if (decorator?.node?.UUID == UUID.Empty || topology == null) return false;
            GraphEdgeDescriptor edge = topology.Edges.FirstOrDefault(candidate => candidate.Source.UUID == decorator.uuid
                && candidate.Reference.Address.FieldName == nameof(Decorator.node));
            return edge != null && Disconnect(edge);
        }

        #endregion

        #region Layout And View Commands

        /// <summary>Resolves existing persisted positions onto a changed topology before persisting it.</summary>
        private void CommitResolvedLayout(GraphTopology changedTopology, UUID overrideUUID = default, Vector2? overridePosition = null)
        {
            GraphLayoutResolver.Resolve(tree, changedTopology);
            if (overridePosition.HasValue)
            {
                GraphNodeDescriptor node = changedTopology.FindNode(overrideUUID);
                if (node != null)
                    node.Position = overridePosition.Value;
            }

            tree.GraphLayout = GraphLayoutResolver.CreateLayout(changedTopology, tree.GraphLayout);
        }

        /// <summary>
        /// Updates in-memory positions while the pointer is dragging one descriptor.
        /// The dragged descriptor supplies the pointer delta; snapping produces one common
        /// delta that is then applied to the full move set.
        /// </summary>
        /// <param name="node">The actually dragged node descriptor.</param>
        /// <param name="position">The new canvas position of the dragged descriptor.</param>
        internal void MoveNode(GraphNodeDescriptor node, Vector2 position)
        {
            if (!editorWindow || node == null)
            {
                return;
            }

            Vector2 appliedPosition = SnapToGrid ? SnapPosition(position) : position;
            if ((node.Position - appliedPosition).sqrMagnitude <= 0.01f)
            {
                canvas?.RefreshTransform();
                return;
            }

            nodeMoved = true;
            Vector2 delta = appliedPosition - node.Position;
            IReadOnlyCollection<GraphNodeDescriptor> seeds = selectedNodeUUIDs.Contains(node.UUID)
                ? selectedNodeUUIDs.Select(uuid => topology?.FindNode(uuid)).Where(descriptor => descriptor != null).ToArray()
                : new[] { node };
            IReadOnlyCollection<GraphNodeDescriptor> moved = CollectMoveSet(seeds, MoveMode);
            foreach (GraphNodeDescriptor descriptor in moved)
            {
                descriptor.Position += delta;
            }

            canvas?.UpdatePresentationPositions(moved, preserveGroupElements: true);
            canvas?.RefreshTransform();
        }

        /// <summary>
        /// Resolves one drag operation into the descriptors that move. Single mode returns only
        /// the explicit seeds; Structure mode recursively follows real Child edges and enabled
        /// Service subtrees. Raw references never move.
        /// </summary>
        internal IReadOnlyCollection<GraphNodeDescriptor> CollectMoveSet(
            IReadOnlyCollection<GraphNodeDescriptor> explicitSeeds,
            GraphMoveMode mode)
        {
            Dictionary<UUID, GraphNodeDescriptor> result = new();
            if (explicitSeeds == null)
            {
                return result.Values;
            }

            foreach (GraphNodeDescriptor seed in explicitSeeds)
            {
                if (seed == null)
                {
                    continue;
                }

                if (mode == GraphMoveMode.Single)
                {
                    result[seed.UUID] = seed;
                    continue;
                }

                CollectStructureMoveSet(seed, result, new HashSet<UUID>());
            }

            return result.Values;
        }

        /// <summary>Recursively collects one Structure-mode movement subtree.</summary>
        private void CollectStructureMoveSet(
            GraphNodeDescriptor seed,
            IDictionary<UUID, GraphNodeDescriptor> result,
            ISet<UUID> visited)
        {
            if (seed == null || !visited.Add(seed.UUID))
            {
                return;
            }

            result[seed.UUID] = seed;
            GraphPresentation presentation = canvas?.Presentation;
            if (presentation == null)
            {
                return;
            }

            // A Service seed always brings its complete Service subtree.
            GraphServiceScope ownScope = presentation.FindServiceScope(seed.UUID);
            if (ownScope != null)
            {
                AddServiceScopeMoveGroup(ownScope, presentation, result, new HashSet<UUID>());
            }

            foreach (GraphEdgeDescriptor edge in topology?.Edges ?? Array.Empty<GraphEdgeDescriptor>())
            {
                if (edge.Source.UUID != seed.UUID || edge.Target == null)
                {
                    continue;
                }

                if (edge.Kind == GraphEdgeKind.Raw)
                {
                    continue;
                }

                if (edge.Kind == GraphEdgeKind.Service)
                {
                    GraphServiceScope targetScope = presentation.FindServiceScope(edge.Target.UUID);
                    if (targetScope == null || GetServiceFollowParent(edge.Target.UUID))
                    {
                        CollectStructureMoveSet(edge.Target, result, visited);
                        if (targetScope != null)
                        {
                            AddServiceScopeMoveGroup(targetScope, presentation, result, new HashSet<UUID>());
                        }
                    }

                    continue;
                }

                CollectStructureMoveSet(edge.Target, result, visited);
            }
        }

        /// <summary>Snaps one graph-space position to the nearest shared canvas grid point.</summary>
        private static Vector2 SnapPosition(Vector2 position)
        {
            float grid = GraphCanvasElement.GridSpacing;
            return new Vector2(
                Mathf.Round(position.x / grid) * grid,
                Mathf.Round(position.y / grid) * grid);
        }

        /// <summary>Builds the union of selected movement groups without moving any UUID twice.</summary>
        private IReadOnlyCollection<GraphNodeDescriptor> GetSelectedMoveGroup(GraphNodeDescriptor anchor)
        {
            Dictionary<UUID, GraphNodeDescriptor> result = new();
            IEnumerable<GraphNodeDescriptor> seeds = selectedNodeUUIDs.Contains(anchor.UUID)
                ? selectedNodeUUIDs.Select(uuid => topology?.FindNode(uuid)).Where(node => node != null)
                : new[] { anchor };
            foreach (GraphNodeDescriptor seed in seeds)
            {
                foreach (GraphNodeDescriptor member in GetMoveGroup(seed))
                {
                    result[member.UUID] = member;
                }
            }

            return result.Values;
        }

        /// <summary>
        /// Commits a completed node drag as one undoable layout write.
        /// </summary>
        internal void CommitNodeMove()
        {
            if (!editorWindow || !nodeMoved || !tree || topology == null)
            {
                nodeMoved = false;
                return;
            }

            Undo.RegisterCompleteObjectUndo(tree, "Move AI graph node");
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
            EditorUtility.SetDirty(tree);
            nodeMoved = false;
            canvas?.RefreshPresentationGeometry();
        }

        /// <summary>Cancels an in-progress node move after callers restore descriptor positions.</summary>
        internal void CancelNodeMove()
        {
            nodeMoved = false;
        }

        /// <summary>Updates one editor-only boundary position during pointer dragging.</summary>
        /// <param name="item">The Entrance or Exit presentation item.</param>
        /// <param name="position">The new graph-space position.</param>
        /// <returns>The actual graph-space position applied after optional snapping.</returns>
        internal Vector2 MoveBoundary(GraphPresentationItem item, Vector2 position)
        {
            if (!editorWindow || item?.Kind is not (GraphPresentationKind.Entrance or GraphPresentationKind.Exit))
            {
                return item?.Position ?? position;
            }

            if (item.Kind == GraphPresentationKind.Entrance
                && canvas?.Presentation?.Relations.Any(relation => relation.Kind == GraphPresentationRelationKind.Entrance) == true)
            {
                return item.Position;
            }

            Vector2 appliedPosition = SnapToGrid ? SnapPosition(position) : position;
            item.Position = appliedPosition;
            item.HasExplicitPosition = true;
            canvas?.RefreshPresentationGeometry();
            return appliedPosition;
        }

        /// <summary>Commits both editor-only boundary positions as one Undoable layout write.</summary>
        internal void CommitBoundaryMove()
        {
            GraphPresentation presentation = canvas?.Presentation;
            if (!editorWindow || !tree || topology == null || presentation?.Entrance == null || presentation.Exit == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(tree, "Move AI graph boundary");
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(
                topology,
                tree.GraphLayout,
                entrancePosition: presentation.Entrance.Position,
                exitPosition: presentation.Exit.Position);
            EditorUtility.SetDirty(tree);
        }

        internal void FitAll()
        {
            canvas?.FitAll();
        }

        internal void FrameSelected()
        {
            canvas?.FrameSelected();
        }

        internal void AutoLayout()
        {
            if (!editorWindow || !tree || topology == null)
            {
                return;
            }

            IReadOnlyList<string> structureErrors = tree.GetStructureValidationErrors();
            if (structureErrors.Count > 0)
            {
                Debug.LogError($"Auto Layout requires a strict single-parent tree.\n{string.Join("\n", structureErrors)}", tree);
                return;
            }

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            GraphPresentation presentation = GraphPresentationBuilder.Build(topology);
            presentation.Entrance.HasExplicitPosition = false;
            presentation.Exit.HasExplicitPosition = false;
            GraphPresentationLayout.Layout(presentation);
            Undo.RegisterCompleteObjectUndo(tree, "Auto Layout AI graph");
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(
                topology,
                tree.GraphLayout,
                entrancePosition: presentation.Entrance.Position,
                exitPosition: presentation.Exit.Position);
            EditorUtility.SetDirty(tree);
            canvas?.SetTopology(topology);
            canvas?.FitAll();
        }

        /// <summary>Gets whether the current Graph selection can be aligned.</summary>
        internal bool CanAlignSelection => editorWindow && tree && topology != null && GetSelectionLayoutItems(coalesceFlowScopes: true).Count >= 2;

        /// <summary>Gets whether the current Graph selection can be distributed.</summary>
        internal bool CanDistributeSelection => editorWindow && tree && topology != null && GetSelectionLayoutItems(coalesceFlowScopes: true).Count >= 3;

        /// <summary>Gets whether the current authored selection can be tidied.</summary>
        internal bool CanTidySelection => editorWindow && tree && topology != null
            && GetSelectionLayoutItems().Count >= 2;

        /// <summary>Tidies the current authored selection using a temporary topology layout.</summary>
        /// <returns>True when at least one canonical movable root changed position.</returns>
        internal bool TidySelection()
        {
            return TidyNodes(SelectedNodes, "Tidy AI graph selection");
        }

        /// <summary>Tidies every authored member of one annotation group.</summary>
        /// <param name="groupUUID">The annotation group UUID.</param>
        /// <returns>True when the group layout changed.</returns>
        internal bool TidyGroup(UUID groupUUID)
        {
            GraphGroupLayoutEntry group = tree?.GraphLayout?.Groups.FirstOrDefault(item => item.UUID == groupUUID) ?? default;
            if (group.UUID == UUID.Empty || tree == null) return false;
            return TidyNodes(group.Members.Select(tree.GetNode).Where(node => node != null), "Tidy AI graph group");
        }

        /// <summary>Gets whether a group has at least two effective movable roots.</summary>
        /// <param name="groupUUID">The annotation group UUID.</param>
        /// <returns>True when the command can change the group layout.</returns>
        internal bool CanTidyGroup(UUID groupUUID)
        {
            GraphGroupLayoutEntry group = tree?.GraphLayout?.Groups.FirstOrDefault(item => item.UUID == groupUUID) ?? default;
            if (group.UUID == UUID.Empty || tree == null) return false;
            IReadOnlyList<SelectionLayoutItem> items = GetSelectionLayoutItems(group.Members.Select(tree.GetNode).Where(node => node != null));
            return items.Count >= 2;
        }

        /// <summary>Computes a topology-aware arrangement and commits it once.</summary>
        /// <param name="nodes">Authored nodes to arrange.</param><param name="undoName">Undo label.</param>
        /// <returns>True when a layout coordinate changed.</returns>
        private bool TidyNodes(IEnumerable<TreeNode> nodes, string undoName)
        {
            List<TreeNode> authored = nodes?.Where(node => node != null).Distinct().ToList() ?? new List<TreeNode>();
            IReadOnlyList<SelectionLayoutItem> items = GetSelectionLayoutItems(authored);
            if (items.Count < 2) return false;
            if (!TryBuildTidyTargets(items, out Dictionary<UUID, Vector2> targets)) return false;

            bool hasChanges = targets.Any(pair => items.Any(item => item.Descriptor.UUID == pair.Key
                && (pair.Value - item.Descriptor.Position).sqrMagnitude > 0.0001f));
            if (!hasChanges) return false;

            return ApplySelectionLayout(items, targets, undoName);
        }

        /// <summary>Builds tidy targets from a temporary topology without mutating the authored graph.</summary>
        /// <param name="items">Canonical movable roots and their visual bounds.</param>
        /// <param name="targets">Descriptor positions after arrangement.</param>
        /// <returns>True when target positions were computed.</returns>
        private bool TryBuildTidyTargets(IReadOnlyList<SelectionLayoutItem> items, out Dictionary<UUID, Vector2> targets)
        {
            targets = new Dictionary<UUID, Vector2>();
            if (items == null || items.Count < 2 || !editorWindow || !tree || topology == null)
            {
                return false;
            }

            try
            {
                GraphTopology temporaryTopology = GraphTopologyBuilder.Build(tree);
                if (temporaryTopology == null || temporaryTopology.Nodes.Count == 0)
                {
                    return false;
                }

                Dictionary<UUID, Vector2> currentPositions = topology.Nodes
                    .ToDictionary(node => node.UUID, node => node.Position);
                foreach (GraphNodeDescriptor descriptor in temporaryTopology.Nodes)
                {
                    if (currentPositions.TryGetValue(descriptor.UUID, out Vector2 position))
                    {
                        descriptor.Position = position;
                    }
                }

                GraphLayoutResolver.ApplyAutoLayout(tree, temporaryTopology);
                GraphPresentation temporaryPresentation = GraphPresentationBuilder.Build(temporaryTopology);
                if (temporaryPresentation == null)
                {
                    return false;
                }

                GraphPresentationLayout.Layout(temporaryPresentation);
                List<SelectionLayoutItem> temporaryItems = new(items.Count);
                foreach (SelectionLayoutItem item in items)
                {
                    GraphNodeDescriptor temporaryDescriptor = temporaryTopology.FindNode(item.Descriptor.UUID);
                    GraphPresentationItem temporaryPresentationItem = temporaryPresentation.Find(item.Descriptor.UUID);
                    GraphNodeDescriptor temporaryRoot = temporaryPresentation.ResolveMovableRoot(item.Descriptor.UUID);
                    if (temporaryDescriptor == null || temporaryPresentationItem?.Node == null
                        || temporaryRoot == null || temporaryRoot.UUID != item.Descriptor.UUID)
                    {
                        targets.Clear();
                        return false;
                    }

                    temporaryItems.Add(new SelectionLayoutItem(
                        temporaryDescriptor,
                        GraphPresentationLayout.GetBounds(temporaryPresentationItem),
                        item.SelectionOrder));
                }

                Vector2 translation = GetSelectionBounds(items).center - GetSelectionBounds(temporaryItems).center;
                foreach (SelectionLayoutItem item in temporaryItems)
                {
                    targets[item.Descriptor.UUID] = item.Descriptor.Position + translation;
                }

                return targets.Count == items.Count;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                targets.Clear();
                return false;
            }
        }

        /// <summary>Aligns all selected authored nodes to one shared visual edge or axis.</summary>
        /// <param name="alignment">The edge or axis to align.</param>
        /// <returns>True when at least one layout coordinate changed.</returns>
        internal bool AlignSelectedNodes(GraphSelectionAlignment alignment)
        {
            if (!CanAlignSelection)
            {
                return false;
            }

            IReadOnlyList<SelectionLayoutItem> items = GetSelectionLayoutItems(coalesceFlowScopes: true);
            if (items.Count < 2)
            {
                return false;
            }

            Rect selectionBounds = GetSelectionBounds(items);
            Dictionary<UUID, Vector2> targets = new();
            foreach (SelectionLayoutItem item in items)
            {
                Vector2 target = item.Descriptor.Position;
                switch (alignment)
                {
                    case GraphSelectionAlignment.Left:
                        target.x += selectionBounds.xMin - item.Bounds.xMin;
                        break;
                    case GraphSelectionAlignment.Center:
                        target.x += selectionBounds.center.x - item.Bounds.center.x;
                        break;
                    case GraphSelectionAlignment.Right:
                        target.x += selectionBounds.xMax - item.Bounds.xMax;
                        break;
                    case GraphSelectionAlignment.Top:
                        target.y += selectionBounds.yMin - item.Bounds.yMin;
                        break;
                    case GraphSelectionAlignment.Middle:
                        target.y += selectionBounds.center.y - item.Bounds.center.y;
                        break;
                    case GraphSelectionAlignment.Bottom:
                        target.y += selectionBounds.yMax - item.Bounds.yMax;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Unsupported Graph selection alignment.");
                }

                targets[item.Descriptor.UUID] = target;
            }

            bool keepsHorizontalAlignment = alignment is GraphSelectionAlignment.Left
                or GraphSelectionAlignment.Center or GraphSelectionAlignment.Right;
            AvoidSelectionOverlap(items, targets, keepsHorizontalAlignment);

            return ApplySelectionLayout(items, targets, "Align AI graph nodes");
        }

        /// <summary>Distributes selected authored nodes with equal gaps along one axis.</summary>
        /// <param name="distribution">The axis along which nodes are distributed.</param>
        /// <returns>True when at least one layout coordinate changed.</returns>
        internal bool DistributeSelectedNodes(GraphSelectionDistribution distribution)
        {
            if (!CanDistributeSelection)
            {
                return false;
            }

            IReadOnlyList<SelectionLayoutItem> items = GetSelectionLayoutItems(coalesceFlowScopes: true);
            if (items.Count < 3)
            {
                return false;
            }

            List<SelectionLayoutItem> ordered = distribution == GraphSelectionDistribution.Horizontal
                ? items.OrderBy(item => item.Bounds.xMin).ThenBy(item => item.SelectionOrder).ToList()
                : items.OrderBy(item => item.Bounds.yMin).ThenBy(item => item.SelectionOrder).ToList();
            Dictionary<UUID, Vector2> targets = new();
            if (distribution == GraphSelectionDistribution.Horizontal)
            {
                float start = ordered[0].Bounds.xMin;
                float end = ordered[^1].Bounds.xMax;
                float totalWidth = ordered.Sum(item => item.Bounds.width);
                float gap = Mathf.Max(
                    GraphPresentationMetrics.SelectionLayoutMinimumGap,
                    (end - start - totalWidth) / (ordered.Count - 1));
                float next = start;
                foreach (SelectionLayoutItem item in ordered)
                {
                    Vector2 target = item.Descriptor.Position;
                    target.x += next - item.Bounds.xMin;
                    targets[item.Descriptor.UUID] = target;
                    next += item.Bounds.width + gap;
                }
            }
            else if (distribution == GraphSelectionDistribution.Vertical)
            {
                float start = ordered[0].Bounds.yMin;
                float end = ordered[^1].Bounds.yMax;
                float totalHeight = ordered.Sum(item => item.Bounds.height);
                float gap = Mathf.Max(
                    GraphPresentationMetrics.SelectionLayoutMinimumGap,
                    (end - start - totalHeight) / (ordered.Count - 1));
                float next = start;
                foreach (SelectionLayoutItem item in ordered)
                {
                    Vector2 target = item.Descriptor.Position;
                    target.y += next - item.Bounds.yMin;
                    targets[item.Descriptor.UUID] = target;
                    next += item.Bounds.height + gap;
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(distribution), distribution, "Unsupported Graph selection distribution.");
            }

            return ApplySelectionLayout(items, targets, "Distribute AI graph nodes");
        }

        /// <summary>
        /// Preserves an alignment axis while moving later visual bounds only as far as needed to avoid overlap.
        /// </summary>
        /// <param name="items">Canonical movable roots with their current visual bounds.</param>
        /// <param name="targets">The pending root positions to adjust.</param>
        /// <param name="keepsHorizontalAlignment">Whether the shared alignment axis is horizontal.</param>
        private static void AvoidSelectionOverlap(
            IReadOnlyList<SelectionLayoutItem> items,
            IDictionary<UUID, Vector2> targets,
            bool keepsHorizontalAlignment)
        {
            IEnumerable<SelectionLayoutItem> ordered = keepsHorizontalAlignment
                ? items.OrderBy(item => item.Bounds.yMin).ThenBy(item => item.SelectionOrder)
                : items.OrderBy(item => item.Bounds.xMin).ThenBy(item => item.SelectionOrder);
            float nextAvailable = float.NegativeInfinity;
            foreach (SelectionLayoutItem item in ordered)
            {
                Vector2 target = targets[item.Descriptor.UUID];
                Vector2 delta = target - item.Descriptor.Position;
                Rect movedBounds = new(item.Bounds.position + delta, item.Bounds.size);
                float currentStart = keepsHorizontalAlignment ? movedBounds.yMin : movedBounds.xMin;
                if (currentStart < nextAvailable)
                {
                    float adjustment = nextAvailable - currentStart;
                    if (keepsHorizontalAlignment)
                    {
                        target.y += adjustment;
                    }
                    else
                    {
                        target.x += adjustment;
                    }

                    movedBounds.position += keepsHorizontalAlignment ? Vector2.up * adjustment : Vector2.right * adjustment;
                    targets[item.Descriptor.UUID] = target;
                }

                float currentEnd = keepsHorizontalAlignment ? movedBounds.yMax : movedBounds.xMax;
                nextAvailable = currentEnd + GraphPresentationMetrics.SelectionLayoutMinimumGap;
            }
        }

        /// <summary>Captures the visual bounds of selected authored nodes in selection order.</summary>
        private IReadOnlyList<SelectionLayoutItem> GetSelectionLayoutItems(bool coalesceFlowScopes = false)
        {
            return GetSelectionLayoutItems(SelectedNodes, coalesceFlowScopes);
        }

        /// <summary>Captures visual layout items for a specific authored node set.</summary>
        /// <param name="nodes">Authored nodes to inspect.</param>
        private IReadOnlyList<SelectionLayoutItem> GetSelectionLayoutItems(
            IEnumerable<TreeNode> nodes,
            bool coalesceFlowScopes = false)
        {
            List<SelectionLayoutItem> result = new();
            int order = 0;
            foreach (TreeNode node in nodes ?? Enumerable.Empty<TreeNode>())
            {
                GraphNodeDescriptor descriptor = topology?.FindNode(node.uuid);
                if (descriptor == null)
                {
                    continue;
                }

                GraphNodeDescriptor movableRoot = canvas?.GetMoveAnchor(descriptor);
                if (movableRoot == null || result.Any(item => item.Descriptor.UUID == movableRoot.UUID)) continue;
                descriptor = movableRoot;
                GraphPresentationItem presentationItem = canvas?.Presentation?.Find(descriptor.UUID);

                Rect bounds = presentationItem != null
                    ? GraphPresentationLayout.GetBounds(presentationItem)
                    : new Rect(descriptor.Position, GraphLayoutResolver.GetNodeSize(descriptor));
                result.Add(new SelectionLayoutItem(descriptor, bounds, order++));
            }

            if (!coalesceFlowScopes)
            {
                return result;
            }

            Dictionary<UUID, List<UUID>> dependentUUIDs = new();
            HashSet<UUID> folded = new();
            HashSet<UUID> selected = result.Select(item => item.Descriptor.UUID).ToHashSet();
            foreach (SelectionLayoutItem item in result)
            {
                GraphFlowScope scope = canvas?.Presentation?.Find(item.Descriptor.UUID)?.FlowScope;
                if (scope == null)
                {
                    continue;
                }

                HashSet<UUID> descendants = new();
                CollectFlowScopeDescendants(scope, descendants, new HashSet<GraphFlowScope>());
                List<UUID> selectedDescendants = descendants.Where(selected.Contains).ToList();
                if (selectedDescendants.Count == 0)
                {
                    continue;
                }

                dependentUUIDs[item.Descriptor.UUID] = selectedDescendants;
                folded.UnionWith(selectedDescendants);
            }

            return result
                .Where(item => !folded.Contains(item.Descriptor.UUID))
                .Select(item => dependentUUIDs.TryGetValue(item.Descriptor.UUID, out List<UUID> dependents)
                    ? item.WithDependents(dependents)
                    : item)
                .ToList();
        }

        /// <summary>Collects real authored descendants recursively contained by one Flow presentation scope.</summary>
        private static void CollectFlowScopeDescendants(
            GraphFlowScope scope,
            ISet<UUID> descendants,
            ISet<GraphFlowScope> visitedScopes)
        {
            if (scope == null || !visitedScopes.Add(scope))
            {
                return;
            }

            foreach (GraphPresentationItem member in scope.Members)
            {
                if (member?.Node != null)
                {
                    descendants.Add(member.TargetUUID);
                }

                if (member?.FlowScope != null)
                {
                    CollectFlowScopeDescendants(member.FlowScope, descendants, visitedScopes);
                }
            }
        }

        /// <summary>Returns the union of the current visual bounds for a selection.</summary>
        private static Rect GetSelectionBounds(IReadOnlyList<SelectionLayoutItem> items)
        {
            Rect bounds = items[0].Bounds;
            for (int index = 1; index < items.Count; index++)
            {
                Rect next = items[index].Bounds;
                bounds = Rect.MinMaxRect(
                    Mathf.Min(bounds.xMin, next.xMin),
                    Mathf.Min(bounds.yMin, next.yMin),
                    Mathf.Max(bounds.xMax, next.xMax),
                    Mathf.Max(bounds.yMax, next.yMax));
            }

            return bounds;
        }

        /// <summary>Applies selected layout targets and commits one grouped Undo operation.</summary>
        private bool ApplySelectionLayout(
            IReadOnlyList<SelectionLayoutItem> items,
            IReadOnlyDictionary<UUID, Vector2> targets,
            string undoName)
        {
            Dictionary<UUID, GraphNodeDescriptor> changed = new();
            HashSet<UUID> moved = new();
            foreach (SelectionLayoutItem item in items)
            {
                if (!targets.TryGetValue(item.Descriptor.UUID, out Vector2 target))
                {
                    continue;
                }

                Vector2 delta = target - item.Descriptor.Position;
                if (delta.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                foreach (GraphNodeDescriptor member in GetMoveGroup(item.Descriptor))
                {
                    GraphPresentationItem presentationItem = canvas?.Presentation?.Find(member.UUID);
                    if (targets.ContainsKey(member.UUID)
                        || presentationItem?.IsRoot != true
                        || presentationItem.Parent != null
                        || !moved.Add(member.UUID))
                    {
                        continue;
                    }
                    member.Position += delta;
                    changed[member.UUID] = member;
                }

                foreach (UUID dependentUUID in item.DependentUUIDs)
                {
                    if (targets.ContainsKey(dependentUUID) || !moved.Add(dependentUUID))
                    {
                        continue;
                    }

                    GraphNodeDescriptor dependent = topology.FindNode(dependentUUID);
                    if (dependent == null)
                    {
                        continue;
                    }

                    dependent.Position += delta;
                    changed[dependent.UUID] = dependent;
                }

                item.Descriptor.Position = target;
                changed[item.Descriptor.UUID] = item.Descriptor;
            }

            if (changed.Count == 0)
            {
                return false;
            }

            // Descriptors are the transient topology snapshot; rebuild presentation once from those canonical positions.
            // This keeps embedded visual items derived and prevents them from becoming persisted movement targets.
            canvas?.SetTopology(topology);
            Undo.RegisterCompleteObjectUndo(tree, undoName);
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
            EditorUtility.SetDirty(tree);
            return true;
        }

        /// <summary>Stores one selected descriptor and its current presentation bounds.</summary>
        private readonly struct SelectionLayoutItem
        {
            internal SelectionLayoutItem(
                GraphNodeDescriptor descriptor,
                Rect bounds,
                int selectionOrder,
                IReadOnlyList<UUID> dependentUUIDs = null)
            {
                Descriptor = descriptor;
                Bounds = bounds;
                SelectionOrder = selectionOrder;
                DependentUUIDs = dependentUUIDs ?? Array.Empty<UUID>();
            }

            internal GraphNodeDescriptor Descriptor { get; }
            internal Rect Bounds { get; }
            internal int SelectionOrder { get; }
            internal IReadOnlyList<UUID> DependentUUIDs { get; }

            /// <summary>Returns this item with selected Flow descendants that must translate with its owner.</summary>
            internal SelectionLayoutItem WithDependents(IReadOnlyList<UUID> dependentUUIDs)
            {
                return new SelectionLayoutItem(Descriptor, Bounds, SelectionOrder, dependentUUIDs);
            }
        }

        #endregion

        #region Service Layout

        /// <summary>Gets whether one Service scope follows its first-placement host.</summary>
        internal bool GetServiceFollowParent(UUID serviceUUID)
        {
            return tree?.GraphLayout?.GetServiceFollowParent(serviceUUID) ?? true;
        }

        /// <summary>Toggles one Service follow setting as a single undoable layout write.</summary>
        internal void ToggleServiceFollowParent(UUID serviceUUID)
        {
            if (!editorWindow || !tree || topology?.FindNode(serviceUUID)?.Node is not Service)
            {
                return;
            }

            bool next = !GetServiceFollowParent(serviceUUID);
            Dictionary<UUID, bool> change = new() { [serviceUUID] = next };
            Undo.RegisterCompleteObjectUndo(tree, "Toggle AI graph Service follow");
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout, change);
            EditorUtility.SetDirty(tree);
            canvas?.SetTopology(topology);
            canvas?.SetSelectedNodes(selectedNodeUUIDs);
        }

        /// <summary>Builds the real UUID group affected by one drag operation.</summary>
        private IReadOnlyCollection<GraphNodeDescriptor> GetMoveGroup(GraphNodeDescriptor movedNode)
        {
            Dictionary<UUID, GraphNodeDescriptor> result = new()
            {
                [movedNode.UUID] = movedNode,
            };
            GraphPresentation presentation = canvas?.Presentation;
            if (presentation == null)
            {
                return result.Values;
            }

            GraphServiceScope ownScope = presentation.FindServiceScope(movedNode.UUID);
            if (ownScope != null)
            {
                AddServiceScopeMoveGroup(ownScope, presentation, result, new HashSet<UUID>());
            }
            else
            {
                foreach (GraphServiceScope scope in presentation.ServiceScopes)
                {
                    if (scope.Host.TargetUUID == movedNode.UUID && GetServiceFollowParent(scope.Owner.TargetUUID))
                    {
                        AddServiceScopeMoveGroup(scope, presentation, result, new HashSet<UUID>());
                    }
                }
            }

            return result.Values;
        }

        /// <summary>Adds one Service subtree and recursively enabled nested Service scopes.</summary>
        private void AddServiceScopeMoveGroup(
            GraphServiceScope scope,
            GraphPresentation presentation,
            IDictionary<UUID, GraphNodeDescriptor> result,
            ISet<UUID> visitedServices)
        {
            if (scope == null || !visitedServices.Add(scope.Owner.TargetUUID))
            {
                return;
            }

            HashSet<UUID> memberUUIDs = new();
            foreach (GraphPresentationItem member in scope.Members)
            {
                if (member.Node != null)
                {
                    result[member.TargetUUID] = member.Node;
                    memberUUIDs.Add(member.TargetUUID);
                }
            }

            foreach (GraphServiceScope nested in presentation.ServiceScopes)
            {
                if (memberUUIDs.Contains(nested.Host.TargetUUID) && GetServiceFollowParent(nested.Owner.TargetUUID))
                {
                    AddServiceScopeMoveGroup(nested, presentation, result, visitedServices);
                }
            }
        }

        #endregion

        #region Inspector

        internal void CollapseInspector()
        {
            if (!editorWindow)
            {
                return;
            }

            inspectorCollapsed = !inspectorCollapsed;
            SaveViewState();
            UpdateView();
        }

        private void DrawInspector()
        {
            if (!editorWindow || inspectorCollapsed)
            {
                return;
            }

            if (selectedNodeUUIDs.Count > 1)
            {
                EditorGUILayout.HelpBox($"Selected {selectedNodeUUIDs.Count} nodes.", MessageType.Info);
                return;
            }

            DrawNodeInspector(SelectedNode);
            if (GUI.changed)
            {
                inspectorContainer?.MarkDirtyRepaint();
                editorWindow.Repaint();
                editorWindow.rootVisualElement.schedule.Execute(RebuildTopology);
            }
        }

        /// <summary>Draws the selected node using the shared IMGUI drawer without routing through TreeModule.</summary>
        private void DrawNodeInspector(TreeNode node)
        {
            inspectorScrollPosition = GUILayout.BeginScrollView(inspectorScrollPosition);
            inspectorScrollPosition.x = 0f;

            if (node is EditorHeadNode)
            {
                editorWindow.TreeModule?.DrawTreeHead();
            }
            else if (node == null || tree == null || tree.nodes == null || !tree.nodes.Contains(node))
            {
                EditorGUILayout.HelpBox("Select a node to inspect its properties.", MessageType.Info);
            }
            else
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawNodeInspectorHeader(node);

                    if (node != tree.Head
                        && editorWindow.reachableNodes != null
                        && !editorWindow.reachableNodes.Contains(node))
                    {
                        EditorGUILayout.HelpBox("This node is unreachable from the tree head.", MessageType.Warning);
                    }

                    if (nodeDrawer == null || nodeDrawer.Node != node)
                    {
                        nodeDrawer = new NodeDrawHandler(editorWindow, node);
                    }

                    nodeDrawer.Draw();
                }
            }

            GUILayout.EndScrollView();
        }

        /// <summary>Draws the Graph Inspector title row and the shared node action menu.</summary>
        private void DrawNodeInspectorHeader(TreeNode node)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(NodeDrawerUtility.GetEditorName(node), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                Rect menuRect = GUILayoutUtility.GetRect(
                    28f,
                    EditorGUIUtility.singleLineHeight,
                    GUILayout.Width(28f));
                if (EditorGUI.DropdownButton(
                    menuRect,
                    new GUIContent("⋮", "Open node actions"),
                    FocusType.Passive,
                    EditorStyles.toolbarButton))
                {
                    GenericMenu menu = new();
                    editorWindow.TreeModule?.CreateRightClickMenu(
                        node,
                        menu,
                        canvas == null ? null : new GraphNodeCommandHandler(this, canvas));
                    menu.ShowAsContext();
                }
            }
        }

        /// <summary>Toggles optional Raw reference presentation from the floating Graph tools.</summary>
        internal void ToggleRawReferences()
        {
            showRawReferences = !showRawReferences;
            SaveViewState();
            RebuildTopology();
            canvas?.RefreshViewOptions();
        }

        /// <summary>Toggles visibility of all derived Service scopes from the floating Graph tools.</summary>
        internal void ToggleServiceVisibility()
        {
            ShowServices = !ShowServices;
        }

        /// <summary>Gets whether the Graph Inspector is currently visible.</summary>
        internal bool InspectorVisible => !inspectorCollapsed;

        private void BeginResize(PointerDownEvent evt)
        {
            if (evt.button != 0)
            {
                return;
            }

            resizingInspector = true;
            resizePointerId = evt.pointerId;
            resizeStartX = evt.position.x;
            resizeStartWidth = inspectorWidth;
            splitter.CapturePointer(resizePointerId);
            evt.StopPropagation();
        }

        private void ResizeInspector(PointerMoveEvent evt)
        {
            if (!resizingInspector || evt.pointerId != resizePointerId)
            {
                return;
            }

            float delta = resizeStartX - evt.position.x;
            inspectorWidth = Mathf.Clamp(resizeStartWidth + delta, InspectorMinWidth, InspectorMaxWidth);
            inspector.style.width = inspectorWidth;
            evt.StopPropagation();
        }

        private void EndResize(EventBase evt)
        {
            if (!resizingInspector)
            {
                return;
            }

            resizingInspector = false;
            if (resizePointerId >= 0)
            {
                splitter.ReleasePointer(resizePointerId);
            }

            resizePointerId = -1;
            evt.StopPropagation();
        }

        #endregion

        internal bool TryUpgradeNode(TreeNode node, bool prompt)
        {
            if (!node.CanUpgrade())
            {
                return false;
            }

            if (prompt && !EditorUtility.DisplayDialog(
                "Upgrade Node",
                $"Upgrade node {node.name} ({node.uuid})?",
                "Upgrade",
                "Cancel"))
            {
                return false;
            }

            if (!tree.TryUpgradeNode(node, out TreeNode upgradedNode))
            {
                EditorUtility.DisplayDialog("Upgrade Failed", $"Upgrade returned no result for node {node.name}.", "OK");
                return false;
            }

            editorWindow.Refresh();
            editorWindow.SelectedNode = upgradedNode;
            return true;
        }
    }
}
