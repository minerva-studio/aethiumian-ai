using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Variables.VariableData;
namespace Amlos.AI.Editor
{
    /// <summary>
    /// Drawer of variables
    /// <br/>
    /// Author : Wendell Cai
    /// </summary>
    public static class VariableFieldDrawers
    {
        /// <summary>
        /// Draw the variable field
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="tree">the behaviour tree data associate with</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public static void DrawVariable(string labelName, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null) => DrawVariable(new GUIContent(labelName), variable, tree, possibleTypes);

        /// <summary>
        /// Draw the variable field
        /// </summary>
        /// <param name="label">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="tree">the behaviour tree data associate with</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public static void DrawVariable(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null)
        {
            possibleTypes ??= (VariableType[])Enum.GetValues(typeof(VariableType));

            if (variable.GetType().IsGenericType && variable.GetType().GetGenericTypeDefinition() == typeof(VariableReference<>))
            {
                DrawVariableReference(label, variable, tree, possibleTypes);
            }
            else if (variable.GetType() == typeof(VariableReference))
            {
                DrawVariableReference(label, variable, tree, possibleTypes);
            }
            else DrawVariableField(label, variable, tree, possibleTypes);
        }

        /// <summary>
        /// Draw a <see cref="VariableReference{T}"/>
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static void DrawVariableReference(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes) => DrawVariableSelection(label, variable, tree, possibleTypes, false);

        /// <summary>
        /// Draw a <see cref="VariableField{T}"/>
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static void DrawVariableField(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes)
        {
            if (variable.IsConstant) DrawVariableConstant(label, variable, tree, possibleTypes);
            else DrawVariableSelection(label, variable, tree, possibleTypes, true);
        }

