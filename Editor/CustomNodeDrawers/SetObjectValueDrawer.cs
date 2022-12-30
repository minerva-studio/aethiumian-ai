using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(SetObjectValue))]
    public class SetObjectValueDrawer : MethodCallerDrawerBase
    {
        public SetObjectValue Node => (SetObjectValue)node;

        public override void Draw()
        {
            if (!DrawObject(Node, out var objectType)) return;
            GUILayout.Space(10);
            DrawSetFields(Node, null, objectType);
        }
    }
}
