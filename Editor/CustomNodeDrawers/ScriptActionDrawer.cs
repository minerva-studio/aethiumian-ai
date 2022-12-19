using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ScriptAction))]
    public class ScriptActionDrawer : ScriptMethodDrawerBase
    {

        public override void Draw()
        {
            ScriptAction action = (ScriptAction)node;

            if (Tree.targetScript == null || !Tree.targetScript.GetClass().IsSubclassOf(typeof(Component)))
            {
                EditorGUILayout.LabelField("No target script assigned, please assign a target script");
                return;
            }

            DrawActionData(action);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            var methods = GetMethods();
            action.methodName = SelectMethod(action.methodName, methods);

            var method = methods.FirstOrDefault(m => m.Name == action.methodName);
            if (method is null)
            {
                action.actionCallTime = ScriptAction.ActionCallTime.fixedUpdate;
                action.endType = ScriptAction.UpdateEndType.byCounter;
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }
            DrawParameters(action, method);
            DrawResultField(action.result, method);
        }

        private void DrawActionData(ScriptAction action)
        {
            action.count ??= new VariableField<int>();
            action.duration ??= new VariableField<float>();
            action.actionCallTime = (ScriptAction.ActionCallTime)EditorGUILayout.EnumPopup("Action Call Time", action.actionCallTime);
            if (action.actionCallTime == ScriptAction.ActionCallTime.once)
            {
                action.endType = ScriptAction.UpdateEndType.byMethod;
                var o = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.EnumPopup("End Type", ScriptAction.UpdateEndType.byMethod);
                GUI.enabled = o;
            }
            else
            {
                action.endType = (ScriptAction.UpdateEndType)EditorGUILayout.EnumPopup(new GUIContent { text = "End Type" }, action.endType, CheckEnum, false);
                switch (action.endType)
                {
                    case ScriptAction.UpdateEndType.byCounter:
                        DrawVariable("Count", action.count);
                        break;
                    case ScriptAction.UpdateEndType.byTimer:
                        DrawVariable("Duration", action.duration);
                        break;
                    case ScriptAction.UpdateEndType.byMethod:
                        if (action.actionCallTime != ScriptAction.ActionCallTime.once)
                        {
                            action.endType = default;
                        }
                        break;
                    default:
                        break;
                }
            }

            bool CheckEnum(Enum arg)
            {
                if (arg is ScriptAction.UpdateEndType.byMethod)
                {
                    return action.actionCallTime == ScriptAction.ActionCallTime.once;
                }
                return true;
            }
        }


        protected override bool IsValidMethod(MethodInfo m)
        {
            if (m.IsGenericMethod) return false;
            if (m.IsGenericMethodDefinition) return false;
            if (m.ContainsGenericParameters) return false;
            ScriptAction ScriptAction = node as ScriptAction;
            ParameterInfo[] parameterInfos = m.GetParameters();
            if (parameterInfos.Length == 0)
            {
                return ScriptAction.endType != ScriptAction.UpdateEndType.byMethod;
            }

            // not start with NodeProgress
            if (parameterInfos[0].ParameterType != typeof(NodeProgress))
            {
                //by method, but method does not start with node progress
                if (ScriptAction.endType == ScriptAction.UpdateEndType.byMethod)
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