using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
#if !UNITY_6000_3_OR_NEWER
using UnityEditor.IMGUI.Controls;
#endif
using UnityEngine;
using static Aethiumian.AI.Variables.VariableUtility;
#if UNITY_6000_3_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#endif

namespace Aethiumian.AI.Editor
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

        private enum VariableUsage
        {
            Value,
            Timer,
            ScriptBinding,
        }



        private struct WideModeScope : IDisposable
        {
            private readonly bool previousWideMode;

            public WideModeScope(bool forceWideMode = true)
            {
                previousWideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = forceWideMode;
            }

            public void Dispose()
            {
                EditorGUIUtility.wideMode = previousWideMode;
            }
        }


        public void DrawVariableTable()
        {
            using var wideMode = new WideModeScope();
            if (tableDrawDetail)
            {
                DrawVariableDetail(selectedVariableData);
                return;
            }

            using (new GUILayout.VerticalScope())
            {
                // EditorGUILayout.LabelField("Variable Table", EditorStyles.boldLabel);

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
            var entries = BuildVariableEntries(localVariables, globalVariables);
            variableTreeView.SetData(entries, mode);

            int totalRows = entries.Length;
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
                        GenericMenu menu = new();
                        menu.AddItem(new GUIContent("Value"), false, () => CreateVariable(VariableUsage.Value));
                        menu.AddItem(new GUIContent("Timer"), false, () => CreateVariable(VariableUsage.Timer));
                        menu.ShowAsContext();
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
        private VariableTableTreeView.VariableEntry[] BuildVariableEntries(
            List<VariableData> localVariables,
            List<VariableData> globalVariables)
        {
            var entries = new HashSet<VariableTableTreeView.VariableEntry>();
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
                var attributeVariables = AIVariableAttribute.GetAttributeVariablesFromType(tree.targetScript.GetClass());
                if (attributeVariables != null)
                {
                    HashSet<UUID> attributeVariableIDs = new();
                    foreach (VariableData variable in attributeVariables)
                    {
                        if (variable == null)
                        {
                            continue;
                        }

                        // Attribute variables are generated from code each draw; UUID de-duplication keeps override chains as one row.
                        if (!attributeVariableIDs.Add(variable.UUID))
                        {
                            continue;
                        }

                        entries.Add(new VariableTableTreeView.VariableEntry(
                            variable,
                            VariableTableTreeView.VariableSource.Attribute,
                            variable.IsStatic ? VariableTableTreeView.VariableScope.Static : VariableTableTreeView.VariableScope.Local));
                    }
                    // checkout some vars that marked as attribute but already deleted in code but still remained in data
                    foreach (VariableData variable in localVariables)
                    {
                        if (variable == null || !variable.IsFromAttribute)
                        {
                            continue;
                        }
                        if (attributeVariableIDs.Contains(variable.UUID))
                        {
                            continue;
                        }
                        if (variable.IsStatic && includeStatic)
                        {
                            entries.Add(new VariableTableTreeView.VariableEntry(
                                variable,
                                VariableTableTreeView.VariableSource.Attribute,
                                VariableTableTreeView.VariableScope.Static));
                        }
                        else if (!variable.IsStatic && includeLocal)
                        {
                            entries.Add(new VariableTableTreeView.VariableEntry(
                                variable,
                                VariableTableTreeView.VariableSource.Attribute,
                                VariableTableTreeView.VariableScope.Local));
                        }
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

            return entries.ToArray();
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
            switch (item.Type)
            {
                case VariableType.String:
                    item.SetDefaultValue(GUILayout.TextField((string)item.GetDefaultValue()));
                    break;
                case VariableType.Int:

                    {
                        int val = (int)item.GetDefaultValue();
                        item.SetDefaultValue(EditorGUILayout.IntField(val));
                    }
                    break;
                case VariableType.Float:

                    {
                        float val = (float)item.GetDefaultValue();
                        item.SetDefaultValue(EditorGUILayout.FloatField(val));
                    }
                    break;
                case VariableType.Bool:

                    {
                        bool val = (bool)item.GetDefaultValue();
                        item.SetDefaultValue(EditorGUILayout.Toggle(val));
                    }
                    break;
                case VariableType.Vector2:

                    {
                        Vector2 val = (Vector2)item.GetDefaultValue();
                        item.SetDefaultValue(EditorGUILayout.Vector2Field("", val));
                    }
                    break;
                case VariableType.Vector3:

                    {
                        Vector3 val = (Vector3)item.GetDefaultValue();
                        item.SetDefaultValue(EditorGUILayout.Vector3Field("", val));
                    }
                    break;
                case VariableType.Vector4:

                    {
                        Vector4 val = (Vector4)item.GetDefaultValue();
                        item.SetDefaultValue(EditorGUILayout.Vector4Field("", val));
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
                EditorGUI.BeginChangeCheck();
                string nextName = EditorGUILayout.DelayedTextField("Name", vd.name);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tree, "Change variable name");
                    vd.name = nextName;
                    EditorUtility.SetDirty(tree);
                }
            }

            DrawUsage(vd);
            if (vd.IsScript)
            {
                DrawScriptVariable(vd);
            }
            else
            {
                if (vd.IsTimer)
                {
                    EditorGUILayout.HelpBox("Write seconds to start. Read remaining seconds. Starts inactive at 0.", MessageType.Info);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.EnumPopup("Type", VariableType.Float);
                        EditorGUILayout.LabelField("Default Value", "Inactive (0)");
                        EditorGUILayout.LabelField("Scope", "Local");
                    }
                }
                else
                {
                    DrawAIVariable(vd);
                }
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
                if (targetClass == null)
                {
                    EditorGUILayout.HelpBox("Script Binding requires a target script.", MessageType.Warning);
                    return;
                }

                string[] options = GetTargetScriptMemberNames(targetClass);
                using (new EditorGUI.DisabledScope(vd.IsFromAttribute))
                {
                    int selected = Array.IndexOf(options, vd.Path);
                    int next = EditorGUILayout.IntPopup("Member", selected, options, Enumerable.Range(0, options.Length).ToArray());
                    if (next >= 0 && options[next] != vd.Path)
                    {
                        MemberInfo member = targetClass.GetMember(options[next]).FirstOrDefault();
                        if (member != null)
                        {
                            ApplyUsage(tree, vd, "Change script binding", item =>
                                VariableAuthoring.ConfigureScriptBinding(item, options[next], GetResultType(member)));
                        }
                    }
                }
                using (new EditorGUI.DisabledScope(true))
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

            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        private void DrawAIVariable(VariableData vd)
        {
            using (new EditorGUI.DisabledScope(vd.IsTimer))
            {
                vd.SetType((VariableType)EditorGUILayout.EnumPopup("Type", vd.Type));
            }

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

        /// <summary>Draws the inferred usage and applies one complete usage transition at a time.</summary>
        private void DrawUsage(VariableData variable)
        {
            string usage = variable.IsScript
                ? "Script Binding"
                : variable.IsTimer
                    ? "Timer"
                    : "Value";
            EditorGUILayout.LabelField("Usage", usage);
            if (variable.IsFromAttribute) return;

            if (!GUILayout.Button("Change Usage…", EditorStyles.miniButton)) return;
            BehaviourTreeData ownerTree = tree;
            VariableData ownerVariable = variable;
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Value"), usage == "Value", () => ApplyUsage(ownerTree, ownerVariable, "Change variable usage", item =>
                VariableAuthoring.ConfigureValue(item, item.Type, item.GetDefaultValue())));
            menu.AddItem(new GUIContent("Timer"), usage == "Timer", () => ApplyUsage(ownerTree, ownerVariable, "Change variable usage", VariableAuthoring.ConfigureTimer));
            if (ownerTree && ownerTree.targetScript)
            {
                string[] members = GetTargetScriptMemberNames(ownerTree.targetScript.GetClass());
                if (members.Length == 0)
                {
                    menu.AddDisabledItem(new GUIContent("Script Binding/No compatible members"));
                }
                else
                {
                    foreach (string memberName in members)
                    {
                        string selectedMember = memberName;
                        menu.AddItem(
                            new GUIContent($"Script Binding/{selectedMember}"),
                            usage == "Script Binding" && ownerVariable.Path == selectedMember,
                            () => ApplyScriptBinding(ownerTree, ownerVariable, selectedMember));
                    }
                }
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Script Binding (missing target script)"));
            }
            menu.ShowAsContext();
        }

        /// <summary>Applies a complete variable-usage mutation with one asset Undo record.</summary>
        private void ApplyUsage(BehaviourTreeData ownerTree, VariableData variable, string undoName, Action<VariableData> configure)
        {
            if (!IsCurrentVariable(ownerTree, variable)) return;
            Undo.RecordObject(ownerTree, undoName);
            configure(variable);
            EditorUtility.SetDirty(ownerTree);
        }

        private void ApplyScriptBinding(BehaviourTreeData ownerTree, VariableData variable, string memberName)
        {
            if (!IsCurrentVariable(ownerTree, variable) || !ownerTree.targetScript) return;

            MemberInfo member = ownerTree.targetScript.GetClass().GetMember(memberName).FirstOrDefault();
            if (member == null) return;

            ApplyUsage(ownerTree, variable, "Change script binding", item =>
                VariableAuthoring.ConfigureScriptBinding(item, memberName, GetResultType(member)));
        }

        private bool IsCurrentVariable(BehaviourTreeData ownerTree, VariableData variable)
        {
            return ownerTree
                && ReferenceEquals(tree, ownerTree)
                && ownerTree.variables != null
                && ownerTree.variables.Any(candidate => ReferenceEquals(candidate, variable));
        }

        private void CreateVariable(VariableUsage usage)
        {
            if (!tree) return;
            VariableData variable = new(
                tree.GenerateNewVariableName(usage == VariableUsage.Timer ? "timer" : "newVar"),
                usage == VariableUsage.Timer ? VariableType.Float : VariableType.Float);
            Undo.RecordObject(tree, "Create Variable");
            if (usage == VariableUsage.Timer)
                VariableAuthoring.ConfigureTimer(variable);
            else
                VariableAuthoring.ConfigureValue(variable, VariableType.Float, 0f);
            tree.variables.Add(variable);
            EditorUtility.SetDirty(tree);
            selectedVariableData = variable;
            tableDrawDetail = true;
        }

        /// <summary>Lists selectable target-script members in the same order for creation and editing.</summary>
        private static string[] GetTargetScriptMemberNames(Type targetClass)
        {
            return targetClass.GetMembers()
                .Concat(targetClass.GetProperties())
                .Where(member => !Attribute.IsDefined(member, typeof(ObsoleteAttribute)))
                .Where(member => member is FieldInfo or PropertyInfo || member is MethodInfo method
                    && method.GetParameters().Length == 0
                    && !method.ContainsGenericParameters
                    && !method.Name.StartsWith("get_")
                    && !method.Name.StartsWith("set_")
                    && method.ReturnType != typeof(void))
                .Select(member => member.Name)
                .Distinct()
                .OrderBy(name => name)
                .ToArray();
        }

    }
}
