using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(CallGameObject))]
    public class CallGameObjectDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not CallGameObject call)
                return;

            EditorGUILayout.LabelField("GameObject", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            call.getGameObject = EditorGUILayout.Toggle("Use this gameObject", call.getGameObject);
            if (!call.getGameObject)
            {
                DrawVariable("Game Object", call.pointingGameObject, VariableUtility.UnityObjectAndGenerics);
                VariableData variableData = tree.GetVariable(call.pointingGameObject.UUID);
                if (!call.pointingGameObject.HasEditorReference)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.HelpBox("No GameObject Assigned", MessageType.Error);
                    return;
                }
                if (!variableData.IsSubclassof(typeof(GameObject)) && !variableData.IsSubclassof(typeof(Component)))
                {
                    EditorGUILayout.HelpBox($"Warning: Referred variable is not set to either component or game object.\nVariable {variableData.name} is set to {variableData.ObjectType?.Name ?? string.Empty}.", MessageType.Warning);
                }
            }
            EditorGUI.indentLevel--;

            methods = GetMethods(typeof(GameObject), INSTANCE_MEMBER);
            DrawMethodData(call);
        }

        private void DrawMethodData(CallGameObject call)
        {
            EditorGUILayout.LabelField("Method", EditorStyles.boldLabel);
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
