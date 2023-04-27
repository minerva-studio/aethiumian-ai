using Amlos.AI.Nodes;
using System;
using System.Runtime.ConstrainedExecution;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(GetComponentValue))]
    public class GetComponentDrawer : MethodCallerDrawerBase
    {
        public GetComponentValue Node => (GetComponentValue)node;

        //    public override void Draw()
        //    {
        //        Node.getComponent = EditorGUILayout.Toggle("Get Component On Self", Node.getComponent);
        //        if (!Node.getComponent) DrawVariable("Component", Node.component);
        //        DrawTypeReference("Component Type", Node.componentReference);
        //        Type componentType = Node.componentReference;
        //        Component component;
        //        if (componentType == null || !componentType.IsSubclassOf(typeof(Component)))
        //        {
        //            GUILayout.Label("Component is not valid");
        //            return;
        //        }
        //        else if (!TreeData.prefab || !TreeData.prefab.TryGetComponent(componentType, out component))
        //        {
        //            GUILayout.Label("Component is not found in the prefab");
        //            return;
        //        }
        //        Type typeOfComponent = typeof(Component);
        //        GUILayoutOption changedButtonWidth = GUILayout.MaxWidth(20);
        //        GUILayoutOption useVariableWidth = GUILayout.MaxWidth(100);
        //        foreach (var memberInfo in componentType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField | BindingFlags.SetProperty))
        //        {
        //            //member is obsolete
        //            if (memberInfo.IsDefined(typeof(ObsoleteAttribute)))
        //            {
        //                continue;
        //            }
        //            //member is too high in the hierachy
        //            if (typeOfComponent.IsSubclassOf(memberInfo.DeclaringType) || typeOfComponent == memberInfo.DeclaringType)
        //            {
        //                continue;
        //            }
        //            //properties that is readonly
        //            Type valueType;
        //            object currentValue;
        //            object newVal;
        //            if (memberInfo is FieldInfo fi)
        //            {
        //                if (fi.FieldType.IsSubclassOf(typeof(Component))) continue;
        //                if (fi.FieldType.IsSubclassOf(typeof(ScriptableObject))) continue;
        //                currentValue = fi.GetValue(component);
        //                valueType = fi.FieldType;
        //            }
        //            else if (memberInfo is PropertyInfo pi)
        //            {
        //                if (pi.PropertyType.IsSubclassOf(typeof(Component))) continue;
        //                if (pi.PropertyType.IsSubclassOf(typeof(ScriptableObject))) continue;
        //                if (!pi.CanWrite) continue;
        //                currentValue = pi.GetValue(component);
        //                valueType = pi.PropertyType;
        //            }
        //            else
        //            {
        //                continue;
        //            }

        //            VariableType type = VariableUtility.GetVariableType(valueType);
        //            if (!EditorFieldDrawers.IsSupported(currentValue)) continue;
        //            if (type == VariableType.Invalid) continue;
        //            if (type == VariableType.Node) continue;

        //            GUILayout.BeginHorizontal();
        //            var changeIsDefined = Node.IsChangeDefinded(memberInfo.Name);

        //            // already have change entry
        //            if (changeIsDefined)
        //            {
        //                DrawVariable(memberInfo.Name.ToTitleCase(), Node.GetChangeEntry(memberInfo.Name).data, VariableUtility.GetCompatibleTypes(type));
        //                //if (GUILayout.Button("Use Variable", useVariableWidth))
        //                //{
        //                //    Node.AddChangeEntry(memberInfo.Name, type);
        //                //    Debug.Log("Add Entry");
        //                //}
        //                if (GUILayout.Button("X", changedButtonWidth))
        //                {
        //                    Node.RemoveChangeEntry(memberInfo.Name);
        //                }
        //            }
        //            // no change entry
        //            else
        //            {
        //                EditorFieldDrawers.DrawField(memberInfo.Name.ToTitleCase(), currentValue, isReadOnly: true);
        //                if (currentValue == null)
        //                {
        //                    Debug.Log($"value {memberInfo.Name.ToTitleCase()} is null: {valueType.FullName}");
        //                }
        //                //else if (!currentValue.Equals(newVal))
        //                //{
        //                //    Debug.Log("value changed");
        //                //    Node.AddChangeEntry(memberInfo.Name, newVal);
        //                //} 
        //                if (GUILayout.Button("Get", useVariableWidth))
        //                {
        //                    Node.AddPointer(memberInfo.Name, type);
        //                    Debug.Log("Add Entry");
        //                }
        //                var prevState = GUI.enabled;
        //                GUI.enabled = false;
        //                GUILayout.Button("-", changedButtonWidth);
        //                GUI.enabled = prevState;
        //            }
        //            GUILayout.EndHorizontal();
        //        }
        //    }

        public override void Draw()
        {
            if (!DrawComponent(Node)) return;

            EditorGUI.indentLevel++;
            DrawTypeReference("Component", Node.type);

            GenericMenu menu = new();
            if (TreeData.targetScript)
                menu.AddItem(new GUIContent("Use Target Script Type"), false, () => Node.TypeReference.SetReferType(TreeData.targetScript.GetClass()));
            if (!Node.GetComponent)
                menu.AddItem(new GUIContent("Use Variable Type"), false, () => Node.TypeReference.SetReferType(TreeData.GetVariableType(Node.Component.UUID)));
            RightClickMenu(menu);

            Type componentType = Node.type;
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
            EditorGUI.indentLevel--;
            GUILayout.Space(10);

            DrawGetFields(Node, component, componentType);
        }
    }
}
