using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{

    internal class VariableTableModule : AIEditorWindowModule
    {
        const float DARK_LINE = 80f / 255f;
        const float Normal_LINE = 64f / 255f;

        enum WindowType
        {
            local,
            global,
        }

        private WindowType windowType;
        private TypeReferenceDrawer typeDrawer;
        private VariableData selectedVariableData;
        private bool tableDrawDetail;

        public void DrawVariableTable()
        {
            EditorGUIUtility.wideMode = true;
            if (tableDrawDetail)
            {
                DrawVariableDetail(selectedVariableData);
                return;
            }

            GUILayout.BeginVertical();
            EditorGUILayout.LabelField("Variable Table", EditorStyles.boldLabel);
            windowType = (WindowType)GUILayout.Toolbar((int)windowType, new string[] { "Local", "Global" }, GUILayout.MinHeight(30));
            var state = GUI.enabled;
            switch (windowType)
            {
                case WindowType.local:
                    if (!Tree)
                    {
                        DrawNewBTWindow();
                    }
                    else DrawVariableTable(Tree.variables);
                    break;
                case WindowType.global:
                    EditorUtility.SetDirty(Settings);
                    DrawVariableTable(Settings.globalVariables);
                    GUI.enabled = false;
                    EditorGUILayout.ObjectField("AI File", Settings, typeof(AISetting), false);
                    GUI.enabled = state;
                    break;
                default:
                    break;
            }
            GUILayout.EndVertical();
        }

        private void DrawVariableTable(List<VariableData> variables)
        {
            GUILayoutOption width = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth);
            GUILayoutOption minWidth = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth);
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth * 3);
            if (variables.Count == 0)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("No local variable exist");
                EditorGUI.indentLevel--;
            }
            else
            {
                GUIContent content;
                GUILayout.BeginHorizontal();
                GUILayout.Label("", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight));
                content = new() { text = "Info", tooltip = "The Info of the variable" };
                GUILayout.Label(content, minWidth, width);
                content = new() { text = "Name", tooltip = "The Name of the variable" };
                GUILayout.Label(content, minWidth, width);
                content = new() { text = "Type", tooltip = "The Type of the variable" };
                GUILayout.Label(content, minWidth, width);
                content = new() { text = "Default", tooltip = "The default value of the variable" };
                EditorGUILayout.LabelField(content, doubleWidth);
                if (windowType == WindowType.local)
                {
                    content = new() { text = "Static", tooltip = "A static variable share in all instance of this behaviour tree" };
                    GUILayout.Label(content, minWidth, width);
                }

                GUILayout.EndHorizontal();

                Color color;
                for (int index = 0; index < variables.Count; index++)
                {
                    color = index % 2 == 0 ? Color.white * DARK_LINE : Color.white * Normal_LINE;
                    var style = EditorFieldDrawers.SetRegionColor(color, out color);
                    GUILayout.BeginHorizontal(style);
                    GUI.backgroundColor = color;

                    VariableData item = variables[index];
                    item.isGlobal = windowType == WindowType.global;
                    if (GUILayout.Button("x", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight)))
                    {
                        variables.RemoveAt(index);
                        index--;
                        GUILayout.EndHorizontal();
                        continue;
                    }
                    if (GUILayout.Button(item.Type + ": " + item.name, minWidth, width))
                    {
                        tableDrawDetail = true;
                        selectedVariableData = item;
                    }

                    item.name = GUILayout.TextField(item.name, minWidth, width);
                    item.SetType((VariableType)EditorGUILayout.EnumPopup(item.Type, minWidth, width));
                    DrawDefaultValue(item);
                    if (windowType == WindowType.local) item.isStatic = EditorGUILayout.Toggle(item.isStatic, minWidth, width);

                    //GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add")) variables.Add(new VariableData(Tree.GenerateNewVariableName("newVar")));
            if (variables.Count > 0 && GUILayout.Button("Remove")) variables.RemoveAt(variables.Count - 1);
            GUILayout.Space(50);
        }

        private void DrawDefaultValue(VariableData item)
        {
            GUILayoutOption minWidth = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth * 3);
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth * 3);
            bool i;
            switch (item.Type)
            {
                case VariableType.String:
                    item.defaultValue = GUILayout.TextField(item.defaultValue, doubleWidth, minWidth);
                    break;
                case VariableType.Int:
                    {
                        i = int.TryParse(item.defaultValue, out int val);
                        if (!i) { val = 0; }
                        item.defaultValue = EditorGUILayout.IntField(val, doubleWidth, minWidth).ToString();
                    }
                    break;
                case VariableType.Float:
                    {
                        i = float.TryParse(item.defaultValue, out float val);
                        if (!i) { val = 0; }
                        item.defaultValue = EditorGUILayout.FloatField(val, doubleWidth, minWidth).ToString();
                    }
                    break;
                case VariableType.Bool:
                    {
                        i = bool.TryParse(item.defaultValue, out bool val);
                        if (!i) { val = false; }
                        item.defaultValue = EditorGUILayout.Toggle(val, doubleWidth, minWidth).ToString();
                    }
                    break;
                case VariableType.Vector2:
                    {
                        i = VectorUtilities.TryParseVector2(item.defaultValue, out Vector2 val);
                        if (!i) { val = default; }
                        item.defaultValue = EditorGUILayout.Vector2Field("", val, doubleWidth, minWidth).ToString();
                    }
                    break;
                case VariableType.Vector3:
                    {
                        i = VectorUtilities.TryParseVector3(item.defaultValue, out Vector3 val);
                        if (!i) { val = default; }
                        item.defaultValue = EditorGUILayout.Vector3Field("", val, doubleWidth, minWidth).ToString();
                    }
                    break;
                case VariableType.Invalid:
                    GUILayout.Label("Invalid Variable Type", doubleWidth, minWidth);
                    break;
                case VariableType.UnityObject:
                    if (item.ObjectType is null) item.UpdateTypeReference(typeof(Object));
                    GUILayout.Label(item.ObjectType.FullName, doubleWidth, minWidth);
                    break;
                case VariableType.Generic:
                    if (item.ObjectType is null) item.UpdateTypeReference(typeof(object));
                    GUILayout.Label(item.ObjectType.FullName, doubleWidth, minWidth);
                    break;
                default:
                    GUILayout.Label($" ", doubleWidth, minWidth);
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
                vd.TypeReference.SetBaseType(typeof(object));
                typeDrawer ??= new TypeReferenceDrawer(vd.TypeReference, "Type Reference");
                typeDrawer.Reset(vd.TypeReference, "Type Reference");
                typeDrawer.Draw();
            }
            else if (vd.Type == VariableType.UnityObject)
            {
                vd.TypeReference.SetBaseType(typeof(UnityEngine.Object));
                typeDrawer ??= new TypeReferenceDrawer(vd.TypeReference, "Type Reference");
                typeDrawer.Reset(vd.TypeReference, "Type Reference");
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