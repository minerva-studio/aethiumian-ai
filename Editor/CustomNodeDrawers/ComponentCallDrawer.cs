using System;
using UnityEditor;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ComponentCall))]
    public class ComponentCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not ComponentCall call) return;

            if (!DrawComponent(call)) return;
            DrawMethodData(call);
        }

        private void DrawMethodData(ComponentCall call)
        {
            EditorGUILayout.LabelField("Method");
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