using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static Amlos.AI.Variables.VariableUtility;

namespace Amlos.AI.Editor
{
    internal class VariableTableModule : AIEditorWindowModule
    {
        [Flags]
        enum VariableFilter
        {
            Local = 1,
            Static = 2,
            Global = 4,
        }

        private VariableFilter variableFilter = VariableFilter.Local;
        private TypeReferenceDrawer typeDrawer;
        private VariableData selectedVariableData;
        private bool tableDrawDetail;

        private TreeViewState variableTreeState;
        private VariableTableTreeView variableTreeView;

        public void DrawVariableTable()
        {
            EditorGUIUtility.wideMode = true;
            if (tableDrawDetail)
            {
                DrawVariableDetail(selectedVariableData);
                return;
            }

            using (new GUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Variable Table", EditorStyles.boldLabel);

                bool includeLocal = variableFilter.HasFlag(VariableFilter.Local);
                bool includeStatic = variableFilter.HasFlag(VariableFilter.Static);
                bool includeGlobal = variableFilter.HasFlag(VariableFilter.Global);
                bool needsTree = includeLocal || includeStatic;

                if (needsTree && !tree)
                {
                    DrawNewBTWindow();
                    if (!includeGlobal)
                    {
                        GUILayout.FlexibleSpace();
                        GUILayout.Space(50);
                        return;
                    }
                }

                if (includeGlobal)
                {
                    EditorUtility.SetDirty(Settings);
                }

                DrawVariableTableButtons(tree != null ? tree.variables : null, needsTree);
                GUILayout.Space(-EditorGUIUtility.standardVerticalSpacing);
                DrawVariableTableTree(tree != null ? tree.variables : null, includeGlobal ? Settings.globalVariables : null);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("AI File", Settings, typeof(AISetting), false);
            }
        }

        /// <summary>
        /// Draws the variable table tree for the current filter selection.
        /// </summary>
        /// <param name="localVariables">The list of local/static variables.</param>
        /// <param name="globalVariables">The list of global variables.</param>
        /// <returns>No return value.</returns>
        private void DrawVariableTableTree(List<VariableData> localVariables, List<VariableData> globalVariables)
        {
            EnsureVariableTreeView();

            VariableTableTreeView.Mode mode = ResolveTreeViewMode();
            List<VariableTableTreeView.VariableEntry> entries = BuildVariableEntries(localVariables, globalVariables);
            variableTreeView.SetData(entries, mode);

            int totalRows = entries.Count;
            float height = Mathf.Max(
                100f,
                (totalRows + 2) * (EditorGUIUtility.singleLineHeight + 6f)
            );

            Rect rect = EditorGUILayout.GetControlRect(false, height);
            variableTreeView.OnGUI(rect);
        }

        /// <summary>
        /// Draws the toolbar with filter and editing controls.
        /// </summary>
        /// <param name="variables">The list of editable variables.</param>
        /// <param name="allowEdits">Whether add/remove operations are allowed.</param>
        /// <returns>No return value.</returns>
        private void DrawVariableTableButtons(List<VariableData> variables, bool allowEdits)
        {
            using (new GUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                VariableFilter newFilter = (VariableFilter)EditorGUILayout.EnumFlagsField(variableFilter, EditorStyles.toolbarPopup, GUILayout.Width(140f));
                if (newFilter != variableFilter)
                {
                    variableFilter = newFilter;
                }

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(!allowEdits || variables == null))
                {
                    if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    {
                        Undo.RecordObject(tree, "Add Variable");
                        variables.Add(new VariableData(tree.GenerateNewVariableName("newVar")));
                        EditorUtility.SetDirty(tree);
                    }

                    using (new EditorGUI.DisabledScope(variables.Count == 0))
                        if (GUILayout.Button("Remove", EditorStyles.toolbarButton, GUILayout.Width(80)))
                        {
                            Undo.RecordObject(tree, "Remove Variable");
                            variables.RemoveAt(variables.Count - 1);
                            EditorUtility.SetDirty(tree);
                        }
                }
            }
        }

        /// <summary>
        /// Builds the filtered variable list for the tree view.
        /// </summary>
        /// <param name="localVariables">The source list of local/static variables.</param>
        /// <param name="globalVariables">The source list of global variables.</param>
        /// <returns>A list of entries matching the current filter.</returns>
        private List<VariableTableTreeView.VariableEntry> BuildVariableEntries(
            List<VariableData> localVariables,
            List<VariableData> globalVariables)
        {
            var entries = new List<VariableTableTreeView.VariableEntry>();
            bool includeLocal = variableFilter.HasFlag(VariableFilter.Local);
            bool includeStatic = variableFilter.HasFlag(VariableFilter.Static);
            bool includeGlobal = variableFilter.HasFlag(VariableFilter.Global);

            if (localVariables != null)
            {
                foreach (VariableData variable in localVariables)
                {
                    if (variable == null || variable.IsFromAttribute)
                    {
                        continue;
                    }

                    if (variable.IsStatic && includeStatic)
                    {
                        entries.Add(new VariableTableTreeView.VariableEntry(
                            variable,
                            VariableTableTreeView.VariableSource.VariableList,
                            VariableTableTreeView.VariableScope.Static));
                    }
                    else if (!variable.IsStatic && includeLocal)
                    {
                        entries.Add(new VariableTableTreeView.VariableEntry(
                            variable,
                            VariableTableTreeView.VariableSource.VariableList,
                            VariableTableTreeView.VariableScope.Local));
                    }
                }
            }

            if (includeLocal && tree && tree.targetScript)
            {
                List<VariableData> attributeVariables = VariableData.GetAttributeVariablesFromType(tree.targetScript.GetClass());
                if (attributeVariables != null)
                {
                    foreach (VariableData variable in attributeVariables)
                    {
                        if (variable == null)
                        {
                            continue;
                        }

                        entries.Add(new VariableTableTreeView.VariableEntry(
                            variable,
                            VariableTableTreeView.VariableSource.Attribute,
                            VariableTableTreeView.VariableScope.Attribute));
                    }
                }
            }

            if (includeGlobal && globalVariables != null)
            {
                foreach (VariableData variable in globalVariables)
                {
                    if (variable == null)
                    {
                        continue;
                    }

                    entries.Add(new VariableTableTreeView.VariableEntry(
                        variable,
                        VariableTableTreeView.VariableSource.VariableList,
                        VariableTableTreeView.VariableScope.Global));
                }
            }

            return entries;
        }

