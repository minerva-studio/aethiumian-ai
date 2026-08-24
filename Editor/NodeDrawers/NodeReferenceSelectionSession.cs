using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Owns one atomic NodeReference selection operation independently of any editor page.
    /// </summary>
    internal sealed class NodeReferenceSelectionSession
    {
        private const string NodePathToken = "nodes.Array.data[";

        private readonly BehaviourTreeData tree;
        private readonly Clipboard clipboard;
        private readonly UUID ownerUUID;
        private readonly string propertyPath;
        private readonly string relativePropertyPath;
        private readonly bool rawReference;
        private readonly AIEditorWindow observer;
        private readonly NodeEditorCommandService commands;
        private readonly bool capturedPropertyValid;

        /// <summary>
        /// Initializes a stable session for a serialized NodeReference property.
        /// </summary>
        /// <param name="tree">The tree owning the property.</param>
        /// <param name="ownerUUID">The stable owner identity.</param>
        /// <param name="propertyPath">The serialized property path captured at draw time.</param>
        /// <param name="rawReference">Whether structure and parent links must remain unchanged.</param>
        /// <param name="clipboard">The optional clipboard source.</param>
        /// <param name="observer">The already-open window, if any.</param>
        internal NodeReferenceSelectionSession(
            BehaviourTreeData tree,
            UUID ownerUUID,
            string propertyPath,
            bool rawReference,
            Clipboard clipboard,
            AIEditorWindow observer)
        {
            this.tree = tree ?? throw new ArgumentNullException(nameof(tree));
            this.ownerUUID = ownerUUID;
            this.propertyPath = propertyPath;
            relativePropertyPath = GetRelativePropertyPath(propertyPath);
            this.rawReference = rawReference;
            this.clipboard = clipboard;
            this.observer = observer;
            commands = observer?.NodeCommands ?? new NodeEditorCommandService(clipboard);
            commands.Rebind(tree);
            capturedPropertyValid = ownerUUID == UUID.Empty || IsCapturedPropertyValid(tree, ownerUUID, propertyPath);
        }

        /// <summary>
        /// Opens the pure candidate dropdown at a concrete IMGUI anchor.
        /// </summary>
        /// <param name="anchor">The button rectangle used as the popup anchor.</param>
        internal void OpenExisting(Rect anchor)
        {
            Open(anchor, NodeSelectionSources.Existing);
        }

        /// <summary>Opens the creatable-node catalogue at a concrete IMGUI anchor.</summary>
        internal void OpenCreate(Rect anchor)
        {
            Open(anchor, NodeSelectionSources.Create);
        }

        /// <summary>Queues the create catalogue for the next matching Graph Inspector draw.</summary>
        internal bool QueueCreate()
        {
            return !rawReference && observer?.NodeSelection?.QueueCreate(this) == true;
        }

        /// <summary>Returns whether this session can queue creation in its current Graph window.</summary>
        internal bool CanQueueCreate => !rawReference && observer?.NodeSelection?.CanQueueCreate(tree) == true;

        /// <summary>Returns whether this session targets the currently drawn serialized property.</summary>
        internal bool Matches(BehaviourTreeData candidateTree, UUID candidateOwner, string candidatePath, bool candidateRawReference)
        {
            return tree == candidateTree && ownerUUID == candidateOwner &&
                propertyPath == candidatePath && rawReference == candidateRawReference;
        }

        /// <summary>Opens one explicitly scoped node catalogue.</summary>
        private void Open(Rect anchor, NodeSelectionSources sources)
        {
            if (!IsValidAnchor(anchor))
            {
                anchor = new Rect(0f, 0f, 1f, EditorGUIUtility.singleLineHeight);
            }

            NodeSelectionDropdown dropdown = new(
                tree,
                clipboard,
                NodeSelectionContext.Nodes,
                choice =>
                {
                    bool committed = ApplyChoice(choice);
                    if (!committed && (choice.Kind != NodeSelectionChoiceKind.ExistingNode || choice.ExistingNodeUUID != UUID.Empty))
                    {
                        observer?.ShowNotification(new GUIContent(AIEditorWindowModule.ConnectionRejectedMessage));
                    }
                },
                CanSelectExistingNode,
                sources);
            dropdown.Show(anchor);
        }

        /// <summary>
        /// Applies a choice as one undoable data transaction.
        /// </summary>
        /// <param name="choice">The mutation-free dropdown result.</param>
        /// <returns>True when the transaction committed.</returns>
        internal bool ApplyChoice(NodeSelectionChoice choice)
        {
            if (!TryResolveProperty(out SerializedProperty property, out TreeNode owner) ||
                !TryGetDestination(owner, out string fieldName, out int index))
            {
                return false;
            }

            if (!TryGetManagedReference(owner, out INodeReference targetReference) ||
                !NodePropertyDrawerUtility.TryGetReferenceUuidProperty(property, out _))
            {
                return false;
            }

            UUID oldUUID = targetReference.UUID;
            bool committed = choice.Kind == NodeSelectionChoiceKind.ExistingNode
                && choice.ExistingNodeUUID == UUID.Empty
                ? commands.ClearReference(owner.uuid, fieldName, index, oldUUID, "Clear node reference", rawReference)
                : commands.CommitChoiceToReference(
                    choice,
                    NodeSelectionContext.Nodes,
                    owner.uuid,
                    fieldName,
                    index,
                    oldUUID,
                    "Assign node reference",
                    out _,
                    rawReference);

            if (committed)
            {
                observer?.RefreshNodeReferenceObserver();
            }

            return committed;
        }

        /// <summary>
        /// Clears the reference through the same transaction path as replacement.
        /// </summary>
        /// <returns>True when the clear committed.</returns>
        internal bool Clear() => ApplyChoice(NodeSelectionChoice.Existing(UUID.Empty));

        /// <summary>
        /// Returns whether an existing node can be assigned without creating a structural cycle.
        /// Raw references intentionally bypass structural validation.
        /// </summary>
        internal bool CanSelectExistingNode(TreeNode candidate)
        {
            if (candidate == null || rawReference)
            {
                return candidate != null;
            }

            TreeNode owner = ownerUUID == UUID.Empty ? null : tree.GetNode(ownerUUID);
            return owner != null
                && TryGetDestination(owner, out string fieldName, out int index)
                && tree.CanSetReference(
                    new NodeReferenceAddress(owner.uuid, fieldName, index),
                    candidate.uuid,
                    allowMoveExisting: true);
        }

        /// <summary>
        /// Resolves the current serialized property and stable owner after the popup closes.
        /// </summary>
        /// <param name="property">The current property instance.</param>
        /// <param name="owner">The current owner node.</param>
        /// <returns>True when both the property and required owner are valid.</returns>
        private bool TryResolveProperty(out SerializedProperty property, out TreeNode owner)
        {
            property = null;
            owner = ownerUUID == UUID.Empty ? null : tree.GetNode(ownerUUID);
            if (ownerUUID != UUID.Empty && (owner == null || !capturedPropertyValid))
            {
                return false;
            }

            if (owner != null && !string.IsNullOrEmpty(relativePropertyPath))
            {
                SerializedProperty ownerProperty = tree.GetNodeProperty(owner);
                if (ownerProperty != null)
                {
                    property = ownerProperty.FindPropertyRelative(relativePropertyPath);
                }
            }

            if (property == null && owner == null)
            {
                property = tree.SerializedObject.FindProperty(propertyPath);
            }
            if (property == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>Extracts the authored field and optional collection index from the captured path.</summary>
        private bool TryGetDestination(TreeNode owner, out string fieldName, out int index)
        {
            fieldName = null;
            index = -1;
            if (owner == null || string.IsNullOrEmpty(relativePropertyPath))
            {
                return false;
            }

            int collectionSeparator = relativePropertyPath.IndexOf(".Array.data[", StringComparison.Ordinal);
            if (collectionSeparator < 0)
            {
                fieldName = relativePropertyPath;
                return true;
            }

            int indexStart = collectionSeparator + ".Array.data[".Length;
            int indexEnd = relativePropertyPath.IndexOf(']', indexStart);
            if (indexEnd < 0 || !int.TryParse(relativePropertyPath[indexStart..indexEnd], out index))
            {
                return false;
            }

            fieldName = relativePropertyPath[..collectionSeparator];
            return true;
        }

        /// <summary>Resolves the current managed reference without retaining a boxed property copy.</summary>
        private bool TryGetManagedReference(TreeNode owner, out INodeReference reference)
        {
            reference = null;
            if (owner == null || string.IsNullOrEmpty(relativePropertyPath))
            {
                return false;
            }

            string normalizedPath = relativePropertyPath
                .Replace(".Array.data[", "[");
            PathReferenceVisitor visitor = new(normalizedPath);
            NodeDescriptorProvider.Get(owner.GetType()).VisitMembers(owner, visitor);
            reference = visitor.Reference;
            return reference != null;
        }

        private sealed class PathReferenceVisitor : NodeMemberVisitor
        {
            private readonly string expectedPath;

            public PathReferenceVisitor(string expectedPath)
            {
                this.expectedPath = expectedPath;
            }

            public INodeReference Reference { get; private set; }

            protected override void OnNodeReference(string path, INodeReference reference)
            {
                if (path == expectedPath)
                {
                    Reference = reference;
                    return;
                }

                if (!expectedPath.EndsWith(".reference", StringComparison.Ordinal)
                    || path != expectedPath.Substring(0, expectedPath.Length - ".reference".Length))
                {
                    return;
                }

                Reference = reference switch
                {
                    Probability.EventWeight weighted => weighted.reference,
                    PseudoProbability.EventWeight weighted => weighted.reference,
                    _ => null,
                };
            }

            protected override void OnVariableBinding(string path, IVariableBinding binding)
            {
            }
        }

        /// <summary>Extracts the owner-relative path used to survive node list reordering.</summary>
        private static string GetRelativePropertyPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            int tokenStart = path.IndexOf(NodePathToken, StringComparison.Ordinal);
            int close = tokenStart < 0 ? -1 : path.IndexOf(']', tokenStart + NodePathToken.Length);
            return close < 0 ? string.Empty : path[(close + 1)..].TrimStart('.');
        }

        /// <summary>
        /// Validates the captured array element before the popup can outlive the draw call.
        /// Later list reordering is intentionally allowed because the owner is resolved by UUID.
        /// </summary>
        private static bool IsCapturedPropertyValid(BehaviourTreeData tree, UUID ownerUUID, string path)
        {
            TreeNode owner = tree?.GetNode(ownerUUID);
            SerializedProperty ownerProperty = owner == null ? null : tree.GetNodeProperty(owner);
            return ownerProperty != null && !string.IsNullOrEmpty(path) &&
                (path.Equals(ownerProperty.propertyPath, StringComparison.Ordinal) ||
                 path.StartsWith(ownerProperty.propertyPath + ".", StringComparison.Ordinal));
        }

        /// <summary>Checks whether an IMGUI anchor can be passed to AdvancedDropdown.</summary>
        private static bool IsValidAnchor(Rect anchor) => anchor.width > 0f && anchor.height > 0f;

    }
}
