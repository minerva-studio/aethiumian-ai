using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(FunctionAction))]
    public sealed class FunctionActionDrawer : NodeDrawerBase
    {
        private FunctionPickerState functionPickerState;
        private FunctionPickerDropdown functionPickerDropdown;

        public override void Draw()
        {
            SerializedProperty functionProperty = FindRelativeProperty(nameof(FunctionAction.function));
            SerializedProperty targetObjectProperty = FindRelativeProperty(nameof(FunctionAction.targetObject));
            SerializedProperty parametersProperty = FindRelativeProperty(nameof(FunctionAction.parameters));
            SerializedProperty resultProperty = FindRelativeProperty(nameof(FunctionAction.result));
            if (functionProperty?.boxedValue is not FunctionReference function)
            {
                EditorGUILayout.HelpBox("Function reference is missing.", MessageType.Error);
                return;
            }

            DrawSelection(functionProperty, targetObjectProperty, function, parametersProperty);
            DrawParameters(function, parametersProperty);
            DrawResult(function, resultProperty);
        }

        private SerializedProperty FindRelativeProperty(string propertyName) => property?.FindPropertyRelative(propertyName);

        private void DrawSelection(SerializedProperty functionProperty, SerializedProperty targetObjectProperty, FunctionReference function, SerializedProperty parametersProperty)
        {
            MethodInfo method = FunctionRegistry.Resolve(function);
            Type receiverType = GetSelectedReceiverType(method);
            string path = BuildFunctionPath(function, method);

            EditorGUILayout.LabelField("Function Action", EditorStyles.boldLabel);
            using (IndentScope.Increase)
            {
                DrawReceiver(targetObjectProperty);
                EditorGUILayout.LabelField("Path", path);
                EditorGUILayout.LabelField("Signature", FunctionRegistry.FormatSignature(method, receiverType));

                using (new GUILayout.HorizontalScope())
                {
                    Rect selectRect = GUILayoutUtility.GetRect(new GUIContent("Select..."), GUI.skin.button, GUILayout.Width(200f));
                    selectRect = EditorGUI.IndentedRect(selectRect);
                    if (GUI.Button(selectRect, "Select..."))
                    {
                        functionPickerState ??= new FunctionPickerState();
                        functionPickerState.SetContext(GetTargetScriptType(), ResolveObjectReceiverType(targetObjectProperty), FunctionRegistry.IsValidActionMethod);
                        functionPickerDropdown ??= new FunctionPickerDropdown(functionPickerState, SelectFunction);
                        functionPickerDropdown.Show(selectRect);
                    }

                    GUILayout.FlexibleSpace();

                    if (function.HasMethod && GUILayout.Button("Clear", GUILayout.Width(80f)))
                    {
                        functionProperty.serializedObject.Update();
                        function.SetMethod(default, null);
                        ApplyBoxed(functionProperty, function);
                        RebuildParameters(parametersProperty, null);
                    }
                }

                if (method != null && !FunctionRegistry.IsValidActionMethod(method))
                {
                    EditorGUILayout.HelpBox("FunctionAction requires an awaitable/coroutine return value or a NodeProgress first parameter.", MessageType.Error);
                }
            }
        }

        private void SelectFunction(FunctionRegistry.FunctionCandidate selected)
        {
            if (selected == null)
            {
                return;
            }

            SerializedProperty functionProperty = FindRelativeProperty(nameof(FunctionAction.function));
            SerializedProperty targetObjectProperty = FindRelativeProperty(nameof(FunctionAction.targetObject));
            SerializedProperty parametersProperty = FindRelativeProperty(nameof(FunctionAction.parameters));
            if (functionProperty?.boxedValue is not FunctionReference function)
            {
                return;
            }

            functionProperty.serializedObject.Update();
            function.SetMethod(selected.Method, selected.CustomId);
            VariableReference receiver = GetReceiver(targetObjectProperty);
            FunctionRegistry.AssignReceiverResource(receiver, selected.ReceiverAssignment, GetTargetScriptType());
            ApplyBoxed(targetObjectProperty, receiver);
            ApplyBoxed(functionProperty, function);
            RebuildParameters(parametersProperty, selected.Method);
        }

        private void DrawReceiver(SerializedProperty targetObjectProperty)
        {
            VariableReference receiver = GetReceiver(targetObjectProperty);
            DrawVariable(new GUIContent("Receiver"), receiver, VariableUtility.UnityObjectAndGenerics, VariableAccessFlag.Read);
            ApplyBoxed(targetObjectProperty, receiver);
        }

        private Type ResolveObjectReceiverType(SerializedProperty targetObjectProperty)
        {
            VariableReference receiver = GetReceiver(targetObjectProperty);
            if (!CanShowObjectCandidates(receiver) || tree == null)
            {
                return null;
            }

            VariableData variableData = tree.GetVariable(receiver.UUID);
            return variableData?.ObjectType;
        }

        private static bool CanShowObjectCandidates(VariableReference receiver)
        {
            return receiver != null
                && receiver.HasEditorReference
                && !FunctionRegistry.IsBuiltInReceiverReference(receiver);
        }

        private static VariableReference GetReceiver(SerializedProperty targetObjectProperty)
        {
            if (targetObjectProperty == null)
            {
                return new VariableReference();
            }

            if (targetObjectProperty.boxedValue is VariableReference receiver)
            {
                return receiver;
            }

            receiver = new VariableReference();
            targetObjectProperty.boxedValue = receiver;
            targetObjectProperty.serializedObject.ApplyModifiedProperties();
            targetObjectProperty.serializedObject.Update();
            return receiver;
        }

        private Type GetTargetScriptType()
        {
            return tree != null && tree.targetScript ? tree.targetScript.GetClass() : null;
        }

        private static Type GetSelectedReceiverType(MethodInfo method)
        {
            if (method == null || method.IsStatic)
            {
                return null;
            }

            return method.DeclaringType;
        }

        private void DrawParameters(FunctionReference function, SerializedProperty parametersProperty)
        {
            MethodInfo method = FunctionRegistry.Resolve(function);
            if (method == null || parametersProperty == null || !parametersProperty.isArray)
            {
                return;
            }

            ParameterInfo[] parameterInfos = method.GetParameters();
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            using (IndentScope.Increase)
            {
                if (parameterInfos.Length == 0)
                {
                    EditorGUILayout.LabelField("None");
                    return;
                }

                for (int i = 0; i < parameterInfos.Length; i++)
                {
                    DrawParameter(parametersProperty.GetArrayElementAtIndex(i), parameterInfos[i]);
                }
            }
        }

        private void DrawParameter(SerializedProperty parameterProperty, ParameterInfo parameterInfo)
        {
            Parameter parameter = parameterProperty.boxedValue as Parameter ?? new Parameter(parameterInfo.ParameterType);
            parameter.ParameterObjectType = parameterInfo.ParameterType;

            if (parameterInfo.ParameterType == typeof(NodeProgress) || parameterInfo.ParameterType == typeof(CancellationToken))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.LabelField($"{parameterInfo.Name.ToTitleCase()} ({parameterInfo.ParameterType.Name})");
                }
                SetParameterType(parameterProperty, parameter, VariableType.Node);
                return;
            }

            VariableType variableType = VariableUtility.GetVariableType(parameterInfo.ParameterType);
            SetParameterType(parameterProperty, parameter, variableType);
            DrawVariable(new GUIContent(parameterInfo.Name.ToTitleCase()), parameter, VariableUtility.GetCompatibleTypes(variableType), VariableAccessFlag.None);
            parameterProperty.boxedValue = parameter;
            parameterProperty.serializedObject.ApplyModifiedProperties();
            parameterProperty.serializedObject.Update();
        }

        private void DrawResult(FunctionReference function, SerializedProperty resultProperty)
        {
            MethodInfo method = FunctionRegistry.Resolve(function);
            if (method == null || resultProperty == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
            using (IndentScope.Increase)
            {
                Type returnType = FunctionRegistry.GetReturnValueType(method.ReturnType);
                if (returnType == typeof(void))
                {
                    EditorGUILayout.LabelField("void");
                    ClearResult(resultProperty);
                    return;
                }

                VariableType variableType = VariableUtility.GetVariableType(returnType);
                if (variableType == VariableType.Invalid)
                {
                    EditorGUILayout.LabelField($"Cannot store {returnType.Name}");
                    ClearResult(resultProperty);
                    return;
                }

                if (resultProperty.boxedValue is not VariableReference result)
                {
                    result = new VariableReference();
                    resultProperty.boxedValue = result;
                }

                DrawVariable(new GUIContent($"Result ({variableType})"), result, VariableUtility.GetCompatibleTypes(variableType), VariableAccessFlag.Read);
                resultProperty.boxedValue = result;
                resultProperty.serializedObject.ApplyModifiedProperties();
                resultProperty.serializedObject.Update();
            }
        }

        private void RebuildParameters(SerializedProperty parametersProperty, MethodInfo method)
        {
            if (parametersProperty == null || !parametersProperty.isArray)
            {
                return;
            }

            ParameterInfo[] parameterInfos = method?.GetParameters() ?? Array.Empty<ParameterInfo>();
            parametersProperty.arraySize = parameterInfos.Length;
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                Parameter parameter = parametersProperty.GetArrayElementAtIndex(i).boxedValue as Parameter ?? new Parameter();
                parameter.ParameterObjectType = parameterInfos[i].ParameterType;
                parameter.ForceSetConstantType(GetParameterVariableType(parameterInfos[i].ParameterType));
                parametersProperty.GetArrayElementAtIndex(i).boxedValue = parameter;
            }

            parametersProperty.serializedObject.ApplyModifiedProperties();
            parametersProperty.serializedObject.Update();
        }

        private static VariableType GetParameterVariableType(Type parameterType)
        {
            return parameterType == typeof(NodeProgress) || parameterType == typeof(CancellationToken)
                ? VariableType.Node
                : VariableUtility.GetVariableType(parameterType);
        }

        private static void SetParameterType(SerializedProperty parameterProperty, Parameter parameter, VariableType type)
        {
            if (parameter.Type != type)
            {
                parameter.ForceSetConstantType(type);
            }

            parameterProperty.boxedValue = parameter;
            parameterProperty.serializedObject.ApplyModifiedProperties();
            parameterProperty.serializedObject.Update();
        }

        private static string BuildFunctionPath(FunctionReference function, MethodInfo method)
        {
            if (function == null || !function.HasMethod)
            {
                return "None";
            }

            string typeName = method?.DeclaringType?.Name ?? function.declaringTypeFullName;
            return $"{typeName}/{function.methodName}";
        }

        private static void ClearResult(SerializedProperty resultProperty)
        {
            if (resultProperty.boxedValue is VariableReference result)
            {
                result.SetReference(null);
                resultProperty.boxedValue = result;
                resultProperty.serializedObject.ApplyModifiedProperties();
                resultProperty.serializedObject.Update();
            }
        }

        private static void ApplyBoxed(SerializedProperty targetProperty, object value)
        {
            if (targetProperty == null)
            {
                return;
            }

            targetProperty.serializedObject.Update();
            targetProperty.boxedValue = value;
            targetProperty.serializedObject.ApplyModifiedProperties();
            targetProperty.serializedObject.Update();
        }
    }
}
