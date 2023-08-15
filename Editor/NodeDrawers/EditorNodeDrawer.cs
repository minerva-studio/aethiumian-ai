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
            if (tree.GetNode(node.uuid) != head)
            {
                tree.headNodeUUID = head.uuid;
            }

            editor.Refresh();
        }
    }
}