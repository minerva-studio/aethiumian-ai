using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 
/// </summary>
namespace Amlos.AI.Editor
{
    /// <summary>
    /// Drawer of variables
    /// 
    /// Author : Wendell Cai
    /// </summary>
    public static class VariableDrawers
    {
        public static void DrawVariable(string labelName, BehaviourTreeData tree, VariableBase variable, VariableType[] possibleTypes = null)
        {
            possibleTypes ??= (VariableType[])Enum.GetValues(typeof(VariableType));

            if (variable.GetType().IsGenericType && variable.GetType().GetGenericTypeDefinition() == typeof(VariableReference<>))
            {
                DrawVariableReference(labelName, tree, variable, possibleTypes);
            }
            else if (variable.GetType() == typeof(VariableReference))
            {
                DrawVariableReference(labelName, tree, variable, possibleTypes);
            }
            else DrawVariableField(labelName, tree, variable, possibleTypes);
        }

        /// <summary>
        /// Call to draw a <see cref="VariableReference{T}"/>
        /// </summary>
        /// <param name="labelName"></param>
        /// <param name="tree"></param>
        /// <param name="variable"></param>
        /// <param name="possibleTypes"></param>
        private static void DrawVariableReference(string labelName, BehaviourTreeData tree, VariableBase variable, VariableType[] possibleTypes) => DrawVariableSelection(labelName, tree, variable, possibleTypes);

        /// <summary>
        /// Call to draw a <see cref="VariableField{T}"/>
        /// </summary>
        /// <param name="labelName"></param>
        /// <param name="tree"></param>
        /// <param name="variable"></param>
        /// <param name="possibleTypes"></param>
        private static void DrawVariableField(string labelName, BehaviourTreeData tree, VariableBase variable, VariableType[] possibleTypes)
        {
            if (variable.IsConstant) DrawVariableConstant(labelName, tree, variable, possibleTypes);
            else DrawVariableSelection(labelName, tree, variable, possibleTypes, true);
        }

        /// <summary>
        /// Call to draw constant variable field
        /// </summary>
        /// <param name="labelName"></param>
        /// <param name="tree"></param>
        /// <param name="variable"></param>
        /// <param name="possibleTypes"></param>
        private static void DrawVariableConstant(string labelName, BehaviourTreeData tree, VariableBase variable, VariableType[] possibleTypes)
        {
            VariableField f;
            FieldInfo newField;
            switch (variable.Type)
            {
                case VariableType.String:
                    newField = variable.GetType().GetField("stringValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
                case VariableType.Int:
                    newField = variable.GetType().GetField("intValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
                case VariableType.Float:
                    newField = variable.GetType().GetField("floatValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
                case VariableType.Bool:
                    newField = variable.GetType().GetField("boolValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
                case VariableType.Vector2:
                    newField = variable.GetType().GetField("vector2Value", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
                case VariableType.Vector3:
                    newField = variable.GetType().GetField("vector3Value", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
                default:
                    newField = null;
                    break;
            }
            GUILayout.BeginHorizontal();
            EditorFieldDrawers.DrawField(labelName, newField, variable);
            if (variable is VariableField vf && vf is not Parameter && vf.IsConstant)
            {
                if (!CanDisplay(variable.Type)) vf.SetType(possibleTypes.FirstOrDefault());
                vf.SetType((VariableType)EditorGUILayout.EnumPopup(GUIContent.none, vf.Type, CanDisplay, false, EditorStyles.popup, GUILayout.MaxWidth(80)));
            }
            if (tree.variables.Count > 0 && GUILayout.Button("Use Variable", GUILayout.MaxWidth(100)))
            {
                variable.SetReference(tree.variables[0]);
            }
            GUILayout.EndHorizontal();

            bool CanDisplay(Enum val)
            {
                return Array.IndexOf(possibleTypes, val) != -1;
            }
        }

        private static void DrawVariableSelection(string labelName, BehaviourTreeData tree, VariableBase variable, VariableType[] possibleTypes, bool allowConvertToConstant = false)
        {
            GUILayout.BeginHorizontal();

            string[] list;
            IEnumerable<VariableData> vars =
            variable.IsGeneric
                ? tree.variables.Where(v => Array.IndexOf(possibleTypes, v.type) != -1)
                : tree.variables.Where(v => v.type == variable.Type && Array.IndexOf(possibleTypes, v.type) != -1);
            ;
            list = vars.Select(v => v.name).Append("Create New...").Prepend("NONE").ToArray();

            if (list.Length < 2)
            {
                EditorGUILayout.LabelField(labelName, "No valid variable found");
                if (GUILayout.Button("Create New", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type));
            }
            else
            {
                string variableName = tree.GetVariable(variable.UUID)?.name ?? "";
                if (Array.IndexOf(list, variableName) == -1)
                {
                    variableName = list[0];
                }
                int selectedIndex = Array.IndexOf(list, variableName);
                if (selectedIndex < 0)
                {
                    if (!variable.HasReference)
                    {
                        EditorGUILayout.LabelField(labelName, $"No Variable");
                        if (GUILayout.Button("Create", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type));
                    }
                    else
                    {
                        EditorGUILayout.LabelField(labelName, $"Variable {variableName} not found");
                        if (GUILayout.Button("Recreate", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type, variableName));
                        if (GUILayout.Button("Clear", GUILayout.MaxWidth(80))) variable.SetReference(null);
                    }
                }
                else
                {
                    int currentIndex = EditorGUILayout.Popup(labelName, selectedIndex, list, GUILayout.MinWidth(400));
                    if (currentIndex < 0) { currentIndex = 0; }
                    if (selectedIndex == 0)
                    {
                        variable.SetReference(null);
                    }
                    //using existing var
                    if (currentIndex != list.Length - 1)
                    {
                        string varName = list[currentIndex];
                        var a = tree.GetVariable(varName);
                        variable.SetReference(a);
                    }
                    //Create new var
                    else
                    {
                        tree.CreateNewVariable(variable.Type);
                    }
                }
            }


            //if (variable.IsGeneric && variable.IsConstant)
            //{
            //    if (!CanDisplay(variable.Type)) variable.Type = possibleTypes.FirstOrDefault();
            //    variable.Type = (VariableType)EditorGUILayout.EnumPopup(GUIContent.none, variable.Type, CanDisplay, false, EditorStyles.popup, GUILayout.MaxWidth(80));
            //}

            if (allowConvertToConstant)
            {
                if (GUILayout.Button("Set Constant", GUILayout.MaxWidth(100)))
                {
                    variable.SetReference(null);
                }
            }
            else
            {
                EditorGUILayout.LabelField("         ", GUILayout.MaxWidth(100));
            }
            GUILayout.EndHorizontal();


            //bool CanDisplay(Enum val)
            //{
            //    return Array.IndexOf(possibleTypes, val) != -1;
            //}
        }
    }
}

