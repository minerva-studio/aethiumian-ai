using Amlos.AI.Nodes;
using Minerva.Module.Editor;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Condition))]
    public class ConditionDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (node is not Condition condition) return;
            DrawNodeReference("Condition", condition.condition);
            DrawNodeReference("True", condition.trueNode);
            using (EditorGUIIndent.Increase)
                if (condition.trueNode.HasEditorReference)
                    EditorGUILayout.LabelField("Return result of true node");
                else
                    EditorGUILayout.LabelField("Return true");


            DrawNodeReference("False", condition.falseNode);
            using (EditorGUIIndent.Increase)
                if (condition.falseNode.HasEditorReference)
                    EditorGUILayout.LabelField("Return result of false node");
                else
                    EditorGUILayout.LabelField("Return false");


            NodeMustNotBeNull(condition.condition, nameof(condition));
        }
    }
}