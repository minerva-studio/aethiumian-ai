using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public static bool DrawVariable(string labelName, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null) => DrawVariable(new GUIContent(labelName), variable, tree, possibleTypes);

        /// <summary>
        /// Draw the variable field
        /// </summary>
        /// <param name="label">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="tree">the behaviour tree data associate with</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public static bool DrawVariable(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null)
        {
            possibleTypes ??= (VariableType[])Enum.GetValues(typeof(VariableType));

            if (variable.GetType().IsGenericType && variable.GetType().GetGenericTypeDefinition() == typeof(VariableReference<>))
            {
                return DrawVariableReference(label, variable, tree, possibleTypes);
            }
            else if (variable.GetType() == typeof(VariableReference))
            {
                return DrawVariableReference(label, variable, tree, possibleTypes);
            }
            else return DrawVariableField(label, variable, tree, possibleTypes);
        }





        /// <summary>
        /// Draw a <see cref="VariableReference{T}"/>
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static bool DrawVariableReference(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes) => DrawVariableSelection(label, variable, tree, possibleTypes, false);

        /// <summary>
        /// Draw a <see cref="VariableField{T}"/>
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static bool DrawVariableField(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes)
        {
            if (variable.IsConstant) return DrawVariableConstant(label, variable, tree, possibleTypes);
            else return DrawVariableSelection(label, variable, tree, possibleTypes, true);
        }






        /// <summary>
        /// Draw constant variable field
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static bool DrawVariableConstant(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes)
        {
            bool isChanged = false;
            List<VariableData> allVariable = GetAllVariable(tree);
            GUILayout.BeginHorizontal();
            switch (variable.Type)
            {
                case VariableType.Int:
                    {
                        var intVal = variable.IntValue;
                        Type type = variable.FieldObjectType;
                        if (type != null)
                        {
                            if (type.IsEnum)
                            {
                                Enum value = (Enum)Enum.Parse(type, intVal.ToString());
                                // draw flag or normal
                                Enum newValue = Attribute.GetCustomAttribute(value.GetType(), typeof(FlagsAttribute)) == null
                                    ? EditorGUILayout.EnumPopup(label, value)
                                    : EditorGUILayout.EnumFlagsField(label, value);

                                isChanged = SetConstantIfChange(tree, label.text, variable, intVal, Convert.ToInt32(value));
                                break;
                            }
                            if (type == typeof(uint))
                            {
                                isChanged = SetConstantIfChange(tree, label.text, variable, intVal, EditorGUILayout.IntField(label, intVal));
                                break;
                            }
                            if (type == typeof(LayerMask))
                            {
                                LayerMask oldMask = new() { value = intVal };
                                LayerMask newValue = EditorFieldDrawers.DrawLayerMask(label, oldMask);
                                isChanged = SetConstantIfChange(tree, label.text, variable, intVal, newValue.value);
                                break;
                            }

                        }
                        isChanged = SetConstantIfChange(tree, label.text, variable, intVal, EditorGUILayout.IntField(label, intVal));
                        break;
                    }
                case VariableType.String:
                    //newField = variable.GetType().GetField("stringValue", BindingFlags.NonPublic | BindingFlags.Instance);
                    //EditorFieldDrawers.DrawField(label, newField, variable);
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.StringValue, EditorGUILayout.TextField(label, variable.StringValue));
                    break;
                case VariableType.Float:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.FloatValue, EditorGUILayout.FloatField(label, variable.FloatValue));
                    break;
                case VariableType.Bool:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.BoolValue, EditorGUILayout.Toggle(label, variable.BoolValue));
                    break;
                case VariableType.Vector2:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.Vector2Value, EditorGUILayout.Vector2Field(label, variable.Vector2Value));
                    break;
                case VariableType.Vector3:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.Vector3Value, EditorGUILayout.Vector3Field(label, variable.Vector3Value));
                    break;
                case VariableType.Vector4:
                    {
                        var v4 = variable.Vector4Value;
                        Type type = variable.FieldObjectType;
                        if (type != null)
                        {
                            if (type == typeof(Color))
                            {
                                Color oldColor = variable.ColorValue;
                                Color newValue = EditorGUILayout.ColorField(label, oldColor);
                                isChanged = SetConstantIfChange(tree, label.text, variable, v4, (Vector4)newValue);
                                break;
                            }
                        }
                        isChanged = SetConstantIfChange(tree, label.text, variable, v4, EditorGUILayout.Vector4Field(label, variable.Vector4Value));
                        break;
                    }
                case VariableType.UnityObject:
                    var asset = variable.UnityObjectValue;
                    // update assets status
                    if (!asset && variable.ConstanUnityObjectUUID != UUID.Empty)
                    {
                        asset = AssetReferenceData.GetAsset(variable.ConstanUnityObjectUUID);
                    }
                    //not in asset reference table 
                    tree.AddAsset(asset, true);
                    tree.RemoveAsset(variable.ConstanUnityObjectUUID);
                    UnityEngine.Object newAsset = null;
                    try { newAsset = EditorGUILayout.ObjectField(label, asset, variable.FieldObjectType, false); }
                    catch { }
                    if (SetConstantIfChange(tree, label.text, variable, asset, newAsset))
                    {
                        isChanged = true;
                        tree.AddAsset(newAsset, true);
                        tree.RemoveAsset(asset);
                    }
                    break;
                default:
                    EditorGUILayout.LabelField(label, new GUIContent($"Cannot set a constant value for {variable.Type}"));
                    break;
            }
            if (variable is VariableField vf && vf is not Parameter && vf.IsConstant)
            {
                if (!CanDisplay(variable.Type)) vf.ForceSetConstantType(possibleTypes.FirstOrDefault());
                vf.ForceSetConstantType((VariableType)EditorGUILayout.EnumPopup(GUIContent.none, vf.Type, CanDisplay, false, EditorStyles.popup, GUILayout.MaxWidth(100)));
                isChanged = true;
            }
            var validFields = allVariable.Where(f => possibleTypes.Any(p => p == f.Type)).ToList();
            if (validFields.Count > 0)
            {
                if (GUILayout.Button("Use Variable", GUILayout.MaxWidth(100)))
                {
                    //Debug.Log(validFields[0]);
                    //Debug.Log(validFields[0]?.name);
                    isChanged = SetVariableIfChange(tree, label.text, variable, validFields[0]);
                    //variable.SetReference(validFields[0]);
                }
            }
            else if (GUILayout.Button("Create Variable", GUILayout.MaxWidth(100)))
            {
                CreateVariable(tree, variable);
                isChanged = true;
                //variable.SetReference(tree.CreateNewVariable(variable.Type));
            }

            GUILayout.EndHorizontal();
            return isChanged;

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
        private static bool DrawVariableSelection(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, bool allowConvertToConstant)
        {
            bool isChanged = false;
            GUILayout.BeginHorizontal();

            List<VariableData> allVariable = GetAllVariable(tree);
            var (rawList, nameList) = GetVariables(variable, tree, possibleTypes, allVariable);

            //NONE, Create new... options only
            if (rawList.Length < 2)
            {
                EditorGUILayout.LabelField(label, "No valid variable found");
                if (GUILayout.Button("Create New", GUILayout.MaxWidth(80)))
                {
                    CreateVariable(tree, variable);
                    isChanged = true;
                }
            }
            else
            {
                var selectedVariable = allVariable.Find(v => v.UUID == variable.UUID);
                string variableName = selectedVariable?.name ?? string.Empty;
                if (string.IsNullOrEmpty(variableName) || variableName == NONE_VARIABLE_NAME)
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
                        if (GUILayout.Button("Create", GUILayout.MaxWidth(80)))
                        {
                            CreateVariable(tree, variable);
                            isChanged = true;
                        }
                    }
                    //has invalid reference
                    else
                    {
                        EditorGUILayout.LabelField(label, $"Variable {variableName} not found");
                        if (GUILayout.Button("Recreate", GUILayout.MaxWidth(80))) CreateVariable(tree, variable, variableName);
                        if (GUILayout.Button("Clear", GUILayout.MaxWidth(80))) SetVariableIfChange(tree, label.text, variable, null);
                    }
                }
                else
                {
                    int currentIndex = EditorGUILayout.Popup(label, selectedIndex, nameList, GUILayout.MinWidth(400));
                    // invalid index
                    if (currentIndex >= 0)
                    {
                        // no variable
                        if (selectedIndex == 0)
                        {
                            isChanged = SetVariableIfChange(tree, label.text, variable, null);
                            //variable.SetReference(null);
                        }
                        //using existing var
                        if (currentIndex != rawList.Length - 1)
                        {
                            string varName = rawList[currentIndex];
                            VariableData a = allVariable.Find(v => v.name == varName);
                            //Debug.Log($"Select {a.name}");
                            isChanged = SetVariableIfChange(tree, label.text, variable, a);
                            //variable.SetReference(a);
                        }
                        //Create new var
                        else
                        {
                            //Debug.Log("Create new val"); 
                            VariableType variableType = possibleTypes.FirstOrDefault();
                            //VariableData newVariableData = tree.CreateNewVariable(variableType); 
                            CreateVariable(tree, variable, variableType);
                            isChanged = true;
                            //Debug.Log(variableType);
                            //variable.SetReference(newVariableData);
                        }
                    }
                }
            }

            if (allowConvertToConstant)
            {
                if (GUILayout.Button("Set Constant", GUILayout.MaxWidth(100)))
                {
                    isChanged = SetVariableIfChange(tree, label.text, variable, null);
                }
            }
            else
            {
                EditorGUILayout.LabelField("         ", GUILayout.MaxWidth(100));
            }
            GUILayout.EndHorizontal();
            return isChanged;
        }

        private static (string[] rawList, string[] nameList) GetVariables(VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, List<VariableData> allVariable)
        {
            IEnumerable<VariableData> vars =
            variable.IsGeneric
                ? allVariable.Where(v => Array.IndexOf(possibleTypes, v.Type) != -1)
                : allVariable.Where(v => v.Type == variable.Type && Array.IndexOf(possibleTypes, v.Type) != -1);

            var rawList = vars.Select(v => v.name).Append("Create New...").Prepend(NONE_VARIABLE_NAME).ToArray();
            var nameList = vars.Select(v => tree.GetVariableDescName(v)).Append("Create New...").Prepend(NONE_VARIABLE_NAME).ToArray();
            return (rawList, nameList);
        }

        private static bool SetConstantIfChange<T>(BehaviourTreeData tree, string variableName, VariableBase variable, T oldVal, T newVal)
        {
            if (newVal == null && oldVal != null)
            {
                Undo.RecordObject(tree, $"Set variable {variableName} in {tree.name} from {oldVal} to default");
                variable.ForceSetConstantValue(newVal);
                return true;
            }
            if (newVal != null && !newVal.Equals(oldVal))
            {
                Undo.RecordObject(tree, $"Set variable {variableName} in {tree.name} from {oldVal} to {newVal}");
                variable.ForceSetConstantValue(newVal);
                return true;
            }
            return false;
        }

        private static bool SetVariableIfChange(BehaviourTreeData tree, string variableName, VariableBase variable, VariableData vd)
        {
            if (vd == null && variable.UUID != UUID.Empty)
            {
                var oldName = tree.GetVariableDescName(variable.UUID);
                Undo.RecordObject(tree, $"Clear variable reference {variableName} in {tree.name} from {oldName}");
                variable.SetReference(vd);
                return true;
            }
            if (vd != null && variable.UUID != vd.UUID)
            {
                var oldName = tree.GetVariableDescName(variable.UUID);
                var newName = vd?.name ?? MISSING_VARIABLE_NAME;
                Undo.RecordObject(tree, $"Set variable {variableName} in {tree.name} reference from {oldName} to {newName}");
                variable.SetReference(vd);
                return true;
            }
            return false;
        }

        private static void CreateVariable(BehaviourTreeData tree, VariableBase variable, string name = null)
        {
            CreateVariable(tree, variable, variable.Type, name);
        }

        private static void CreateVariable(BehaviourTreeData tree, VariableBase variable, VariableType type, string name = null)
        {
            string newVarName = name ?? tree.GenerateNewVariableName(variable.Type.ToString());
            Undo.RecordObject(tree, $"Create Variable {newVarName} in {tree.name}");
            variable.SetReference(tree.CreateNewVariable(type, newVarName));
        }






        private static List<VariableData> GetAllVariable(BehaviourTreeData tree)
        {
            if (tree == null)
            {
                Debug.Log("Missing Tree when achiving variables");
                return new List<VariableData>();
            }

            List<VariableData> enumerable = tree.EditorVariables.Union(AISetting.Instance.globalVariables).ToList();
            enumerable.Add(GameObjectVariable);
            enumerable.Add(TransformVariable);
            enumerable.Add(VariableData.TargetScriptVariable);
            return enumerable;
        }
    }
}

