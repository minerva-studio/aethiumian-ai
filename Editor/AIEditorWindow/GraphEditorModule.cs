using Aethiumian.AI.Nodes;
using System;
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
            host.Clear();
            host.AddToClassList("ai-editor-graph-host");

            Toolbar toolbar = new()
            {
                name = "ai-editor-graph-toolbar",
            };
            toolbar.AddToClassList("ai-editor-graph-toolbar");

            ToolbarButton fitAll = new(FitAll)
            {
                name = "ai-editor-graph-fit-all",
                text = "Fit All",
                tooltip = "Fit all graph nodes in the viewport.",
            };
            ToolbarButton frameSelected = new(FrameSelected)
            {
                name = "ai-editor-graph-frame-selected",
                text = "Frame Selected",
                tooltip = "Frame the selected graph node.",
            };
            ToolbarButton autoLayout = new(AutoLayout)
            {
                name = "ai-editor-graph-auto-layout",
                text = "Auto Layout",
                tooltip = "Generate a deterministic top-down layout and save it.",
            };
            rawReferencesToggle = new()
            {
                name = "ai-editor-graph-show-raw-references",
                text = "Raw References",
                tooltip = "Show raw references as dotted edges.",
            };
            rawReferencesToggle.SetValueWithoutNotify(showRawReferences);
            collapseInspectorButton = new(CollapseInspector)
            {
                name = "ai-editor-graph-inspector-toggle",
                text = "Inspector",
                tooltip = "Collapse or expand the node inspector.",
            };

            toolbar.Add(fitAll);
            toolbar.Add(frameSelected);
            toolbar.Add(autoLayout);
            VisualElement toolbarSpacer = new();
            toolbarSpacer.AddToClassList("ai-editor-graph-toolbar-spacer");
            toolbar.Add(toolbarSpacer);
            toolbar.Add(rawReferencesToggle);
            toolbar.Add(collapseInspectorButton);

            body = new VisualElement
            {
                name = "ai-editor-graph-body",
            };
            body.AddToClassList("ai-editor-graph-body");

            canvas = new GraphCanvasElement(this);
            canvas.Pan = viewPan;
            canvas.Zoom = viewZoom;
            body.Add(canvas);

            splitter = new VisualElement
            {
                name = "ai-editor-graph-inspector-splitter",
            };
            splitter.AddToClassList("ai-editor-graph-inspector-splitter");
            splitter.RegisterCallback<PointerDownEvent>(BeginResize);
            splitter.RegisterCallback<PointerMoveEvent>(ResizeInspector);
            splitter.RegisterCallback<PointerUpEvent>(EndResize);
            splitter.RegisterCallback<PointerCancelEvent>(EndResize);
            body.Add(splitter);

            inspector = new VisualElement
            {
                name = "ai-editor-graph-inspector",
            };
            inspector.AddToClassList("ai-editor-graph-inspector");
            inspector.style.width = inspectorWidth;
            inspectorContainer = new IMGUIContainer(DrawInspector)
            {
                name = "ai-editor-graph-inspector-imgui",
            };
            inspectorContainer.AddToClassList("ai-editor-graph-inspector-imgui");
            inspector.Add(inspectorContainer);
            body.Add(inspector);

            host.Add(toolbar);
            host.Add(body);
            rawReferencesToggle.RegisterValueChangedCallback(OnRawReferencesChanged);
            RebuildTopology();
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

            if (tree != null && framedTree != tree)
            {
                framedTree = tree;
                canvas?.RequestFitAllWhenGeometryIsValid();
            }
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
                node.Position = position;
                canvas?.UpdatePresentationPosition(node, position);
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
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology);
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

            GraphLayoutResolver.ApplyAutoLayout(tree, topology);
            Undo.RegisterCompleteObjectUndo(tree, "Auto Layout AI graph");
            tree.GraphLayout = GraphLayoutResolver.CreateLayout(topology);
            EditorUtility.SetDirty(tree);
            canvas?.SetTopology(topology);
            canvas?.FitAll();
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
