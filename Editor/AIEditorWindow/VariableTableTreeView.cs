using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static Amlos.AI.Variables.VariableUtility;

namespace Amlos.AI.Editor
{
    internal sealed class VariableTableTreeView : TreeView
    {
        internal enum Mode
        {
            Local,
            Global,
            Mixed,
        }

        internal enum VariableSource
        {
            VariableList,
            Attribute,
        }

        internal enum VariableScope
        {
            Local,
            Static,
            Global,
            Attribute,
        }

        internal readonly struct VariableEntry
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="VariableEntry"/> struct.
            /// </summary>
            /// <param name="data">The variable data for the entry.</param>
            /// <param name="source">The source of the variable entry.</param>
            /// <param name="scope">The scope of the variable entry.</param>
            /// <returns>No return value.</returns>
            public VariableEntry(VariableData data, VariableSource source, VariableScope scope)
            {
                Data = data;
                Source = source;
                Scope = scope;
            }

            /// <summary>
            /// Gets the variable data for the entry.
            /// </summary>
            public VariableData Data { get; }

            /// <summary>
            /// Gets the source of the variable entry.
            /// </summary>
            public VariableSource Source { get; }

            /// <summary>
            /// Gets the scope of the variable entry.
            /// </summary>
            public VariableScope Scope { get; }
        }

        private sealed class VariableItem : TreeViewItem
        {
            public VariableData Data { get; set; }
            public VariableSource Source { get; set; }
            public VariableScope Scope { get; set; }
        }

        private readonly Func<Type> getTargetScriptType;
        private readonly Action<VariableData> onOpenDetail;
        private readonly Action<VariableData> onRequestRemove;
        private readonly Func<VariableData, bool> isAttributeEnabled;
        private readonly Action<VariableData, bool> setAttributeEnabled;

        private readonly List<VariableItem> rows = new();
        private Mode mode;

        private enum ColumnId
        {
            Action = 0,
            Src,
            Scope,
            Name,
            Type,
            Default,
            Static,
        }

        public VariableTableTreeView(
            TreeViewState state,
            MultiColumnHeader header,
            Func<Type> getTargetScriptType,
            Action<VariableData> onOpenDetail,
            Action<VariableData> onRequestRemove,
            Func<VariableData, bool> isAttributeEnabled,
            Action<VariableData, bool> setAttributeEnabled
        ) : base(state, header)
        {
            this.getTargetScriptType = getTargetScriptType;
            this.onOpenDetail = onOpenDetail;
            this.onRequestRemove = onRequestRemove;
            this.isAttributeEnabled = isAttributeEnabled;
            this.setAttributeEnabled = setAttributeEnabled;

            showBorder = true;
            showAlternatingRowBackgrounds = true;
            rowHeight = EditorGUIUtility.singleLineHeight + 6f;
        }

