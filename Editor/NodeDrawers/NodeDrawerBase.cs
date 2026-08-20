using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static Aethiumian.AI.Editor.AIEditorWindow;
using static Aethiumian.AI.Editor.NodePropertyDrawerUtility;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Base class of node drawer
    /// </summary>
    public abstract partial class NodeDrawerBase
    {
        public Dictionary<(UnityEngine.Object, string), ReorderableList> listDrawers = new();
        private readonly Dictionary<(UnityEngine.Object, string), NodeReferenceTreeView> nodeListViews = new();



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
                this.node = property.GetAIValue() as TreeNode;
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

        private static readonly GUIContent RandomSourceOverrideContent = new(
            "Random Source Override",
            "Optional random source for this node. If empty, the tree/global/default random source is used.");

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
            FieldInfo field = NodeDrawerFieldMetadata.GetField(property);
            bool readOnly = NodeDrawerFieldMetadata.IsReadOnly(field);

            // Handle arrays/lists with ReorderableList
            if (property.isArray)
            {
                using (new EditorGUI.DisabledScope(readOnly))
                {
                    DrawPropertyArray(label, property);
                }
                return;
            }

            using (new EditorGUI.DisabledScope(readOnly))
            {
                EditorGUILayout.PropertyField(property, label, true);
            }

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
                EditorGUI.PropertyField(rect, element, new GUIContent("Element " + index), false);
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

        /// <summary>
        /// Draw a RandomSourceBinding override field.
        /// </summary>
        protected void DrawRandomSourceOverride(SerializedProperty randomSourceBinding)
        {
            if (randomSourceBinding == null)
            {
                return;
            }

            DrawProperty(RandomSourceOverrideContent, randomSourceBinding);
        }

        #endregion


        /// <summary>
        /// Draw a type reference through its serialized property.
        /// </summary>
        /// <param name="label">Label of the field.</param>
        /// <param name="typeReferenceProperty">Serialized type reference property.</param>
        protected void DrawTypeReferenceProperty(GUIContent label, SerializedProperty typeReferenceProperty)
        {
            if (typeReferenceProperty == null)
            {
                return;
            }

            DrawProperty(label, typeReferenceProperty);
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

        #endregion





        /// <summary>
        /// Draw a variable field through its serialized property.
        /// </summary>
        /// <param name="label">Label of the field.</param>
        /// <param name="variableProperty">Serialized variable property.</param>
        /// <param name="possibleTypes">Allowed variable types.</param>
        /// <param name="variableAccessFlag">Access constraint.</param>
        protected bool DrawVariableProperty(GUIContent label, SerializedProperty variableProperty, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            if (variableProperty?.boxedValue is not VariableBase variable)
            {
                return false;
            }

            float height = VariableFieldDrawers.GetVariableHeight(variable, tree, possibleTypes, variableAccessFlag);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            return DrawVariableProperty(rect, label, variableProperty, possibleTypes, variableAccessFlag);
        }

        /// <summary>Draws a variable property with a transient reflected object type override.</summary>
        protected bool DrawVariableProperty(GUIContent label, SerializedProperty variableProperty, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, Type objectTypeOverride)
        {
            if (variableProperty?.boxedValue is not VariableBase variable)
            {
                return false;
            }

            float height = VariableFieldDrawers.GetVariableHeight(variable, tree, possibleTypes, variableAccessFlag);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            return VariableFieldDrawers.DrawVariable(rect, label, variableProperty, possibleTypes, variableAccessFlag, objectTypeOverride);
        }

        /// <summary>
        /// Draw a variable field through its serialized property in the provided rect.
        /// </summary>
        /// <param name="rect">Target draw rect.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="variableProperty">Serialized variable property.</param>
        /// <param name="possibleTypes">Allowed variable types.</param>
        /// <param name="variableAccessFlag">Access constraint.</param>
        protected bool DrawVariableProperty(Rect rect, GUIContent label, SerializedProperty variableProperty, VariableType[] possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            if (variableProperty == null)
            {
                return false;
            }

            return VariableFieldDrawers.DrawVariable(rect, label, variableProperty, possibleTypes, variableAccessFlag);
        }

        /// <summary>Draws a variable property in a fixed rectangle with a transient reflected type override.</summary>
        protected bool DrawVariableProperty(Rect rect, GUIContent label, SerializedProperty variableProperty, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag, Type objectTypeOverride)
        {
            return VariableFieldDrawers.DrawVariable(rect, label, variableProperty, possibleTypes, variableAccessFlag, objectTypeOverride);
        }

        /// <summary>
        /// Apply a boxed value to a serialized property and refresh it.
        /// </summary>
        /// <param name="targetProperty">Serialized property to update.</param>
        /// <param name="value">Boxed value.</param>
        protected static void ApplyBoxedValue(SerializedProperty targetProperty, object value)
        {
            if (targetProperty == null)
            {
                return;
            }

            targetProperty.serializedObject.Update();
            targetProperty.boxedValue = value;
            targetProperty.serializedObject.ApplyModifiedProperties();
            targetProperty.serializedObject.Update();
        }





        /// <summary>Dispatches reference deletion from a direct GUI mouse event.</summary>
        public void DeleteReference(Func<TreeNode> resolveReference, System.Action removeReference)
        {
            if (resolveReference == null || removeReference == null)
            {
                return;
            }

            Event guiEvent = Event.current;
            if (guiEvent == null)
            {
                return;
            }

            if (guiEvent.button == 0)
            {
                ConfirmDeleteReference(resolveReference, ExecuteRemove);
            }
            else
            {
                ShowDeleteReferenceMenu(resolveReference, ExecuteRemove);
            }

            bool ExecuteRemove()
            {
                removeReference();
                return true;
            }
        }

        /// <summary>Runs the existing confirmation flow for removing a reference or deleting its node.</summary>
        private void ConfirmDeleteReference(
            Func<TreeNode> resolveReference,
            Func<bool> removeReference,
            System.Action onRemoveSucceeded = null,
            System.Action onRejected = null)
        {
            if (resolveReference == null || removeReference == null)
            {
                return;
            }

            if (editor.editorSetting.debugMode) Debug.Log("Delete");
            int option = EditorUtility.DisplayDialogComplex(
                "Delete",
                "Delete removed node from the tree?",
                "Delete",
                "Cancel",
                "Remove Only");
            switch (option)
            {
                case 0:
                    TreeNode childNode = resolveReference();
                    if (childNode == null)
                    {
                        onRejected?.Invoke();
                        break;
                    }
                    editor.TryDeleteNode(childNode);
                    break;
                case 2:
                    if (removeReference())
                    {
                        onRemoveSucceeded?.Invoke();
                    }
                    else
                    {
                        onRejected?.Invoke();
                    }
                    break;
            }
        }

        /// <summary>Shows the right-click commands for removing a reference with or without its node.</summary>
        private void ShowDeleteReferenceMenu(Func<TreeNode> resolveReference, Func<bool> removeReference)
        {
            if (resolveReference == null || removeReference == null)
            {
                return;
            }

            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Remove"), false, () => { removeReference(); });
            menu.AddItem(new GUIContent("Remove and delete"), false, () => DeleteResolvedReference(resolveReference));
            menu.ShowAsContext();
        }

        /// <summary>Deletes the currently resolved referenced node when it still exists.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DeleteResolvedReference(Func<TreeNode> resolveReference)
        {
            TreeNode childNode = resolveReference?.Invoke();
            if (childNode != null)
            {
                editor.TryDeleteNode(childNode);
            }
        }

        /// <summary>Resolves one exact current authored occurrence from stable topology identity.</summary>
        private TreeNode ResolveNodeListOccurrence(UUID ownerUuid, string fieldName, int index, UUID expectedTargetUuid)
        {
            TreeNode owner = tree?.GetNode(ownerUuid);
            if (owner == null || string.IsNullOrEmpty(fieldName) || index < 0 || expectedTargetUuid == UUID.Empty)
            {
                return null;
            }

            return NodeTopologySnapshot.Create(tree.EditorNodes)
                .GetOutgoing(owner)
                .FirstOrDefault(occurrence => occurrence.FieldName == fieldName
                    && occurrence.Index == index
                    && occurrence.Target?.uuid == expectedTargetUuid)
                .Target;
        }

        protected TreeNode ResolveNodeListEntry(SerializedProperty list, int index)
        {
            if (list == null || !list.isArray || index < 0 || index >= list.arraySize)
            {
                return null;
            }

            list.serializedObject.Update();
            SerializedProperty elementProperty = list.GetArrayElementAtIndex(index);
            TryResolveReferencedNode(elementProperty, out TreeNode node);
            return node;
        }

        protected bool RemoveFromList(SerializedProperty list, int index)
        {
            if (list == null || !list.isArray || index < 0 || index >= list.arraySize)
            {
                return false;
            }

            string fieldName = GetRelativeNodePropertyPath(list.propertyPath);
            TreeNode owner = tree.GetNode(node.uuid);
            return owner != null
                && !string.IsNullOrEmpty(fieldName)
                && tree.TryDisconnectReference(
                    owner.uuid,
                    fieldName,
                    index,
                    $"Remove node reference from {fieldName}");
        }

        /// <summary>
        /// Create a right click menu for last GUI Rect
        /// </summary>
        /// <param name="menu"></param>
        protected bool RightClickMenu(GenericMenu menu)
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type != EventType.MouseDown
                || Event.current.button != 1
                || !rect.Contains(Event.current.mousePosition))
            {
                return false;
            }

            menu.ShowAsContext();
            return true;
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
        /// Draw the service list for the given node using a tree view.
        /// </summary>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public void DrawNodeService()
        {
            if (!node.CanEditServices() || !node.TryAsServiceHost(out var serviceHost))
            {
                return;
            }

            TreeNode hostNode = serviceHost.Node;
            GUILayout.BeginVertical();
            GUILayout.Space(10);

            serviceHost.EnsureServices();

            SerializedProperty nodeProperty = tree.GetNodeProperty(hostNode);
            if (nodeProperty == null)
            {
                EditorGUILayout.HelpBox("Service list data is missing for this node.", MessageType.Warning);
                GUILayout.EndVertical();
                return;
            }

            SerializedProperty servicesProperty = nodeProperty.FindPropertyRelative(nameof(ServiceHostNode.services));
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
                hostNode,
                newNode => new NodeReference { UUID = newNode.uuid },
                NodeSelectionContext.Services,
                addRect => AddServiceReference(serviceHost, servicesProperty, addRect),
                addRect => ShowServiceAddMenu(serviceHost, servicesProperty, addRect));

            treeView.Draw();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Add a service reference to the list for the given node.
        /// </summary>
        /// <param name="serviceHost">The node that owns the service list.</param>
        /// <param name="servicesProperty">The serialized services list property.</param>
        /// <param name="anchor">The Services Add button rectangle.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void AddServiceReference(IServiceHostNode serviceHost, SerializedProperty servicesProperty, Rect anchor)
        {
            if (serviceHost == null || servicesProperty == null)
            {
                return;
            }

            UUID ownerUUID = serviceHost.Node.uuid;
            editor.OpenNodeChoiceDropdown(NodeSelectionContext.Services, choice =>
            {
                if (!editor.TreeModule.CommitChoiceToCollection(
                    choice,
                    NodeSelectionContext.Services,
                    ownerUUID,
                    nameof(ServiceHostNode.services),
                    -1,
                    "Assign Service reference"))
                {
                    editor.TreeModule.ShowConnectionRejectedNotification();
                }
            },
            anchor,
            candidate => candidate != null
                && tree.CanInsertReference(ownerUUID, nameof(ServiceHostNode.services), candidate.uuid, allowMoveExisting: true));
        }

        /// <summary>
        /// Show the add menu for service entries.
        /// </summary>
        /// <param name="serviceHost">The node that owns the service list.</param>
        /// <param name="servicesProperty">The serialized services list property.</param>
        /// <param name="anchor">The Services Add button rectangle.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private void ShowServiceAddMenu(IServiceHostNode serviceHost, SerializedProperty servicesProperty, Rect anchor)
        {
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("Add"), false, () => AddServiceReference(serviceHost, servicesProperty, anchor));
            menu.ShowAsContext();
        }

        /// <summary>
        /// Draws an upgrade panel when the node is marked as obsolete.
        /// </summary>
        /// <returns>No return value.</returns>
        /// <exception cref="ExitGUIException">Thrown by Unity when GUI processing is aborted.</exception>
        public void DrawUpgradeControls()
        {
            if (node == null)
            {
                return;
            }

            Attribute obsoleteAttribute = Attribute.GetCustomAttribute(node.GetType(), typeof(ObsoleteAttribute));
            bool isObsolete = obsoleteAttribute != null;
            bool canUpgrade = node.CanUpgrade();
            if (!isObsolete && !canUpgrade)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (isObsolete)
                {
                    EditorGUILayout.LabelField("Obsolete Node", EditorStyles.boldLabel);
                    string message = (obsoleteAttribute as ObsoleteAttribute)?.Message ?? "This node is marked as obsolete and may cause issues during runtime.";
                    EditorGUILayout.HelpBox(message, MessageType.Warning);
                }
                if (canUpgrade)
                {
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
}

