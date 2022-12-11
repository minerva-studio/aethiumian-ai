using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(SetComponentValue))]
    public class SetComponentDrawer : NodeDrawerBase
    {
        public SetComponentValue Node => (SetComponentValue)node;

        public override void Draw()
        {
            DrawComponent("Component", Node.componentReference);
            Type componentType = Node.componentReference;
            Component component;
            if (componentType == null || !componentType.IsSubclassOf(typeof(Component)))
            {
                GUILayout.Label("Component is not valid");
                return;
            }
            else if (!Tree.prefab || !Tree.prefab.TryGetComponent(componentType, out component))
            {
                GUILayout.Label("Component is not found in the prefab");
                return;
            }
            Type typeOfComponent = typeof(Component);
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
                object currentValue;
                object newVal;
                if (memberInfo is FieldInfo fi)
                {
                    if (fi.FieldType.IsSubclassOf(typeof(Component))) continue;
                    if (fi.FieldType.IsSubclassOf(typeof(ScriptableObject))) continue;
                    currentValue = fi.GetValue(component);
                    valueType = fi.FieldType;
                }
                else if (memberInfo is PropertyInfo pi)
                {
                    if (pi.PropertyType.IsSubclassOf(typeof(Component))) continue;
                    if (pi.PropertyType.IsSubclassOf(typeof(ScriptableObject))) continue;
                    if (!pi.CanWrite) continue;
                    currentValue = pi.GetValue(component);
                    valueType = pi.PropertyType;
                }
                else
                {
                    continue;
                }

                VariableType type = VariableTypeExtensions.GetVariableType(valueType);
                if (!EditorFieldDrawers.IsSupported(currentValue)) continue;
                if (type == VariableType.Invalid) continue;
                if (type == VariableType.Node) continue;

                GUILayout.BeginHorizontal();
                var changeIsDefined = Node.IsChangeDefinded(memberInfo.Name);

                // already have change entry
                if (changeIsDefined)
                {
                    DrawVariable(memberInfo.Name.ToTitleCase(), Node.GetChangeEntry(memberInfo.Name).data, new VariableType[] { type });
                    //if (GUILayout.Button("Use Variable", useVariableWidth))
                    //{
                    //    Node.AddChangeEntry(memberInfo.Name, type);
                    //    Debug.Log("Add Entry");
                    //}
                    if (GUILayout.Button("X", changedButtonWidth))
                    {
                        Node.RemoveChangeEntry(memberInfo.Name);
                    }
                }
                // no change entry
                else
                {
                    newVal = EditorFieldDrawers.DrawField(memberInfo.Name.ToTitleCase(), currentValue);
                    if (currentValue == null)
                    {
                        Debug.Log($"value {memberInfo.Name.ToTitleCase()} is null: {valueType.FullName}");
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
