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
        private static readonly VariableType[] ALL_VARIABLES = (VariableType[])Enum.GetValues(typeof(VariableType));

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
        /// <param name="label">Label of the field.</param>
        /// <param name="property">The serialized property representing the variable.</param>
        /// <returns>True if any value changes occurred.</returns> 
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

            // from member info, try get contraint
            var memberInfo = property.GetMemberInfo();
            VariableType[] possibleTypes = null;
            VariableAccessFlag variableAccessFlag = VariableAccessFlag.All;
            if (memberInfo != null)
            {
                possibleTypes = variable.GetVariableTypes(memberInfo);
                variableAccessFlag = variable.GetAccessFlag(memberInfo);
            }

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();
            DrawVariable(position, label, variable, tree, possibleTypes, variableAccessFlag);
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
        public static void DrawVariable(Rect position, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            possibleTypes ??= ALL_VARIABLES;
            Rect row = GetRowRect(position);

            Type type = variable.GetType();
            if ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(VariableReference<>)) || type == typeof(VariableReference))
                DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: false);
            else if (!variable.IsConstant)
                DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: true);
            else
                DrawVariableConstant(row, label, variable, tree, possibleTypes);
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

            EditorGUI.BeginChangeCheck();
            DrawVariable(rect, label, variable, tree, possibleTypes, variableAccessFlag);
            return EditorGUI.EndChangeCheck();
        }





        /// <summary>
        /// Draw constant variable field
        /// </summary>
        /// <param name="label"></param>
        /// <param name="variable"></param>
        /// <param name="tree"></param>
        /// <param name="possibleTypes"></param>
        private static void DrawVariableConstant(Rect row, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes)
        {
            List<VariableData> allVariable = GetAllVariable(tree);

            Rect contentRect = row;
            Rect enumRect = Rect.zero;

            if (variable is VariableField vf && vf is not Parameter && vf.IsConstant)
            {
                if (!CanDisplay(vf.Type)) vf.ForceSetConstantType(possibleTypes.FirstOrDefault());
                enumRect = ReserveRight(ref contentRect, EnumPopupWidth);
            }

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
                            variable.ForceSetConstantValue(Convert.ToInt32(newValue));
                        }
                        else if (type == typeof(uint))
                        {
                            variable.ForceSetConstantValue(EditorGUI.IntField(contentRect, label, intVal));
                        }
                        else if (type == typeof(LayerMask))
                        {
                            LayerMask oldMask = new() { value = intVal };
                            LayerMask newValue = DrawLayerMask(contentRect, label, oldMask);
                            variable.ForceSetConstantValue(newValue.value);
                        }
                        else
                        {
                            variable.ForceSetConstantValue(EditorGUI.IntField(contentRect, label, intVal));
                        }
                        break;
                    }
                case VariableType.String:
                    variable.ForceSetConstantValue(EditorGUI.TextField(contentRect, label, variable.StringValue));
                    break;
                case VariableType.Float:
                    variable.ForceSetConstantValue(EditorGUI.FloatField(contentRect, label, variable.FloatValue));
                    break;
                case VariableType.Bool:
                    variable.ForceSetConstantValue(EditorGUI.Toggle(contentRect, label, variable.BoolValue));
                    break;
                case VariableType.Vector2:
                    variable.ForceSetConstantValue(EditorGUI.Vector2Field(contentRect, label, variable.Vector2Value));
                    break;
                case VariableType.Vector3:
                    variable.ForceSetConstantValue(EditorGUI.Vector3Field(contentRect, label, variable.Vector3Value));
                    break;
                case VariableType.Vector4:
                    {
                        Vector4 v4 = variable.Vector4Value;
                        Type type = variable.FieldObjectType;
                        if (type == typeof(Color))
                        {
                            Color oldColor = variable.ColorValue;
                            Color newValue = EditorGUI.ColorField(contentRect, label, oldColor);
                            variable.ForceSetConstantValue((Vector4)newValue);
                        }
                        else
                        {
                            variable.ForceSetConstantValue(EditorGUI.Vector4Field(contentRect, label, v4));
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
                        variable.ForceSetConstantValue(newAsset);

                        if (newAsset != asset)
                        {
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
            }

            string actionLabel = allVariable.Any(f => possibleTypes.Any(p => p == f.Type)) ? "Use Variable" : "Create Variable";
            if (GUI.Button(actionRect, actionLabel))
            {
                var validFields = allVariable.Where(f => possibleTypes.Any(p => p == f.Type)).ToList();
                if (validFields.Count > 0)
                {
                    variable.SetReference(validFields[0]);
                }
                else
                {
                    CreateVariable(tree, variable);
                }
            }

            bool CanDisplay(Enum val)
            {
                return Array.IndexOf(possibleTypes, val) != -1 && (val is not VariableType.Generic and not VariableType.Invalid);
            }
        }

        private static void DrawVariableSelection(Rect row, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, bool allowConvertToConstant)
        {
            Rect contentRect = row;

            Rect actionRect = Rect.zero;
            if (allowConvertToConstant)
            {
                actionRect = ReserveRight(ref contentRect, ButtonWidth);
            }

            List<VariableData> allVariable = GetAllVariable(tree);
            var rawList = GetRawVariables(variable, tree, possibleTypes, variableAccessFlag, allVariable);

            if (rawList.Length < 2)
            {
                EditorGUI.LabelField(contentRect, label, new GUIContent("No valid variable found"));
                Rect buttonRect = ReserveRight(ref contentRect, SmallButtonWidth);
                if (GUI.Button(buttonRect, "Create New"))
                {
                    CreateVariable(tree, variable);
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
                        }

                        if (GUI.Button(clearRect, "Clear"))
                        {
                            variable.SetReference(null);
                        }
                    }
                }
                else
                {
                    GUIContent[] nameList = GetVariableOption(variable, tree, possibleTypes, variableAccessFlag, allVariable);
                    int currentIndex = EditorGUI.Popup(contentRect, label, selectedIndex, nameList, EditorStyles.popup);
                    if (currentIndex >= 0)
                    {
                        if (selectedIndex == 0)
                        {
                            variable.SetReference(null);
                        }
                        if (currentIndex != rawList.Length - 1)
                        {
                            string varName = rawList[currentIndex];
                            VariableData a = allVariable.Find(v => v.name == varName);
                            variable.SetReference(a);
                        }
                        else
                        {
                            VariableType variableType = possibleTypes.FirstOrDefault();
                            CreateVariable(tree, variable, variableType);
                        }
                    }
                }
            }

            if (allowConvertToConstant && GUI.Button(actionRect, "Set Constant"))
            {
                variable.SetReference(null);
            }
        }



        #region Save

        private static string[] GetRawVariables(VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, List<VariableData> allVariable)
        {
            IEnumerable<VariableData> vars = allVariable.Where((v) => Filter(v, variable, tree, possibleTypes, variableAccessFlag));

            var rawList = vars.Select(v => v.name).Append("Create New...").Prepend(NONE_VARIABLE_NAME).ToArray();
            return rawList;
        }

        private static GUIContent[] GetVariableOption(VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, List<VariableData> allVariable)
        {
            IEnumerable<VariableData> vars = allVariable.Where((v) => Filter(v, variable, tree, possibleTypes, variableAccessFlag));
            var nameList = vars.Select(v => tree.GetVariableDescName(v)).Append("Create New...").Prepend(NONE_VARIABLE_NAME).Select(o => new GUIContent(o)).ToArray();
            return nameList;
        }


        static bool Filter(VariableData variableData, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag)
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

        private static void CreateVariable(BehaviourTreeData tree, VariableBase variable, string name = null)
        {
            CreateVariable(tree, variable, variable.Type, name);
        }

        private static void CreateVariable(BehaviourTreeData tree, VariableBase variable, VariableType type, string name = null)
        {
            string newVarName = name ?? tree.GenerateNewVariableName(variable.Type.ToString());
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

