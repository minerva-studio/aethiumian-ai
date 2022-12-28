using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Amlos.AI.Editor
{
    public abstract class MethodCallerDrawerBase : NodeDrawerBase
    {
        /// <summary>
        /// Binding flags for instance method
        /// </summary>
        protected const BindingFlags INSTANCE_MEMBER = BindingFlags.Public | BindingFlags.Instance;
        protected bool showParentMethod;
        protected int selected;
        protected Type refType;
        protected MethodInfo[] methods;
        internal TypeReferenceDrawer typeReferenceDrawer;

        /// <summary>
        /// Check whether method is valid
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        protected virtual bool IsValidMethod(MethodInfo method)
        {
            if (method.IsGenericMethod) return false;
            if (method.IsGenericMethodDefinition) return false;
            if (method.ContainsGenericParameters) return false;
            if (Attribute.IsDefined(method, typeof(ObsoleteAttribute))) return false;
            if (method.IsSpecialName) return false;
            ParameterInfo[] parameterInfos = method.GetParameters();
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

        /// <summary>
        /// Draw component selection
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        protected bool DrawComponent(IComponentMethodCaller caller)
        {
            EditorGUILayout.LabelField("Component Data", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            caller.GetComponent = EditorGUILayout.Toggle("Get Component On Self", caller.GetComponent);
            if (!caller.GetComponent)
            {
                DrawVariable("Component", caller.Component);
                VariableData variableData = TreeData.GetVariable(caller.Component.UUID);
                if (variableData != null) caller.TypeReference.SetType(variableData.typeReference);
                if (!caller.Component.HasReference)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("No Component Assigned");
                    EditorGUI.indentLevel--;
                    return false;
                }
            }
            bool v = DrawReferType(caller);
            EditorGUI.indentLevel--;
            return v;
        }

        /// <summary>
        /// Generic Method caller: Draw Refer type and get method
        /// </summary>
        /// <param name="caller"></param> 
        /// <returns></returns>
        protected bool DrawReferType(IGenericMethodCaller caller)
        {
            typeReferenceDrawer = DrawTypeReference("Type", caller.TypeReference, typeReferenceDrawer);
            if (TreeData.targetScript)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(40);
                if (GUILayout.Button("Use Target Script"))
                {
                    caller.TypeReference.SetType(TreeData.targetScript.GetClass());
                }
                GUILayout.EndHorizontal();
            }

            if (caller.TypeReference.ReferType == null)
            {
                EditorGUILayout.LabelField("Cannot load type");
                return false;
            }
            if (caller.TypeReference.ReferType != refType)
            {
                methods = GetMethods(caller.TypeReference.ReferType, INSTANCE_MEMBER);
                if (!showParentMethod) methods = methods.Where(m => m.DeclaringType == caller.TypeReference.ReferType).ToArray();
                refType = caller.TypeReference.ReferType;
            }
            return true;
        }

        /// <summary>
        /// Draw action data
        /// </summary>
        /// <param name="caller"></param>
        protected void DrawActionData(ComponentActionBase caller)
        {
            EditorGUILayout.LabelField("Action Data");
            EditorGUI.indentLevel++;
            caller.count ??= new VariableField<int>();
            caller.duration ??= new VariableField<float>();
            caller.actionCallTime =
                (ComponentActionBase.ActionCallTime)
                EditorGUILayout.EnumPopup("Action Call Time", caller.actionCallTime);
            if (caller.actionCallTime == ComponentActionBase.ActionCallTime.once)
            {
                caller.endType = ComponentActionBase.UpdateEndType.byMethod;
                var o = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.EnumPopup("End Type", ComponentActionBase.UpdateEndType.byMethod);
                GUI.enabled = o;
            }
            else
            {
                caller.endType = (ComponentActionBase.UpdateEndType)EditorGUILayout.EnumPopup(new GUIContent { text = "End Type" }, caller.endType, CheckEnum, false);
                switch (caller.endType)
                {
                    case ComponentActionBase.UpdateEndType.byCounter:
                        DrawVariable("Count", caller.count);
                        break;
                    case ComponentActionBase.UpdateEndType.byTimer:
                        DrawVariable("Duration", caller.duration);
                        break;
                    case ComponentActionBase.UpdateEndType.byMethod:
                        if (caller.actionCallTime != ComponentActionBase.ActionCallTime.once)
                        {
                            caller.endType = default;
                        }
                        break;
                    default:
                        break;
                }
            }
            EditorGUI.indentLevel--;

            bool CheckEnum(Enum arg)
            {
                if (arg is ComponentActionBase.UpdateEndType.byMethod)
                {
                    return caller.actionCallTime == ComponentActionBase.ActionCallTime.once;
                }
                return true;
            }
        }

        /// <summary>
        /// Select method
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="methods"></param>
        /// <returns></returns>
        protected string SelectMethod(string methodName)
        {
            //if (!Tree.targetScript)
            //{
            //    return EditorGUILayout.TextField("Method Name", methodName);
            //}

            string[] options = methods.Select(m => m.Name).ToArray();
            if (options.Length == 0)
            {
                EditorGUILayout.LabelField("Method Name", "No valid method found");
                return methodName;
            }
            GUILayout.BeginHorizontal();
            selected = ArrayUtility.IndexOf(options, methodName);
            if (selected == -1 && !showParentMethod)
            {
                showParentMethod = true;
                UpdateMethods();
                options = methods.Select(m => m.Name).ToArray();
                selected = ArrayUtility.IndexOf(options, methodName);
            }
            selected = Mathf.Max(selected, 0);
            selected = EditorGUILayout.Popup("Method Name", selected, options);
            if (node is IGenericMethodCaller)
                if (showParentMethod)
                {
                    if (GUILayout.Button("Hide Parent Method", GUILayout.MaxWidth(200)))
                    {
                        showParentMethod = false;
                        UpdateMethods();
                    }
                }
                else if (GUILayout.Button("Display Parent Method", GUILayout.MaxWidth(200)))
                {
                    showParentMethod = true;
                    UpdateMethods();
                }
            GUILayout.EndHorizontal();
            return options[selected];
        }

        /// <summary>
        /// Draw all parameters
        /// </summary>
        /// <param name="caller"></param>
        /// <param name="method"></param>
        protected void DrawParameters(IMethodCaller caller, MethodInfo method)
        {
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

                    VariableType variableType = VariableUtility.GetVariableType(item.ParameterType);
                    DrawVariable(item.Name.ToTitleCase(), parameter, VariableUtility.GetCompatibleTypes(variableType));
                    parameter.type = variableType;
                }
                EditorGUI.indentLevel--;
            }
        }

        /// <summary>
        /// Draw the result variable field
        /// </summary>
        /// <param name="result"></param>
        /// <param name="method"></param>
        protected void DrawResultField(VariableReference result, MethodInfo method)
        {
            if (method.ReturnType == typeof(void))
            {
                EditorGUILayout.LabelField("Result", "void");
                result.SetReference(null);
                return;
            }
            VariableType variableType = VariableUtility.GetVariableType(method.ReturnType);
            if (variableType != VariableType.Invalid)
            {
                DrawVariable($"Result ({variableType})", result, VariableUtility.GetCompatibleTypes(variableType));
            }
            else
            {
                EditorGUILayout.LabelField("Result", $"Cannot store value type {method.ReturnType.Name}");
                result.SetReference(null);
            }
        }

        /// <summary>
        /// Update possible method to select after any change was made
        /// </summary>
        protected void UpdateMethods()
        {
            Type type = node is IGenericMethodCaller genericMethodCaller
                ? genericMethodCaller.TypeReference?.ReferType
                : TreeData.targetScript.GetClass();
            if (node is GameObjectCall) type = typeof(GameObject);
            methods = GetMethods(type, INSTANCE_MEMBER);
            //Debug.Log(methods.Length);
            if (!showParentMethod) methods = methods.Where(m => m.DeclaringType == type).ToArray();
        }


        /// <summary>
        /// Get methods defined in this type
        /// </summary>
        /// <remarks>
        /// This will return only method
        /// </remarks>
        /// <param name="type"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        protected MethodInfo[] GetMethods(Type type, BindingFlags flags)
        {
            return type
                .GetMethods(flags)
                .Where(m => !m.IsSpecialName && IsValidMethod(m))
                .ToArray();
        }

        [Obsolete]
        protected MethodInfo[] GetMethods(BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
        {
            return TreeData.targetScript.GetClass()
                .GetMethods(flags)
                .Where(m => !m.IsSpecialName && IsValidMethod(m))
                .ToArray();
        }

    }
}