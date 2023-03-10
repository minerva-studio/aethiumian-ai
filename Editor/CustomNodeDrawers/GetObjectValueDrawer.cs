using Amlos.AI.Nodes;
using UnityEngine;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(GetObjectValue))]
    public class GetObjectValueDrawer : MethodCallerDrawerBase
    {
        public GetObjectValue Node => (GetObjectValue)node;

        public override void Draw()
        {
            if (!DrawObject(Node, out var objectType)) return;
            GUILayout.Space(10);
            DrawGetFields(Node, null, objectType);
        }
    }
}
