using System;
using UnityEditor;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(GameObjectCall))]
    public class GameObjectCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not GameObjectCall call) return;

            call.getGameObject = EditorGUILayout.Toggle("Use this gameObject", call.getGameObject);
            if (!call.getGameObject)
            {
                DrawVariable("Game Object", call.pointingGameObject);
                VariableData variableData = TreeData.GetVariable(call.pointingGameObject.UUID);
                if (!call.pointingGameObject.HasReference)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("No GameObject Assigned");
                    return;
                }
            }

            methods = GetMethods(typeof(GameObject), INSTANCE_MEMBER);
            DrawMethodData(call);
        }
        private void DrawMethodData(GameObjectCall call)
        {
            EditorGUILayout.LabelField("Method");
            EditorGUI.indentLevel++;
            //GUILayout.Space(EditorGUIUtility.singleLineHeight);
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