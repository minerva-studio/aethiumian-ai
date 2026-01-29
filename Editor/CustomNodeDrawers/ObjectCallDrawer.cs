using Amlos.AI.Nodes;
using UnityEditor;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ObjectCall))]
    public class ObjectCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not ObjectCall call) return;

            UnityEditor.SerializedProperty objectProperty = FindRelativeProperty(nameof(ObjectCall.@object));
            UnityEditor.SerializedProperty typeProperty = FindRelativeProperty(nameof(ObjectCall.type));
            if (!DrawObject(objectProperty, typeProperty, out var objectType)) return;
            UpdateMethods();

            DrawMethodData(call);
        }

        private void DrawMethodData(ObjectCall call)
        {
            EditorGUILayout.LabelField("Method", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var method = SelectMethod(property.FindPropertyRelative(nameof(ObjectCall.methodName)));
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(method);
            DrawResultField(property.FindPropertyRelative(nameof(ObjectCall.result)), method);
            EditorGUI.indentLevel--;
        }
    }
}
