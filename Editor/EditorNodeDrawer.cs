using Amlos.AI;
using static Amlos.Editor.AIEditor;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(EditorHeadNode))]
    public class EditorNodeDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            var head = node;
            DrawNodeSelection("Head", node);

            //node switch
            if (tree.GetNode(node.uuid) != head)
            {
                tree.headNodeUUID = head.uuid;
            }

            editor.Refresh();
        }
    }
}