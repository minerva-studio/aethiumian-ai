using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(EditorHeadNode))]
    public class EditorNodeDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            var head = node;
            DrawNodeReference("Head", node.ToRawReference());

            //node switch
            if (TreeData.GetNode(node.uuid) != head)
            {
                TreeData.headNodeUUID = head.uuid;
            }

            editor.Refresh();
        }
    }
}