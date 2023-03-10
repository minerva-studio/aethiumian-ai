using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Visual;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    internal class GraphModule : AIEditorWindowModule
    {
        private List<GraphNode> GraphNodes
        {
            get => Tree ? Tree.Graph.graphNodes : null;
            set => Tree.Graph.graphNodes = value;
        }
        private List<Connection> Connections
        {
            get => Tree ? Tree.Graph.connections : null;
            set => Tree.Graph.connections = value;
        }
        private ConnectionPoint selectedInPoint;
        private ConnectionPoint selectedOutPoint;

        private Vector2 offset;
        private Vector2 drag;

        public void DrawGraph()
        {
            DrawGrid(20, 0.2f, Color.gray);
            DrawGrid(100, 0.4f, Color.gray);

            DrawNodes();
            DrawConnections();

            DrawConnectionLine(Event.current);

            ProcessNodeEvents(Event.current);
            ProcessEvents(Event.current);
        }

        private void DrawGrid(float gridSpacing, float gridOpacity, Color gridColor)
        {
            int widthDivs = Mathf.CeilToInt(position.width / gridSpacing);
            int heightDivs = Mathf.CeilToInt(position.height / gridSpacing);

            Handles.BeginGUI();
            Handles.color = new Color(gridColor.r, gridColor.g, gridColor.b, gridOpacity);

            offset += drag * 0.5f;
            Vector3 newOffset = new(offset.x % gridSpacing, offset.y % gridSpacing, 0);

            for (int i = 0; i < widthDivs; i++)
            {
                Handles.DrawLine(
                    new Vector3(gridSpacing * i, -gridSpacing, 0) + newOffset,
                    new Vector3(gridSpacing * i, position.height, 0f) + newOffset
                );
            }

            for (int j = 0; j < heightDivs; j++)
            {
                Handles.DrawLine(
                    new Vector3(-gridSpacing, gridSpacing * j, 0) + newOffset,
                    new Vector3(position.width, gridSpacing * j, 0f) + newOffset
                );
            }

            Handles.color = Color.white;
            Handles.EndGUI();
        }

        private void DrawNodes()
        {
            if (GraphNodes != null)
            {
                for (int i = GraphNodes.Count - 1; i >= 0; i--)
                {
                    GraphNode graphNode = GraphNodes[i];
                    if (graphNode == null)
                    {
                        GraphNodes.Remove(graphNode);
                        continue;
                    }
                    TreeNode child = Tree.GetNode(graphNode.uuid);
                    if (child == null)
                    {
                        GraphNodes.Remove(graphNode);
                        continue;
                    }
                    int index = 0;
                    string orderInfo;
                    TreeNodeType type;
                    if (child == Tree.Head)
                    {
                        type = TreeNodeType.head;
                        orderInfo = "Head";
                        index = 0;
                    }
                    else
                    {
                        TreeNode parentNode = Tree.GetNode(child.parent.UUID);
                        if (parentNode != null)
                        {
                            index = parentNode.GetIndexInfo(child);
                            orderInfo = parentNode.GetOrderInfo(child);
                        }
                        else
                        {
                            index = 0;
                            orderInfo = "";
                        }
                        type = !editorWindow.reachableNodes.Contains(child)
                            ? TreeNodeType.unused
                            : TreeNodeType.@default;
                    }

                    graphNode.OnRemoveNode = OnClickRemoveNode;
                    graphNode.OnSelectNode = OnClickSelectNode;
                    graphNode.inPoint.OnClickConnectionPoint = OnClickInPoint;
                    graphNode.outPoint.OnClickConnectionPoint = OnClickOutPoint;
                    graphNode.Refresh(child, orderInfo, index, type);
                    graphNode.Draw();
                }
            }
        }

        private void DrawConnections()
        {
            if (Connections != null)
            {
                for (int i = Connections.Count - 1; i >= 0; i--)
                {
                    if (Connections[i] == null)
                    {
                        Connections.RemoveAt(i);
                        continue;
                    }
                    Connections[i].OnClickRemoveConnection = OnClickRemoveConnection;
                    Connections[i].Draw();
                }
            }
        }

        private void ProcessEvents(Event e)
        {
            drag = Vector2.zero;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        ClearConnectionSelection();
                    }

                    if (e.button == 1)
                    {
                        ProcessContextMenu(e.mousePosition);
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0)
                    {
                        OnDrag(e.delta);
                    }
                    break;
            }
        }

        private void ProcessNodeEvents(Event e)
        {
            if (GraphNodes != null)
            {
                for (int i = GraphNodes.Count - 1; i >= 0; i--)
                {
                    bool guiChanged = GraphNodes[i].ProcessEvents(e);

                    if (guiChanged)
                    {
                        GUI.changed = true;
                    }
                }
            }
        }

        private void DrawConnectionLine(Event e)
        {
            if (selectedInPoint != null && selectedOutPoint == null)
            {
                Handles.DrawBezier(
                    selectedInPoint.rect.center,
                    e.mousePosition,
                    selectedInPoint.rect.center + Vector2.left * 50f,
                    e.mousePosition - Vector2.left * 50f,
                    Color.white,
                    null,
                    2f
                );

                GUI.changed = true;
            }

            if (selectedOutPoint != null && selectedInPoint == null)
            {
                Handles.DrawBezier(
                    selectedOutPoint.rect.center,
                    e.mousePosition,
                    selectedOutPoint.rect.center - Vector2.left * 50f,
                    e.mousePosition + Vector2.left * 50f,
                    Color.white,
                    null,
                    2f
                );

                GUI.changed = true;
            }
        }

        private void ProcessContextMenu(Vector2 mousePosition)
        {
            GenericMenu genericMenu = new();
            genericMenu.AddItem(
                new GUIContent("Add node"),
                false,
                () => OnClickAddNode(mousePosition)
            );
            genericMenu.ShowAsContext();
        }

        private void OnDrag(Vector2 delta)
        {
            drag = delta;

            if (GraphNodes != null)
            {
                for (int i = 0; i < GraphNodes.Count; i++)
                {
                    GraphNodes[i].Drag(delta);
                }
            }

            GUI.changed = true;
        }

        private void OnClickAddNode(Vector2 mousePosition)
        {
            GraphNodes ??= new List<GraphNode>();

            GraphNodes.Add(new GraphNode(mousePosition, 200, 80));
        }

        private void OnClickInPoint(ConnectionPoint inPoint)
        {
            selectedInPoint = inPoint;

            if (selectedOutPoint != null)
            {
                if (selectedOutPoint.node != selectedInPoint.node)
                {
                    CreateConnection();
                    ClearConnectionSelection();
                }
                else
                {
                    ClearConnectionSelection();
                }
            }
        }

        private void OnClickOutPoint(ConnectionPoint outPoint)
        {
            selectedOutPoint = outPoint;

            if (selectedInPoint != null)
            {
                if (selectedOutPoint.node != selectedInPoint.node)
                {
                    CreateConnection();
                    ClearConnectionSelection();
                }
                else
                {
                    ClearConnectionSelection();
                }
            }
        }

        private void OnClickRemoveNode(GraphNode node)
        {
            if (Connections != null)
            {
                List<Connection> connectionsToRemove = new();

                for (int i = 0; i < Connections.Count; i++)
                {
                    if (
                        Connections[i].inPoint == node.inPoint
                        || Connections[i].outPoint == node.outPoint
                    )
                    {
                        connectionsToRemove.Add(Connections[i]);
                    }
                }

                for (int i = 0; i < connectionsToRemove.Count; i++)
                {
                    Connections.Remove(connectionsToRemove[i]);
                }

                connectionsToRemove = null;
            }
            GraphNodes.Remove(node);
        }

        private void OnClickSelectNode(GraphNode gnode)
        {
            TreeNode treeNode = Tree.GetNode(gnode.uuid);
            editorWindow.SelectedNode = treeNode;
            //Debug.Log(treeNode);
            editorWindow.window = Window.nodes;
        }

        private void OnClickRemoveConnection(Connection connection)
        {
            Connections.Remove(connection);
        }

        private void CreateConnection()
        {
            Connections ??= new List<Connection>();
            Connections.Add(
                new Connection(selectedInPoint, selectedOutPoint, OnClickRemoveConnection)
            );
        }

        private void ClearConnectionSelection()
        {
            selectedInPoint = null;
            selectedOutPoint = null;
        }

        /// <summary>
        /// Create the graph of this behaviour tree
        /// </summary>
        public void CreateGraph()
        {
            GraphNodes ??= new List<GraphNode>();
            Connections ??= new List<Connection>();
            GraphNodes.Clear();
            Connections.Clear();

            List<TreeNode> created = new();

            CreateGraph(Tree.Head, Vector2.one * 200, created);
        }

        /// <summary>
        /// recursion of creating graph
        /// </summary>
        /// <param name="treeNode"></param>
        /// <param name="position"></param>
        /// <param name="created"></param>
        /// <param name="lvl"></param>
        /// <returns></returns>
        private GraphNode CreateGraph(TreeNode treeNode, Vector2 position, List<TreeNode> created, int lvl = 1)
        {
            GraphNode graphNode = new(position, 200, 80) { uuid = treeNode.uuid };
            GraphNodes.Add(graphNode);
            created.Add(treeNode);
            List<NodeReference> list = treeNode.GetChildrenReference();
            Debug.Log(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                NodeReference item = list[i];

                TreeNode child = editorWindow.allNodes.FirstOrDefault(n => n.uuid == item.UUID);

                if (child == null)
                    continue;
                if (created.Contains(child))
                    continue;

                var childPos =
                    position + ((float)i / list.Count - 0.5f) * (2000f / lvl) * Vector2.right;
                childPos.y += 100;
                var node = CreateGraph(child, childPos, created, ++lvl);
                if (node != null)
                    Connections.Add(
                        new Connection(node.inPoint, graphNode.outPoint, OnClickRemoveConnection)
                    );
            }
            return graphNode;
        }
    }
}
