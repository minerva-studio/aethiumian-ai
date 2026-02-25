using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Base class of node drawer
    /// </summary>
    public abstract partial class NodeDrawerBase
    {
        private const float NodeListMinHeight = 60f;
        private const float NodeListMaxHeight = 320f;
        private const float NodeListHeaderButtonWidth = 70f;

        public Dictionary<(UnityEngine.Object, string), ReorderableList> listDrawers = new();
        private readonly Dictionary<(UnityEngine.Object, string), NodeReferenceTreeView> nodeListViews = new();



        private NodeReferenceTreeView serviceTreeView;
        private TreeViewState serviceTreeViewState;

        public TreeNode node { get; private set; }
        public SerializedProperty property { get; private set; }
        public AIEditorWindow editor { get; private set; }


        /// <summary> The behaviour tree data </summary>
        protected BehaviourTreeData tree => property.serializedObject.targetObject as BehaviourTreeData;


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
                this.property.serializedObject.Update();
                this.node = property.GetValue() as TreeNode;
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
        public void DrawNodeReference(GUIContent label, SerializedProperty property)
        {
            DrawNodeReference(label, property, node);
        }

        public void DrawNodeReference(GUIContent label, SerializedProperty property, TreeNode target)
        {
            if (property == null)
            {
                return;
            }

            TreeNode resolvedTarget = target;
            var reference = property.boxedValue as INodeReference;
            bool isRawReference = reference?.IsRawReference ?? false;

            float height = NodeReferencePropertyDrawer.GetDrawerHeight();
            Rect rect = EditorGUILayout.GetControlRect(false, height);
            NodeReferencePropertyDrawer.DrawNodeReference(rect, property, label, isRawReference, resolvedTarget);

            TreeNode referencingNode = reference != null ? tree.GetNode(reference.UUID) : null;
            if (referencingNode == null)
            {
                return;
            }

            var oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 1;
            NodeDrawerUtility.DrawNodeBaseInfo(tree, referencingNode);
            EditorGUI.indentLevel = oldIndent;
        }

        public void DrawNodeReferenceModify(SerializedProperty property, INodeReference reference, TreeNode node, TreeNode target)
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
        public void DrawNodeReference(GUIContent label, RawNodeReference reference)
        {
            DrawNodeReference(label, reference,
            (TreeNode n) =>
            {
                Undo.RecordObject(tree, n is null ? $"Clear reference on {tree}" : $"Assign node to field on {tree}");
                ((INodeReference)reference).Set(n);
            });
        }

        /// <summary>
        /// Draw a node reference (Legacy)
        /// </summary> 
        public void DrawNodeReference(string labelName, NodeReference reference) => DrawNodeReference(new GUIContent(labelName), reference);

        /// <summary>
        /// Draw a node reference (Legacy)
        /// </summary> 
        public void DrawNodeReference(GUIContent label, NodeReference reference)
        {
            DrawNodeReference(label, reference,
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

        private void DrawNodeReference(GUIContent label, INodeReference reference, SelectNodeEvent selectNodeEvent)
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
        /// Draw the service list for the given node using a tree view.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public void DrawNodeService()
        {
            if (node == null)
            {
                return;
            }

            GUILayout.BeginVertical();
            GUILayout.Space(10);

            node.services ??= new List<NodeReference>();

            SerializedProperty nodeProperty = tree.GetNodeProperty(node);
            if (nodeProperty == null)
            {
                EditorGUILayout.HelpBox("Service list data is missing for this node.", MessageType.Warning);
                GUILayout.EndVertical();
                return;
            }

            SerializedProperty servicesProperty = nodeProperty.FindPropertyRelative(nameof(TreeNode.services));
            if (servicesProperty == null)
            {
                EditorGUILayout.HelpBox("Service list property is missing for this node.", MessageType.Warning);
                GUILayout.EndVertical();
                return;
            }


            var treeView = GetNodeListTreeView(servicesProperty);
            treeView.SetData(
                new GUIContent("Services"),
                servicesProperty,
                node,
                newNode => new NodeReference { UUID = newNode.uuid },
                RightWindow.Services,
                () => AddServiceReference(node, servicesProperty),
                () => ShowServiceAddMenu(node, servicesProperty),
                index => RemoveServiceReference(servicesProperty, index));

            treeView.Draw();

            if (servicesProperty.arraySize == 0)
            {
                EditorGUILayout.LabelField("No service");
            }
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Add a service reference to the list for the given node.
        /// </summary>
        /// <param name="treeNode">The node that owns the service list.</param>
        /// <param name="servicesProperty">The serialized services list property.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void AddServiceReference(TreeNode treeNode, SerializedProperty servicesProperty)
        {
            if (treeNode == null || servicesProperty == null)
            {
                return;
            }

            editor.OpenSelectionWindow(RightWindow.Services, (selectedNode) =>
            {
                if (selectedNode is not Service service)
                {
                    return;
                }

                treeNode.AddService(service);
                service.parent = treeNode;
                servicesProperty.serializedObject.Update();
            });
        }

        /// <summary>
        /// Show the add menu for service entries.
        /// </summary>
        /// <param name="treeNode">The node that owns the service list.</param>
        /// <param name="servicesProperty">The serialized services list property.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void ShowServiceAddMenu(TreeNode treeNode, SerializedProperty servicesProperty)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Add"), false, () => AddServiceReference(treeNode, servicesProperty));
            menu.ShowAsContext();
        }

        /// <summary>
        /// Remove a service entry and optionally delete the service node.
        /// </summary>
        /// <param name="servicesProperty">The serialized services list property.</param>
        /// <param name="index">The index to remove.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void RemoveServiceReference(SerializedProperty servicesProperty, int index)
        {
            if (servicesProperty == null || servicesProperty.arraySize == 0)
            {
                return;
            }

            if (index < 0 || index >= servicesProperty.arraySize)
            {
                index = servicesProperty.arraySize - 1;
            }

            TreeNode removedNode = RemoveFromList(servicesProperty, index);
            if (removedNode is Service service)
            {
                if (EditorUtility.DisplayDialog("Delete Service", "Do you want to delete the service from the tree too?", "OK", "Cancel"))
                {
                    tree.Remove(service);
                }
            }
        }

        /// <summary>
        /// Draws an upgrade panel when the node is marked as obsolete.
        /// </summary>
        /// <returns>No return value.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        public void DrawUpgradeControls()
        {
            if (node == null || !node.CanUpgrade())
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Obsolete Node", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("This node can be upgraded to a newer version.");

                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Upgrade", GUILayout.Width(90f)))
                    {
                        editor.TryUpgradeNode(node);
                    }
                }
            }
        }
    }
}

