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
            return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DrawNodeReference(position, property, label, isRawReference: false);
            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Draw a node reference field using a fixed position.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="property">Serialized property.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="isRawReference">True if this is a raw node reference.</param>
        internal static void DrawNodeReference(Rect position, SerializedProperty property, GUIContent label, bool isRawReference)
        {
            if (!NodePropertyDrawerContext.TryGetTree(property, out var tree))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            SerializedProperty uuidProperty = property.FindPropertyRelative("uuid");
            UUID uuid = uuidProperty?.boxedValue is UUID value ? value : UUID.Empty;
            TreeNode referenceNode = tree.GetNode(uuid);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var headerRect = new Rect(position.x, position.y, position.width, lineHeight);
            var buttonRect = new Rect(position.x, position.y + lineHeight + spacing, position.width, lineHeight);

            string nodeName = referenceNode?.name ?? "None";
            EditorGUI.LabelField(headerRect, label, new GUIContent(nodeName));

            if (referenceNode == null)
            {
                DrawSelectButton(buttonRect, property, uuidProperty, tree, isRawReference);
                return;
            }

            DrawAssignedButtons(buttonRect, property, uuidProperty, tree, referenceNode, isRawReference);
        }

        private static void DrawSelectButton(Rect rect, SerializedProperty property, SerializedProperty uuidProperty, BehaviourTreeData tree, bool isRawReference)
        {
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
                ApplyNodeReference(property, uuidProperty, tree, selectedNode, isRawReference);
            }, isRawReference);
        }

        private static void DrawAssignedButtons(Rect rect, SerializedProperty property, SerializedProperty uuidProperty, BehaviourTreeData tree, TreeNode referenceNode, bool isRawReference)
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
                        ApplyNodeReference(property, uuidProperty, tree, selectedNode, isRawReference);
                    }, isRawReference);
                }
            }
            x += ButtonWidth + 4f;

            if (GUI.Button(new Rect(x, rect.y, ButtonWidth, rect.height), "Clear"))
            {
                ApplyNodeReference(property, uuidProperty, tree, null, isRawReference);
            }
        }

        private static void ApplyNodeReference(SerializedProperty property, SerializedProperty uuidProperty, BehaviourTreeData tree, TreeNode newNode, bool isRawReference)
        {
            property.serializedObject.Update();

            UUID newUuid = newNode?.uuid ?? UUID.Empty;
            UUID oldUuid = uuidProperty?.boxedValue is UUID old ? old : UUID.Empty;

            if (uuidProperty != null)
            {
                uuidProperty.boxedValue = newUuid;
            }

            if (!isRawReference && NodePropertyDrawerContext.TryGetNode(property, tree, out var owner))
            {
                TreeNode oldNode = tree.GetNode(oldUuid);
                if (oldNode != null)
                {
                    oldNode.parent.UUID = UUID.Empty;
                }

                if (newNode != null)
                {
                    newNode.parent = owner;
                }
            }

            property.serializedObject.ApplyModifiedProperties();
            property.serializedObject.Update();
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
            return (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
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
