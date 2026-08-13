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
        private float inspectorWidth = 300f;
        private bool inspectorCollapsed;
        private bool resizingInspector;
        private int resizePointerId = -1;
        private float resizeStartX;
        private float resizeStartWidth;
        private bool nodeMoved;
        private bool showRawReferences;
        private bool showServices;
        private bool showGrid = true;
        private BehaviourTreeData topologyTree;
        private BehaviourTreeData framedTree;
        private Vector2 viewPan;
        private float viewZoom = 1f;
        private readonly List<UUID> selectedNodeUUIDs = new();
        private bool synchronizingWindowSelection;

        /// <summary>
        /// Initializes a module for the owning editor window.
        /// </summary>
        /// <param name="editorWindow">The owner window.</param>
        internal GraphEditorModule(AIEditorWindow editorWindow)
        {
            Initialize(editorWindow);
        }

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

        /// <summary>Gets or sets whether all derived Service scopes are visible in the Graph view.</summary>
        internal bool ShowServices
        {
            get => showServices;
            set
            {
                if (showServices == value) return;
                showServices = value;
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
                showGrid = value;
                canvas?.SetGridVisible(value);
            }
        }

        /// <summary>
        /// Gets the single inspector IMGUI container.
        /// </summary>
        internal IMGUIContainer InspectorContainer => inspectorContainer;

        /// <summary>
        /// Gets the current selected node from the window authority.
        /// </summary>
        internal TreeNode SelectedNode => editorWindow ? editorWindow.SelectedNode : null;

        /// <summary>Gets the ordered authored-node selection owned by the Graph page.</summary>
        internal IReadOnlyList<TreeNode> SelectedNodes => selectedNodeUUIDs
            .Select(uuid => tree?.GetNode(uuid))
            .Where(node => node != null)
            .ToArray();

        /// <summary>Gets whether the Graph selection contains the authored node.</summary>
        internal bool IsNodeSelected(TreeNode node) => node != null && selectedNodeUUIDs.Contains(node.uuid);

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
        private void RebuildTopology(IReadOnlyDictionary<UUID, Vector2> preservedPositions)
        {
            if (host == null || !editorWindow)
            {
                return;
            }

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
            return topology?.Nodes.ToDictionary(node => node.UUID, node => node.Position)
                ?? new Dictionary<UUID, Vector2>();
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
            inspectorContainer?.MarkDirtyRepaint();
            editorWindow.Repaint();
        }

        /// <summary>Removes selection entries that no longer belong to the active tree.</summary>
        private void PruneSelection()
        {
            selectedNodeUUIDs.RemoveAll(uuid => tree?.GetNode(uuid) == null);
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
                && tree.headNodeUUID != node.uuid;
        }

        /// <summary>Sets the authored Graph tree head without changing parents, references, or layout.</summary>
        /// <param name="node">The authored node to make Head.</param>
        /// <returns><c>true</c> when the head changed and the Graph was rebuilt.</returns>
        internal bool SetHead(TreeNode node)
        {
            if (!CanSetHead(node))
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            Undo.RecordObject(tree, "Set tree Head");
            tree.headNodeUUID = node.uuid;
            EditorUtility.SetDirty(tree);
            tree.SerializedObject.Update();
            RebuildTopology(positions);
            return true;
        }

        /// <summary>Checks whether the editor-only Entrance can target an authored node.</summary>
        /// <param name="targetUUID">The candidate authored node UUID.</param>
        /// <returns>The validation result without writing serialized data or Undo state.</returns>
        internal GraphTopologyEditResult CanAssignEntrance(UUID targetUUID)
        {
            if (!editorWindow || !tree)
            {
                return GraphTopologyEditResult.Failure("The graph editor is not attached to a tree.");
            }

            TreeNode target = tree.GetNode(targetUUID);
            if (target == null)
            {
                return GraphTopologyEditResult.Failure("Entrance targets must belong to the current tree.");
            }

            if (target is Service)
            {
                return GraphTopologyEditResult.Failure("A Service cannot be the tree Entrance target.");
            }

            return targetUUID == tree.headNodeUUID
                ? GraphTopologyEditResult.Failure("The node is already the tree Head.")
                : GraphTopologyEditResult.Success(targetUUID);
        }

        /// <summary>Assigns the editor-only Entrance to one authored non-Service node.</summary>
        /// <param name="targetUUID">The authored node UUID selected by the Entrance gesture.</param>
        /// <returns><c>true</c> when the Head changed and the Graph was rebuilt.</returns>
        internal bool AssignEntrance(UUID targetUUID)
        {
            if (!CanAssignEntrance(targetUUID).Succeeded)
            {
                return false;
            }

            Dictionary<UUID, Vector2> positions = CaptureTopologyPositions();
            Undo.RecordObject(tree, "Set tree Head");
            tree.headNodeUUID = targetUUID;
            EditorUtility.SetDirty(tree);
            tree.SerializedObject.Update();
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
            Undo.RecordObject(tree, "Disconnect tree Entrance");
            tree.headNodeUUID = UUID.Empty;
            EditorUtility.SetDirty(tree);
            tree.SerializedObject.Update();
            RebuildTopology(positions);
            return true;
        }

        /// <summary>Renames an authored node as one undoable graph command.</summary>
        internal bool RenameNode(TreeNode node, string value)
        {
            string name = value?.Trim();
            if (!editorWindow || !tree || node == null || tree.GetNode(node.uuid) != node || string.IsNullOrEmpty(name)) return false;
            return ExecuteNodeCommand($"Rename {node.name}", () =>
            {
                node.name = name;
                return node;
            });
        }

        /// <summary>Copies a node through the single editor clipboard authority.</summary>
        internal void CopyNode(TreeNode node, bool includeSubtree) => TreeModule?.CopyNode(node, includeSubtree);

        /// <summary>Copies the current authored Graph selection and its relative layout.</summary>
        internal bool CopySelectedNodes()
        {
            IReadOnlyList<TreeNode> nodes = SelectedNodes;
            if (nodes.Count == 0 || topology == null) return false;
            List<Vector2> positions = nodes.Select(node => topology.FindNode(node.uuid)?.Position ?? Vector2.zero).ToList();
            editorWindow.Clipboard.WriteGraphSelection(nodes, positions, tree);
            return editorWindow.Clipboard.IsGraphSelection;
        }

        /// <summary>Pastes a detached Graph selection centered at the requested graph position.</summary>
        internal bool PasteGraphSelection(Vector2 center)
        {
            if (!editorWindow || !tree || !editorWindow.Clipboard.TryGetGraphSelection(out List<TreeNode> nodes, out List<Vector2> positions))
            {
                return false;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Paste AI graph selection");
            Undo.RegisterCompleteObjectUndo(tree, "Paste AI graph selection");
            try
            {
                foreach (TreeNode node in nodes) node.name = tree.GenerateNewNodeName(node.name);
                tree.AddRange(nodes, false);
                tree.SerializedObject.ApplyModifiedProperties();
                tree.SerializedObject.Update();
                tree.RegenerateTable();

                Vector2 sourceCenter = GetPositionBoundsCenter(positions);
                Vector2 delta = center - sourceCenter;
                GraphTopology changedTopology = GraphTopologyBuilder.Build(tree, showRawReferences);
                GraphLayoutResolver.Resolve(tree, changedTopology);
                for (int index = 0; index < nodes.Count; index++)
                {
                    GraphNodeDescriptor descriptor = changedTopology.FindNode(nodes[index].uuid);
                    if (descriptor != null) descriptor.Position = positions[index] + delta;
                }

                tree.GraphLayout = GraphLayoutResolver.CreateLayout(changedTopology, tree.GraphLayout);
                EditorUtility.SetDirty(tree);
                Undo.CollapseUndoOperations(undoGroup);
                SetGraphSelection(nodes);
                RebuildTopology();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                Undo.RevertAllDownToGroup(undoGroup);
                tree.RegenerateTable();
                RebuildTopology();
                return false;
            }
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
        internal bool DuplicateNode(TreeNode node) => ExecuteNodeCommand($"Duplicate {node?.name}", () => TreeModule?.DuplicateNode(node));

        /// <summary>Pastes compatible values while retaining the target node identity.</summary>
        internal bool PasteValue(TreeNode node) => ExecuteNodeCommand($"Paste value to {node?.name}", () => TreeModule?.PasteValue(node) == true ? node : null);

        /// <summary>Pastes clipboard structure into one single-reference slot.</summary>
        internal bool PasteTo(TreeNode owner, INodeReferenceSingleSlot slot) => ExecuteNodeCommand(
            $"Paste under {owner?.name}", () => TreeModule?.PasteTo(owner, slot));

        /// <summary>Pastes clipboard structure into one list-reference slot position.</summary>
        internal bool PasteAt(TreeNode owner, INodeReferenceListSlot slot, int index) => ExecuteNodeCommand(
            $"Paste under {owner?.name}", () => TreeModule?.PasteAt(owner, slot, index));

        /// <summary>Confirms and atomically deletes one authored node and all incoming references.</summary>
        internal bool DeleteNode(TreeNode node)
        {
            if (!editorWindow || !tree || node == null || tree.GetNode(node.uuid) != node)
                return false;

            GraphTopologyEditService service = new(tree);
            if (!service.TryAnalyzeDelete(node.uuid, out GraphNodeDeleteImpact impact))
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
            if (nodes.Count == 1) return DeleteNode(nodes[0]);

            HashSet<UUID> selected = nodes.Select(node => node.uuid).ToHashSet();
            int structural = 0;
            int services = 0;
            int raw = 0;
            int detachedChildren = 0;
            bool removesHead = false;
            GraphTopologyEditService analysis = new(tree);
            foreach (TreeNode node in nodes)
            {
                if (!analysis.TryAnalyzeDelete(node.uuid, out GraphNodeDeleteImpact impact)) return false;
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

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Delete {nodes.Count} AI graph nodes");
            Undo.RegisterCompleteObjectUndo(tree, $"Delete {nodes.Count} AI graph nodes");
            try
            {
                GraphTopologyEditService service = new(tree);
                foreach (TreeNode node in nodes)
                {
                    if (!service.Delete(node.uuid).Succeeded)
                    {
                        Undo.RevertAllDownToGroup(undoGroup);
                        tree.RegenerateTable();
                        RebuildTopology();
                        return false;
                    }
                }

                GraphTopology updatedTopology = GraphTopologyBuilder.Build(tree, showRawReferences);
                CommitResolvedLayout(updatedTopology);
                EditorUtility.SetDirty(tree);
                Undo.CollapseUndoOperations(undoGroup);
                SetGraphSelection(Array.Empty<TreeNode>());
                RebuildTopology();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                Undo.RevertAllDownToGroup(undoGroup);
                tree.RegenerateTable();
                RebuildTopology();
                return false;
            }
        }

        /// <summary>Commits an already-confirmed graph deletion without opening UI.</summary>
        internal bool CommitDeleteNode(TreeNode node, GraphNodeDeleteImpact impact)
        {
            if (!editorWindow || !tree || node == null || tree.GetNode(node.uuid) != node)
                return false;

            GraphTopologyEditService service = new(tree);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Delete AI graph node {node.name}");
            Undo.RegisterCompleteObjectUndo(tree, $"Delete AI graph node {node.name}");
            try
            {
                if (!service.Delete(node.uuid).Succeeded)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return false;
                }

                GraphTopology updatedTopology = GraphTopologyBuilder.Build(tree, showRawReferences);
                CommitResolvedLayout(updatedTopology);
                EditorUtility.SetDirty(tree);
                Undo.CollapseUndoOperations(undoGroup);
                SelectNode(impact.ParentUUID == UUID.Empty ? null : tree.GetNode(impact.ParentUUID));
                RebuildTopology();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                Undo.RevertAllDownToGroup(undoGroup);
                tree.RegenerateTable();
                RebuildTopology();
                return false;
            }
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

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            string undoName = setAsEntranceHead
                ? "Create and set tree Head"
                : port == null ? "Create AI graph node" : "Create and connect AI graph node";
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(tree, undoName);
            try
            {
                TreeNode node = NodeFactory.Create(nodeType);
                node.name = tree.GenerateNewNodeName(NodeMenuCache.Shared.GetDisplayName(nodeType));
                tree.Add(node, false);

                if (port != null && !TryAssign(port, node.uuid, out _))
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    tree.RegenerateTable();
                    RebuildTopology();
                    return false;
                }

                if (setAsEntranceHead)
                {
                    tree.headNodeUUID = node.uuid;
                }

                GraphTopology updatedTopology = GraphTopologyBuilder.Build(tree, showRawReferences);
                CommitResolvedLayout(updatedTopology, node.uuid, position);
                EditorUtility.SetDirty(tree);
                Undo.CollapseUndoOperations(undoGroup);
                SelectNode(node);
                RebuildTopology();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                Undo.RevertAllDownToGroup(undoGroup);
                tree.RegenerateTable();
                RebuildTopology();
                return false;
            }
        }

        /// <summary>Commits one node command, its resolved layout, selection, and graph refresh together.</summary>
        private bool ExecuteNodeCommand(string undoName, Func<TreeNode> command)
        {
            if (!editorWindow || !tree || command == null) return false;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RegisterCompleteObjectUndo(tree, undoName);
            try
            {
                TreeNode result = command();
                if (result == null)
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    return false;
                }

                tree.SerializedObject.ApplyModifiedProperties();
                tree.SerializedObject.Update();
                tree.RegenerateTable();
                CommitResolvedLayout(GraphTopologyBuilder.Build(tree, showRawReferences));
                EditorUtility.SetDirty(tree);
                Undo.CollapseUndoOperations(undoGroup);
                SelectNode(result);
                RebuildTopology();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                Undo.RevertAllDownToGroup(undoGroup);
                tree.RegenerateTable();
                RebuildTopology();
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
                && edge.CollectionIndex >= 0
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
                && candidate.FieldName == edge.FieldName
                && candidate.CollectionIndex >= 0);
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
            GraphTopologyEditResult result = new GraphTopologyEditService(tree).Reorder(
                new GraphReferenceAddress(edge.Source.UUID, edge.FieldName, edge.CollectionIndex),
                destinationIndex);
            if (!result.Succeeded)
            {
                return false;
            }

            RebuildTopology(positions);
            return true;
        }

        /// <summary>Checks an authored port assignment without creating Undo state or dirtying the tree.</summary>
        internal GraphTopologyEditResult CanAssign(GraphPortDescriptor port, UUID targetUUID)
        {
            if (!editorWindow || !tree || port == null)
            {
                return GraphTopologyEditResult.Failure("The graph editor is not attached to a tree.");
            }

            GraphTopologyEditService service = new(tree);
            return port.Operation switch
            {
                GraphPortOperation.Connect => service.CanConnect(port.Address, targetUUID),
                GraphPortOperation.Replace => service.CanReplace(port.Address, targetUUID),
                GraphPortOperation.Insert => service.CanInsert(port.Address, targetUUID),
                _ => GraphTopologyEditResult.Failure("The authored port operation is not supported."),
            };
        }

        /// <summary>Executes one authored port command and rebuilds the graph only after a successful mutation.</summary>
        internal bool Assign(GraphPortDescriptor port, UUID targetUUID)
        {
            if (!editorWindow || !tree || port == null)
            {
                return false;
            }

            if (!TryAssign(port, targetUUID, out _))
            {
                return false;
            }

            RebuildTopology();
            return true;
        }

        /// <summary>Runs one authored port mutation without rebuilding the graph presentation.</summary>
        private bool TryAssign(GraphPortDescriptor port, UUID targetUUID, out GraphTopologyEditResult result)
        {
            if (!editorWindow || !tree || port == null)
            {
                result = GraphTopologyEditResult.Failure("The graph editor is not attached to a tree.");
                return false;
            }

            GraphTopologyEditService service = new(tree);
            result = port.Operation switch
            {
                GraphPortOperation.Connect => service.Connect(port.Address, targetUUID),
                GraphPortOperation.Replace => service.Replace(port.Address, targetUUID),
                GraphPortOperation.Insert => service.Insert(port.Address, int.MaxValue, targetUUID),
                _ => GraphTopologyEditResult.Failure("The authored port operation is not supported."),
            };
            return result.Succeeded;
        }

        /// <summary>Disconnects one selected authored edge through the topology mutation owner.</summary>
        internal bool Disconnect(GraphEdgeDescriptor edge)
        {
            if (!editorWindow || !tree || edge == null)
            {
                return false;
            }

            GraphTopologyEditResult result = new GraphTopologyEditService(tree).Disconnect(
                new GraphReferenceAddress(edge.Source.UUID, edge.FieldName, edge.CollectionIndex));
            if (!result.Succeeded)
            {
                return false;
            }

            RebuildTopology();
            return true;
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
        /// Updates a node's in-memory position while the pointer is dragging it.
        /// </summary>
        /// <param name="node">The moved node descriptor.</param>
        /// <param name="position">The new canvas position.</param>
        internal void MoveNode(GraphNodeDescriptor node, Vector2 position)
        {
            if (!editorWindow || node == null)
            {
                return;
            }

            GraphNodeDescriptor anchor = canvas?.GetMoveAnchor(node) ?? node;
            Vector2 anchorPosition = canvas?.GetMoveAnchorPosition(node, position) ?? position;
            if ((anchor.Position - anchorPosition).sqrMagnitude > 0.01f)
            {
                nodeMoved = true;
                Vector2 delta = anchorPosition - anchor.Position;
                IReadOnlyCollection<GraphNodeDescriptor> moved = GetSelectedMoveGroup(anchor);
                foreach (GraphNodeDescriptor descriptor in moved)
                {
                    descriptor.Position += delta;
                }

                canvas?.UpdatePresentationPositions(moved);
            }

            canvas?.RefreshTransform();
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
        }

        /// <summary>Updates one editor-only boundary position during pointer dragging.</summary>
        /// <param name="item">The Entrance or Exit presentation item.</param>
        /// <param name="position">The new graph-space position.</param>
        internal void MoveBoundary(GraphPresentationItem item, Vector2 position)
        {
            if (!editorWindow || item?.Kind is not (GraphPresentationKind.Entrance or GraphPresentationKind.Exit))
            {
                return;
            }

            item.Position = position;
            item.HasExplicitPosition = true;
            canvas?.RefreshPresentationGeometry();
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

            editorWindow.TreeModule?.DrawGraphInspector(SelectedNode, ref inspectorScrollPosition);
            if (GUI.changed)
            {
                inspectorContainer?.MarkDirtyRepaint();
                editorWindow.Repaint();
                editorWindow.rootVisualElement.schedule.Execute(RebuildTopology);
            }
        }

        /// <summary>Toggles optional Raw reference presentation from the floating Graph tools.</summary>
        internal void ToggleRawReferences()
        {
            showRawReferences = !showRawReferences;
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
    }
}
