using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Amlos.AI.Visual
{

    public enum TreeNodeType
    {
        head,
        @default,
        unused,
    }

    [Serializable]
    public class GraphNode
    {
        public Rect rect;

        public UUID uuid;

        Vector2 lastClick;
        string name;
        string order;
        int index;

        public TreeNodeType treeNodeType;

        public bool isDragged;
        public bool isSelected;

        [SerializeReference] public ConnectionPoint inPoint;
        [SerializeReference] public ConnectionPoint outPoint;


        public Action<GraphNode> OnRemoveNode;
        public Action<GraphNode> OnSelectNode;

        private GUIStyle style;
        private bool firstClickDone;

        public GUIStyle Style { get => style ??= GraphNodeStyle.defaultNodeStyle; set => style = value; }

        public GUIStyle DefaultNodeStyle => GetNodeStyle();

        public GUIStyle SelectedNodeStyle => GetSelectedNodeStyle();


        public Rect OrderRect => new Rect(rect.x, rect.y, 200, 30);
        public Rect IndexRect => new Rect(rect.x, rect.y, 40, 40);


        public GraphNode(Vector2 position, float width, float height)
        {
            rect = new Rect(position.x, position.y, width, height);
            inPoint = new ConnectionPoint(this, ConnectionPointType.In);
            outPoint = new ConnectionPoint(this, ConnectionPointType.Out);
        }

        public void Drag(Vector2 delta)
        {
            rect.position += delta;
        }

        public void Refresh(TreeNode treeNode, string order, int index, TreeNodeType treeNodeType)
        {
            this.name = treeNode.name;
            this.order = order;
            this.treeNodeType = treeNodeType;
            this.index = index;
            Style = isSelected ? SelectedNodeStyle : DefaultNodeStyle;

        }

        public void Draw()
        {
            inPoint.Draw();
            outPoint.Draw();

            var oldColor = GUI.contentColor;
            GUI.contentColor = Color.white;
            GUI.Box(rect, name, Style);
            if (order != string.Empty) GUI.Label(OrderRect, order, Style);
            if (index != 0) GUI.Label(IndexRect, index.ToString(), Style);
            GUI.contentColor = oldColor;
        }

        public bool ProcessEvents(Event e)
        {
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0)
                    {
                        lastClick = e.mousePosition;
                        if (rect.Contains(e.mousePosition))
                        {
                            isDragged = true;
                            GUI.changed = true;
                            isSelected = true;
                            Style = SelectedNodeStyle;
                        }
                        else
                        {
                            GUI.changed = true;
                            isSelected = false;
                            Style = DefaultNodeStyle;
                        }
                    }

                    if (e.button == 1 && isSelected && rect.Contains(e.mousePosition))
                    {
                        ProcessContextMenu();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    isDragged = false;
                    if (rect.Contains(e.mousePosition))
                        if ((e.mousePosition - lastClick).magnitude < 0.01)
                        {
                            if (firstClickDone)
                            {
                                OnSelectNode?.Invoke(this);
                                firstClickDone = false;
                            }
                            else firstClickDone = true;
                        }
                    break;
                case EventType.MouseDrag:
                    if (e.button == 0 && isDragged)
                    {
                        Drag(e.delta);
                        e.Use();
                        return true;
                    }
                    break;
            }

            return false;
        }

        private void ProcessContextMenu()
        {
#if UNITY_EDITOR
            GenericMenu genericMenu = new GenericMenu();
            genericMenu.AddItem(new GUIContent("Remove node"), false, OnClickRemoveNode);
            genericMenu.ShowAsContext();
#endif
        }

        private void OnClickRemoveNode()
        {
            if (OnRemoveNode != null)
            {
                OnRemoveNode(this);
            }
        }

        private GUIStyle GetNodeStyle()
        {
            switch (treeNodeType)
            {
                case TreeNodeType.head:
                    return GraphNodeStyle.headNodeStyle;
                case TreeNodeType.unused:
                case TreeNodeType.@default:
                default:
                    return GraphNodeStyle.defaultNodeStyle;
            }
        }

        private GUIStyle GetSelectedNodeStyle()
        {
            switch (treeNodeType)
            {
                case TreeNodeType.head:
                    return GraphNodeStyle.headSelectedNodeStyle;
                case TreeNodeType.unused:
                case TreeNodeType.@default:
                default:
                    return GraphNodeStyle.selectedNodeStyle;
            }
        }
    }

    public enum ConnectionPointType
    {
        In,
        Out
    }

}