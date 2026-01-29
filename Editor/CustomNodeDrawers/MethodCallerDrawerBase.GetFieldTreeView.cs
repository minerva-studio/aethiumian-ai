using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Amlos.AI.Editor
{
    public abstract partial class MethodCallerDrawerBase
    {
        /// <summary>
        /// Display mode for the field TreeView.
        /// </summary>
        private enum FieldTreeViewMode
        {
            Get,
            Set
        }

        /// <summary>
        /// TreeView for get/set field entries and previews.
        /// </summary>
        private sealed class FieldTreeView : TreeView
        {
            private enum ColumnId
            {
                Type,
                Field,
            }

            private readonly MethodCallerDrawerBase owner;
            private readonly List<GetFieldTreeViewItem> items = new();

            private FieldTreeViewMode mode;
            private ObjectGetValueBase getNode;
            private ObjectSetValueBase setNode;
            private object baseObject;
            private Type objectType;
            private SerializedProperty entryListProperty;

            public float TotalHeight => totalHeight;

            public float HeaderHeight => multiColumnHeader?.height ?? 0f;

            public FieldTreeView(TreeViewState state, MultiColumnHeader header, MethodCallerDrawerBase owner) : base(state, header)
            {
                this.owner = owner;
                showBorder = true;
                showAlternatingRowBackgrounds = true;
                rowHeight = EditorGUIUtility.singleLineHeight + 6f;
            }

            /// <summary>
            /// Set the data sources for the TreeView.
            /// </summary>
            /// <param name="mode">Field view mode.</param>
            /// <param name="getNode">Target get node.</param>
            /// <param name="setNode">Target set node.</param>
            /// <param name="baseObject">Object instance for preview.</param>
            /// <param name="objectType">Resolved object type.</param>
            /// <param name="entryListProperty">Serialized entry list.</param>
            public void SetData(
                FieldTreeViewMode mode,
                TreeNode node,
                object baseObject,
                Type objectType,
                SerializedProperty entryListProperty)
            {
                bool shouldReload = mode != this.mode || objectType != this.objectType;

                this.mode = mode;
                this.getNode = node as ObjectGetValueBase;
                this.setNode = node as ObjectSetValueBase;
                this.baseObject = baseObject;
                this.objectType = objectType;
                this.entryListProperty = entryListProperty;

                if (shouldReload)
                {
                    Reload();
                }
            }

            protected override TreeViewItem BuildRoot()
            {
                var root = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
                items.Clear();

                if (objectType != null)
                {
                    int id = 0;
                    foreach (var memberInfo in objectType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField | BindingFlags.SetProperty))
                    {
                        if (memberInfo.IsDefined(typeof(ObsoleteAttribute))) continue;
                        if (typeof(Component).IsSubclassOf(memberInfo.DeclaringType) || typeof(Component) == memberInfo.DeclaringType) continue;
                        if (baseObject is Renderer && memberInfo.Name == nameof(Renderer.material)) continue;
                        if (baseObject is Renderer && memberInfo.Name == nameof(Renderer.materials)) continue;
                        if (!owner.TryGetValueAndType(memberInfo, baseObject, out Type valueType, out object currentValue, mode == FieldTreeViewMode.Set)) continue;

                        VariableType variableType = VariableUtility.GetVariableType(valueType);
                        if (variableType == VariableType.Invalid || variableType == VariableType.Node) continue;

                        items.Add(new GetFieldTreeViewItem(id++, 0, memberInfo.Name)
                        {
                            MemberInfo = memberInfo,
                            ValueType = valueType,
                            CurrentValue = currentValue,
                            VariableType = variableType
                        });
                    }
                }

                ApplySorting(items);
                root.children = items.Cast<TreeViewItem>().ToList();
                SetupDepthsFromParentsAndChildren(root);
                return root;
            }


            protected override float GetCustomRowHeight(int row, TreeViewItem item)
            {
                if (item is GetFieldTreeViewItem fieldItem)
                {
                    return GetFieldRowHeight(fieldItem, entryListProperty);
                }

                return base.GetCustomRowHeight(row, item);
            }

            protected override void RowGUI(RowGUIArgs args)
            {
                if (args.item is not GetFieldTreeViewItem fieldItem)
                {
                    base.RowGUI(args);
                    return;
                }

                for (int i = 0; i < args.GetNumVisibleColumns(); i++)
                {
                    int columnIndex = args.GetColumn(i);
                    ColumnId column = (ColumnId)columnIndex;
                    Rect cellRect = args.GetCellRect(i);

                    CenterRectUsingSingleLineHeight(ref cellRect);

                    switch (column)
                    {
                        case ColumnId.Field:
                            DrawFieldCell(cellRect, fieldItem);
                            break;
                        case ColumnId.Type:
                            DrawTypeCell(cellRect, fieldItem);
                            break;
                    }
                }
            }

            /// <summary>
            /// Draw the field cell for a row.
            /// </summary>
            /// <param name="rect">Target cell rect.</param>
            /// <param name="item">Field row item.</param>
            private void DrawFieldCell(Rect rect, GetFieldTreeViewItem item)
            {
                string memberName = item.MemberInfo.Name;
                bool hasEntry = TryGetEntryProperties(entryListProperty, memberName, out var entryProperty, out var dataProperty);

                if (mode == FieldTreeViewMode.Get)
                {
                    DrawGetFieldCell(rect, item, memberName, hasEntry, dataProperty);
                }
                else
                {
                    DrawSetFieldCell(rect, item, memberName, hasEntry, dataProperty);
                }
            }

            /// <summary>
            /// Draw the type cell for a row.
            /// </summary>
            /// <param name="rect">Target cell rect.</param>
            /// <param name="item">Field row item.</param>
            private void DrawTypeCell(Rect rect, GetFieldTreeViewItem item)
            {
                string label = GetFieldType(item.ValueType, item.VariableType);
                EditorGUI.LabelField(rect, label);
            }

            /// <summary>
            /// Draw a get-field row.
            /// </summary>
            /// <param name="rect">Target cell rect.</param>
            /// <param name="item">Field row item.</param>
            /// <param name="memberName">Member name.</param>
            /// <param name="hasEntry">Whether the entry exists.</param>
            /// <param name="dataProperty">Serialized data property.</param>
            private void DrawGetFieldCell(Rect rect, GetFieldTreeViewItem item, string memberName, bool hasEntry, SerializedProperty dataProperty)
            {
                const float removeButtonWidth = 20f;
                const float getButtonWidth = 60f;
                const float spacing = 4f;

                Rect contentRect = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);

                if (hasEntry && dataProperty != null)
                {
                    Rect removeRect = new Rect(contentRect.xMax - removeButtonWidth, contentRect.y, removeButtonWidth, EditorGUIUtility.singleLineHeight);
                    Rect fieldRect = new Rect(contentRect.x, contentRect.y, contentRect.width - removeButtonWidth - spacing, contentRect.height);

                    owner.DrawVariableProperty(fieldRect, new GUIContent(memberName.ToTitleCase()), dataProperty, new[] { item.VariableType }, VariableAccessFlag.Read);

                    if (GUI.Button(removeRect, "X"))
                    {
                        Undo.RecordObject(owner.tree, $"Remove entry ({memberName}) in {getNode.name}");
                        RemoveEntry(memberName);
                    }
                }
                else
                {
                    float previewWidth = Mathf.Max(0f, contentRect.width - getButtonWidth - removeButtonWidth - spacing * 2f);
                    Rect previewRect = new Rect(contentRect.x, contentRect.y, previewWidth, contentRect.height);
                    Rect getRect = new Rect(previewRect.xMax + spacing, contentRect.y, getButtonWidth, EditorGUIUtility.singleLineHeight);
                    Rect disabledRect = new Rect(getRect.xMax + spacing, contentRect.y, removeButtonWidth, EditorGUIUtility.singleLineHeight);

                    DrawGetFieldPreview(previewRect, item);

                    if (GUI.Button(getRect, "Get"))
                    {
                        Undo.RecordObject(owner.tree, $"Add new entry ({memberName}) in {getNode.name}");
                        AddEntry(memberName, item.VariableType, item.ValueType, null);
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUI.Button(disabledRect, "-");
                    }
                }
            }

            /// <summary>
            /// Draw a set-field row.
            /// </summary>
            /// <param name="rect">Target cell rect.</param>
            /// <param name="item">Field row item.</param>
            /// <param name="memberName">Member name.</param>
            /// <param name="hasEntry">Whether the entry exists.</param>
            /// <param name="dataProperty">Serialized data property.</param>
            private void DrawSetFieldCell(Rect rect, GetFieldTreeViewItem item, string memberName, bool hasEntry, SerializedProperty dataProperty)
            {
                const float removeButtonWidth = 20f;
                const float modifyButtonWidth = 70f;
                const float spacing = 4f;

                Rect contentRect = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);

                if (hasEntry && dataProperty != null)
                {
                    Rect removeRect = new Rect(contentRect.xMax - removeButtonWidth, contentRect.y, removeButtonWidth, EditorGUIUtility.singleLineHeight);
                    Rect fieldRect = new Rect(contentRect.x, contentRect.y, contentRect.width - removeButtonWidth - spacing, contentRect.height);

                    EnsureParameterObjectType(dataProperty, item.ValueType);
                    owner.DrawVariableProperty(fieldRect, new GUIContent(memberName.ToTitleCase()), dataProperty, VariableUtility.GetCompatibleTypes(item.VariableType), VariableAccessFlag.None);

                    if (GUI.Button(removeRect, "X"))
                    {
                        Undo.RecordObject(owner.tree, $"Remove entry ({memberName}) in {setNode.name}");
                        RemoveEntry(memberName);
                    }
                }
                else
                {
                    float previewWidth = Mathf.Max(0f, contentRect.width - modifyButtonWidth - removeButtonWidth - spacing * 2f);
                    Rect previewRect = new Rect(contentRect.x, contentRect.y, previewWidth, contentRect.height);
                    Rect modifyRect = new Rect(previewRect.xMax + spacing, contentRect.y, modifyButtonWidth, EditorGUIUtility.singleLineHeight);
                    Rect disabledRect = new Rect(modifyRect.xMax + spacing, contentRect.y, removeButtonWidth, EditorGUIUtility.singleLineHeight);

                    bool changed = DrawSetFieldPreview(previewRect, item, out object newValue);
                    if (changed)
                    {
                        Undo.RecordObject(owner.tree, $"Add new entry ({memberName}) in {setNode.name}");
                        AddEntry(memberName, item.VariableType, item.ValueType, newValue);
                    }

                    if (GUI.Button(modifyRect, "Modify"))
                    {
                        Undo.RecordObject(owner.tree, $"Add new entry ({memberName}) in {setNode.name}");
                        AddEntry(memberName, item.VariableType, item.ValueType, null);
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUI.Button(disabledRect, "-");
                    }
                }
            }

            /// <summary>
            /// Compute the row height for a field entry.
            /// </summary>
            /// <param name="item">Field row item.</param>
            /// <param name="entryListProperty">Serialized entry list.</param>
            /// <returns>Row height in pixels.</returns>
            private float GetFieldRowHeight(GetFieldTreeViewItem item, SerializedProperty entryListProperty)
            {
                if (item == null)
                {
                    return EditorGUIUtility.singleLineHeight + 4f;
                }

                if (TryGetEntryProperties(entryListProperty, item.MemberInfo.Name, out _, out SerializedProperty dataProperty)
                    && dataProperty?.boxedValue is VariableBase variable)
                {
                    VariableType[] possibleTypes = mode == FieldTreeViewMode.Get
                        ? new[] { item.VariableType }
                        : VariableUtility.GetCompatibleTypes(item.VariableType);
                    VariableAccessFlag accessFlag = mode == FieldTreeViewMode.Get
                        ? VariableAccessFlag.Read
                        : VariableAccessFlag.None;

                    float height = VariableFieldDrawers.GetVariableHeight(variable, owner.tree, possibleTypes, accessFlag);
                    return Mathf.Max(EditorGUIUtility.singleLineHeight, height) + 4f;
                }

                return EditorGUIUtility.singleLineHeight + 4f;
            }

            /// <summary>
            /// Try to locate the entry and data properties for a specific member.
            /// </summary>
            /// <param name="entryListProperty">Serialized entry list.</param>
            /// <param name="memberName">Member name.</param>
            /// <param name="entryProperty">Resolved entry property.</param>
            /// <param name="dataProperty">Resolved data property.</param>
            /// <returns>True if a matching entry is found.</returns>
            private bool TryGetEntryProperties(SerializedProperty entryListProperty, string memberName, out SerializedProperty entryProperty, out SerializedProperty dataProperty)
            {
                entryProperty = null;
                dataProperty = null;

                if (entryListProperty == null || !entryListProperty.isArray)
                {
                    return false;
                }

                for (int i = 0; i < entryListProperty.arraySize; i++)
                {
                    SerializedProperty elementProperty = entryListProperty.GetArrayElementAtIndex(i);
                    var nameProperty = elementProperty.FindPropertyRelative(nameof(FieldPointer.name));
                    if (!string.Equals(nameProperty.stringValue, memberName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entryProperty = elementProperty;
                    dataProperty = elementProperty.FindPropertyRelative(nameof(FieldPointer.data));
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Draw the preview value for get fields.
            /// </summary>
            /// <param name="position">Target draw rect.</param>
            /// <param name="item">Field row item.</param>
            private void DrawGetFieldPreview(Rect position, GetFieldTreeViewItem item)
            {
                object currentValue = item.CurrentValue;
                if (currentValue == null)
                {
                    string label = GetFieldInfo(item.ValueType, item.VariableType);
                    EditorGUI.LabelField(position, item.MemberInfo.Name.ToTitleCase(), label);
                    return;
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    DrawField(position, new GUIContent(item.MemberInfo.Name.ToTitleCase()), currentValue, item.ValueType);
                }
            }

            /// <summary>
            /// Draw the preview value for set fields and detect edits.
            /// </summary>
            /// <param name="position">Target draw rect.</param>
            /// <param name="item">Field row item.</param>
            /// <param name="newValue">Updated value when edited.</param>
            /// <returns>True if the preview value changed.</returns>
            private bool DrawSetFieldPreview(Rect position, GetFieldTreeViewItem item, out object newValue)
            {
                object currentValue = item.CurrentValue;
                newValue = currentValue;

                if (currentValue == null)
                {
                    string label = GetFieldInfo(item.ValueType, item.VariableType);
                    EditorGUI.LabelField(position, item.MemberInfo.Name.ToTitleCase(), label);
                    return false;
                }

                EditorGUI.BeginChangeCheck();
                newValue = DrawField(position, new GUIContent(item.MemberInfo.Name.ToTitleCase()), currentValue, item.ValueType);
                if (!EditorGUI.EndChangeCheck())
                {
                    return false;
                }

                return !Equals(currentValue, newValue);
            }

            /// <summary>
            /// Apply sorting based on the active header.
            /// </summary>
            /// <param name="items">Items to sort.</param>
            private void ApplySorting(List<GetFieldTreeViewItem> items)
            {
                if (multiColumnHeader == null)
                {
                    return;
                }

                int sortedColumnIndex = multiColumnHeader.sortedColumnIndex;
                if (sortedColumnIndex < 0)
                {
                    return;
                }

                bool ascending = multiColumnHeader.state.columns[sortedColumnIndex].sortedAscending;
                switch ((ColumnId)sortedColumnIndex)
                {
                    case ColumnId.Type:
                        items.Sort((a, b) => string.Compare(GetTypeSortKey(a), GetTypeSortKey(b), StringComparison.OrdinalIgnoreCase));
                        break;
                    default:
                        items.Sort((a, b) => string.Compare(a.MemberInfo.Name, b.MemberInfo.Name, StringComparison.OrdinalIgnoreCase));
                        break;
                }

                if (!ascending)
                {
                    items.Reverse();
                }
            }

            /// <summary>
            /// Build the sort key for the type column.
            /// </summary>
            /// <param name="item">Field row item.</param>
            /// <returns>Sortable string.</returns>
            private static string GetTypeSortKey(GetFieldTreeViewItem item)
            {
                string typeName = item.ValueType?.FullName ?? string.Empty;
                return $"{item.VariableType}-{typeName}";
            }

            /// <summary>
            /// Ensure the parameter object type matches the current field type.
            /// </summary>
            /// <param name="dataProperty">Serialized data property.</param>
            /// <param name="valueType">Resolved field type.</param>
            private void EnsureParameterObjectType(SerializedProperty dataProperty, Type valueType)
            {
                if (dataProperty?.boxedValue is not Parameter parameter)
                {
                    return;
                }

                if (parameter.ParameterObjectType == valueType)
                {
                    return;
                }

                parameter.ParameterObjectType = valueType;
                dataProperty.boxedValue = parameter;
                dataProperty.serializedObject.ApplyModifiedProperties();
                dataProperty.serializedObject.Update();
            }

            /// <summary>
            /// Add a serialized get/set field entry.
            /// </summary>
            /// <param name="memberName">Member name.</param>
            /// <param name="variableType">Variable type.</param>
            /// <param name="valueType">Resolved field type.</param>
            /// <param name="constantValue">Optional constant value.</param>
            private void AddEntry(string memberName, VariableType variableType, Type valueType, object constantValue)
            {
                if (entryListProperty == null || !entryListProperty.isArray)
                {
                    if (mode == FieldTreeViewMode.Get)
                    {
                        getNode?.AddPointer(memberName, variableType);
                    }
                    else
                    {
                        setNode?.AddChangeEntry(memberName, valueType);
                    }
                    return;
                }

                int index = entryListProperty.arraySize;
                entryListProperty.arraySize++;
                SerializedProperty elementProperty = entryListProperty.GetArrayElementAtIndex(index);
                var nameProperty = elementProperty.FindPropertyRelative(nameof(FieldPointer.name));
                var dataProperty = elementProperty.FindPropertyRelative(nameof(FieldPointer.data));
                nameProperty.stringValue = memberName;

                InitializeEntryData(dataProperty, variableType, valueType);
                ApplyConstantValue(dataProperty, constantValue);

                entryListProperty.serializedObject.ApplyModifiedProperties();
                entryListProperty.serializedObject.Update();
            }

            /// <summary>
            /// Remove a serialized get/set field entry.
            /// </summary>
            /// <param name="memberName">Member name.</param>
            private void RemoveEntry(string memberName)
            {
                if (entryListProperty == null || !entryListProperty.isArray)
                {
                    if (mode == FieldTreeViewMode.Get)
                    {
                        getNode?.RemoveChangeEntry(memberName);
                    }
                    else
                    {
                        setNode?.RemoveChangeEntry(memberName);
                    }
                    return;
                }

                for (int i = 0; i < entryListProperty.arraySize; i++)
                {
                    SerializedProperty elementProperty = entryListProperty.GetArrayElementAtIndex(i);
                    SerializedProperty nameProperty = elementProperty.FindPropertyRelative(nameof(FieldPointer.name));
                    if (!string.Equals(nameProperty.stringValue, memberName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entryListProperty.DeleteArrayElementAtIndex(i);
                    entryListProperty.serializedObject.ApplyModifiedProperties();
                    entryListProperty.serializedObject.Update();
                    return;
                }
            }

            /// <summary>
            /// Initialize entry data based on the current mode.
            /// </summary>
            /// <param name="dataProperty">Serialized data property.</param>
            /// <param name="variableType">Variable type.</param>
            /// <param name="valueType">Resolved field type.</param>
            private void InitializeEntryData(SerializedProperty dataProperty, VariableType variableType, Type valueType)
            {
                if (dataProperty == null)
                {
                    return;
                }

                if (mode == FieldTreeViewMode.Set)
                {
                    Parameter parameter = dataProperty.boxedValue as Parameter ?? new Parameter(valueType);
                    parameter.ParameterObjectType = valueType;
                    parameter.ForceSetConstantType(variableType);
                    dataProperty.boxedValue = parameter;
                }
                else
                {
                    VariableReference reference = dataProperty.boxedValue as VariableReference ?? new VariableReference();
                    //reference.ForceSetConstantType(variableType);
                    dataProperty.boxedValue = reference;
                }
            }

            /// <summary>
            /// Apply a constant value to an entry data property when provided.
            /// </summary>
            /// <param name="dataProperty">Serialized data property.</param>
            /// <param name="constantValue">Constant value to apply.</param>
            private static void ApplyConstantValue(SerializedProperty dataProperty, object constantValue)
            {
                if (constantValue == null || dataProperty?.boxedValue is not VariableBase variable)
                {
                    return;
                }

                variable.ForceSetConstantValue(constantValue);
                dataProperty.boxedValue = variable;
            }

            /// <summary>
            /// Draw a value in a fixed rect.
            /// </summary>
            /// <param name="position">Target draw rect.</param>
            /// <param name="label">Field label.</param>
            /// <param name="value">Current value.</param>
            /// <param name="type">Value type.</param>
            /// <returns>Updated value when editable; otherwise the original value.</returns>
            public static object DrawField(Rect position, GUIContent label, object value, Type type)
            {
                if (value is int i)
                {
                    return EditorGUI.IntField(position, label, i);
                }
                else if (value is long l)
                {
                    return EditorGUI.LongField(position, label, l);
                }
                else if (value is float f)
                {
                    return EditorGUI.FloatField(position, label, f);
                }
                else if (value is double d)
                {
                    return EditorGUI.DoubleField(position, label, d);
                }
                else if (value is bool b)
                {
                    return EditorGUI.Toggle(position, label, b);
                }
                else if (value is Vector2 v2)
                {
                    return EditorGUI.Vector2Field(position, label, v2);
                }
                else if (value is Vector2Int v2i)
                {
                    return EditorGUI.Vector2IntField(position, label, v2i);
                }
                else if (value is Vector3 v3)
                {
                    return EditorGUI.Vector3Field(position, label, v3);
                }
                else if (value is Vector3Int v3i)
                {
                    return EditorGUI.Vector3IntField(position, label, v3i);
                }
                else if (value is Vector4 v4)
                {
                    return EditorGUI.Vector4Field(position, label, v4);
                }
                else if (value is Quaternion quat)
                {
                    Vector4 quatVec = new Vector4(quat.x, quat.y, quat.z, quat.w);
                    quatVec = EditorGUI.Vector4Field(position, label, quatVec);
                    return new Quaternion(quatVec.x, quatVec.y, quatVec.z, quatVec.w);
                }
                else if (value is UUID uUID)
                {
                    EditorGUI.LabelField(position, label, uUID.Value);
                    return uUID;
                }
                else if (value is Color color)
                {
                    return EditorGUI.ColorField(position, label, color);
                }
                else if (value is Rect rect)
                {
                    return EditorGUI.RectField(position, label, rect);
                }
                else if (value is RectInt rectInt)
                {
                    return EditorGUI.RectIntField(position, label, rectInt);
                }
                else if (value is Bounds bounds)
                {
                    return EditorGUI.BoundsField(position, label, bounds);
                }
                else if (value is BoundsInt boundsInt)
                {
                    return EditorGUI.BoundsIntField(position, label, boundsInt);
                }
                else if (value is LayerMask lm)
                {
                    string[] layers = System.Linq.Enumerable.Range(0, 31).Select(index => LayerMask.LayerToName(index)).Where(l => !string.IsNullOrEmpty(l)).ToArray();
                    return new LayerMask { value = EditorGUI.MaskField(position, label, lm.value, layers) };
                }
                else if (type == typeof(string))
                {
                    return EditorGUI.TextField(position, label, value as string ?? string.Empty);
                }
                else if (type == typeof(Gradient))
                {
                    return EditorGUI.GradientField(position, label, value as Gradient);
                }
                else if (value is Enum e)
                {
                    if (Attribute.GetCustomAttribute(type, typeof(FlagsAttribute)) != null)
                    {
                        return EditorGUI.EnumFlagsField(position, label, e);
                    }
                    else return EditorGUI.EnumPopup(position, label, e);
                }
                else if (value is UnityEngine.Object || type?.IsSubclassOf(typeof(UnityEngine.Object)) == true || type == typeof(UnityEngine.Object))
                {
                    return EditorGUI.ObjectField(position, label, value as UnityEngine.Object, type, true);
                }
                else if (type is null)
                {
                    EditorGUI.LabelField(position, label.text, "null");
                }
                else EditorGUI.LabelField(position, label.text, $"({type?.Name ?? "[Unknown]"})");
                return value;
            }
        }

        /// <summary>
        /// Draw a variable property inside a fixed rect.
        /// </summary>
        /// <param name="rect">Target draw rect.</param>
        /// <param name="label">Field label.</param>
        /// <param name="variableProperty">Serialized variable property.</param>
        /// <param name="possibleTypes">Allowed variable types.</param>
        /// <param name="variableAccessFlag">Access constraint.</param>
        /// <returns>True when the value changes.</returns>
        private bool DrawVariableProperty(Rect rect, GUIContent label, SerializedProperty variableProperty, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag)
        {
            if (variableProperty?.boxedValue is not VariableBase variable)
            {
                return false;
            }

            EditorGUI.BeginChangeCheck();
            VariableFieldDrawers.DrawVariable(rect, label, variable, tree, possibleTypes, variableAccessFlag);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyVariableProperty(variableProperty, variable);
                return true;
            }

            return false;
        }

        /// <summary>
        /// TreeView item for field data rows.
        /// </summary>
        private sealed class GetFieldTreeViewItem : TreeViewItem
        {
            public MemberInfo MemberInfo { get; set; }
            public Type ValueType { get; set; }
            public object CurrentValue { get; set; }
            public VariableType VariableType { get; set; }

            public GetFieldTreeViewItem(int id, int depth, string displayName) : base(id, depth, displayName)
            {
            }
        }

        /// <summary>
        /// Ensure the field TreeView and header are initialized.
        /// </summary>
        private void EnsureFieldTreeView()
        {
            fieldTreeViewState ??= new TreeViewState();
            fieldTreeHeader ??= CreateFieldTreeHeader();
            fieldTreeView ??= new FieldTreeView(fieldTreeViewState, fieldTreeHeader, this);
        }

        /// <summary>
        /// Create the header used by the field TreeView.
        /// </summary>
        /// <returns>Configured header instance.</returns>
        private MultiColumnHeader CreateFieldTreeHeader()
        {
            var columns = new[]
            {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Type"),
                    width = 25f,
                    minWidth = 10f,
                    autoResize = true,
                    canSort = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Field"),
                    width = 200f,
                    minWidth = 100f,
                    autoResize = true,
                    canSort = true
                }
            };

            var state = new MultiColumnHeaderState(columns)
            {
                sortedColumnIndex = 0
            };
            state.columns[0].sortedAscending = true;

            var header = new MultiColumnHeader(state)
            {
                height = 22f
            };
            header.sortingChanged += OnFieldTreeSortingChanged;
            header.ResizeToFit();
            return header;
        }

        /// <summary>
        /// Handle field TreeView sorting changes.
        /// </summary>
        /// <param name="header">Active header.</param>
        private void OnFieldTreeSortingChanged(MultiColumnHeader header)
        {
            fieldTreeView?.Reload();
        }

    }
}
