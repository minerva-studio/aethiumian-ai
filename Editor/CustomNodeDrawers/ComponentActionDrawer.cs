using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ComponentAction))]
    public class ComponentActionDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not ComponentAction action) return;

            if (!DrawComponent(action)) return;

            DrawActionData(action);
            DrawMethodData(action);
        }

        private void DrawMethodData(ComponentAction action)
        {
            EditorGUILayout.LabelField("Method");
            EditorGUI.indentLevel++;
            //GUILayout.Space(EditorGUIUtility.singleLineHeight);
            action.methodName = SelectMethod(action.methodName);

            var method = methods.FirstOrDefault(m => m.Name == action.methodName);
            if (method is null)
            {
                action.actionCallTime = ComponentActionBase.ActionCallTime.fixedUpdate;
                action.endType = ComponentActionBase.UpdateEndType.byCounter;
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }
            DrawParameters(action, method);
            DrawResultField(action.result, method);
            EditorGUI.indentLevel--;
        }

        protected override bool IsValidMethod(MethodInfo m)
        {
            if (m.IsGenericMethod) return false;
            if (m.IsGenericMethodDefinition) return false;
            if (m.ContainsGenericParameters) return false;
            if (Attribute.IsDefined(m, typeof(ObsoleteAttribute))) return false;
            ComponentAction ComponentAction = node as ComponentAction;

            ParameterInfo[] parameterInfos = m.GetParameters();
            if (parameterInfos.Length == 0)
            {
                return ComponentAction.endType != ComponentActionBase.UpdateEndType.byMethod;
            }

            // not start with NodeProgress
            if (parameterInfos[0].ParameterType != typeof(NodeProgress))
            {
                //by method, but method does not start with node progress
                if (ComponentAction.endType == ComponentActionBase.UpdateEndType.byMethod)
                {
                    return false;
                }
                //not by method, but first argument is invalid
                else if (parameterInfos[0].ParameterType.GetVariableType() == VariableType.Invalid)
                {
                    return false;
                }
            }

            //check 1+ param
            for (int i = 1; i < parameterInfos.Length; i++)
            {
                ParameterInfo item = parameterInfos[i];
                VariableType variableType = item.ParameterType.GetVariableType();
                if (variableType == VariableType.Invalid || variableType == VariableType.Node)
                {
                    return false;
                }
            }

            return true;
        }
    }
}