        /// <summary>
        /// Sets the entries used to populate the tree view.
        /// </summary>
        /// <param name="entries">The entries to display in the tree view.</param>
        /// <param name="mode">The display mode for the tree view.</param>
        /// <returns>No return value.</returns>
        internal void SetData(IReadOnlyList<VariableEntry> entries, Mode mode)
        {
            this.mode = mode;
            rows.Clear();

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    VariableEntry entry = entries[i];
                    if (entry.Data == null)
                    {
                        continue;
                    }

                    rows.Add(new VariableItem
                    {
                        Data = entry.Data,
                        Source = entry.Source,
                        Scope = entry.Scope,
                        displayName = entry.Data.name,
                    });
                }
            }

            Reload();
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { id = -1, depth = -1, displayName = "Root" };

            if (rows.Count == 0)
            {
                // Add a dummy item when there are no variables to prevent TreeView errors
                root.AddChild(new TreeViewItem { id = 0, depth = 0, displayName = string.Empty });
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    rows[i].id = i;
                    rows[i].depth = 0;
                    root.AddChild(rows[i]);
                }
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item is not VariableItem item || item.Data == null)
            {
                base.RowGUI(args);
                return;
            }

            VariableData variable = item.Data;

            for (int i = 0; i < args.GetNumVisibleColumns(); i++)
            {
                ColumnId col = (ColumnId)args.GetColumn(i);
                Rect rect = args.GetCellRect(i);
                CenterRectUsingSingleLineHeight(ref rect);
                DrawCell(rect, variable, item.Source, item.Scope, col);
            }
        }

        private void DrawCell(Rect rect, VariableData variable, VariableSource source, VariableScope scope, ColumnId column)
        {
            switch (column)
            {
                case ColumnId.Action:
                    DrawActionCell(rect, variable, source);
                    break;
                case ColumnId.Src:
                    DrawSourceCell(rect, source);
                    break;
                case ColumnId.Scope:
                    DrawScopeCell(rect, scope);
                    break;
                case ColumnId.Name:
                    DrawNameCell(rect, variable, source);
                    break;
                case ColumnId.Type:
                    DrawTypeCell(rect, variable, source, scope);
                    break;
                case ColumnId.Default:
                    DrawDefaultCell(rect, variable, source);
                    break;
                case ColumnId.Static:
                    DrawStaticCell(rect, variable, scope);
                    break;
                default:
                    break;
            }
        }

        private void DrawActionCell(Rect rect, VariableData variable, VariableSource source)
        {
            if (source == VariableSource.Attribute)
            {
                bool enabled = isAttributeEnabled?.Invoke(variable) ?? false;
                EditorGUI.BeginChangeCheck();
                bool nowEnabled = EditorGUI.Toggle(rect, enabled);
                if (EditorGUI.EndChangeCheck())
                {
                    setAttributeEnabled?.Invoke(variable, nowEnabled);
                }
                return;
            }

            if (GUI.Button(rect, "x"))
            {
                onRequestRemove?.Invoke(variable);
            }
        }

        private static void DrawSourceCell(Rect rect, VariableSource source)
        {
            EditorGUI.LabelField(rect, source == VariableSource.Attribute ? "Attr" : "Var");
        }

        /// <summary>
        /// Draws the variable scope cell for the current row.
        /// </summary>
        /// <param name="rect">The rectangle used to draw the cell.</param>
        /// <param name="variable">The variable displayed in the row.</param>
        /// <param name="source">The source of the variable entry.</param>
        /// <returns>No return value.</returns>
        private static void DrawScopeCell(Rect rect, VariableScope scope)
        {
            string scopeLabel = scope switch
            {
                VariableScope.Local => "Local",
                VariableScope.Static => "Static",
                VariableScope.Global => "Global",
                VariableScope.Attribute => "Attribute",
                _ => string.Empty,
            };
            EditorGUI.LabelField(rect, scopeLabel);
        }

        private void DrawNameCell(Rect rect, VariableData variable, VariableSource source)
        {
            if (source == VariableSource.Attribute)
            {
                if (GUI.Button(rect, variable.name))
                {
                    onOpenDetail?.Invoke(variable);
                }
                return;
            }

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.TextField(rect, variable.name);
            if (EditorGUI.EndChangeCheck())
            {
                variable.name = newName;
            }
        }

        private void DrawTypeCell(Rect rect, VariableData variable, VariableSource source, VariableScope scope)
        {
            if (source == VariableSource.Attribute)
            {
                EditorGUI.LabelField(rect, variable.Type.ToString());
                return;
            }

            Type targetType = getTargetScriptType?.Invoke();
            VariableType typeValue = scope == VariableScope.Global || mode == Mode.Global
                ? variable.Type
                : (GetVariableType(variable, targetType) ?? VariableType.Invalid);

            using (GUIEnable.By(!variable.IsScript))
            {
                EditorGUI.BeginChangeCheck();
                var newType = (VariableType)EditorGUI.EnumPopup(rect, typeValue);
                if (EditorGUI.EndChangeCheck())
                {
                    variable.SetType(newType);
                }
            }
        }

        private void DrawDefaultCell(Rect rect, VariableData variable, VariableSource source)
        {
            if (source == VariableSource.Attribute)
            {
                EditorGUI.LabelField(rect, "(From Attribute)");
                return;
            }

            if (variable.IsScript)
            {
                EditorGUI.LabelField(rect, "(From Script)");
                return;
            }

            switch (variable.Type)
            {
                case VariableType.String:
                    EditorGUI.BeginChangeCheck();
                    string s = EditorGUI.TextField(rect, variable.DefaultValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        variable.DefaultValue = s;
                    }
                    break;
                case VariableType.Int:
                    {
                        bool ok = int.TryParse(variable.DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out int v);
                        if (!ok) v = 0;
                        EditorGUI.BeginChangeCheck();
                        int nv = EditorGUI.IntField(rect, v);
                        if (EditorGUI.EndChangeCheck())
                        {
                            variable.DefaultValue = nv.ToString(CultureInfo.InvariantCulture);
                        }
                        break;
                    }
                case VariableType.Float:
                    {
                        bool ok = float.TryParse(variable.DefaultValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
                        if (!ok) v = 0;
                        EditorGUI.BeginChangeCheck();
                        float nv = EditorGUI.FloatField(rect, v);
                        if (EditorGUI.EndChangeCheck())
                        {
                            variable.DefaultValue = nv.ToString(CultureInfo.InvariantCulture);
                        }
                        break;
                    }
                case VariableType.Bool:
                    {
                        bool ok = bool.TryParse(variable.DefaultValue, out bool v);
                        if (!ok) v = false;
                        EditorGUI.BeginChangeCheck();
                        bool nv = EditorGUI.Toggle(rect, v);
                        if (EditorGUI.EndChangeCheck())
                        {
                            variable.DefaultValue = nv.ToString();
                        }
                        break;
                    }
                case VariableType.Vector2:
                    {
                        bool ok = VectorUtility.TryParseVector2(variable.DefaultValue, out Vector2 v);
                        if (!ok) v = default;
                        EditorGUI.BeginChangeCheck();
                        Vector2 nv = EditorGUI.Vector2Field(rect, GUIContent.none, v);
                        if (EditorGUI.EndChangeCheck())
                        {
                            variable.DefaultValue = nv.ToString();
                        }
                        break;
                    }
                case VariableType.Vector3:
                    {
                        bool ok = VectorUtility.TryParseVector3(variable.DefaultValue, out Vector3 v);
                        if (!ok) v = default;
                        EditorGUI.BeginChangeCheck();
                        Vector3 nv = EditorGUI.Vector3Field(rect, GUIContent.none, v);
                        if (EditorGUI.EndChangeCheck())
                        {
                            variable.DefaultValue = nv.ToString();
                        }
                        break;
                    }
                case VariableType.UnityObject:
                    if (variable.ObjectType is null) variable.SetBaseType(typeof(UnityEngine.Object));
                    EditorGUI.LabelField(rect, variable.ObjectType.FullName);
                    break;
                case VariableType.Generic:
                    if (variable.ObjectType is null) variable.SetBaseType(typeof(object));
                    EditorGUI.LabelField(rect, variable.ObjectType.FullName);
                    break;
                case VariableType.Invalid:
                    EditorGUI.LabelField(rect, "Invalid Variable Type");
                    break;
                default:
                    EditorGUI.LabelField(rect, string.Empty);
                    break;
            }
        }

        private void DrawStaticCell(Rect rect, VariableData variable, VariableScope scope)
        {
            if (mode == Mode.Global || scope == VariableScope.Global || scope == VariableScope.Attribute)
            {
                EditorGUI.LabelField(rect, "-");
                return;
            }

            if (variable.IsScript)
            {
                EditorGUI.LabelField(rect, "-");
                return;
            }

            EditorGUI.BeginChangeCheck();
            bool nv = EditorGUI.Toggle(rect, variable.IsStatic);
            if (EditorGUI.EndChangeCheck())
            {
                variable.IsStatic = nv;
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            if (FindItem(id, rootItem) is VariableItem item && item.Data != null)
            {
                onOpenDetail?.Invoke(item.Data);
            }
        }

        internal static MultiColumnHeader CreateHeader(Mode mode)
        {
            var columns = new List<MultiColumnHeaderState.Column>();

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent(string.Empty),
                width = 28,
                minWidth = 28,
                autoResize = false,
            });

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Src", "Source of the variable"),
                width = 40,
                minWidth = 40,
                autoResize = false,
            });

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Scope", "Scope of the variable"),
                width = 80,
                minWidth = 70,
                autoResize = false,
            });

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Name", "The Name of the variable"),
                width = 220,
                minWidth = 120,
                autoResize = true,
            });

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Type", "The Type of the variable"),
                width = 120,
                minWidth = 90,
                autoResize = true,
            });

            columns.Add(new MultiColumnHeaderState.Column
            {
                headerContent = new GUIContent("Default", "The default value of the variable"),
                width = 240,
                minWidth = 160,
                autoResize = true,
            });

            if (mode == Mode.Local || mode == Mode.Mixed)
            {
                columns.Add(new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Static", "A static variable shares in all instance of this behaviour tree"),
                    width = 60,
                    minWidth = 50,
                    autoResize = false,
                });
            }

            var headerState = new MultiColumnHeaderState(columns.ToArray());
            return new MultiColumnHeader(headerState) { height = 22f };
        }
    }
}
