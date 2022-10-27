using System;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Amlos.AI.Visual
{
    [Serializable]
    public class ConnectionPoint
    {
        public Rect rect;

        public ConnectionPointType type;

        [SerializeReference] public GraphNode node;

        public GUIStyle style
        {
            get
            {
                return type switch
                {
                    ConnectionPointType.Out => GraphNodeStyle.outPointStyle,
                    _ => GraphNodeStyle.inPointStyle,
                };
            }
        }

        public Action<ConnectionPoint> OnClickConnectionPoint;

        public ConnectionPoint(GraphNode node, ConnectionPointType type)
        {
            this.node = node;
            this.type = type;
            rect = new Rect(0, 0, 20f, 10f);
        }

        public void Draw()
        {
            rect.x = node.rect.x + node.rect.width * 0.5f - rect.width * 0.5f;

            switch (type)
            {
                case ConnectionPointType.In:
                    rect.y = node.rect.y - rect.height + 8f;
                    break;

                case ConnectionPointType.Out:
                    rect.y = node.rect.y + node.rect.height - 8f;
                    break;
            }

            if (GUI.Button(rect, "", style))
            {
                if (OnClickConnectionPoint != null)
                {
                    OnClickConnectionPoint(this);
                }
            }
        }
    }

}