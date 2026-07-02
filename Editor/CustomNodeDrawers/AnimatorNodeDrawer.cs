using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(Nodes.Animator))]
    public class AnimatorNodeDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            Nodes.Animator ac = node as Nodes.Animator;
            SerializedProperty parametersProperty = property.FindPropertyRelative(nameof(Nodes.Animator.parameters));
            if (!tree.AnimatorController)
            {
                EditorGUILayout.HelpBox($"Animator of the AI {tree.name} has not yet been assigned", MessageType.Warning);
                return;
            }
            var parameters = tree.AnimatorController.parameters;

            // no parameter
            if (parameters.Length == 0)
            {
                EditorGUILayout.HelpBox($"Animator {tree.AnimatorController.name} has no parameter", MessageType.Warning);
                return;
            }

            foreach (var item in parameters)
            {
                int parameterIndex = ac.parameters?.FindIndex(p => p.parameter == item.name) ?? -1;
                var p = parameterIndex >= 0 ? ac.parameters[parameterIndex] : null;
                if (p == null)
                {
                    p = new Nodes.Animator.Parameter();
                    ac.parameters ??= new();
                    ac.parameters.Add(p);
                    parameterIndex = ac.parameters.Count - 1;
                }

                if (parametersProperty != null && parameterIndex >= parametersProperty.arraySize)
                {
                    parametersProperty.arraySize = parameterIndex + 1;
                    parametersProperty.GetArrayElementAtIndex(parameterIndex).boxedValue = p;
                    parametersProperty.serializedObject.ApplyModifiedProperties();
                    parametersProperty.serializedObject.Update();
                }

                SerializedProperty parameterProperty = parametersProperty?.GetArrayElementAtIndex(parameterIndex);
                GUILayout.BeginHorizontal();
                var enabled = GUI.enabled;
                p.use = EditorGUILayout.Toggle(p.use, GUILayout.Width(EditorGUIUtility.singleLineHeight * 2));
                p.parameter = item.name;
                p.type = Nodes.Animator.Convert(item.type);
                var displayName = item.name.ToTitleCase();
                ApplyBoxedValue(parameterProperty, p);

                GUI.enabled = p.use;
                switch (p.type)
                {
                    case Nodes.Animator.ParameterType.@int:
                        DrawVariableProperty(new GUIContent(displayName), parameterProperty?.FindPropertyRelative(nameof(p.valueInt)));
                        break;
                    case Nodes.Animator.ParameterType.@float:
                        DrawVariableProperty(new GUIContent(displayName), parameterProperty?.FindPropertyRelative(nameof(p.valueFloat)));
                        break;
                    case Nodes.Animator.ParameterType.@bool:
                        DrawVariableProperty(new GUIContent(displayName), parameterProperty?.FindPropertyRelative(nameof(p.valueBool)));
                        break;
                    case Nodes.Animator.ParameterType.trigger:
                        p.setTrigger = (Nodes.Animator.TriggerSet)
                            EditorGUILayout.EnumPopup(displayName, p.setTrigger);
                        ApplyBoxedValue(parameterProperty, p);
                        break;
                    default:
                        break;
                }
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();

            }
        }
    }
}
