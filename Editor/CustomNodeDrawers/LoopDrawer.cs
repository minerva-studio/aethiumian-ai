﻿using Amlos.AI;
using Minerva.Module;
using UnityEditor;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(Loop))]
    public class LoopDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (this.node is not Loop loop) return;
            Loop.LoopType loopType = loop.loopType;
            loop.loopType = (Loop.LoopType)EditorGUILayout.EnumPopup("Loop Type", loopType); 

            if (loopType == Loop.LoopType.@while) DrawNodeSelection("Condition", loop.condition);
            if (loopType == Loop.LoopType.@for) DrawVariable("Loop Count", loop.loopCount);
            DrawNodeList(nameof(Loop), loop.events, loop);
            if (loopType == Loop.LoopType.@doWhile) DrawNodeSelection("Condition", loop.condition);

        }
    }
}