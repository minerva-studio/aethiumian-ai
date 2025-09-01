using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Variables.VariableUtility;

namespace Amlos.AI.Editor
{
    internal class VariableTableModule : AIEditorWindowModule
    {
        const float DARK_LINE = 80f / 255f;
        const float Normal_LINE = 64f / 255f;

        enum WindowType
        {
            Local,
            Global,
        }

        private WindowType windowType;
        private TypeReferenceDrawer typeDrawer;
        private VariableData selectedVariableData;
        private bool tableDrawDetail;
        private GUILayoutOption GUIVariableEntryWidth;
        private GUILayoutOption GUIVariableEntryMinWidth;

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
            windowType = (WindowType)
                GUILayout.Toolbar(
                    (int)windowType,
                    new string[] { "Local", "Global" },
                    GUILayout.MinHeight(30)
                );
            var state = GUI.enabled;
            switch (windowType)
            {
                case WindowType.Local:
                    if (!Tree)
                        DrawNewBTWindow();
                    else
                        DrawVariableTable(Tree.variables);
                    break;
                case WindowType.Global:
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
            GUIVariableEntryWidth = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth);
            GUIVariableEntryMinWidth = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth);
            using (new GUILayout.VerticalScope())
            {
                DrawVariableTableHeader(GUIVariableEntryWidth, GUIVariableEntryMinWidth);
                if (variables.Count == 0)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("No local variable exist");
                    EditorGUI.indentLevel--;
                }
                else
                {
                    void DrawHeader(VariableData item)
                    {
                        if (GUILayout.Button("x", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight)))
                        {
                            Tree.RemoveVariable(item.UUID);
                        }
                    }
                    VariableData[] selected = variables.Where(v => !v.IsFromAttribute).ToArray();
                    DrawVariableTable_DrawList(selected, DrawHeader);

                }

                // from attributes
                if (windowType != WindowType.Global)
                {
                    List<VariableData> attributeVariables = VariableData.GetAttributeVariablesFromType(Tree.targetScript.GetClass());
                    if (attributeVariables.Count > 0)
                    {
                        EditorGUILayout.LabelField("From attributes");
                        void DrawHeader(VariableData item)
                        {
                            var isEnabled = variables.FirstOrDefault(v => v.UUID == item.UUID) != null;
                            var isNowEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight));
                            if (isEnabled != isNowEnabled)
                            {
                                if (!isNowEnabled)
                                    Tree.RemoveVariable(item.UUID);
                                else
                                    Tree.AddVariable(item);
                            }
                        }
                        DrawVariableTable_DrawList(attributeVariables, DrawHeader);
                    }
                }
            }


            if (GUILayout.Button("Add"))
                variables.Add(new VariableData(Tree.GenerateNewVariableName("newVar")));
            if (variables.Count > 0 && GUILayout.Button("Remove"))
                variables.RemoveAt(variables.Count - 1);
            GUILayout.FlexibleSpace();
            GUILayout.Space(50);
        }

        private void DrawVariableTableHeader(GUILayoutOption width, GUILayoutOption minWidth)
        {
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(EditorSetting.variableTableEntryWidth * 3);
            GUIContent content;
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("", GUILayout.MaxWidth(EditorGUIUtility.singleLineHeight));
                content = new() { text = "Info", tooltip = "The Info of the variable" };
                GUILayout.Label(content, minWidth, width);
                content = new() { text = "Name", tooltip = "The Name of the variable" };
                GUILayout.Label(content, minWidth, width);
                content = new() { text = "Type", tooltip = "The Type of the variable" };
                GUILayout.Label(content, minWidth, width);
                content = new() { text = "Default", tooltip = "The default value of the variable" };
                EditorGUILayout.LabelField(content, doubleWidth);
                if (windowType == WindowType.Local)
                {
                    content = new()
                    {
                        text = "Static",
                        tooltip = "A static variable share in all instance of this behaviour tree"
                    };
                    GUILayout.Label(content, minWidth, width);
                }
            }
        }

        private void DrawVariableTable_DrawList(IReadOnlyList<VariableData> variables, Action<VariableData> action)
        {
            for (int index = 0; index < variables.Count; index++)
            {
                VariableData item = variables[index];
                Color color = index % 2 == 0 ? Color.white * DARK_LINE : Color.white * Normal_LINE;
                var style = EditorFieldDrawers.SetRegionColor(color, out color);
                using (new GUILayout.HorizontalScope(style))
                {
                    GUI.backgroundColor = color;
                    item.IsGlobal = windowType == WindowType.Global;
                    action?.Invoke(item);
                    DrawVariableEntry(item);
                }
            }
        }

        private void DrawVariableEntry(VariableData item)
        {
            GUILayoutOption width = GUIVariableEntryWidth;
            GUILayoutOption minWidth = GUIVariableEntryMinWidth;

            if (GUILayout.Button(item.Type + ": " + item.name, minWidth, width))
            {
                tableDrawDetail = true;
                selectedVariableData = item;
            }

            item.name = GUILayout.TextField(item.name, minWidth, width);
            var typeValue = item.IsGlobal ? (item.Type) : (GetVariableType(item, Tree.targetScript ? Tree.targetScript.GetClass() : null) ?? VariableType.Invalid);
            using (GUIEnable.By(!item.IsScript))
                item.SetType((VariableType)EditorGUILayout.EnumPopup(typeValue, minWidth, width));

            DrawDefaultValue(item);
            if (windowType == WindowType.Local)
                if (!item.IsScript)
                    item.IsStatic = EditorGUILayout.Toggle(item.IsStatic, minWidth, width);
                else
                    EditorGUILayout.LabelField("-", minWidth, width);
        }

        private void DrawDefaultValue(VariableData item)
        {
            GUILayoutOption minWidth = GUILayout.MaxWidth(
                EditorSetting.variableTableEntryWidth * 3
            );
            GUILayoutOption doubleWidth = GUILayout.MaxWidth(
                EditorSetting.variableTableEntryWidth * 3
            );
            if (item.IsScript)
            {
                GUILayout.Label("(From Script)", doubleWidth, minWidth);
                return;
            }
            bool i;
            switch (item.Type)
            {
                case VariableType.String:
                    item.DefaultValue = GUILayout.TextField(
                        item.DefaultValue,
                        doubleWidth,
                        minWidth
                    );
                    break;
                case VariableType.Int:

                    {
                        i = int.TryParse(item.DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out int val);
                        if (!i)
                        {
                            val = 0;
                        }
                        item.DefaultValue = EditorGUILayout
                            .IntField(val, doubleWidth, minWidth)
                            .ToString();
                    }
                    break;
                case VariableType.Float:

                    {
                        i = float.TryParse(item.DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float val);
                        if (!i)
                        {
                            val = 0;
                        }
                        item.DefaultValue = EditorGUILayout
                            .FloatField(val, doubleWidth, minWidth)
                            .ToString();
                    }
                    break;
                case VariableType.Bool:

                    {
                        i = bool.TryParse(item.DefaultValue, out bool val);
                        if (!i)
                        {
                            val = false;
                        }
                        item.DefaultValue = EditorGUILayout
                            .Toggle(val, doubleWidth, minWidth)
                            .ToString();
                    }
                    break;
                case VariableType.Vector2:

                    {
                        i = VectorUtility.TryParseVector2(item.DefaultValue, out Vector2 val);
                        if (!i)
                        {
                            val = default;
                        }
                        item.DefaultValue = EditorGUILayout
                            .Vector2Field("", val, doubleWidth, minWidth)
                            .ToString();
                    }
                    break;
                case VariableType.Vector3:

                    {
                        i = VectorUtility.TryParseVector3(item.DefaultValue, out Vector3 val);
                        if (!i)
                        {
                            val = default;
                        }
                        item.DefaultValue = EditorGUILayout
                            .Vector3Field("", val, doubleWidth, minWidth)
                            .ToString();
                    }
                    break;
                case VariableType.Invalid:
                    GUILayout.Label("Invalid Variable Type", doubleWidth, minWidth);
                    break;
                case VariableType.UnityObject:
                    if (item.ObjectType is null)
                        item.SetBaseType(typeof(UnityEngine.Object));
                    GUILayout.Label(item.ObjectType.FullName, doubleWidth, minWidth);
                    break;
                case VariableType.Generic:
                    if (item.ObjectType is null)
                        item.SetBaseType(typeof(object));
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

            var oldName = vd.name;
            vd.name = EditorGUILayout.DelayedTextField("Name", vd.name);
            if (oldName != vd.name) Undo.RecordObject(Tree, "Change variable name");

            using (GUIEnable.By(!vd.IsFromAttribute))
            {
                var isFromScript = vd.IsScript;
                vd.SetScript(EditorGUILayout.Toggle("From Script", vd.IsScript));
                if (isFromScript != vd.IsScript) Undo.RecordObject(Tree, "Set variable from script");
            }

            if (vd.IsScript)
            {
                DrawScriptVariable(vd);
            }
            else
            {
                DrawAIVariable(vd);
            }
            GUILayout.Space(50);
            if (GUILayout.Button("Return", GUILayout.MaxHeight(30), GUILayout.MaxWidth(100)))
            {
                tableDrawDetail = false;
            }
        }

        private void DrawScriptVariable(VariableData vd)
        {
            try
            {
                var targetClass = Tree.targetScript ? Tree.targetScript.GetClass() : null;
                using (GUIEnable.By(!vd.IsFromAttribute))
                {
                    var oldPath = vd.Path;
                    if (targetClass == null)
                    {
                        vd.Path = EditorGUILayout.DelayedTextField("Path", vd.Path);
                    }
                    else
                    {
                        var members = targetClass.GetMembers();
                        var options = members.Concat(targetClass.GetProperties()).Where(m => !Attribute.IsDefined(m, typeof(ObsoleteAttribute))).Where(
                               s => s switch
                               {
                                   FieldInfo or PropertyInfo => true,
                                   MethodInfo m => m.GetParameters().Length == 0
                                   && !m.ContainsGenericParameters
                                   && !m.Name.StartsWith("get_")
                                   && !m.Name.StartsWith("set_")
                                   && m.ReturnType != typeof(void),
                                   _ => false,
                               }
                        ).Select(s => s.Name).Distinct().ToArray();
                        Array.Sort(options);
                        int idx = Array.IndexOf(options, vd.Path);
                        idx = EditorGUILayout.IntPopup("Path", idx, options, System.Linq.Enumerable.Range(0, options.Length).ToArray());
                        if (idx >= 0)
                        {
                            vd.Path = options[idx];
                        }
                    }
                    if (oldPath != vd.Path)
                    {
                        Undo.RecordObject(Tree, "Set Path on variable " + vd.name);
                    }
                }
                using (GUIEnable.By(false))
                {
                    if (targetClass != null)
                    {
                        MemberInfo[] memberInfos = targetClass.GetMember(vd.Path);
                        if (memberInfos.Length > 0)
                        {
                            MemberInfo memberInfo = memberInfos[0];
                            var memberResultType = GetResultType(memberInfo);
                            VariableType selected = GetVariableType(memberResultType);
                            EditorGUILayout.EnumPopup("Type", selected);
                            if (vd.Type == VariableType.Generic || vd.Type == VariableType.UnityObject)
                            {
                                EditorGUILayout.LabelField("Object Type", memberResultType.FullName);
                            }
                            EditorGUILayout.Space(20);
                            EditorGUILayout.LabelField("Properties", EditorStyles.boldLabel);

                            EditorGUILayout.Toggle("Read", CanRead(memberInfo));
                            EditorGUILayout.Toggle("Write", CanWrite(memberInfo));
                            EditorGUILayout.Toggle(new GUIContent("Static", "Whether the value is from a static field/property/methods"), IsStatic(memberInfo));
                        }
                        else EditorGUILayout.LabelField("Type", "Unknown (member not found)");
                    }
                    else EditorGUILayout.LabelField("Type", "Unknown (target unknown)");
                }

            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        private void DrawAIVariable(VariableData vd)
        {
            vd.SetType((VariableType)EditorGUILayout.EnumPopup("Type", vd.Type));

            if (vd.Type == VariableType.Generic)
            {
                vd.TypeReference.SetBaseType(typeof(object));
                typeDrawer ??= new TypeReferenceDrawer(vd.TypeReference, "Type Reference", Tree);
                typeDrawer.Reset(vd.TypeReference, "Type Reference", Tree);
                typeDrawer.Draw();
            }
            else if (vd.Type == VariableType.UnityObject)
            {
                vd.TypeReference.SetBaseType(typeof(UnityEngine.Object));
                typeDrawer ??= new TypeReferenceDrawer(vd.TypeReference, "Type Reference", Tree);
                typeDrawer.Reset(vd.TypeReference, "Type Reference", Tree);
                typeDrawer.Draw();
            }
            else
            {
                EditorGUILayout.LabelField("Default Value:");
                DrawDefaultValue(vd);
            }
        }

        public void Reset()
        {
            selectedVariableData = null;
            tableDrawDetail = false;
        }
    }
}
