﻿using static Amlos.AI.Editor.AIEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(EditorHeadNode))]
    public class EditorNodeDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            var head = node;
            DrawNodeSelection("Head", node);

            //node switch
            if (Tree.GetNode(node.uuid) != head)
            {
                Tree.headNodeUUID = head.uuid;
            }

            editor.Refresh();
        }
    }
}