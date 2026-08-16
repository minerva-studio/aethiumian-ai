using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableData;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Drawer of variables
    /// <br/>
    /// Author : Wendell Cai
    /// </summary>
    public static class VariableFieldDrawers
    {
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

        internal readonly struct VariableRowLayout
        {
            internal Rect ContentRect { get; }
            internal Rect OverflowRect { get; }
            internal bool HasOverflow { get; }

            internal VariableRowLayout(Rect contentRect, Rect overflowRect, bool hasOverflow)
            {
                ContentRect = contentRect;
                OverflowRect = overflowRect;
                HasOverflow = hasOverflow;
            }
        }

        /// <summary>Calculates a variable row without reserving space when no action is executable.</summary>
        internal static VariableRowLayout CalculateRowLayout(Rect position, bool hasAction)
        {
            Rect content = position;
            if (!hasAction)
                return new VariableRowLayout(content, Rect.zero, false);

            float overflowWidth = Mathf.Min(GraphInspectorLayout.OverflowWidth, Mathf.Max(0f, content.width));
            Rect overflow = new(content.xMax - overflowWidth, content.y, overflowWidth, content.height);
            content.width = Mathf.Max(0f, content.width - overflowWidth - FieldSpacing);
            return new VariableRowLayout(content, overflow, true);
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
        public static bool DrawVariable(Rect position, GUIContent label, SerializedProperty property)
            => DrawVariable(position, label, property, null, null);

        /// <summary>
        /// Draw the variable field within a fixed position with explicit variable constraints.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="property">The serialized property representing the variable.</param>
        /// <param name="possibleTypes">Allowed variable types, or null to resolve from the field metadata.</param>
        /// <param name="variableAccessFlag">Access constraint, or null to resolve from the field metadata.</param>
        /// <returns>True if the property value changes.</returns>
        public static bool DrawVariable(Rect position, GUIContent label, SerializedProperty property, VariableType[] possibleTypes, VariableAccessFlag? variableAccessFlag)
        {
            if (property == null)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Variable property is missing"));
                return false;
            }

            if (property.serializedObject.targetObject is not BehaviourTreeData tree || tree == null)
            {
                // error that tree is missing
                EditorGUI.LabelField(position, label, new GUIContent("Behaviour Tree Data is missing"));
                return false;
            }
            if (property.boxedValue is not VariableBase variable)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Variable field is missing"));
                return false;
            }

            // from member info, try get contraint
            var memberInfo = property.GetAIMemberInfo();
            VariableType[] resolvedTypes = possibleTypes;
            VariableAccessFlag resolvedAccessFlag = variableAccessFlag ?? VariableAccessFlag.All;
            if (memberInfo != null)
            {
                resolvedTypes ??= variable.GetVariableTypes(memberInfo);
                if (variableAccessFlag == null)
                {
                    resolvedAccessFlag = variable.GetAccessFlag(memberInfo);
                }
            }

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();
            DrawVariable(position, label, variable, tree, resolvedTypes, resolvedAccessFlag, property);
            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.Update();
                property.boxedValue = variable;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                EditorGUI.EndProperty();
                return true;
            }
            EditorGUI.EndProperty();
            return false;
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
        /// <param name="sourceProperty">Optional serialized property used to safely resolve menu mutations later.</param>
        /// <returns>True if any value changes occurred.</returns>
        public static void DrawVariable(Rect position, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None, SerializedProperty sourceProperty = null)
        {
            possibleTypes ??= ALL_VARIABLES;
            Rect row = GetRowRect(position);

            Type type = variable.GetType();
            if ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(VariableReference<>)) || type == typeof(VariableReference))
                DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: false, sourceProperty);
            else if (!variable.IsConstant)
                DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: true, sourceProperty);
            else
                DrawVariableConstant(row, label, variable, tree, possibleTypes, sourceProperty);
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
        private static void DrawVariableConstant(Rect row, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, SerializedProperty sourceProperty)
        {
            List<VariableData> allVariable = GetAllVariable(tree);
            var validFields = allVariable.Where(f => possibleTypes.Any(p => p == f.Type)).ToList();
            IEnumerable<VariableType> constantTypes = possibleTypes.Contains(VariableType.Generic) ? ALL_VARIABLES : possibleTypes;
            bool hasConstantTypeAction = variable is VariableField fieldForLayout && fieldForLayout is not Parameter && fieldForLayout.IsConstant
                && constantTypes.Any(type => CanDisplay(type));
            bool hasAction = validFields.Count > 0 || !hasConstantTypeAction && possibleTypes.Any(type => type is not VariableType.Generic and not VariableType.Invalid) || hasConstantTypeAction;
            VariableRowLayout layout = CalculateRowLayout(row, hasAction);
            Rect contentRect = layout.ContentRect;
            if (variable is VariableField vf && vf is not Parameter && vf.IsConstant)
            {
                if (!CanDisplay(vf.Type)) vf.ForceSetConstantType(possibleTypes.FirstOrDefault());
            }

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

                        UnityEngine.Object newAsset = EditorGUI.ObjectField(contentRect, label, asset, variable.FieldObjectType, false);
                        variable.ForceSetConstantValue(newAsset);
                        break;
                    }
                default:
                    EditorGUI.LabelField(contentRect, label, new GUIContent($"Cannot set a constant value for {variable.Type}"));
                    break;
            }

            if (layout.HasOverflow && GUI.Button(layout.OverflowRect, "⋮", EditorStyles.miniButton))
            {
                GenericMenu menu = new();
                AddMutation(menu, tree, "Use Variable", sourceProperty, variable, validFields.Count > 0, v => v.SetReference(validFields[0]));
                AddMutation(menu, tree, "Create Variable", sourceProperty, variable, validFields.Count == 0, v => CreateVariable(tree, v));
                if (variable is VariableField field && field is not Parameter && field.IsConstant)
                {
                    foreach (VariableType candidate in constantTypes.Where(candidate => CanDisplay(candidate)))
                    {
                        VariableType type = candidate;
                        AddMutation(menu, tree, $"Constant Type/{type}", sourceProperty, variable, true, v => ((VariableField)v).ForceSetConstantType(type));
                    }
                }
                menu.ShowAsContext();
            }

            bool CanDisplay(Enum val)
            {
                return (Array.IndexOf(possibleTypes, val) != -1 || possibleTypes.Contains(VariableType.Generic))
                    && (val is not VariableType.Generic and not VariableType.Invalid);
            }
        }

        private static void DrawVariableSelection(Rect row, GUIContent label, VariableBase variable, BehaviourTreeData tree, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, bool allowConvertToConstant, SerializedProperty sourceProperty)
        {
            List<VariableData> allVariable = GetAllVariable(tree);
            var rawList = GetRawVariables(variable, tree, possibleTypes, variableAccessFlag, allVariable);
            string variableName = allVariable.Find(v => v.UUID == variable.UUID)?.name ?? string.Empty;
            bool referenceNameIsMissing = variable.HasEditorReference && allVariable.Find(v => v.UUID == variable.UUID) == null;
            bool hasValidVariable = rawList.Skip(1).Any(name => name != "Create New...");
            bool hasInvalidReference = referenceNameIsMissing;
            bool hasAction = (allowConvertToConstant && variable.HasEditorReference) || !hasValidVariable || hasInvalidReference;
            VariableRowLayout layout = CalculateRowLayout(row, hasAction);
            Rect contentRect = layout.ContentRect;

            if (rawList.Length < 2)
            {
                EditorGUI.LabelField(contentRect, label, new GUIContent("No valid variable found"));
            }
            else
            {
                var selectedVariable = allVariable.Find(v => v.UUID == variable.UUID);
                variableName = selectedVariable?.name ?? string.Empty;
                if (string.IsNullOrEmpty(variableName) || variableName == NONE_VARIABLE_NAME)
                {
                    variableName = rawList[0];
                }
                else if (Array.IndexOf(rawList, variableName) == -1)
                {
                    variableName = rawList[0];
                }

                int selectedIndex = Array.IndexOf(rawList, variableName);
                if (referenceNameIsMissing) selectedIndex = -1;
                if (selectedIndex < 0)
                {
                    if (!variable.HasEditorReference)
                    {
                        EditorGUI.LabelField(contentRect, label, new GUIContent("No Variable"));
                    }
                    else
                    {
                        EditorGUI.LabelField(contentRect, label, new GUIContent($"Missing Variable ({variable.UUID})"));
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

            if (layout.HasOverflow && GUI.Button(layout.OverflowRect, "⋮", EditorStyles.miniButton))
            {
                GenericMenu menu = new();
                if (allowConvertToConstant && variable.HasEditorReference)
                    AddMutation(menu, tree, "Set Constant", sourceProperty, variable, true, v => v.SetReference(null));
                if (!hasValidVariable && !hasInvalidReference)
                    AddMutation(menu, tree, "Create Variable", sourceProperty, variable, true, v => CreateVariable(tree, v));
                if (hasInvalidReference)
                {
                    AddMutation(menu, tree, "Recreate", sourceProperty, variable, true, v => CreateVariable(tree, v));
                    AddMutation(menu, tree, "Clear", sourceProperty, variable, true, v => v.SetReference(null));
                }
                menu.ShowAsContext();
            }
        }

        /// <summary>Registers a menu mutation with a fresh serialized-property lookup.</summary>
        private static void AddMutation(GenericMenu menu, BehaviourTreeData tree, string path, SerializedProperty source, VariableBase fallback, bool enabled, Action<VariableBase> mutation)
        {
            if (!enabled) { menu.AddDisabledItem(new GUIContent(path)); return; }
            UnityEngine.Object target = source?.serializedObject.targetObject;
            string propertyPath = source?.propertyPath;
            menu.AddItem(new GUIContent(path), false, () => ApplyMutation(tree, target, propertyPath, fallback, mutation));
        }

        /// <summary>Applies one menu mutation in a single undo and dirty transaction.</summary>
        private static void ApplyMutation(BehaviourTreeData tree, UnityEngine.Object target, string propertyPath, VariableBase fallback, Action<VariableBase> mutation)
        {
            UnityEngine.Object undoTarget = target != null ? target : tree;
            if (undoTarget == null) return;
            if (target == null || string.IsNullOrEmpty(propertyPath))
            {
                Undo.RecordObject(undoTarget, "Edit Variable");
                mutation(fallback);
                EditorUtility.SetDirty(undoTarget);
                return;
            }
            SerializedObject serializedObject = new(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property?.boxedValue is not VariableBase current) return;
            Undo.RecordObject(target, "Edit Variable");
            if (tree != null && tree != target) Undo.RecordObject(tree, "Edit Variable");
            mutation(current);
            property.boxedValue = current;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            if (tree != null && tree != target) EditorUtility.SetDirty(tree);
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

