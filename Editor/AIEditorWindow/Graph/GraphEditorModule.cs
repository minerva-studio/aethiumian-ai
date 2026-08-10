using Aethiumian.AI.Nodes;
using System;
using System.Collections.Generic;
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
            if (host == null || !editorWindow)
            {
                return;
            }

            topologyTree = tree;
            topology = GraphTopologyBuilder.Build(tree, showRawReferences);
            GraphLayoutResolver.Resolve(tree, topology);
            canvas?.SetTopology(topology);
            canvas?.SetSelectedNode(SelectedNode);
            UpdateInspectorVisibility();
            inspectorContainer?.MarkDirtyRepaint();

            RequestInitialFrameForVisibleTree();
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

            if ((node.Position - position).sqrMagnitude > 0.01f)
            {
                nodeMoved = true;
                Vector2 delta = position - node.Position;
                IReadOnlyCollection<GraphNodeDescriptor> moved = GetMoveGroup(node);
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
    }
}
