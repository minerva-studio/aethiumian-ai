using Minerva.Module;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ScriptCall))]
    public class ScriptCallDrawer : ScriptMethodDrawerBase
    {
        public override void Draw()
        {
            ScriptCall call = (ScriptCall)node;

            //            call.returnValue = EditorGUILayout.Toggle("Return Value", call.returnValue);
            if (tree.targetScript)
            {
                string[] options = GetOptions();
                if (options.Length == 0)
                {
                    EditorGUILayout.LabelField("Method Name", "No valid method found");
                }
                else
                {
                    selected = ArrayUtility.IndexOf(options, call.methodName);
                    if (selected < 0)
                    {
                        selected = 0;
                    }

                    selected = EditorGUILayout.Popup("Method Name", selected, options);
                    call.methodName = options[selected];
                }
            }
            else
            {
                call.methodName = EditorGUILayout.TextField("Method Name", call.methodName);
            }

            if (tree.targetScript == null)
            {
                EditorGUILayout.LabelField("No target script assigned, please assign a target script");
                return;
            }

            var method = tree.targetScript.GetClass().GetMethod(call.methodName, BindingFlags.Public | BindingFlags.Instance);
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            var paramters = method.GetParameters();
            if (paramters.Length == 0)
            {
                call.parameters = new List<Parameter>();
                return;
            }



            EditorGUILayout.LabelField("Parameters:");
            if (call.parameters is null)
            {
                call.parameters = new List<Parameter>();
            }
            if (call.parameters.Count > paramters.Length)
            {
                call.parameters.RemoveRange(paramters.Length, call.parameters.Count - paramters.Length);
            }
            else if (call.parameters.Count < paramters.Length)
            {
                for (int i = call.parameters.Count; i < paramters.Length; i++)
                {
                    call.parameters.Add(new Parameter());
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
                    call.parameters[i].type = VariableType.Invalid;
                    GUI.enabled = true;
                    continue;
                }
                call.parameters[i].type = item.ParameterType.GetVariableType();
                DrawVariable(item.Name.ToTitleCase(), call.parameters[i]);
            }
            EditorGUI.indentLevel--;
        }
    }
}