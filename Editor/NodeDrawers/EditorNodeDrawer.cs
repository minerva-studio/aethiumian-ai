using static Amlos.AI.Editor.AIEditorWindow;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(EditorHeadNode))]
    public class EditorNodeDrawer : NodeDrawerBase
    {
        static UnityEngine.GUIContent label = new("Head");

        public override void Draw()
        {
            var head = node;
            DrawNodeReference(label, node.ToRawReference());

            //node switch
            if (tree.GetNode(node.uuid) != head)
            {
                tree.headNodeUUID = head.uuid;
            }

            editor.Refresh();
        }
    }
}
