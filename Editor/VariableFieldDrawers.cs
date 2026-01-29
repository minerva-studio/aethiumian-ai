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
        private const float ButtonWidth = 100f;
        private const float SmallButtonWidth = 80f;
        private const float EnumPopupWidth = 90f;
        private const float FieldSpacing = 4f;

        private static Rect GetRowRect(Rect position)
        {
            Rect row = position;
            row.height = EditorGUIUtility.singleLineHeight;
            // position should already be indented
            return row;
            //return EditorGUI.IndentedRect(row);
        }

        private static Rect ReserveRight(ref Rect rect, float width)
        {
            Rect right = new Rect(rect.xMax - width, rect.y, width, rect.height);
            rect.width -= width + FieldSpacing;
            return right;
        }

        private static LayerMask DrawLayerMask(Rect position, GUIContent label, LayerMask lm)
        {
            string[] layers = System.Linq.Enumerable.Range(0, 31)
                .Select(index => LayerMask.LayerToName(index))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();
            return new LayerMask { value = EditorGUI.MaskField(position, label, lm.value, layers) };
        }

        /// <summary>
        /// Draw the variable field within a fixed position.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="labelName">Name of the label.</param>
        /// <param name="variable">The variable instance.</param>
        /// <param name="tree">The behaviour tree data associated with the variable.</param>
        /// <param name="possibleTypes">Type constraint, null for no restraint.</param>
        /// <param name="variableAccessFlag">Access constraint for the variable.</param>
        /// <returns>True if any value changes occurred.</returns>
        public static bool DrawVariable(Rect position, string labelName, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            return DrawVariable(position, new GUIContent(labelName), variable, tree, possibleTypes, variableAccessFlag);
        }

        public static void DrawVariable(Rect position, GUIContent label, SerializedProperty property)
        {
            var variable = (VariableBase)property.boxedValue;
            var tree = property.serializedObject.targetObject as BehaviourTreeData;

            if (tree == null)
            {
                // error that tree is missing
                EditorGUI.LabelField(position, label, new GUIContent("Behaviour Tree Data is missing"));
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();


            DrawVariable(position, label, variable, tree);

            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.Update();
                property.boxedValue = variable;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Draw the variable field within a fixed position.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="variable">The variable instance.</param>
        /// <param name="tree">The behaviour tree data associated with the variable.</param>
        /// <param name="possibleTypes">Type constraint, null for no restraint.</param>
        /// <param name="variableAccessFlag">Access constraint for the variable.</param>
        /// <returns>True if any value changes occurred.</returns>
        public static bool DrawVariable(Rect position, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            possibleTypes ??= (VariableType[])Enum.GetValues(typeof(VariableType));
            Rect row = GetRowRect(position);

            if (variable.GetType().IsGenericType && variable.GetType().GetGenericTypeDefinition() == typeof(VariableReference<>))
                return DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: false);
            if (variable.GetType() == typeof(VariableReference))
                return DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: false);
            if (!variable.IsConstant)
                return DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: true);

            return DrawVariableConstant(row, label, variable, tree, possibleTypes);
        }

        /// <summary>
        /// Draw the variable field
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="tree">the behaviour tree data associate with</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public static bool DrawVariable(string labelName, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            return DrawVariable(new GUIContent(labelName), variable, tree, possibleTypes, variableAccessFlag);
        }

        /// <summary>
        /// Draw the variable field
        /// </summary>
        /// <param name="label">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="tree">the behaviour tree data associate with</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public static bool DrawVariable(GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            float height = GetVariableHeight(variable, tree, possibleTypes, variableAccessFlag);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            return DrawVariable(rect, label, variable, tree, possibleTypes, variableAccessFlag);
        }





        /// <summary>
        /// Draw constant variable field
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static bool DrawVariableConstant(Rect row, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes)
        {
            bool isChanged = false;
            List<VariableData> allVariable = GetAllVariable(tree);

            Rect contentRect = row;
            Rect enumRect = Rect.zero;

            if (variable is VariableField vf && vf is not Parameter && vf.IsConstant)
            {
                if (!CanDisplay(vf.Type)) vf.ForceSetConstantType(possibleTypes.FirstOrDefault());
                enumRect = ReserveRight(ref contentRect, EnumPopupWidth);
            }

            string actionLabel = allVariable.Any(f => possibleTypes.Any(p => p == f.Type)) ? "Use Variable" : "Create Variable";
            Rect actionRect = ReserveRight(ref contentRect, ButtonWidth);

            switch (variable.Type)
            {
                case VariableType.Int:
                    {
                        int intVal = variable.IntValue;
                        Type type = variable.FieldObjectType;
                        if (type != null && type.IsEnum)
                        {
                            Enum value = (Enum)Enum.Parse(type, intVal.ToString());
                            Enum newValue = Attribute.GetCustomAttribute(value.GetType(), typeof(FlagsAttribute)) == null
                                ? EditorGUI.EnumPopup(contentRect, label, value)
                                : EditorGUI.EnumFlagsField(contentRect, label, value);
                            isChanged = SetConstantIfChange(tree, label.text, variable, intVal, Convert.ToInt32(newValue));
                        }
                        else if (type == typeof(uint))
                        {
                            isChanged = SetConstantIfChange(tree, label.text, variable, intVal, EditorGUI.IntField(contentRect, label, intVal));
                        }
                        else if (type == typeof(LayerMask))
                        {
                            LayerMask oldMask = new() { value = intVal };
                            LayerMask newValue = DrawLayerMask(contentRect, label, oldMask);
                            isChanged = SetConstantIfChange(tree, label.text, variable, intVal, newValue.value);
                        }
                        else
                        {
                            isChanged = SetConstantIfChange(tree, label.text, variable, intVal, EditorGUI.IntField(contentRect, label, intVal));
                        }
                        break;
                    }
                case VariableType.String:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.StringValue, EditorGUI.TextField(contentRect, label, variable.StringValue));
                    break;
                case VariableType.Float:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.FloatValue, EditorGUI.FloatField(contentRect, label, variable.FloatValue));
                    break;
                case VariableType.Bool:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.BoolValue, EditorGUI.Toggle(contentRect, label, variable.BoolValue));
                    break;
                case VariableType.Vector2:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.Vector2Value, EditorGUI.Vector2Field(contentRect, label, variable.Vector2Value));
                    break;
                case VariableType.Vector3:
                    isChanged = SetConstantIfChange(tree, label.text, variable, variable.Vector3Value, EditorGUI.Vector3Field(contentRect, label, variable.Vector3Value));
                    break;
                case VariableType.Vector4:
                    {
                        Vector4 v4 = variable.Vector4Value;
                        Type type = variable.FieldObjectType;
                        if (type == typeof(Color))
                        {
                            Color oldColor = variable.ColorValue;
                            Color newValue = EditorGUI.ColorField(contentRect, label, oldColor);
                            isChanged = SetConstantIfChange(tree, label.text, variable, v4, (Vector4)newValue);
                        }
                        else
                        {
                            isChanged = SetConstantIfChange(tree, label.text, variable, v4, EditorGUI.Vector4Field(contentRect, label, v4));
                        }
                        break;
                    }
                case VariableType.UnityObject:
                    {
                        var asset = variable.UnityObjectValue;
                        if (!asset && variable.ConstanUnityObjectUUID != UUID.Empty)
                        {
                            asset = AssetReferenceData.GetAsset(variable.ConstanUnityObjectUUID);
                        }
                        tree.AddAsset(asset, true);
                        tree.RemoveAsset(variable.ConstanUnityObjectUUID);

                        UnityEngine.Object newAsset = EditorGUI.ObjectField(contentRect, label, asset, variable.FieldObjectType, false);
                        if (SetConstantIfChange(tree, label.text, variable, asset, newAsset))
                        {
                            isChanged = true;
                            tree.AddAsset(newAsset, true);
                            tree.RemoveAsset(asset);
                        }
                        break;
                    }
                default:
                    EditorGUI.LabelField(contentRect, label, new GUIContent($"Cannot set a constant value for {variable.Type}"));
                    break;
            }

            if (enumRect != Rect.zero)
            {
                VariableField vf2 = (VariableField)variable;
                vf2.ForceSetConstantType((VariableType)EditorGUI.EnumPopup(enumRect, GUIContent.none, vf2.Type, CanDisplay, false));
                isChanged = true;
            }

            if (GUI.Button(actionRect, actionLabel))
            {
                var validFields = allVariable.Where(f => possibleTypes.Any(p => p == f.Type)).ToList();
                if (validFields.Count > 0)
                {
                    isChanged = SetVariableIfChange(tree, label.text, variable, validFields[0]);
                }
                else
                {
                    CreateVariable(tree, variable);
                    isChanged = true;
                }
            }

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
        private static bool DrawVariableSelection(Rect row, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, bool allowConvertToConstant)
        {
            bool isChanged = false;
            Rect contentRect = row;

            Rect actionRect = Rect.zero;
            if (allowConvertToConstant)
            {
                actionRect = ReserveRight(ref contentRect, ButtonWidth);
            }

            List<VariableData> allVariable = GetAllVariable(tree);
            var (rawList, nameList) = GetVariables(variable, tree, possibleTypes, variableAccessFlag, allVariable);

            if (rawList.Length < 2)
            {
                EditorGUI.LabelField(contentRect, label, new GUIContent("No valid variable found"));
                Rect buttonRect = ReserveRight(ref contentRect, SmallButtonWidth);
                if (GUI.Button(buttonRect, "Create New"))
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
                if (selectedIndex < 0)
                {
                    if (!variable.HasEditorReference)
                    {
                        EditorGUI.LabelField(contentRect, label, new GUIContent("No Variable"));
                        Rect buttonRect = ReserveRight(ref contentRect, SmallButtonWidth);
                        if (GUI.Button(buttonRect, "Create"))
                        {
                            CreateVariable(tree, variable);
                            isChanged = true;
                        }
                    }
                    else
                    {
                        EditorGUI.LabelField(contentRect, label, new GUIContent($"Variable {variableName} not found"));
                        Rect rightRect = ReserveRight(ref contentRect, (SmallButtonWidth * 2f) + FieldSpacing);
                        Rect recreateRect = new Rect(rightRect.x, rightRect.y, SmallButtonWidth, rightRect.height);
                        Rect clearRect = new Rect(recreateRect.xMax + FieldSpacing, recreateRect.y, SmallButtonWidth, recreateRect.height);

                        if (GUI.Button(recreateRect, "Recreate"))
                        {
                            CreateVariable(tree, variable, variableName);
                            isChanged = true;
                        }

                        if (GUI.Button(clearRect, "Clear"))
                        {
                            isChanged = SetVariableIfChange(tree, label.text, variable, null);
                        }
                    }
                }
                else
                {
                    //int currentIndex = EditorGUI.Popup(contentRect, label, selectedIndex, nameList);
                    int currentIndex = EditorGUI.Popup(contentRect, label, selectedIndex, nameList.Select(n => new GUIContent(n)).ToArray(), EditorStyles.popup);
                    if (currentIndex >= 0)
                    {
                        if (selectedIndex == 0)
                        {
                            isChanged = SetVariableIfChange(tree, label.text, variable, null);
                        }
                        if (currentIndex != rawList.Length - 1)
                        {
                            string varName = rawList[currentIndex];
                            VariableData a = allVariable.Find(v => v.name == varName);
                            isChanged = SetVariableIfChange(tree, label.text, variable, a);
                        }
                        else
                        {
                            VariableType variableType = possibleTypes.FirstOrDefault();
                            CreateVariable(tree, variable, variableType);
                            isChanged = true;
                        }
                    }
                }
            }

            if (allowConvertToConstant && GUI.Button(actionRect, "Set Constant"))
            {
                isChanged = SetVariableIfChange(tree, label.text, variable, null);
            }

            return isChanged;
        }



        #region Save

        private static (string[] rawList, string[] nameList) GetVariables(VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, List<VariableData> allVariable)
        {
            IEnumerable<VariableData> vars = allVariable.Where(Filter);

            var rawList = vars.Select(v => v.name).Append("Create New...").Prepend(NONE_VARIABLE_NAME).ToArray();
            var nameList = vars.Select(v => tree.GetVariableDescName(v)).Append("Create New...").Prepend(NONE_VARIABLE_NAME).ToArray();
            return (rawList, nameList);

            bool Filter(VariableData variableData)
            {
                if (!variable.IsGeneric && variableData.Type != variable.Type) return false;
                if (Array.IndexOf(possibleTypes, variableData.Type) == -1) return false;
                // check read/write permission is possible
                if (variableData.IsScript && tree.targetScript)
                {
                    if ((variableAccessFlag & VariableAccessFlag.Read) != 0)
                        if (variableData.IsReadable(tree.targetScript.GetClass()) == false) return false;
                    if ((variableAccessFlag & VariableAccessFlag.Write) != 0)
                        if (variableData.IsWritable(tree.targetScript.GetClass()) == false) return false;
                }

                return true;
            }
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

        #endregion




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

        /// <summary>
        /// Get the height required to draw the variable field with fixed positioning.
        /// </summary>
        /// <param name="variable">The variable instance.</param>
        /// <param name="tree">The behaviour tree data associated with the variable.</param>
        /// <param name="possibleTypes">Type constraint, null for no restraint.</param>
        /// <param name="variableAccessFlag">Access constraint for the variable.</param>
        /// <returns>The required height for drawing the field.</returns>
        public static float GetVariableHeight(VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}

