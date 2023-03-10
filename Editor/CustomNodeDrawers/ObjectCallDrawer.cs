using Amlos.AI.Nodes;
using System.Linq;
using UnityEditor;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ObjectCall))]
    public class ObjectCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not ObjectCall call) return;

            if (!DrawObject(call, out _)) return;
            UpdateMethods();

            DrawMethodData(call);
        }

        private void DrawMethodData(ObjectCall call)
        {
            EditorGUILayout.LabelField("Method", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            call.methodName = SelectMethod(call.methodName);

            var method = methods.FirstOrDefault(m => m.Name == call.MethodName);
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(call, method);
            DrawResultField(call.result, method);
            EditorGUI.indentLevel--;
        }
    }
}