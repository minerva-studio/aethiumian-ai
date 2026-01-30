using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Property drawer for node references.
    /// </summary>
    [CustomPropertyDrawer(typeof(NodeReference))]
    public sealed class NodeReferencePropertyDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 70f;

        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetDrawerHeight();
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DrawNodeReference(position, property, label, isRawReference: false);
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Get the fixed height used by the node reference drawer.
        /// </summary>
        /// <returns>The required height for the drawer.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        internal static float GetDrawerHeight()
        {
            return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
        }

        /// <summary>
        /// Draw a node reference field using a fixed position.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="property">Serialized property.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="isRawReference">True if this is a raw node reference.</param>
        /// <param name="ownerOverride">Optional owner node override for clipboard paste.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        internal static void DrawNodeReference(Rect position, SerializedProperty property, GUIContent label, bool isRawReference, TreeNode ownerOverride = null)
        {
            if (!NodePropertyDrawerContext.TryGetTree(property, out var tree))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            TreeNode ownerNode = ownerOverride;
            if (ownerNode == null)
            {
                NodePropertyDrawerContext.TryGetNode(property, tree, out ownerNode);
            }

            var nodeReference = property.boxedValue as INodeReference;
            UUID uuid = nodeReference.UUID;
            TreeNode referenceNode = tree.GetNode(uuid);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var headerRect = new Rect(position.x, position.y, position.width, lineHeight);
            var buttonRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);
            headerRect = EditorGUI.IndentedRect(headerRect);
            buttonRect = EditorGUI.IndentedRect(buttonRect);

            string nodeName = referenceNode?.name ?? "None";
            EditorGUI.LabelField(headerRect, label, new GUIContent(nodeName));

            if (referenceNode == null)
            {
                DrawSelectButton(buttonRect, property, tree, ownerNode, isRawReference);
                return;
            }

            DrawAssignedButtons(buttonRect, property, tree, referenceNode, ownerNode, isRawReference);
        }

        private static void DrawSelectButton(Rect rect, SerializedProperty property, BehaviourTreeData tree, TreeNode ownerNode, bool isRawReference)
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 1 && rect.Contains(Event.current.mousePosition))
            {
                ShowPasteMenu(property, tree, ownerNode, isRawReference);
                Event.current.Use();
                return;
            }

            if (!GUI.Button(new Rect(rect.x, rect.y, ButtonWidth, rect.height), "Select"))
            {
                return;
            }

            if (AIEditorWindow.Instance == null)
            {
                return;
            }

            AIEditorWindow.Instance.OpenSelectionWindow(RightWindow.All, selectedNode =>
            {
                ApplyNodeReference(property, tree, selectedNode, ownerNode, isRawReference);
            }, isRawReference);
        }

        private static void DrawAssignedButtons(Rect rect, SerializedProperty property, BehaviourTreeData tree, TreeNode referenceNode, TreeNode ownerNode, bool isRawReference)
        {
            float x = rect.x;

            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), "Open"))
            {
                if (AIEditorWindow.Instance != null)
                {
                    AIEditorWindow.Instance.SelectedNode = referenceNode;
                }
            }
            x += ButtonWidth + 4f;

            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), "Replace"))
            {
                if (AIEditorWindow.Instance != null)
                {
                    AIEditorWindow.Instance.OpenSelectionWindow(RightWindow.All, selectedNode =>
                    {
                        ApplyNodeReference(property, tree, selectedNode, ownerNode, isRawReference);
                    }, isRawReference);
                }
            }
            x += ButtonWidth + 4f;

            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), "Clear"))
            {
                ApplyNodeReference(property, tree, null, ownerNode, isRawReference);
            }
        }

        private static void ShowPasteMenu(SerializedProperty property, BehaviourTreeData tree, TreeNode ownerNode, bool isRawReference)
        {
            GenericMenu menu = new();
            if (isRawReference || AIEditorWindow.Instance == null || ownerNode == null)
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
                menu.ShowAsContext();
                return;
            }

            if (AIEditorWindow.Instance.clipboard.HasContent)
            {
                menu.AddItem(new GUIContent("Paste"), false, () => PasteNodeReference(property, tree, ownerNode));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            menu.ShowAsContext();
        }

        private static void PasteNodeReference(SerializedProperty property, BehaviourTreeData tree, TreeNode ownerNode)
        {
            if (AIEditorWindow.Instance == null || property == null || ownerNode == null)
            {
                return;
            }

            if (property.boxedValue is not INodeReference reference)
            {
                return;
            }

            property.serializedObject.Update();
            AIEditorWindow.Instance.clipboard.PasteTo(tree, ownerNode, reference);
            property.boxedValue = reference;
            property.serializedObject.ApplyModifiedProperties();
            property.serializedObject.Update();
        }

        private static void ApplyNodeReference(SerializedProperty property, BehaviourTreeData tree, TreeNode newNode, TreeNode ownerOverride, bool isRawReference)
        {
            property.serializedObject.Update();
            tree.SerializedObject.Update();

            Undo.RecordObject(tree, "Assign node reference");

            var uuidProperty = property.FindPropertyRelative("uuid");

            UUID newUuid = newNode?.uuid ?? UUID.Empty;
            UUID oldUuid = uuidProperty?.boxedValue is UUID old ? old : UUID.Empty;

            if (uuidProperty != null)
            {
                uuidProperty.boxedValue = newUuid;
            }

            TreeNode ownerNode = ownerOverride;
            if (ownerNode == null)
            {
                NodePropertyDrawerContext.TryGetNode(property, tree, out ownerNode);
            }

            if (!isRawReference && ownerNode != null)
            {
                TreeNode oldNode = tree.GetNode(oldUuid);
                if (oldNode != null)
                {
                    UpdateNodeParent(tree, oldNode, null);
                }

                if (newNode != null)
                {
                    UpdateNodeParent(tree, newNode, ownerNode);
                }
            }

            property.serializedObject.ApplyModifiedProperties();
            property.serializedObject.Update();
            tree.SerializedObject.ApplyModifiedProperties();
            tree.SerializedObject.Update();
        }

        /// <summary>
        /// Updates the parent reference for a child node and persists it through serialized properties.
        /// </summary>
        /// <param name="tree">The behaviour tree data containing the node.</param>
        /// <param name="childNode">The child node whose parent is updated.</param>
        /// <param name="parentNode">The new parent node, or null to clear the parent.</param>
        /// <returns>None.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private static void UpdateNodeParent(BehaviourTreeData tree, TreeNode childNode, TreeNode parentNode)
        {
            if (tree == null || childNode == null)
            {
                return;
            }

            SerializedProperty nodeProperty = tree.GetNodeProperty(childNode);
            SerializedProperty parentProperty = nodeProperty?.FindPropertyRelative(nameof(TreeNode.parent));
            SerializedProperty parentUuidProperty = parentProperty?.FindPropertyRelative("uuid");

            if (parentUuidProperty != null)
            {
                parentUuidProperty.boxedValue = parentNode?.uuid ?? UUID.Empty;
            }

            childNode.parent = parentNode ?? NodeReference.Empty;
        }
    }

    /// <summary>
    /// Property drawer for raw node references.
    /// </summary>
    [CustomPropertyDrawer(typeof(RawNodeReference))]
    internal sealed class RawNodeReferencePropertyDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return NodeReferencePropertyDrawer.GetDrawerHeight();
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            NodeReferencePropertyDrawer.DrawNodeReference(position, property, label, isRawReference: true);
            EditorGUI.EndProperty();
        }
    }
}
