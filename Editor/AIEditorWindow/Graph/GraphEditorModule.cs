using Aethiumian.AI.Nodes;
using Aethiumian.AI.Accessors;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
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
        private ToolbarToggle rawReferencesToggle;
        private ToolbarButton collapseInspectorButton;
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
        private BehaviourTreeData topologyTree;
        private BehaviourTreeData framedTree;
        private Vector2 viewPan;
        private float viewZoom = 1f;

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

        /// <summary>
        /// Gets the single inspector IMGUI container.
        /// </summary>
        internal IMGUIContainer InspectorContainer => inspectorContainer;

        /// <summary>
        /// Gets the current selected node from the window authority.
        /// </summary>
        internal TreeNode SelectedNode => editorWindow ? editorWindow.SelectedNode : null;

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
            Toolbar toolbar = RequireElement<Toolbar>(host, "ai-editor-graph-toolbar");
            ToolbarButton fitAll = RequireElement<ToolbarButton>(toolbar, "ai-editor-graph-fit-all");
            ToolbarButton frameSelected = RequireElement<ToolbarButton>(toolbar, "ai-editor-graph-frame-selected");
            ToolbarButton autoLayout = RequireElement<ToolbarButton>(toolbar, "ai-editor-graph-auto-layout");
            rawReferencesToggle = RequireElement<ToolbarToggle>(toolbar, "ai-editor-graph-show-raw-references");
            collapseInspectorButton = RequireElement<ToolbarButton>(toolbar, "ai-editor-graph-inspector-toggle");
            body = RequireElement<VisualElement>(host, "ai-editor-graph-body");
            VisualElement canvasHost = RequireElement<VisualElement>(body, "ai-editor-graph-canvas-host");
            splitter = RequireElement<VisualElement>(body, "ai-editor-graph-inspector-splitter");
            inspector = RequireElement<VisualElement>(body, "ai-editor-graph-inspector");
            VisualElement inspectorContentHost = RequireElement<VisualElement>(inspector, "ai-editor-graph-inspector-content-host");

            fitAll.clicked -= FitAll;
            fitAll.clicked += FitAll;
            frameSelected.clicked -= FrameSelected;
            frameSelected.clicked += FrameSelected;
            autoLayout.clicked -= AutoLayout;
            autoLayout.clicked += AutoLayout;
            collapseInspectorButton.clicked -= CollapseInspector;
            collapseInspectorButton.clicked += CollapseInspector;
            rawReferencesToggle.UnregisterValueChangedCallback(OnRawReferencesChanged);
            rawReferencesToggle.SetValueWithoutNotify(showRawReferences);
            rawReferencesToggle.RegisterValueChangedCallback(OnRawReferencesChanged);

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
            canvas?.SetSelectedNode(SelectedNode);
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

            canvas?.SetSelectedNode(SelectedNode);
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
            if (collapseInspectorButton != null)
            {
                collapseInspectorButton.text = inspectorCollapsed ? "Show Inspector" : "Hide Inspector";
            }

            inspector?.SetEnabled(!inspectorCollapsed);
            if (inspector != null)
            {
                inspector.style.display = inspectorCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (splitter != null)
            {
                splitter.style.display = inspectorCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            }
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
            if (editorWindow)
            {
                editorWindow.SelectedNode = node;
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

            canvas?.SetSelectedNode(node);
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
            if (!editorWindow || !tree || !NodeMenuCache.IsCreatableNodeType(nodeType))
            {
                return false;
            }

            bool requiresService = port?.AnchorKind == GraphPortAnchorKind.Service;
            if (port != null && typeof(Service).IsAssignableFrom(nodeType) != requiresService)
            {
                return false;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(port == null ? "Create AI graph node" : "Create and connect AI graph node");
            Undo.RegisterCompleteObjectUndo(tree, port == null ? "Create AI graph node" : "Create and connect AI graph node");
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
                IReadOnlyCollection<GraphNodeDescriptor> moved = GetMoveGroup(anchor);
                foreach (GraphNodeDescriptor descriptor in moved)
                {
                    descriptor.Position += delta;
                }

                canvas?.UpdatePresentationPositions(moved);
            }

            canvas?.RefreshTransform();
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

        private void FitAll()
        {
            canvas?.FitAll();
        }

        private void FrameSelected()
        {
            canvas?.FrameSelected();
        }

        private void AutoLayout()
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
            Undo.RegisterCompleteObjectUndo(tree, "Auto Layout AI graph");
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology, tree.GraphLayout);
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
            canvas?.SetSelectedNode(SelectedNode);
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

        private void CollapseInspector()
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

            editorWindow.TreeModule?.DrawGraphInspector(SelectedNode, ref inspectorScrollPosition);
            if (GUI.changed)
            {
                inspectorContainer?.MarkDirtyRepaint();
                editorWindow.Repaint();
                editorWindow.rootVisualElement.schedule.Execute(RebuildTopology);
            }
        }

        private void OnRawReferencesChanged(ChangeEvent<bool> evt)
        {
            showRawReferences = evt.newValue;
            RebuildTopology();
        }

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