        /// <summary>
        /// Draw constant variable field
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static void DrawVariableConstant(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes)
        {
            FieldInfo newField;
            List<VariableData> allVariable = GetAllVariable(tree);
            GUILayout.BeginHorizontal();
            switch (variable.Type)
            {
                case VariableType.Int:
                    Type type = variable.FieldObjectType;
                    //Debug.Log(type.Name);
                    newField = variable.GetType().GetField("intValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (type != null && type.IsEnum)
                    {
                        Enum value = (Enum)Enum.Parse(type, variable.IntValue.ToString());
                        Enum newValue = Attribute.GetCustomAttribute(value.GetType(), typeof(FlagsAttribute)) == null
                            ? EditorGUILayout.EnumPopup(label, value)
                            : EditorGUILayout.EnumFlagsField(label, value);

                        newField.SetValue(variable, newValue);
                    }
                    else //if (type == typeof(int))
                    {
                        EditorFieldDrawers.DrawField(label, newField, variable);
                    }
                    break;
                case VariableType.String:
                    newField = variable.GetType().GetField("stringValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    EditorFieldDrawers.DrawField(label, newField, variable);
                    break;
                case VariableType.Float:
                    newField = variable.GetType().GetField("floatValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    EditorFieldDrawers.DrawField(label, newField, variable);
                    break;
                case VariableType.Bool:
                    newField = variable.GetType().GetField("boolValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    EditorFieldDrawers.DrawField(label, newField, variable);
                    break;
                case VariableType.Vector2:
                    newField = variable.GetType().GetField("vector2Value", BindingFlags.NonPublic | BindingFlags.Instance);
                    EditorFieldDrawers.DrawField(label, newField, variable);
                    break;
                case VariableType.Vector3:
                    newField = variable.GetType().GetField("vector3Value", BindingFlags.NonPublic | BindingFlags.Instance);
                    EditorFieldDrawers.DrawField(label, newField, variable);
                    break;
                case VariableType.Vector4:
                    newField = variable.GetType().GetField("vector4Value", BindingFlags.NonPublic | BindingFlags.Instance);
                    EditorFieldDrawers.DrawField(label, newField, variable);
                    break;
                case VariableType.UnityObject:
                    var uuidField = variable.GetType().GetField("unityObjectUUIDValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    var objectField = variable.GetType().GetField("unityObjectValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    var uuid = (UUID)uuidField.GetValue(variable);
                    var asset = AssetReferenceData.GetAsset(uuid);
                    objectField.SetValue(variable, asset);
                    UnityEngine.Object newAsset = null;
                    //not in asset reference table 
                    tree.AddAsset(asset, true);
                    try { newAsset = EditorGUILayout.ObjectField(label, asset, variable.FieldObjectType, false); }
                    catch { }
                    if (newAsset != asset)
                    {
                        tree.AddAsset(newAsset, true);
                        tree.RemoveAsset(asset);
                        uuid = AssetReferenceData.GetUUID(newAsset);
                        uuidField.SetValue(variable, uuid);
                        objectField.SetValue(variable, newAsset);
                        //Debug.Log("set");
                    }
                    break;
                default:
                    newField = null;
                    EditorGUILayout.LabelField(label, new GUIContent($"Cannot set a constant value for {variable.Type}"));
                    break;
            }
            if (variable is VariableField vf && vf is not Parameter && vf.IsConstant)
            {
                if (!CanDisplay(variable.Type)) vf.ForceSetConstantType(possibleTypes.FirstOrDefault());
                vf.ForceSetConstantType((VariableType)EditorGUILayout.EnumPopup(GUIContent.none, vf.Type, CanDisplay, false, EditorStyles.popup, GUILayout.MaxWidth(100)));
            }
            var validFields = allVariable.Where(f => possibleTypes.Any(p => p == f.Type)).ToList();
            if (validFields.Count > 0)
            {
                if (GUILayout.Button("Use Variable", GUILayout.MaxWidth(100)))
                {
                    variable.SetReference(validFields[0]);
                }
            }
            else if (GUILayout.Button("Create Variable", GUILayout.MaxWidth(100)))
            {
                variable.SetReference(tree.CreateNewVariable(variable.Type));
            }

            GUILayout.EndHorizontal();

            bool CanDisplay(Enum val)
            {
                return Array.IndexOf(possibleTypes, val) != -1 && (val is not VariableType.Generic and not VariableType.Invalid);
            }
        }

        /// <summary>
        /// Draw variable selection when using variable
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        /// <param name="allowConvertToConstant"></param>
        private static void DrawVariableSelection(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, bool allowConvertToConstant)
        {
            GUILayout.BeginHorizontal();

            List<VariableData> allVariable = GetAllVariable(tree);
            IEnumerable<VariableData> vars =
            variable.IsGeneric
                ? allVariable.Where(v => Array.IndexOf(possibleTypes, v.Type) != -1)
                : allVariable.Where(v => v.Type == variable.Type && Array.IndexOf(possibleTypes, v.Type) != -1);

            string[] rawList = vars.Select(v => v.name).Append("Create New...").Prepend("NONE").ToArray();
            string[] nameList = vars.Select(v => GetDescriptiveName(v)).Append("Create New...").Prepend("NONE").ToArray();

            //NONE, Create new... options only
            if (rawList.Length < 2)
            {
                EditorGUILayout.LabelField(label, "No valid variable found");
                if (GUILayout.Button("Create New", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type));
            }
            else
            {
                var selectedVariable = allVariable.Find(v => v.UUID == variable.UUID);

                string variableName = selectedVariable?.name ?? string.Empty;
                if (string.IsNullOrEmpty(variableName))
                {
                    variableName = rawList[0];
                }
                else if (Array.IndexOf(rawList, variableName) == -1)
                {
                    variableName = rawList[0];
                }
                int selectedIndex = Array.IndexOf(rawList, variableName);
                //index not found
                if (selectedIndex < 0)
                {
                    //no editor reference at all
                    if (!variable.HasEditorReference)
                    {
                        EditorGUILayout.LabelField(label, $"No Variable");
                        if (GUILayout.Button("Create", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type));
                    }
                    //has invalid reference
                    else
                    {
                        EditorGUILayout.LabelField(label, $"Variable {variableName} not found");
                        if (GUILayout.Button("Recreate", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type, variableName));
                        if (GUILayout.Button("Clear", GUILayout.MaxWidth(80))) variable.SetReference(null);
                    }
                }
                else
                {
                    int currentIndex = EditorGUILayout.Popup(label, selectedIndex, nameList, GUILayout.MinWidth(400));
                    // invalid index
                    if (currentIndex < 0) { currentIndex = 0; }
                    // no variable
                    if (selectedIndex == 0)
                    {
                        variable.SetReference(null);
                    }
                    //using existing var
                    if (currentIndex != rawList.Length - 1)
                    {
                        string varName = rawList[currentIndex];
                        VariableData a = allVariable.Find(v => v.name == varName);
                        //Debug.Log($"Select {a.name}");
                        variable.SetReference(a);
                    }
                    //Create new var
                    else
                    {
                        //Debug.Log("Create new val");
                        VariableType variableType = possibleTypes.FirstOrDefault();
                        //Debug.Log(variableType);
                        VariableData newVariableData = tree.CreateNewVariable(variableType);
                        variable.SetReference(newVariableData);
                    }
                }
            }

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

        /// <summary>
        /// Get a descriptive name for the variable
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        private static string GetDescriptiveName(VariableData v)
        {
            if (v.isStatic)
            {
                return $"{v.name} [Static]";
            }
            else if (v.isGlobal)
            {
                return $"{v.name} [Global]";
            }
            else if (v.isStandard)
            {
                return $"{v.name} [Standard]";
            }
            else
            {
                return v.name;
            }
        }

        private static List<VariableData> GetAllVariable(BehaviourTreeData tree)
        {
            if (tree == null)
            {
                Debug.Log("Missing Tree when achiving variables");
                return new List<VariableData>();
            }

            List<VariableData> enumerable = tree.variables.Union(AISetting.Instance.globalVariables).ToList();
            enumerable.Add(GameObjectVariable);
            enumerable.Add(TransformVariable);
            enumerable.Add(TargetScriptVariable);
            return enumerable;
        }
    }
}

