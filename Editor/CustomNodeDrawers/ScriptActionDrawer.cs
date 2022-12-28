using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [DoNotRelease]
    [Obsolete]
    [CustomNodeDrawer(typeof(ScriptAction))]
    public class ScriptActionDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            ScriptAction action = (ScriptAction)node;

            if (TreeData.targetScript == null || !TreeData.targetScript.GetClass().IsSubclassOf(typeof(Component)))
            {
                EditorGUILayout.LabelField("No target script assigned, please assign a target script");
                return;
            }

            DrawActionData(action);

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            methods = GetMethods();
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
        }

        protected override bool IsValidMethod(MethodInfo m)
        {
            if (m.IsGenericMethod) return false;
            if (m.IsGenericMethodDefinition) return false;
            if (m.ContainsGenericParameters) return false;
            if (Attribute.IsDefined(m, typeof(ObsoleteAttribute))) return false;

            ScriptAction ScriptAction = node as ScriptAction;
            ParameterInfo[] parameterInfos = m.GetParameters();
            if (parameterInfos.Length == 0)
            {
                return ScriptAction.endType != ComponentActionBase.UpdateEndType.byMethod;
            }

            // not start with NodeProgress
            if (parameterInfos[0].ParameterType != typeof(NodeProgress))
            {
                //by method, but method does not start with node progress
                if (ScriptAction.endType == ComponentActionBase.UpdateEndType.byMethod)
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