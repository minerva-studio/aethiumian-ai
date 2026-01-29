using Amlos.AI.Nodes;
using UnityEditor;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ComponentCall))]
    public class ComponentCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not ComponentCall call)
                return;

            if (!DrawComponent())
                return;
            if (!DrawReferType(INSTANCE_MEMBER))
                return;

            DrawMethodData();
        }

        private void DrawMethodData()
        {
            EditorGUILayout.LabelField("Method", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var method = SelectMethod(property.FindPropertyRelative(nameof(ComponentCall.methodName)));
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(method);
            DrawResultField(property.FindPropertyRelative(nameof(ComponentCall.result)), method);
            EditorGUI.indentLevel--;
        }
    }
}
