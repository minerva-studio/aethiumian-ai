using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(EditorHeadNode))]
    public class EditorNodeDrawer : NodeDrawerBase
    {
        static readonly GUIContent label = new("Head");

        public override void Draw()
        {
            GUILayout.Label(label, EditorStyles.boldLabel);
        }
    }
}
