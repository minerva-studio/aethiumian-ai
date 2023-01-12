using Minerva.Module;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{

    internal class VariableTableModule : AIEditorWindowModule
    {
        enum WindowType
        {
            local,
            @static,
            global,
        }

        private WindowType windowType;
        private TypeReferenceDrawer typeDrawer;
        private VariableData selectedVariableData;
        private bool tableDrawDetail;

        public void DrawVariableTable()
        {
            if (tableDrawDetail)
            {
                DrawVariableDetail(selectedVariableData);
                return;
            }

            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Variable Table", EditorStyles.boldLabel);
            windowType = (WindowType)GUILayout.Toolbar((int)windowType, new string[] { "Local", "Static", "Global" }, GUILayout.MinHeight(30));
            var state = GUI.enabled;
            switch (windowType)
            {
                case WindowType.local:
                    if (!tree)
                    {
                        DrawNewBTWindow();
                    }
                    else DrawVariableTable(tree.variables);
                    break;
                case WindowType.@static:
                    //DrawVariables();
                    break;
                case WindowType.global:
                    EditorUtility.SetDirty(settings);
                    DrawVariableTable(settings.globalVariables);
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("AI File", settings, typeof(AISetting), false);
                    GUI.enabled = state;
                    break;
                default:
                    break;
            }
            GUILayout.EndVertical();
        }

        private void DrawVariableTable(List<VariableData> variables)
        {
            GUILayoutOption width = GUILayout.MaxWidth(editorSetting.variableTableEntryWidth);
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(editorSetting.variableTableEntryWidth * 3);
            if (variables.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("No local variable exist");
                EditorGUI.indentLevel--;
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.LabelField("Info", width);
                //EditorGUILayout.LabelField("", width);
                GUILayout.Label("Name", width);
                GUILayout.Label("Type", width);
                GUILayout.Label("Default", doubleWidth);
                GUILayout.EndHorizontal();

                for (int index = 0; index < variables.Count; index++)
                {
                    VariableData item = variables[index];
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("x", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight)))
                    {
                        variables.RemoveAt(index);
                        index--;
                        GUILayout.EndHorizontal();
                        continue;
                    }
                    if (GUILayout.Button(item.Type + ": " + item.name, width))
                    {
                        tableDrawDetail = true;
                        selectedVariableData = item;
                    }
                    item.name = GUILayout.TextField(item.name, width);
                    item.SetType((VariableType)EditorGUILayout.EnumPopup(item.Type, width));
                    DrawDefaultValue(item);

                    //GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add")) variables.Add(new VariableData(tree.GenerateNewVariableName("newVar"), defaultValue: "default"));
            if (variables.Count > 0 && GUILayout.Button("Remove")) variables.RemoveAt(variables.Count - 1);
            GUILayout.Space(50);
        }

        private void DrawDefaultValue(VariableData item)
        {
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(editorSetting.variableTableEntryWidth * 3);
            bool i;
            switch (item.Type)
            {
                case VariableType.String:
                    item.defaultValue = GUILayout.TextField(item.defaultValue, doubleWidth);
                    break;
                case VariableType.Int:
                    {
                        i = int.TryParse(item.defaultValue, out int val);
                        if (!i) { val = 0; }
                        item.defaultValue = EditorGUILayout.IntField(val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Float:
                    {
                        i = float.TryParse(item.defaultValue, out float val);
                        if (!i) { val = 0; }
                        item.defaultValue = EditorGUILayout.FloatField(val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Bool:
                    {
                        i = bool.TryParse(item.defaultValue, out bool val);
                        if (!i) { val = false; }
                        item.defaultValue = EditorGUILayout.Toggle(val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Vector2:
                    {
                        i = VectorUtilities.TryParseVector2(item.defaultValue, out Vector2 val);
                        if (!i) { val = default; }
                        item.defaultValue = EditorGUILayout.Vector2Field("", val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Vector3:
                    {
                        i = VectorUtilities.TryParseVector3(item.defaultValue, out Vector3 val);
                        if (!i) { val = default; }
                        item.defaultValue = EditorGUILayout.Vector3Field("", val, doubleWidth).ToString();
                    }
                    break;
                case VariableType.Invalid:
                    GUILayout.Label("Invalid Variable Type");
                    break;
                case VariableType.UnityObject:
                    item.typeReference ??= new TypeReference();
                    if (item.typeReference.BaseType is null) item.typeReference.SetBaseType(typeof(UnityEngine.Object));
                    GUILayout.Label(item.typeReference.classFullName, doubleWidth);
                    break;
                case VariableType.Generic:
                    item.typeReference ??= new TypeReference();
                    if (item.typeReference.BaseType is null) item.typeReference.SetBaseType(typeof(object));
                    GUILayout.Label(item.typeReference.classFullName, doubleWidth);
                    break;
                default:
                    GUILayout.Label($" ");
                    break;
            }
        }

        private void DrawVariableDetail(VariableData vd)
        {
            EditorGUILayout.LabelField(vd.Type + ": " + vd.name);
            vd.name = EditorGUILayout.TextField("Name", vd.name);
            vd.SetType((VariableType)EditorGUILayout.EnumPopup("Type", vd.Type));

            if (vd.Type == VariableType.Generic)
            {
                vd.typeReference ??= new();
                vd.typeReference.SetBaseType(typeof(object));
                typeDrawer ??= new TypeReferenceDrawer(vd.typeReference, "Type Reference");
                typeDrawer.Reset(vd.typeReference, "Type Reference");
                typeDrawer.Draw();
            }
            else if (vd.Type == VariableType.UnityObject)
            {
                vd.typeReference ??= new();
                vd.typeReference.SetBaseType(typeof(UnityEngine.Object));
                typeDrawer ??= new TypeReferenceDrawer(vd.typeReference, "Type Reference");
                typeDrawer.Reset(vd.typeReference, "Type Reference");
                typeDrawer.Draw();
            }
            else
            {
                EditorGUILayout.LabelField("Default Value:"); DrawDefaultValue(vd);
            }
            GUILayout.Space(50);
            if (GUILayout.Button("Return", GUILayout.MaxHeight(30), GUILayout.MaxWidth(100)))
            {
                tableDrawDetail = false;
            }
        }

        public void Reset()
        {
            selectedVariableData = null;
            tableDrawDetail = false;
        }
    }
}