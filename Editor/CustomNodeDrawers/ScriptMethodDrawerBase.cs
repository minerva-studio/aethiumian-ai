using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    public abstract class ScriptMethodDrawerBase : NodeDrawerBase
    {
        protected int selected;

        public void DrawResultField(VariableReference result, MethodInfo method)
        {
            if (method.ReturnType == typeof(void))
            {
                EditorGUILayout.LabelField("Result", "No return value");
                result.SetReference(null);
                return;
            }
            VariableType variableType = VariableUtility.GetVariableType(method.ReturnType);
            if (variableType != VariableType.Invalid)
            {
                DrawVariable("Result", result, VariableUtility.GetCompatibleTypes(variableType));
            }
            else
            {
                EditorGUILayout.LabelField("Result", $"Cannot store value type {method.ReturnType.Name}");
                result.SetReference(null);
            }
        }

        protected MethodInfo[] GetMethods()
        {
            return Tree.targetScript.GetClass()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && IsValidMethod(m))
                .ToArray();
        }

        protected virtual bool IsValidMethod(MethodInfo m)
        {
            if (m.IsGenericMethod) return false;
            if (m.IsGenericMethodDefinition) return false;
            if (m.ContainsGenericParameters) return false;
            ParameterInfo[] parameterInfos = m.GetParameters();
            if (parameterInfos.Length == 0) return true;


            for (int i = 0; i < parameterInfos.Length; i++)
            {
                ParameterInfo item = parameterInfos[i];
                VariableType variableType = item.ParameterType.GetVariableType();
                if (variableType == VariableType.Invalid) return false;
                if (variableType == VariableType.Node && (i != 0 || item.ParameterType != typeof(NodeProgress)))
                {
                    return false;
                }
            }

            return true;
        }


        protected void DrawParameters(IMethodCaller caller, MethodInfo method)
        {
            //Debug.Log(method);
            //Debug.Log(method.IsGenericMethod);
            //Debug.Log(method.IsGenericMethodDefinition);
            var parameterInfo = method.GetParameters();
            if (parameterInfo.Length == 0)
            {
                caller.Parameters = new List<Parameter>();
            }
            else
            {
                EditorGUILayout.LabelField("Parameters:");
                caller.Parameters ??= new List<Parameter>();
                if (caller.Parameters.Count > parameterInfo.Length)
                {
                    caller.Parameters.RemoveRange(parameterInfo.Length, caller.Parameters.Count - parameterInfo.Length);
                }
                else if (caller.Parameters.Count < parameterInfo.Length)
                {
                    for (int i = caller.Parameters.Count; i < parameterInfo.Length; i++)
                    {
                        caller.Parameters.Add(new Parameter());
                    }
                }

                EditorGUI.indentLevel++;
                for (int i = 0; i < parameterInfo.Length; i++)
                {
                    ParameterInfo item = parameterInfo[i];
                    //Debug.Log(item);
                    Parameter parameter = caller.Parameters[i];
                    if (item.ParameterType == typeof(NodeProgress))
                    {
                        GUI.enabled = false;
                        EditorGUILayout.LabelField(item.Name.ToTitleCase() + " (Node Progress)");
                        parameter.type = VariableType.Node;
                        GUI.enabled = true;
                        continue;
                    }
                    parameter.type = VariableUtility.GetVariableType(item.ParameterType);
                    DrawVariable(item.Name.ToTitleCase(), parameter, VariableUtility.GetCompatibleTypes(parameter.type));
                }
                EditorGUI.indentLevel--;
            }
        }

        public string SelectMethod(string methodName, MethodInfo[] methods)
        {
            if (!Tree.targetScript)
            {
                return EditorGUILayout.TextField("Method Name", methodName);
            }
            else
            {
                string[] options = methods.Select(m => m.Name).ToArray();
                if (options.Length == 0)
                {
                    EditorGUILayout.LabelField("Method Name", "No valid method found");
                }
                else
                {
                    selected = ArrayUtility.IndexOf(options, methodName);
                    if (selected < 0)
                    {
                        selected = 0;
                    }

                    selected = EditorGUILayout.Popup("Method Name", selected, options);
                    return options[selected];
                }
            }
            return methodName;
        }
    }
}