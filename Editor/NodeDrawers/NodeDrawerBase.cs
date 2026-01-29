using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Base class of node drawer
    /// </summary>
    public abstract class NodeDrawerBase
    {
        public Dictionary<(UnityEngine.Object, string), ReorderableList> listDrawers = new();
        private TypeReferenceDrawer TypeDrawer;
        private Vector2 listScrollView;


        public TreeNode node { get; private set; }
        public SerializedProperty property { get; private set; }
        public AIEditorWindow editor { get; private set; }


        /// <summary> The behaviour tree data </summary>
        protected BehaviourTreeData tree => property.serializedObject.targetObject as BehaviourTreeData;
        /// <summary> Whether to use SerializedProperty-based drawing (recommended) </summary>
        protected bool UsePropertyDrawer => editor.editorSetting.useSerializationPropertyDrawer;


        public NodeDrawerBase() { }
        public NodeDrawerBase(AIEditorWindow editor, TreeNode node)
        {
            this.editor = editor;
            this.node = node;
        }




        public void Init(AIEditorWindow editor, SerializedProperty serializedProperty)
        {
            this.editor = editor;
            this.property = serializedProperty;
            if (property != null)
            {
                node = property.GetValue() as TreeNode;
            }
        }

        /// <summary>
        /// Call when AI editor is drawing the node
        /// </summary>
        public abstract void Draw();



        /// <summary>
        /// Draw base node info
        /// </summary>
        public void DrawNodeBaseInfo() => NodeDrawerUtility.DrawNodeBaseInfo(tree, node);





        #region Field Drawing - Legacy (Deprecated)

        /// <summary>
        /// Draw a field (Legacy method - use DrawProperty instead)
        /// </summary>
        /// <param name="labelName">label of the field</param>
        /// <param name="field">field info</param>
        /// <param name="target">target of the field</param>
        [Obsolete("Use DrawProperty with SerializedProperty instead for proper undo/redo support")]
        protected void DrawField(string labelName, FieldInfo field, TreeNode target) => DrawField(new GUIContent(labelName), field, target);

        /// <summary>
        /// Draw a field (Legacy method - use DrawProperty instead)
        /// </summary>
        /// <param name="label">label of the field</param>
        /// <param name="field">field info</param>
        /// <param name="target">target of the field</param>
        [Obsolete("Use DrawProperty with SerializedProperty instead for proper undo/redo support")]
        protected void DrawField(GUIContent label, FieldInfo field, TreeNode target)
        {
            Type fieldType = field.FieldType;

            //Null Determine
            if (fieldType.IsClass && field.GetValue(target) is null)
            {
                try
                {
                    field.SetValue(target, Activator.CreateInstance(fieldType));
                }
                catch (Exception)
                {
                    field.SetValue(target, default);
                    Debug.LogWarning("Field " + field.Name + " has not initialized yet. Provide this information if there are bugs");
                }
            }

            //special case
            if (!DrawSpecialField_Legacy(label, field, target))
            {
                EditorFieldDrawers.DrawField(tree, label, field, target);
            }
        }

        private bool DrawSpecialField_Legacy(GUIContent label, FieldInfo field, TreeNode target)
        {
            object value = field.GetValue(target);
            Type fieldType = field.FieldType;
            if (value is VariableBase variableFieldBase)
            {
                var possibleType = variableFieldBase.GetVariableTypes(field);
                var access = variableFieldBase.GetAccessFlag(field);
                DrawVariable(label, variableFieldBase, possibleType, access);
                return true;
            }
            else if (value is NodeReference rawReference)
            {
                DrawNodeReference(label, rawReference);
                return true;
            }
            else if (value is RawNodeReference reference)
            {
                DrawNodeReference(label, reference);
                return true;
            }
            else if (value is TypeReference typeReference)
            {
                DrawTypeReference(label, typeReference);
                return true;
            }
            else if (value is List<NodeReference>)
            {
                var list = (List<NodeReference>)value;
                DrawNodeList(label, list, target);
                return true;
            }
            else if (value is List<RawNodeReference>)
            {
                var list = (List<RawNodeReference>)value;
                DrawNodeList(label, list, target);
                return true;
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = value as IList;
                DrawList(label, list);
                return true;
            }
            else if (CustomAIFieldDrawerAttribute.IsDrawerDefined(fieldType))
            {
                float height = CustomAIFieldDrawerAttribute.GetDrawerHeight(value, tree);
                Rect rect = EditorGUILayout.GetControlRect(true, height);
                CustomAIFieldDrawerAttribute.TryInvoke(out object result, rect, label, value, tree);
                field.SetValue(target, result);
                return true;
            }
            return false;
        }

        #endregion




        #region Property Drawing - Recommended

        /// <summary>
        /// Draw a field using SerializedProperty (Recommended for undo/redo support)
        /// </summary>
        /// <param name="property">serialized property</param>
        /// 
        /// 
        protected void DrawProperty(SerializedProperty property) => DrawProperty(new GUIContent(property.displayName), property);

        /// <summary>
        /// Draw a field using SerializedProperty (Recommended for undo/redo support)
        /// </summary>
        /// <param name="label">label of the field</param>
        /// <param name="property">serialized property</param>
        /// 
        /// 
        protected void DrawProperty(GUIContent label, SerializedProperty property)
        {
            // Handle arrays/lists with ReorderableList
            if (property.isArray)
            {
                DrawPropertyArray(label, property);
                return;
            }

            EditorGUILayout.PropertyField(property, label, true);

            // Apply changes
            if (property.serializedObject.hasModifiedProperties)
            {
                property.serializedObject.ApplyModifiedProperties();
            }
            // Update to reflect any changes
            property.serializedObject.Update();
        }

        /// <summary>
        /// Draw array property with ReorderableList
        /// </summary>
        private void DrawPropertyArray(GUIContent label, SerializedProperty property)
        {
            if (!listDrawers.TryGetValue((property.serializedObject.targetObject, property.propertyPath), out var rl))
            {
                rl = new ReorderableList(property.serializedObject, property);
                BuildGenericReorderableList(rl);
                listDrawers.Add((property.serializedObject.targetObject, property.propertyPath), rl);
            }
            rl.drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, label);
            rl.serializedProperty = property;
            var height = rl.GetHeight();
            var rect = EditorGUILayout.GetControlRect(false, height);
            rect = EditorGUI.IndentedRect(rect);
            rl.DoList(rect);
        }

        private static void BuildGenericReorderableList(ReorderableList rl)
        {
            rl.onAddCallback += (l) =>
            {
                l.serializedProperty.InsertArrayElementAtIndex(l.serializedProperty.arraySize);
                l.serializedProperty.serializedObject.ApplyModifiedProperties();
                l.serializedProperty.serializedObject.Update();
            };
            rl.onRemoveCallback += (l) =>
            {
                l.serializedProperty.DeleteArrayElementAtIndex(l.serializedProperty.arraySize - 1);
                l.serializedProperty.serializedObject.ApplyModifiedProperties();
                l.serializedProperty.serializedObject.Update();
            };
            rl.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                var element = rl.serializedProperty.GetArrayElementAtIndex(index);
                EditorFieldDrawers.PropertyField(rect, element, new GUIContent("Element " + index));
                element.serializedObject.ApplyModifiedProperties();
                element.serializedObject.Update();
            };
            rl.onReorderCallbackWithDetails = (ReorderableList l, int oldIndex, int newIndex) =>
            {
                l.serializedProperty.serializedObject.Update();
                l.serializedProperty.MoveArrayElement(oldIndex, newIndex);
                l.serializedProperty.serializedObject.ApplyModifiedProperties();
                l.serializedProperty.serializedObject.Update();
            };
        }

        #endregion


        /// <summary>
        /// Draw a type reference
        /// </summary>
        /// <remarks>
        /// This method is performance inefficient if calling this multiple times in single Node Drawer
        /// </remarks>
        /// <param name="label"></param>
        /// <param name="typeReference"></param>s
        /// <returns></returns>
        [Obsolete]
        public void DrawTypeReference(GUIContent label, TypeReference typeReference)
        {
            var typeDrawer = new TypeReferenceDrawer(typeReference, label);
            DrawTypeReference(label, typeReference, ref typeDrawer);
        }


        /// <summary>
        /// Draw a type reference and return the drawer object
        /// </summary>
        /// <param name="label"></param>
        /// <param name="typeReference"></param>
        /// <param name="typeDrawer"></param>
        /// <returns></returns>
        internal void DrawTypeReference(GUIContent label, TypeReference typeReference, ref TypeReferenceDrawer typeDrawer)
        {
            typeDrawer ??= new TypeReferenceDrawer(typeReference, label);
            EditorGUI.BeginChangeCheck();
            typeDrawer.Draw();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tree, $"Change type reference {label.text}");
            }
        }


        #region Node Reference Drawing - Property-based

        /// <summary>
        /// Draw a node reference using SerializedProperty
        /// </summary>
        private void DrawNodeReference_Property(GUIContent label, SerializedProperty property, INodeReference reference, TreeNode target)
        {
            reference.Node = tree.GetNode(reference.UUID);
            TreeNode referencingNode = reference.Node;
            string nodeName = referencingNode?.name ?? string.Empty;

            using var scope = new GUILayout.HorizontalScope();

            label.text = string.Concat(label.text, ": ", nodeName);
            EditorGUILayout.LabelField(label);
            using var indent = EditorGUIIndent.Increase;

            // no selection
            if (referencingNode is null)
            {
                if (GUILayout.Button("Select.."))
                {
                    if (Event.current.button == 0)
                    {
                        editor.OpenSelectionWindow(RightWindow.All, (selectedNode) =>
                        {
                            property.serializedObject.Update();
                            SetNodeReference(reference, selectedNode, target);
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                        }, reference.IsRawReference);
                    }
                    else if (!reference.IsRawReference)
                    {
                        GenericMenu menu = new();
                        if (editor.clipboard.HasContent)
                            menu.AddItem(new GUIContent("Paste"), false, () =>
                            {
                                property.serializedObject.Update();
                                editor.clipboard.PasteTo(editor.tree, target, reference);
                                property.serializedObject.ApplyModifiedProperties();
                                property.serializedObject.Update();
                            });
                        else menu.AddDisabledItem(new GUIContent("Paste"));
                        menu.ShowAsContext();
                    }
                }
            }
            // has reference
            else
            {
                scope.Dispose();
                using (new GUILayout.HorizontalScope())
                {
                    DrawNodeReferenceModify_Property(property, reference, referencingNode, target);
                    var oldIndent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 1;
                    NodeDrawerUtility.DrawNodeBaseInfo(tree, referencingNode);
                    EditorGUI.indentLevel = oldIndent;
                }
            }
        }

        private void DrawNodeReferenceModify_Property(SerializedProperty property, INodeReference reference, TreeNode node, TreeNode target)
        {
            using (new GUILayout.HorizontalScope(GUILayout.Width(80)))
            {
                GUILayout.Space(EditorGUI.indentLevel * 16);
                using (new GUILayout.VerticalScope(GUILayout.Width(80)))
                {
                    if (GUILayout.Button("Open"))
                    {
                        editor.SelectedNode = node;
                    }
                    else if (GUILayout.Button("Replace"))
                    {
                        editor.OpenSelectionWindow(RightWindow.All, (newNode) =>
                        {
                            property.serializedObject.Update();
                            SetNodeReference(reference, newNode, target);
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                        }, reference.IsRawReference);
                    }
                    else if (GUILayout.Button("Delete"))
                    {
                        DeleteReference(() =>
                        {
                            property.serializedObject.Update();
                            TreeNode oldRef = reference.Node ?? tree.GetNode(reference.UUID);
                            reference.Set(null);
                            property.serializedObject.ApplyModifiedProperties();
                            property.serializedObject.Update();
                            return oldRef;
                        });
                    }
                }
            }
        }

        private void SetNodeReference(INodeReference reference, TreeNode newNode, TreeNode target)
        {
            if (reference is NodeReference nodeRef)
            {
                var old = tree.GetNode(nodeRef);
                if (old != null) old.parent.UUID = UUID.Empty;

                nodeRef.Node = newNode;
                if (newNode is not null)
                {
                    nodeRef.UUID = newNode.uuid;
                    newNode.parent = target;
                }
                else
                {
                    nodeRef.UUID = UUID.Empty;
                }
            }
            else if (reference is RawNodeReference rawRef)
            {
                rawRef.Set(newNode);
            }
        }

        #endregion




        #region Node Reference Drawing - Legacy

        /// <summary>
        /// Draw a node reference (Legacy)
        /// </summary>
        [Obsolete("Use DrawNodeReference_Property instead")]
        public void DrawNodeReference(string labelName, RawNodeReference reference) => DrawNodeReference(new GUIContent(labelName), reference);

        /// <summary>
        /// Draw a node reference (Legacy)
        /// </summary>
        [Obsolete("Use DrawNodeReference_Property instead")]
        public void DrawNodeReference(GUIContent label, RawNodeReference reference)
        {
            DrawNodeReference_Legacy(label, reference,
            (TreeNode n) =>
            {
                Undo.RecordObject(tree, n is null ? $"Clear reference on {tree}" : $"Assign node to field on {tree}");
                ((INodeReference)reference).Set(n);
            });
        }

        /// <summary>
        /// Draw a node reference (Legacy)
        /// </summary>
        [Obsolete("Use DrawNodeReference_Property instead")]
        public void DrawNodeReference(string labelName, NodeReference reference) => DrawNodeReference(new GUIContent(labelName), reference);

        /// <summary>
        /// Draw a node reference (Legacy)
        /// </summary>
        [Obsolete("Use DrawNodeReference_Property instead")]
        public void DrawNodeReference(GUIContent label, NodeReference reference)
        {
            DrawNodeReference_Legacy(label, reference,
            (TreeNode n) =>
            {
                Undo.RecordObject(tree, n is null ? $"Clear reference on {tree}" : $"Assign node to field on {tree}");

                var old = tree.GetNode(reference);
                if (old != null) old.parent.UUID = UUID.Empty;

                reference.Node = n;
                if (n is not null)
                {
                    reference.UUID = n.uuid;
                    n.parent = node;
                }
                else
                {
                    reference.UUID = UUID.Empty;
                }
            });
        }

        private void DrawNodeReference_Legacy(GUIContent label, INodeReference reference, SelectNodeEvent selectNodeEvent)
        {
            reference.Node = tree.GetNode(reference.UUID);
            TreeNode referencingNode = reference.Node;
            string nodeName = referencingNode?.name ?? string.Empty;

            using var scope = new GUILayout.HorizontalScope();

            label.text = string.Concat(label.text, ": ", nodeName);
            EditorGUILayout.LabelField(label);
            using var indent = EditorGUIIndent.Increase;

            // no selection
            if (referencingNode is null)
            {
                if (GUILayout.Button("Select.."))
                {
                    if (Event.current.button == 0)
                    {
                        editor.OpenSelectionWindow(RightWindow.All, selectNodeEvent, reference.IsRawReference);
                    }
                    else if (!reference.IsRawReference)
                    {
                        GenericMenu menu = new();

                        if (editor.clipboard.HasContent) menu.AddItem(new GUIContent("Paste"), false, () => editor.clipboard.PasteTo(editor.tree, node, reference));
                        else menu.AddDisabledItem(new GUIContent("Paste"));

                        menu.ShowAsContext();
                    }
                }
            }
            // has reference
            else
            {
                scope.Dispose();
                using (new GUILayout.HorizontalScope())
                {
                    DrawNodeReferenceModify(reference, referencingNode);
                    var oldIndent = EditorGUI.indentLevel;
                    EditorGUI.indentLevel = 1;
                    NodeDrawerUtility.DrawNodeBaseInfo(tree, referencingNode);
                    EditorGUI.indentLevel = oldIndent;
                }
            }
        }

        private void DrawNodeReferenceModify(INodeReference reference, TreeNode node)
        {
            using (new GUILayout.HorizontalScope(GUILayout.Width(80)))
            {
                GUILayout.Space(EditorGUI.indentLevel * 16);
                using (new GUILayout.VerticalScope(GUILayout.Width(80)))
                {
                    if (GUILayout.Button("Open"))
                    {
                        Debug.Log("Open");
                        editor.SelectedNode = node;
                    }
                    else if (GUILayout.Button("Replace"))
                    {
                        editor.OpenSelectionWindow(RightWindow.All, (newNode) => ReplaceNodeReference(reference, newNode), reference.IsRawReference);
                    }
                    else if (GUILayout.Button("Delete"))
                    {
                        DeleteReference(DeleteNodeReference);
                    }
                }
            }

            TreeNode DeleteNodeReference()
            {
                TreeNode oldRef = reference.Node ?? tree.GetNode(reference.UUID);
                reference.Set(null);
                return oldRef;
            }
        }

        #endregion

        /// <summary>
        /// Draw variable field, same as <seealso cref="VariableFieldDrawers.DrawVariable(string, VariableBase, BehaviourTreeData, VariableType[], VariableAccessFlag)"/>
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public bool DrawVariable(string labelName, VariableBase variable, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
            => VariableFieldDrawers.DrawVariable(labelName, variable, tree, possibleTypes, variableAccessFlag);
        /// <summary>
        /// Draw variable field, same as <seealso cref="VariableFieldDrawers.DrawVariable(GUIContent, VariableBase, BehaviourTreeData, VariableType[], VariableAccessFlag)"/>
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public bool DrawVariable(GUIContent label, VariableBase variable, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
            => VariableFieldDrawers.DrawVariable(label, variable, tree, possibleTypes, variableAccessFlag);




        protected void DrawNodeList(string labelName, List<RawNodeReference> list, TreeNode node) => DrawNodeList(new GUIContent(labelName), list, node);
        protected void DrawNodeList(GUIContent label, List<RawNodeReference> list, TreeNode node)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                RawNodeReference item = list[i];
                var childNode = tree.GetNode(item.UUID);


                Color color = i % 2 == 0 ? Color.white * (80 / 255f) : Color.white * (64 / 255f);
                var colorStyle = SetRegionColor(color, out var baseColor);
                GUILayout.BeginHorizontal(colorStyle);
                GUI.backgroundColor = baseColor;


                DrawNodeListItemCommonModify(list, i);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                if (childNode == null)
                {
                    var currentColor = GUI.contentColor;
                    GUI.contentColor = Color.red;
                    GUILayout.Label("Node not found: " + item);
                    GUI.contentColor = currentColor;
                }
                else
                {
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField(NodeDrawerUtility.GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    NodeDrawerUtility.DrawNodeBaseInfo(tree, childNode);
                    EditorGUI.indentLevel--;
                    GUILayout.EndVertical();
                }
                EditorGUI.indentLevel = oldIndent;
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 16);
            //GUILayout.BeginVertical();
            if (GUILayout.Button("Add"))
            {
                editor.OpenSelectionWindow(RightWindow.All, (n) =>
                {
                    list.Add(n.ToRawReference());
                }, true);
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            //GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        protected void DrawNodeList(string labelName, List<NodeReference> list, TreeNode node) => DrawNodeList(new GUIContent(labelName), list, node);
        protected void DrawNodeList(GUIContent label, List<NodeReference> list, TreeNode node)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            listScrollView = GUILayout.BeginScrollView(listScrollView);
            for (int i = 0; i < list.Count; i++)
            {
                NodeReference item = list[i];
                var childNode = tree.GetNode(item.UUID);

                Color color = i % 2 == 0 ? Color.white * (80 / 255f) : Color.white * (64 / 255f);
                var colorStyle = SetRegionColor(color, out var baseColor);
                GUILayout.BeginHorizontal(colorStyle);
                GUI.backgroundColor = baseColor;

                DrawNodeListItemCommonModify(list, i);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                if (childNode == null)
                {
                    var currentColor = GUI.contentColor;
                    GUI.contentColor = Color.red;
                    GUILayout.Label("Node not found: " + item);
                    GUI.contentColor = currentColor;
                }
                else
                {
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField(NodeDrawerUtility.GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    NodeDrawerUtility.DrawNodeBaseInfo(tree, childNode);
                    EditorGUI.indentLevel--;
                    GUILayout.EndVertical();
                }
                EditorGUI.indentLevel = oldIndent;
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            GUILayout.EndScrollView();
            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 16);
            //GUILayout.BeginVertical();
            if (GUILayout.Button("Add"))
            {
                if (Event.current.button == 0)
                {
                    OpenEditorSelectWindow(list, node);
                }
                else
                {
                    GenericMenu menu = new();
                    menu.AddItem(new GUIContent("Add"), false, () => OpenEditorSelectWindow(list, node));
                    var slot = node.GetListSlot();
                    if (slot is not null)
                    {
                        menu.AddItem(new GUIContent("Paste Under (at first)"), false, () => editor.clipboard.PasteAsFirst(editor.tree, node, slot));
                        menu.AddItem(new GUIContent("Paste Under (at last)"), false, () => editor.clipboard.PasteAsLast(editor.tree, node, slot));
                    }
                    menu.ShowAsContext();
                }
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    var nr = list[^1];
                    list.RemoveAt(list.Count - 1);
                    var removed = tree.GetNode(nr);
                    removed.parent = null;
                }
            //GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            void OpenEditorSelectWindow(List<NodeReference> list, TreeNode node)
            {
                editor.OpenSelectionWindow(RightWindow.All, (n) =>
                {
                    list.Add(n);
                    n.parent = node;
                });
            }
        }

        protected ReorderableList DrawNodeList<T>(string labelName, SerializedProperty list, TreeNode node) where T : INodeReference, new() => DrawNodeList<T>(new GUIContent(labelName), list, node);
        protected ReorderableList DrawNodeList<T>(GUIContent label, SerializedProperty list, TreeNode node) where T : INodeReference, new()
        {
            ReorderableList reorderableList = new ReorderableList(list.serializedObject, list, true, true, true, true);
            reorderableList.elementHeightCallback = GetHeight;
            reorderableList.drawElementCallback = DrawElement;
            reorderableList.onReorderCallbackWithDetails = ReorderCallbackDelegateWithDetails;
            reorderableList.drawHeaderCallback = DrawHeader;
            reorderableList.onAddCallback = AddNode;
            reorderableList.onRemoveCallback = RemoveLast;

            return reorderableList;

            void DrawElement(Rect position, int index, bool isActive, bool isFocused)
            {
                reorderableList.serializedProperty.serializedObject.Update();

                SerializedProperty referenceProperty = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                INodeReference reference = (INodeReference)referenceProperty.GetValue();
                TreeNode node = editor.tree.GetNode(reference.UUID);
                SerializedProperty nodeProperty = tree.GetNodeProperty(node);


                position.y += 2f;
                Rect singleLine = position;
                singleLine.height = EditorGUIUtility.singleLineHeight;
                Rect singleButtom = singleLine;
                singleLine.x += 100;
                singleButtom.width = 80;

                if (node == null)
                {
                    EditorGUI.LabelField(singleLine, "Outdated");
                    return;
                }

                // tools
                string label = null;
                switch (node)
                {
                    case Nodes.Action:
                        label = "Action";
                        break;
                    case Call:
                        label = "Call";
                        break;
                    case Flow:
                        label = "Flow";
                        break;
                    case Arithmetic:
                        label = "Math";
                        break;
                    case DetermineBase:
                        label = "Determine";
                        break;
                }
                GUI.Label(singleButtom, label);
                singleButtom.y += (EditorGUIUtility.singleLineHeight + 2f);
                if (GUI.Button(singleButtom, "Open"))
                {
                    TreeNode nodeElement = GetTreeNodeFromElement(reference);
                    editor.SelectedNode = nodeElement;
                    return;
                }
                singleButtom.y += (EditorGUIUtility.singleLineHeight + 2f);
                if (GUI.Button(singleButtom, "Delete"))
                {
                    Remove(list, index);
                    return;
                }

                // fields
                using (GUIEnable.By(false))
                {
                    var script = MonoScriptCache.Get(node.GetType());
                    EditorGUI.ObjectField(singleLine, "Script", script, typeof(MonoScript), false);
                }
                singleLine.y += (EditorGUIUtility.singleLineHeight + 2f);
                if (nodeProperty == null)
                {
                    EditorGUI.LabelField(singleLine, "Outdated");
                    return;
                }

                SerializedProperty nameProperty = nodeProperty.FindPropertyRelative(nameof(TreeNode.name));
                EditorGUI.PropertyField(singleLine, nameProperty);
                if (reference is Probability.EventWeight w)
                {
                    singleLine.y += (EditorGUIUtility.singleLineHeight + 2f);
                    EditorGUI.PropertyField(singleLine, referenceProperty.FindPropertyRelative(nameof(Probability.EventWeight.weight)));
                }
                if (reference is PseudoProbability.EventWeight pw)
                {
                    singleLine.y += (EditorGUIUtility.singleLineHeight + 2f);
                    SerializedProperty serializedProperty = referenceProperty.FindPropertyRelative(nameof(PseudoProbability.EventWeight.weight));
                    VariableBase variable = pw.weight;
                    using (GUIEnable.By(false))
                    {
                        GUIContent weightLabel = variable.HasEditorReference ? new GUIContent("Weight (Default)") : new GUIContent("Weight");
                        EditorGUI.IntField(singleLine, weightLabel, GetCurrentWeight(pw.weight));
                    }
                    DrawVariable(new GUIContent(nameProperty.stringValue), variable, new VariableType[] { VariableType.Int }, VariableAccessFlag.Read);
                    referenceProperty.boxedValue = pw;
                    referenceProperty.serializedObject.ApplyModifiedProperties();
                    //serializedProperty.boxedValue = variable;
                }
                if (NodeDrawerUtility.showUUID)
                {
                    singleLine.y += (EditorGUIUtility.singleLineHeight + 2f);
                    EditorGUI.LabelField(singleLine, "UUID", node.uuid);
                }
                //if (nodeProperty.serializedObject.hasModifiedProperties)
                //{
                //    nodeProperty.serializedObject.ApplyModifiedProperties();
                //    nodeProperty.serializedObject.Update();
                //}
            }

            int GetCurrentWeight(VariableField<int> weight)
            {
                if (weight.IsConstant) return weight.Constant;
                if (weight.HasEditorReference)
                {
                    var data = tree.GetVariable(weight.UUID);
                    if (data != null && int.TryParse(data.DefaultValue, out var i)) return i;
                }
                return 0;
            }

            void ReorderCallbackDelegateWithDetails(ReorderableList rl, int oldIndex, int newIndex)
            {
                rl.serializedProperty.serializedObject.Update();

                rl.serializedProperty.MoveArrayElement(oldIndex, newIndex);
                rl.serializedProperty.serializedObject.ApplyModifiedProperties();
                rl.serializedProperty.serializedObject.Update();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void DrawHeader(Rect rect)
            {
                EditorGUI.LabelField(rect, label);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void Remove(SerializedProperty list, int index)
            {
                DeleteReference(() => RemoveFromList(list, index));
            }

            void AddNode(ReorderableList list)
            {
                if (Event.current.button == 0)
                {
                    OpenEditorSelectWindow(list, node);
                }
                else
                {
                    GenericMenu menu = new();
                    menu.AddItem(new GUIContent("Add"), false, () => OpenEditorSelectWindow(list, node));
                    var slot = node.GetListSlot();
                    if (slot is not null)
                    {
                        menu.AddItem(new GUIContent("Paste Under (at first)"), false, () => editor.clipboard.PasteAsFirst(editor.tree, node, slot));
                        menu.AddItem(new GUIContent("Paste Under (at last)"), false, () => editor.clipboard.PasteAsLast(editor.tree, node, slot));
                    }
                    menu.ShowAsContext();
                }
            }

            void OpenEditorSelectWindow(ReorderableList list, TreeNode node)
            {
                editor.OpenSelectionWindow(RightWindow.All, (newNode) =>
                {
                    var newRef = new T { UUID = newNode.uuid };
                    list.serializedProperty.serializedObject.Update();
                    list.serializedProperty.InsertArrayElementAtIndex(list.serializedProperty.arraySize);
                    list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1).boxedValue = newRef;
                    list.serializedProperty.serializedObject.ApplyModifiedProperties();
                    newNode.parent = node;
                    list.serializedProperty.serializedObject.Update();
                });
            }

            void RemoveLast(ReorderableList list)
            {
                int index = list.selectedIndices.Count > 0 ? list.selectedIndices.First() : (list.serializedProperty.arraySize - 1);
                Remove(list.serializedProperty, index);
            }

            float GetHeight(int index)
            {
                var p = list.GetArrayElementAtIndex((int)index).boxedValue;
                return (EditorGUIUtility.singleLineHeight + 2f) * (p is Probability.EventWeight or PseudoProbability.EventWeight && NodeDrawerUtility.showUUID ? 4 : 3) + 2f;
            }
        }

        public void DeleteReference(Func<TreeNode> deletion)
        {
            if (deletion == null)
            {
                return;
            }

            if (Event.current.button == 0)
            {
                if (editor.editorSetting.debugMode) Debug.Log("Delete");
                int opt = EditorUtility.DisplayDialogComplex("Delete", "Delete removed node from the tree?", "Delete", "Cancel", "Remove Only");
                switch (opt)
                {
                    case 0:
                        DetroyNode();
                        break;
                    case 1:
                        break;
                    case 2:
                        deletion();
                        break;
                }
            }
            else
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Remove"), false, () => deletion());
                menu.AddItem(new GUIContent("Remove and delete"), false, DetroyNode);
                menu.ShowAsContext();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void DetroyNode()
            {
                TreeNode childNode = deletion();
                if (childNode != null)
                {
                    editor.TryDeleteNode(childNode);
                }
            }
        }

        protected void DrawNodeListItemCommonModify<T>(List<T> list, int index) where T : INodeReference
        {
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));
            GUILayout.Space(EditorGUI.indentLevel * 16 + 4);
            if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
            {
                DeleteReference(() => RemoveFromList(list, index));
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginVertical(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));

            //GUILayout.Space((EditorGUI.indentLevel - 1) * 16);
            if (GUILayout.Button("Open", GUILayout.MaxWidth(60)))
            {
                T t = list[index];
                TreeNode nodeElement = GetTreeNodeFromElement(t);
                editor.SelectedNode = nodeElement;
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }

            if (list[index] is INodeReference && GUILayout.Button("Replace"))
            {
                DrawNodeListItemCommonModify_Replace();
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }

            var currentStatus = GUI.enabled;
            if (index == 0) GUI.enabled = false;
            if (GUILayout.Button("Up"))
            {
                ListItem_Up(list, index);
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }
            GUI.enabled = currentStatus;
            if (index == list.Count - 1) GUI.enabled = false;
            if (GUILayout.Button("Down"))
            {
                ListItem_Down(list, index);
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }
            GUI.enabled = currentStatus;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();


            void DrawNodeListItemCommonModify_Replace()
            {
                T reference = list[index];
                TreeNode nodeElement = GetTreeNodeFromElement(reference);
                var listClone = new List<T>(list);
                editor.OpenSelectionWindow(RightWindow.All,
                (n) =>
                {
                    // replacing same node
                    if (n == nodeElement)
                    {
                        list.Insert(index, reference);
                        EditorUtility.DisplayDialog("Replacing node error",
                            $"Cannot replace node {nodeElement.name} because selected node is same as the old one",
                            "OK");
                        return;
                    }
                    // switch
                    var oldT = listClone.FirstOrDefault(e => GetTreeNodeFromElement(e) == n);
                    if (oldT != null)
                    {
                        Undo.RecordObject(tree, $"Switch node {node.name} with {n.name}");
                        int targetIndex = listClone.IndexOf(oldT);
                        list.Insert(targetIndex, oldT);
                        (list[targetIndex], list[index]) = (list[index], list[targetIndex]);
                        return;
                    }
                    // new 
                    ReplaceNodeReference(reference, n);
                });
            }
        }

        /// <summary>
        /// Remove helper of node list item modifier
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        protected TreeNode RemoveFromList<T>(List<T> list, int index)
        {
            T element = list[index];

            TreeNode childNode = RemoveNodeReference(element);
            list.RemoveAt(index);

            return childNode;
        }

        private TreeNode RemoveNodeReference<T>(T refType)
        {
            Undo.RecordObject(tree, $"Remove node {node.name}");
            TreeNode childNode = GetTreeNodeFromElement(refType);
            if (refType is not RawNodeReference)
                childNode.parent = NodeReference.Empty;
            return childNode;
        }

        private void ReplaceNodeReference<T>(T refType, TreeNode newNode)
        {
            Undo.RecordObject(tree, $"Replace node {node.name} with {newNode.name}");
            if (refType is INodeReference reference)
            {
                var oldNode = tree.GetNode(reference.UUID);
                if (oldNode != null && refType is not RawNodeReference) oldNode.parent = NodeReference.Empty;
                reference.UUID = newNode?.uuid ?? UUID.Empty;
            }
            if (newNode != null && refType is not RawNodeReference)
            {
                newNode.parent = node;
            }
        }

        private void ReplaceNodeReference(INodeReference reference, TreeNode newNode)
        {
            Undo.RecordObject(tree, $"Replace node {node.name} with {newNode.name}");
            var oldNode = tree.GetNode(reference.UUID);
            reference.Set(newNode);

            if (!reference.IsRawReference)
            {
                if (oldNode != null) oldNode.parent = NodeReference.Empty;
                if (newNode != null) newNode.parent = node;
            }

        }

        protected TreeNode RemoveFromList(SerializedProperty list, int index)
        {
            var property = list.GetArrayElementAtIndex(index);
            TreeNode node = GetTreeNodeFromElement(property.GetValue());

            Undo.RecordObject(list.serializedObject.targetObject, $"Remove node {node?.name}");
            if (node != null)
            {
                node.parent = NodeReference.Empty;
            }
            list.serializedObject.Update();
            list.DeleteArrayElementAtIndex(index);
            list.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            list.serializedObject.Update();

            return node;
        }

        private void ListItem_Down(IList list, int index)
        {
            if (Event.current.button == 0)
            {
                (list[index], list[index + 1]) = (list[index + 1], list[index]);
            }
            else
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Down 1"), false, () => (list[index], list[index + 1]) = (list[index + 1], list[index]));
                menu.AddItem(new GUIContent("To Last"), false, () => { var item = list[index]; list.RemoveAt(index); list.Add(item); });
                menu.ShowAsContext();
            }
        }

        private void ListItem_Up(IList list, int index)
        {
            if (Event.current.button == 0)
            {
                (list[index], list[index - 1]) = (list[index - 1], list[index]);
            }
            else
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Up 1"), false, () => (list[index], list[index - 1]) = (list[index - 1], list[index]));
                menu.AddItem(new GUIContent("To First"), false, () => { var item = list[index]; list.RemoveAt(index); list.Insert(0, item); });
                menu.ShowAsContext();
            }
        }

        /// <summary>
        /// Convert T to tree node if possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element"></param>
        /// <returns></returns>
        protected TreeNode GetTreeNodeFromElement<T>(T element)
        {
            TreeNode childNode;
            switch (element)
            {
                case UUID uuid:
                    childNode = tree.GetNode(uuid);
                    break;
                case TreeNode n:
                    childNode = n;
                    break;
                case NodeReference nr:
                    childNode = tree.GetNode(nr);
                    break;
                case RawNodeReference rnr:
                    childNode = tree.GetNode(rnr);
                    break;
                case Probability.EventWeight w:
                    childNode = tree.GetNode(w.reference);
                    break;
                case PseudoProbability.EventWeight pw:
                    childNode = tree.GetNode(pw.reference);
                    break;
                default:
                    Debug.Log("Cannot find a not based on " + element.GetType().Name);
                    childNode = null;
                    break;
            }

            return childNode;
        }

        //private T GetElement<T>(TreeNode n)
        //{
        //    Type type = typeof(T);
        //    if (type == typeof(NodeReference))
        //    {
        //        return (T)(object)(NodeReference)n;
        //    }
        //    if (type == typeof(RawNodeReference))
        //    {
        //        return (T)(object)(new RawNodeReference() { UUID = n.uuid });
        //    }
        //    if (type == typeof(UUID))
        //    {
        //        return (T)(object)n.uuid;
        //    }
        //    if (type == typeof(TreeNode))
        //    {
        //        return (T)(object)n;
        //    }
        //}

        protected void DrawList(string labelName, IList list) => DrawList(new GUIContent(labelName), list);
        protected void DrawList(GUIContent label, IList list)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                GUILayout.BeginHorizontal();
                DrawListItemCommonModify(list, i);
                if (list.Count == 0)
                {
                    GUILayout.EndHorizontal();
                    break;
                }

                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                if (EditorFieldDrawers.DrawField(i.ToString(), ref item, objectToUndo: tree))
                    list[i] = item;

                EditorGUI.indentLevel = oldIndent;
                GUILayout.EndHorizontal();
                //GUILayout.Space(10);
            }
            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 16);
            //GUILayout.BeginVertical();
            if (GUILayout.Button("Add"))
            {
                Type type = list.GetType().GenericTypeArguments[0];
                if (list.Count > 0) list.Add(list[list.Count - 1]);
                else if (type.IsValueType) list.Add(default);
                else if (type == typeof(string)) list.Add(string.Empty);
                else
                {
                    try
                    {
                        list.Add(Activator.CreateInstance(type));
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("Failed to initalized the list's first element, try edit this in inspector first");
                    }
                }
            }
            if (list.Count != 0)
                if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            //GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        protected void DrawListItemCommonModify(IList list, int index)
        {
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));
            GUILayout.Space(EditorGUI.indentLevel * 16 + 4);
            if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
            {
                Debug.Log("Delete");
                list.RemoveAt(index);
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginVertical(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));

            //GUILayout.Space((EditorGUI.indentLevel - 1) * 16); 
            var item = list[index];

            var currentStatus = GUI.enabled;
            if (index == 0) GUI.enabled = false;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Up"))
            {
                ListItem_Up(list, index);
                //list.Remove(item);
                //list.Insert(index - 1, item);
            }
            GUI.enabled = currentStatus;
            if (index == list.Count - 1) GUI.enabled = false;
            if (GUILayout.Button("Down"))
            {
                ListItem_Down(list, index);
                //list.Remove(item);
                //list.Insert(index + 1, item);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = currentStatus;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }




        /// <summary>
        /// Create a right click menu for last GUI Rect
        /// </summary>
        /// <param name="menu"></param>
        protected bool RightClickMenu(GenericMenu menu) => EditorFieldDrawers.RightClickMenu(menu);

        /// <summary>
        /// Create a right click menu for Given Rect
        /// </summary>
        /// <param name="menu"></param>
        protected bool RightClickMenu(GenericMenu menu, Rect rect) => EditorFieldDrawers.RightClickMenu(menu, rect);





        private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }
        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                      .Where(t => string.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                      .ToArray();
        }


        protected GUIStyle SetRegionColor(Color color, out Color baseColor)
        {
            GUIStyle colorStyle = new();
            colorStyle.normal.background = Texture2D.whiteTexture;

            baseColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            return colorStyle;
        }

        /// <summary>
        /// Show warning when reference is null
        /// </summary>
        /// <param name="reference"></param>
        protected void NodeMustNotBeNull(NodeReference reference, string fieldName)
        {
            if (!reference.HasEditorReference)
            {
                EditorGUILayout.HelpBox($"{node.GetType()} \"{node.name}\" does not have a valid {fieldName} node, this will cause error during runtime!", MessageType.Warning);
                return;
            }
        }

        /// <summary>
        /// Create a node reference list drawer for serialized properties.
        /// </summary>
        /// <param name="label">List label.</param>
        /// <param name="list">Serialized list property.</param>
        /// <param name="node">Target node.</param>
        /// <param name="elementType">Element type for the list.</param>
        /// <returns>A configured reorderable list.</returns>
        internal ReorderableList CreateNodeReferenceList(GUIContent label, SerializedProperty list, TreeNode node, Type elementType)
        {
            if (elementType == typeof(NodeReference))
            {
                return DrawNodeList<NodeReference>(label, list, node);
            }

            if (elementType == typeof(RawNodeReference))
            {
                return DrawNodeList<RawNodeReference>(label, list, node);
            }

            throw new ArgumentException($"Unsupported node reference list element type: {elementType}", nameof(elementType));
        }
    }
}

