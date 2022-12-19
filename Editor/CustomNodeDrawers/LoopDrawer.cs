using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Loop))]
    public class LoopDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Loop loop) return;
            Loop.LoopType loopType = loop.loopType;
            loop.loopType = (Loop.LoopType)EditorGUILayout.EnumPopup("Loop Type", loopType);

            if (loopType == Loop.LoopType.@while) DrawNodeReference("Condition", loop.condition);
            if (loopType == Loop.LoopType.@for) DrawVariable("Loop Count", loop.loopCount);
            DrawNodeList(nameof(Loop), loop.events, loop);
            if (loopType == Loop.LoopType.@doWhile) DrawNodeReference("Condition", loop.condition);

        }
    }
}