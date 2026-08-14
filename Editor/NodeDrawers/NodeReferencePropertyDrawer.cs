using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using UnityEditor;
using UnityEngine;
using static Aethiumian.AI.Editor.AIEditorWindow;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Property drawer for node references.
    /// </summary>
    [CustomPropertyDrawer(typeof(NodeReference))]
    public sealed class NodeReferencePropertyDrawer : PropertyDrawer
    {
        private const float OverflowButtonWidth = 22f;

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
            => EditorGUIUtility.singleLineHeight;

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
            if (!NodePropertyDrawerUtility.TryGetTree(property, out var tree))
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            TreeNode ownerNode = ownerOverride;
            if (ownerNode == null)
            {
                NodePropertyDrawerUtility.TryGetNode(property, tree, out ownerNode);
            }

            var nodeReference = property.boxedValue as INodeReference;
            UUID uuid = nodeReference.UUID;
            TreeNode referenceNode = tree.GetNode(uuid);

            Rect indentedPosition = EditorGUI.IndentedRect(position);
            string nodeName = referenceNode?.name ?? "None";
            ResponsiveIMGUILayout layout = ResponsiveIMGUILayout.CalculateSingleLine(indentedPosition, OverflowButtonWidth);
            EditorGUI.LabelField(layout.LabelRect, label);
            TryOpenPendingCreate(tree, property, ownerNode, isRawReference, layout.ValueRect);
            GUIContent valueContent = new(
                nodeName,
                referenceNode == null ? "Select a node." : $"Replace '{nodeName}'.");
            if (layout.ValueRect.width > 0f && GUI.Button(layout.ValueRect, valueContent, EditorStyles.popup))
            {
                OpenExistingSelection(tree, property, ownerNode, isRawReference, layout.ValueRect);
            }

            if (GUI.Button(layout.OverflowRect, "⋮", EditorStyles.miniButton))
            {
                ShowReferenceMenu(property, tree, referenceNode, ownerNode, isRawReference);
            }
        }

        /// <summary>Shows direct reference commands and queues the create catalogue when requested.</summary>
        private static void ShowReferenceMenu(SerializedProperty property, BehaviourTreeData tree, TreeNode referenceNode, TreeNode ownerNode, bool isRawReference)
        {
            GenericMenu menu = new();
            NodeReferenceSelectionSession session = CreateSession(tree, property, ownerNode, isRawReference);
            if (referenceNode == null)
            {
                AddCreateMenuItem(menu, session);
                if (isRawReference || ownerNode == null || !AIEditorWindow.SharedClipboard.HasSingleRootContent)
                {
                    menu.AddDisabledItem(new GUIContent("Paste"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Paste"), false, () => PasteNodeReference(property, tree, ownerNode, isRawReference));
                }
            }
            else
            {
                menu.AddItem(new GUIContent("Open"), false, () => AIEditorWindow.OpenNode(tree, referenceNode));
                AddCreateMenuItem(menu, session);
                if (isRawReference || ownerNode == null || !AIEditorWindow.SharedClipboard.HasSingleRootContent)
                {
                    menu.AddDisabledItem(new GUIContent("Paste"));
                }
                else
                {
                    menu.AddItem(new GUIContent("Paste"), false, () => PasteNodeReference(property, tree, ownerNode, isRawReference));
                }
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Clear"), false, () => ClearNodeReference(property, tree, ownerNode, isRawReference));
            }

            menu.ShowAsContext();
        }

        /// <summary>Adds the deferred Create command when the current Graph context can host it.</summary>
        private static void AddCreateMenuItem(GenericMenu menu, NodeReferenceSelectionSession session)
        {
            if (session?.CanQueueCreate == true)
            {
                menu.AddItem(new GUIContent("Create…"), false, () => session.QueueCreate());
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Create…"));
            }
        }

        private static void PasteNodeReference(SerializedProperty property, BehaviourTreeData tree, TreeNode ownerNode, bool isRawReference)
        {
            if (property == null || ownerNode == null || !AIEditorWindow.SharedClipboard.HasSingleRootContent)
            {
                return;
            }

            if (property.boxedValue is not INodeReference)
            {
                return;
            }

            NodeReferenceSelectionSession session = CreateSession(tree, property, ownerNode, isRawReference);
            if (session == null || session.ApplyChoice(NodeSelectionChoice.Paste()))
            {
                return;
            }

            if (AIEditorWindow.TryGetOpenWindow(tree, out AIEditorWindow observer))
            {
                observer.ShowNotification(new GUIContent(AIEditorWindowModule.ConnectionRejectedMessage));
            }
        }

        /// <summary>
        /// Opens a NodeReference session using the current tree window as an optional observer.
        /// </summary>
        private static void OpenExistingSelection(BehaviourTreeData tree, SerializedProperty property, TreeNode ownerNode, bool isRawReference, Rect anchor)
        {
            CreateSession(tree, property, ownerNode, isRawReference)?.OpenExisting(anchor);
        }

        /// <summary>Consumes a window-owned Create request only for the property that queued it.</summary>
        private static void TryOpenPendingCreate(BehaviourTreeData tree, SerializedProperty property, TreeNode ownerNode, bool isRawReference, Rect anchor)
        {
            if (AIEditorWindow.TryGetOpenWindow(tree, out AIEditorWindow observer) &&
                observer.TryConsumeNodeReferenceCreation(tree, ownerNode?.uuid ?? UUID.Empty, property.propertyPath, isRawReference, out NodeReferenceSelectionSession session))
            {
                session.OpenCreate(anchor);
            }
        }

        /// <summary>
        /// Clears a NodeReference through the shared transaction session.
        /// </summary>
        private static void ClearNodeReference(SerializedProperty property, BehaviourTreeData tree, TreeNode ownerNode, bool isRawReference)
        {
            CreateSession(tree, property, ownerNode, isRawReference)?.Clear();
        }

        /// <summary>
        /// Creates a stable session from the current property without retaining the property instance.
        /// </summary>
        private static NodeReferenceSelectionSession CreateSession(BehaviourTreeData tree, SerializedProperty property, TreeNode ownerNode, bool isRawReference)
        {
            if (tree == null || property == null)
            {
                return null;
            }

            AIEditorWindow.TryGetOpenWindow(tree, out AIEditorWindow observer);
            return new NodeReferenceSelectionSession(
                tree,
                ownerNode?.uuid ?? UUID.Empty,
                property.propertyPath,
                isRawReference,
                AIEditorWindow.SharedClipboard,
                observer);
        }

        /// <summary>
        /// Update a child node parent through serialized properties.
        /// </summary>
        /// <param name="tree">Behaviour tree data.</param>
        /// <param name="childNode">Child node to update.</param>
        /// <param name="parentNode">New parent node, or null to clear.</param>
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

    /// <summary>
    /// Describes the standard label/value layout used by IMGUI node drawers.
    /// </summary>
    internal readonly struct ResponsiveIMGUILayout
    {
        public Rect LabelRect { get; }
        public Rect ValueRect { get; }
        public Rect OverflowRect { get; }

        private ResponsiveIMGUILayout(Rect labelRect, Rect valueRect, Rect overflowRect)
        {
            LabelRect = labelRect;
            ValueRect = valueRect;
            OverflowRect = overflowRect;
        }

        /// <summary>
        /// Calculates a single-row label, value and overflow layout.
        /// </summary>
        public static ResponsiveIMGUILayout CalculateSingleLine(Rect position, float overflowWidth = 22f)
        {
            const float minimumValueWidth = 70f;
            float line = EditorGUIUtility.singleLineHeight;
            float width = Mathf.Max(0f, position.width);
            float overflow = Mathf.Clamp(overflowWidth, 0f, width);
            float labelWidth = Mathf.Min(EditorGUIUtility.labelWidth, Mathf.Max(0f, width - overflow - minimumValueWidth));
            Rect labelRect = new(position.x, position.y, labelWidth, line);
            Rect valueRect = new(labelRect.xMax, position.y, Mathf.Max(0f, width - labelWidth - overflow), line);
            Rect overflowRect = new(valueRect.xMax, position.y, overflow, line);
            return new ResponsiveIMGUILayout(labelRect, valueRect, overflowRect);
        }
    }
}
