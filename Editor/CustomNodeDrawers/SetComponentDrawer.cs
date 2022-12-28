using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(SetComponentValue))]
    public class SetComponentDrawer : NodeDrawerBase
    {
        public SetComponentValue Node => (SetComponentValue)node;

        public override void Draw()
        {
            Node.getComponent = EditorGUILayout.Toggle("Get Component On Self", Node.getComponent);
            if (!Node.getComponent)
            {
                DrawVariable("Component", Node.component);
                VariableData variableData = TreeData.GetVariable(Node.component.UUID);
                if (variableData != null) Node.componentReference.SetType(variableData.typeReference);
                if (!Node.component.HasReference)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("No Component Assigned");
                    return;
                }
            }

            DrawTypeReference("Component", Node.componentReference);
            Type componentType = Node.componentReference;
            Component component = null;
            if (componentType == null || !componentType.IsSubclassOf(typeof(Component)))
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Component is not valid");
                return;
            }
            else if (Node.getComponent && (!TreeData.prefab || !TreeData.prefab.TryGetComponent(componentType, out component)))
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Component is not found in the prefab");
                return;
            }
            Type typeOfComponent = typeof(Component);
            GUILayout.Space(10);
            DrawAllField(componentType, component, typeOfComponent);
        }

        private void DrawAllField(Type componentType, Component component, Type typeOfComponent)
        {
            GUILayoutOption changedButtonWidth = GUILayout.MaxWidth(20);
            GUILayoutOption useVariableWidth = GUILayout.MaxWidth(100);

            foreach (var memberInfo in componentType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField | BindingFlags.SetProperty))
            {
                //member is obsolete
                if (memberInfo.IsDefined(typeof(ObsoleteAttribute)))
                {
                    continue;
                }
                //member is too high in the hierachy
                if (typeOfComponent.IsSubclassOf(memberInfo.DeclaringType) || typeOfComponent == memberInfo.DeclaringType)
                {
                    continue;
                }
                //properties that is readonly
                Type valueType;
                object currentValue = null;
                object newVal;
                if (memberInfo is FieldInfo fi)
                {
                    if (fi.FieldType.IsSubclassOf(typeof(Component))) continue;
                    if (fi.FieldType.IsSubclassOf(typeof(ScriptableObject))) continue;
                    if (component) currentValue = fi.GetValue(component);
                    valueType = fi.FieldType;
                }
                else if (memberInfo is PropertyInfo pi)
                {
                    if (pi.PropertyType.IsSubclassOf(typeof(Component))) continue;
                    if (pi.PropertyType.IsSubclassOf(typeof(ScriptableObject))) continue;
                    if (!pi.CanWrite) continue;
                    if (component) currentValue = pi.GetValue(component);
                    valueType = pi.PropertyType;
                }
                else
                {
                    continue;
                }

                VariableType type = VariableUtility.GetVariableType(valueType);
                if (!VariableUtility.IsSupported(valueType)) continue;
                if (type == VariableType.Invalid) continue;
                if (type == VariableType.Node) continue;

                GUILayout.BeginHorizontal();
                var changeIsDefined = Node.IsChangeDefinded(memberInfo.Name);

                // already have change entry
                if (changeIsDefined)
                {
                    DrawVariable(memberInfo.Name.ToTitleCase(), Node.GetChangeEntry(memberInfo.Name).data, new VariableType[] { type });
                    if (GUILayout.Button("X", changedButtonWidth))
                    {
                        Node.RemoveChangeEntry(memberInfo.Name);
                    }
                }
                // no change entry
                else
                {
                    newVal = EditorFieldDrawers.DrawField(memberInfo.Name.ToTitleCase(), currentValue, false);
                    if (currentValue == null)
                    {
                        string label2 = type == VariableType.UnityObject || type == VariableType.Generic ? $"({type}: {valueType.Name})" : $"({type})";
                        EditorGUILayout.LabelField(memberInfo.Name.ToTitleCase(), label2);
                        //Debug.Log($"value {memberInfo.Name.ToTitleCase()} is null: {valueType.FullName}");
                    }
                    else if (!currentValue.Equals(newVal))
                    {
                        Debug.Log("value changed");
                        Node.AddChangeEntry(memberInfo.Name, newVal);
                    }

                    if (GUILayout.Button("Modify", useVariableWidth))
                    {
                        Node.AddChangeEntry(memberInfo.Name, type);
                        Debug.Log("Add Entry");
                    }
                    var prevState = GUI.enabled;
                    GUI.enabled = false;
                    GUILayout.Button("-", changedButtonWidth);
                    GUI.enabled = prevState;
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
