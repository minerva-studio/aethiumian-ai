using Amlos.AI;
using Minerva.Module;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(ScriptAction))]
    public class ScriptActionDrawer : ScriptMethodDrawerBase
    {
        ScriptAction ScriptAction => node as ScriptAction;

        public override void Draw()
        {
            ScriptAction action = (ScriptAction)node;
            action.count ??= new VariableField<int>();
            action.duration ??= new VariableField<float>();

            if (tree.targetScript)
            {
                string[] options = GetOptions();
                if (options.Length == 0)
                {
                    EditorGUILayout.LabelField("Method Name", "No valid method found");
                }
                else
                {
                    selected = ArrayUtility.IndexOf(options, action.methodName);
                    if (selected < 0)
                    {
                        selected = 0;
                    }

                    selected = EditorGUILayout.Popup("Method Name", selected, options);
                    action.methodName = options[selected];
                }
            }
            else
            {
                action.methodName = EditorGUILayout.TextField("Method Name", action.methodName);
            }

            if (tree.targetScript == null)
            {
                EditorGUILayout.LabelField("No target script assigned, please assign a target script");
                return;
            }

            var method = tree.targetScript.GetClass().GetMethod(action.methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method is null)
            {
                action.actionCallTime = ScriptAction.ActionCallTime.fixedUpdate;
                action.endType = ScriptAction.UpdateEndType.byCounter;
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            var paramters = method.GetParameters();
            if (paramters.Length == 0)
            {
                action.parameters = new List<Parameter>();
            }
            else
            {
                EditorGUILayout.LabelField("Parameters:");
                if (action.parameters is null)
                {
                    action.parameters = new List<Parameter>();
                }

                if (action.parameters.Count > paramters.Length)
                {
                    action.parameters.RemoveRange(paramters.Length, action.parameters.Count - paramters.Length);
                }
                else if (action.parameters.Count < paramters.Length)
                {
                    for (int i = action.parameters.Count; i < paramters.Length; i++)
                    {
                        action.parameters.Add(new Parameter());
                    }
                }

                EditorGUI.indentLevel++;
                for (int i = 0; i < paramters.Length; i++)
                {
                    ParameterInfo item = paramters[i];
                    if (item.ParameterType == typeof(NodeProgress))
                    {
                        GUI.enabled = false;
                        EditorGUILayout.LabelField(item.Name.ToTitleCase() + " (Node Progress)");
                        action.parameters[i].type = VariableType.Invalid;
                        GUI.enabled = true;
                        continue;
                    }
                    action.parameters[i].type = item.ParameterType.GetVariableType();
                    DrawVariable(" - " + item.Name.ToTitleCase(), action.parameters[i]);
                }
                EditorGUI.indentLevel--;
            }

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
                action.endType = (ScriptAction.UpdateEndType)EditorGUILayout.EnumPopup("End Type", action.endType);
                switch (action.endType)
                {
                    case ScriptAction.UpdateEndType.byCounter:
                        DrawVariable("Count", action.count);
                        break;
                    case ScriptAction.UpdateEndType.byTimer:
                        DrawVariable("Duration", action.duration);
                        break;
                    case ScriptAction.UpdateEndType.byMethod:
                        break;
                    default:
                        break;
                }
            }

        }

        protected override bool IsValidMethod(MethodInfo m)
        {
            ParameterInfo[] parameterInfos = m.GetParameters();
            if (parameterInfos.Length == 0)
            {
                return ScriptAction.endType != ScriptAction.UpdateEndType.byMethod;
            }

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
                if (item.ParameterType.GetVariableType() == VariableType.Invalid)
                {
                    return false;
                }
            }

            return true;
        }
    }
}