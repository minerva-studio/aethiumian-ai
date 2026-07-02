using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(Condition))]
    public class ConditionDrawer : NodeDrawerBase
    {
        static class Styles
        {
            public static readonly GUIContent ConditionLabel = new("Condition", "The condition to evaluate");
            public static readonly GUIContent TrueLabel = new("True Node", "The node to execute if the condition is true");
            public static readonly GUIContent FalseLabel = new("False Node", "The node to execute if the condition is false");
        }

        public override void Draw()
        {
            if (node is not Condition condition) return;
            DrawNodeReference(Styles.ConditionLabel, property.FindPropertyRelative(nameof(condition.condition)));
            DrawNodeReference(Styles.TrueLabel, property.FindPropertyRelative(nameof(condition.trueNode)));
            using (IndentScope.Increase)
                if (condition.trueNode.HasEditorReference)
                    EditorGUILayout.LabelField("Return result of true node");
                else
                    EditorGUILayout.LabelField("Return true");


            DrawNodeReference(Styles.FalseLabel, property.FindPropertyRelative(nameof(condition.falseNode)));
            using (IndentScope.Increase)
                if (condition.falseNode.HasEditorReference)
                    EditorGUILayout.LabelField("Return result of false node");
                else
                    EditorGUILayout.LabelField("Return false");

            if (GUILayout.Button("Switch true/false"))
            {
                (condition.falseNode, condition.trueNode) = (condition.trueNode, condition.falseNode);
            }


            NodeMustNotBeNull(condition.condition, nameof(condition));
        }
    }
}