        /// <summary>
        /// Resolves the tree view mode based on the current filter.
        /// </summary>
        /// <returns>The tree view mode.</returns>
        private VariableTableTreeView.Mode ResolveTreeViewMode()
        {
            bool includeLocal = variableFilter.HasFlag(VariableFilter.Local);
            bool includeStatic = variableFilter.HasFlag(VariableFilter.Static);
            bool includeGlobal = variableFilter.HasFlag(VariableFilter.Global);

            if (includeGlobal && (includeLocal || includeStatic))
            {
                return VariableTableTreeView.Mode.Mixed;
            }

            return includeGlobal
                ? VariableTableTreeView.Mode.Global
                : VariableTableTreeView.Mode.Local;
        }

        private void EnsureVariableTreeView()
        {
            if (variableTreeView != null)
            {
                return;
            }

            variableTreeState ??= new TreeViewState();
            var header = VariableTableTreeView.CreateHeader(VariableTableTreeView.Mode.Mixed);

            variableTreeView = new VariableTableTreeView(
                variableTreeState,
                header,
                getTargetScriptType: () => tree && tree.targetScript ? tree.targetScript.GetClass() : null,
                onOpenDetail: OpenDetail,
                onRequestRemove: RemoveVariable,
                isAttributeEnabled: IsAttributeVariableEnabled,
                setAttributeEnabled: SetAttributeVariableEnabled
            );
        }

        private void OpenDetail(VariableData variableData)
        {
            tableDrawDetail = true;
            selectedVariableData = variableData;
        }

        private void RemoveVariable(VariableData variableData)
        {
            if (!tree || variableData == null)
            {
                return;
            }
            tree.RemoveVariable(variableData.UUID);
        }

        private bool IsAttributeVariableEnabled(VariableData variableData)
        {
            if (!tree || variableData == null)
            {
                return false;
            }
            return tree.variables.Any(v => v.UUID == variableData.UUID);
        }

        private void SetAttributeVariableEnabled(VariableData variableData, bool enabled)
        {
            if (!tree || variableData == null)
            {
                return;
            }

            bool exists = tree.variables.Any(v => v.UUID == variableData.UUID);
            if (enabled == exists)
            {
                return;
            }

            if (!enabled)
            {
                tree.RemoveVariable(variableData.UUID);
            }
            else
            {
                tree.AddVariable(variableData);
            }
        }

        private void DrawDefaultValue(VariableData item)
        {
            if (item.IsScript)
            {
                GUILayout.Label("(From Script)");
                return;
            }
            bool i;
            switch (item.Type)
            {
                case VariableType.String:
                    item.DefaultValue = GUILayout.TextField(item.DefaultValue);
                    break;
                case VariableType.Int:

                    {
                        i = int.TryParse(item.DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out int val);
                        if (!i)
                        {
                            val = 0;
                        }
                        item.DefaultValue = EditorGUILayout
                            .IntField(val)
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
                            .FloatField(val)
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
                            .Toggle(val)
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
                            .Vector2Field("", val)
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
                            .Vector3Field("", val)
                            .ToString();
                    }
                    break;
                case VariableType.Invalid:
                    GUILayout.Label("Invalid Variable Type");
                    break;
                case VariableType.UnityObject:
                    if (item.ObjectType is null)
                        item.SetBaseType(typeof(UnityEngine.Object));
                    GUILayout.Label(item.ObjectType.FullName);
                    break;
                case VariableType.Generic:
                    if (item.ObjectType is null)
                        item.SetBaseType(typeof(object));
                    GUILayout.Label(item.ObjectType.FullName);
                    break;
                default:
                    GUILayout.Label($" ");
                    break;
            }
        }

        private void DrawVariableDetail(VariableData vd)
        {
            EditorGUILayout.LabelField(vd.Type + ": " + vd.name);

            using (new EditorGUI.DisabledScope(vd.IsFromAttribute))
            {
                var oldName = vd.name;
                vd.name = EditorGUILayout.DelayedTextField("Name", vd.name);
                if (oldName != vd.name) Undo.RecordObject(tree, "Change variable name");

                var isFromScript = vd.IsScript;
                vd.SetScript(EditorGUILayout.Toggle("From Script", vd.IsScript));
                if (isFromScript != vd.IsScript) Undo.RecordObject(tree, "Set variable from script");
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
                var targetClass = tree.targetScript ? tree.targetScript.GetClass() : null;
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
                        Undo.RecordObject(tree, "Set Path on variable " + vd.name);
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

            EditorGUI.BeginChangeCheck();
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
                EditorGUILayout.LabelField("Default Value:");
                DrawDefaultValue(vd);
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tree, "Change variable " + vd.name);
            }
        }

        public void Reset()
        {
            selectedVariableData = null;
            tableDrawDetail = false;
        }
    }
}
