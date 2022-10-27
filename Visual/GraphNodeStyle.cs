using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Amlos.AI.Visual
{
    public static class GraphNodeStyle
    {
        public static GUIStyle headNodeStyle;
        public static GUIStyle defaultNodeStyle;
        public static GUIStyle selectedNodeStyle;
        public static GUIStyle headSelectedNodeStyle;
        public static GUIStyle inPointStyle;
        public static GUIStyle outPointStyle;

        static GraphNodeStyle()
        {
#if UNITY_EDITOR
            defaultNodeStyle = new GUIStyle();
            defaultNodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node1.png") as Texture2D;
            defaultNodeStyle.border = new RectOffset(12, 12, 12, 12);
            defaultNodeStyle.alignment = TextAnchor.MiddleCenter;

            headNodeStyle = new GUIStyle();
            headNodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node2.png") as Texture2D;
            headNodeStyle.border = new RectOffset(12, 12, 12, 12);
            headNodeStyle.alignment = TextAnchor.MiddleCenter;

            selectedNodeStyle = new GUIStyle();
            selectedNodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node1 on.png") as Texture2D;
            selectedNodeStyle.border = new RectOffset(12, 12, 12, 12);
            selectedNodeStyle.alignment = TextAnchor.MiddleCenter;

            headSelectedNodeStyle = new GUIStyle();
            headSelectedNodeStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node2 on.png") as Texture2D;
            headSelectedNodeStyle.border = new RectOffset(12, 12, 12, 12);
            headSelectedNodeStyle.alignment = TextAnchor.MiddleCenter;

            inPointStyle = new GUIStyle();
            inPointStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn.png") as Texture2D;
            inPointStyle.active.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn on.png") as Texture2D;
            inPointStyle.border = new RectOffset(4, 4, 12, 12);

            outPointStyle = new GUIStyle();
            outPointStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn.png") as Texture2D;
            outPointStyle.active.background = EditorGUIUtility.Load("builtin skins/darkskin/images/btn on.png") as Texture2D;
            outPointStyle.border = new RectOffset(4, 4, 12, 12);

#endif
        }
    }

}