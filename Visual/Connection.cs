using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Amlos.AI.Visual
{
    [Serializable]
    public class Connection
    {
        [SerializeReference] public ConnectionPoint inPoint;
        [SerializeReference] public ConnectionPoint outPoint;
        public Action<Connection> OnClickRemoveConnection;

        public Connection(ConnectionPoint inPoint, ConnectionPoint outPoint, Action<Connection> OnClickRemoveConnection)
        {
            this.inPoint = inPoint;
            this.outPoint = outPoint;
            this.OnClickRemoveConnection = OnClickRemoveConnection;
        }
#if UNITY_EDITOR
        public void Draw()
        {
            //soft line
            Handles.DrawBezier(
                inPoint.rect.center,
                outPoint.rect.center,
                inPoint.rect.center + Vector2.down * 50f,
                outPoint.rect.center - Vector2.down * 50f,
                Color.white,
                null,
                2f
            );
            if (Handles.Button((inPoint.rect.center + outPoint.rect.center) * 0.5f, Quaternion.identity, 4, 8, Handles.RectangleHandleCap))
            {
                if (OnClickRemoveConnection != null)
                {
                    OnClickRemoveConnection(this);
                }
            }
        }

#endif
    }

}