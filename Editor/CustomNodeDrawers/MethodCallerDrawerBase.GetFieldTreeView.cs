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
        /// TreeView for get-field entries and previews.
        /// </summary>
        private sealed class GetFieldTreeView : TreeView
        {
            private readonly MethodCallerDrawerBase owner;
            private readonly List<GetFieldTreeViewItem> items = new();
            private ObjectGetValueBase node;
            private object baseObject;
            private Type objectType;
            private SerializedProperty entryListProperty;


            public float TotalHeight => totalHeight;

            public GetFieldTreeView(TreeViewState state, MethodCallerDrawerBase owner) : base(state)
            {
                this.owner = owner;
                showBorder = true;
                showAlternatingRowBackgrounds = true;
                rowHeight = EditorGUIUtility.singleLineHeight + 6f;
            }

            /// <summary>
            /// Set the data sources for the TreeView.
            /// </summary>
            /// <param name="node">Target node.</param>
            /// <param name="baseObject">Object instance for preview.</param>
            /// <param name="objectType">Resolved object type.</param>
            /// <param name="entryListProperty">Serialized entry list.</param>
            public void SetData(ObjectGetValueBase node, object baseObject, Type objectType, SerializedProperty entryListProperty)
            {
                bool shouldReload = objectType != this.objectType;

                this.node = node;
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
                        if (!owner.TryGetValueAndType(memberInfo, baseObject, out Type valueType, out object currentValue)) continue;

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

                root.children = items.Cast<TreeViewItem>().ToList();
                SetupDepthsFromParentsAndChildren(root);
                return root;
            }

            protected override float GetCustomRowHeight(int row, TreeViewItem item)
            {
                if (item is GetFieldTreeViewItem fieldItem)
                {
                    return GetGetFieldRowHeight(fieldItem, entryListProperty);
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

                const float removeButtonWidth = 20f;
                const float getButtonWidth = 60f;
                const float spacing = 4f;

                Rect rowRect = args.rowRect;
                Rect contentRect = new Rect(rowRect.x, rowRect.y + 2f, rowRect.width, rowRect.height - 4f);

                string memberName = fieldItem.MemberInfo.Name;
                bool hasEntry = TryGetEntryProperties(entryListProperty, memberName, out var entryProperty);
                SerializedProperty dataProperty = hasEntry ? entryProperty.FindPropertyRelative(nameof(FieldPointer.data)) : null;

                if (hasEntry && dataProperty != null)
                {
                    Rect removeRect = new Rect(contentRect.xMax - removeButtonWidth, contentRect.y, removeButtonWidth, EditorGUIUtility.singleLineHeight);
                    Rect fieldRect = new Rect(contentRect.x, contentRect.y, contentRect.width - removeButtonWidth - spacing, contentRect.height);

                    owner.DrawVariableProperty(fieldRect, new GUIContent(memberName.ToTitleCase()), dataProperty, new[] { fieldItem.VariableType }, VariableAccessFlag.Read);

                    if (GUI.Button(removeRect, "X"))
                    {
                        Undo.RecordObject(owner.tree, $"Remove entry ({memberName}) in {node.name}");
                        RemoveGetEntry(node, entryListProperty, memberName);
                    }
                }
                else
                {
                    float previewWidth = Mathf.Max(0f, contentRect.width - getButtonWidth - removeButtonWidth - spacing * 2f);
                    Rect previewRect = new Rect(contentRect.x, contentRect.y, previewWidth, contentRect.height);
                    Rect getRect = new Rect(previewRect.xMax + spacing, contentRect.y, getButtonWidth, EditorGUIUtility.singleLineHeight);
                    Rect disabledRect = new Rect(getRect.xMax + spacing, contentRect.y, removeButtonWidth, EditorGUIUtility.singleLineHeight);

                    var oldState = EditorGUIUtility.wideMode;
                    EditorGUIUtility.wideMode = true;
                    DrawGetFieldPreview(previewRect, fieldItem);
                    EditorGUIUtility.wideMode = oldState;

                    if (GUI.Button(getRect, "Get"))
                    {
                        Undo.RecordObject(owner.tree, $"Add new entry ({memberName}) in {node.name}");
                        AddGetEntry(node, entryListProperty, memberName, fieldItem.VariableType);
                    }

                    using (new EditorGUI.DisabledScope(true))
                    {
                        GUI.Button(disabledRect, "-");
                    }
                }
            }

            /// <summary>
            /// Compute the row height for a get-field entry.
            /// </summary>
            /// <param name="item">Field row item.</param>
            /// <param name="entryListProperty">Serialized entry list.</param>
            /// <returns>Row height in pixels.</returns>
            private float GetGetFieldRowHeight(GetFieldTreeViewItem item, SerializedProperty entryListProperty)
            {
                if (item == null)
                {
                    return EditorGUIUtility.singleLineHeight + 4f;
                }

                if (TryGetEntryProperties(entryListProperty, item.MemberInfo.Name, out var entryProperty)
                    && entryProperty?.FindPropertyRelative(nameof(FieldPointer.data))?.boxedValue is VariableBase variable)
                {
                    float height = VariableFieldDrawers.GetVariableHeight(variable, owner.tree, new[] { item.VariableType }, VariableAccessFlag.Read);
                    return Mathf.Max(EditorGUIUtility.singleLineHeight, height) + 4f;
                }

                return EditorGUIUtility.singleLineHeight + 4f;
            }

            /// <summary>
            /// Try to locate the entry property for a specific member.
            /// </summary>
            /// <param name="entryListProperty">Serialized entry list.</param>
            /// <param name="memberName">Member name.</param>
            /// <param name="entryProperty">Resolved entry property.</param>
            /// <returns>True if a matching entry is found.</returns>
            private bool TryGetEntryProperties(SerializedProperty entryListProperty, string memberName, out SerializedProperty entryProperty)
            {
                entryProperty = null;

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
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Draw the preview value for non-selected get fields.
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

            /// <summary>
            /// Add a serialized get-field entry.
            /// </summary>
            /// <param name="node">Target node.</param>
            /// <param name="entryListProperty">Entry list property.</param>
            /// <param name="memberName">Member name.</param>
            /// <param name="variableType">Variable type.</param>
            private void AddGetEntry(ObjectGetValueBase node, SerializedProperty entryListProperty, string memberName, VariableType variableType)
            {
                if (entryListProperty == null || !entryListProperty.isArray)
                {
                    node.AddPointer(memberName, variableType);
                    return;
                }

                int index = entryListProperty.arraySize;
                entryListProperty.arraySize++;
                SerializedProperty elementProperty = entryListProperty.GetArrayElementAtIndex(index);
                var nameProperty = elementProperty.FindPropertyRelative(nameof(FieldPointer.name));
                var dataProperty = elementProperty.FindPropertyRelative(nameof(FieldPointer.data));
                nameProperty.stringValue = memberName;
                //InitializeEntryData(dataProperty, variableType);

                entryListProperty.serializedObject.ApplyModifiedProperties();
                entryListProperty.serializedObject.Update();
            }

            /// <summary>
            /// Remove a serialized get-field entry.
            /// </summary>
            /// <param name="node">Target node.</param>
            /// <param name="entryListProperty">Entry list property.</param>
            /// <param name="memberName">Member name.</param>
            private void RemoveGetEntry(ObjectGetValueBase node, SerializedProperty entryListProperty, string memberName)
            {
                if (entryListProperty == null || !entryListProperty.isArray)
                {
                    node.RemoveChangeEntry(memberName);
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
    }
}
