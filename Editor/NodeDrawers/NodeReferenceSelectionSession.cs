using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections;
using System.Collections.Generic;
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
            return !rawReference && observer?.QueueNodeReferenceCreation(this) == true;
        }

        /// <summary>Returns whether this session can queue creation in its current Graph window.</summary>
        internal bool CanQueueCreate => !rawReference && observer?.CanQueueNodeReferenceCreation(tree) == true;

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
                choice => ApplyChoice(choice),
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
                !TryResolveCandidate(choice, out TreeNode newNode, out System.Collections.Generic.List<TreeNode> pastedNodes))
            {
                return false;
            }

            if (!TryGetManagedReference(owner, out INodeReference targetReference) ||
                !NodePropertyDrawerUtility.TryGetReferenceUuidProperty(property, out SerializedProperty uuidProperty))
            {
                WarnInvalidProperty();
                return false;
            }

            UUID oldUUID = targetReference.UUID;

            if (choice.Kind == NodeSelectionChoiceKind.ExistingNode && newNode != null && oldUUID == newNode.uuid)
            {
                return false;
            }

            if (choice.Kind == NodeSelectionChoiceKind.ExistingNode && newNode != null && !CanSelectExistingNode(newNode))
            {
                return false;
            }

            TreeNode oldNode = tree.GetNode(oldUUID);
            TreeNode newParent = !rawReference && newNode != null ? tree.GetParent(newNode) : null;
            UUID newParentUUID = newParent?.uuid ?? UUID.Empty;
            if (!rawReference && newParent != null && (owner == null || newParentUUID != owner.uuid))
            {
                if (!EditorUtility.DisplayDialog(
                    "Node has a parent already",
                    $"This Node is connecting to {newParent.name}, move{(owner == null ? string.Empty : $" under {owner.name}")}?",
                    "OK",
                    "Cancel"))
                {
                    return false;
                }
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Assign node reference");
            Undo.RegisterCompleteObjectUndo(tree, "Assign node reference");
            try
            {
                if (pastedNodes != null)
                {
                    tree.AddRange(pastedNodes, recordUndo: false);
                }
                else if (choice.Kind == NodeSelectionChoiceKind.CreateType && newNode != null)
                {
                    tree.Add(newNode, recordUndo: false);
                }

                // Adding nodes can invalidate array-backed SerializedProperty instances. Refresh
                // and resolve the destination again from the stable owner UUID and relative path.
                tree.SerializedObject.Update();
                tree.RegenerateTable();
                if (!TryResolveProperty(out property, out owner) ||
                    !TryGetManagedReference(owner, out targetReference) ||
                    !NodePropertyDrawerUtility.TryGetReferenceUuidProperty(property, out uuidProperty))
                {
                    WarnInvalidProperty();
                    throw new InvalidOperationException("The NodeReference property became invalid during selection.");
                }

                oldNode = oldUUID == UUID.Empty ? null : tree.GetNode(oldUUID);
                newNode = newNode == null ? null : tree.GetNode(newNode.uuid);
                newParent = newParentUUID == UUID.Empty ? null : tree.GetNode(newParentUUID);

                if (!rawReference)
                {
                    DetachNode(oldNode, owner);
                    if (newParent != null && (owner == null || newParent.uuid != owner.uuid))
                    {
                        RemoveFromParent(newParent, newNode);
                    }
                }

                uuidProperty.boxedValue = newNode?.uuid ?? UUID.Empty;
                if (!rawReference && newNode != null)
                {
                    SetParent(newNode, owner?.uuid ?? UUID.Empty);
                }

                // Every destination and ownership property belongs to this SerializedObject.
                // Apply the complete mutation once so Undo observes one atomic graph change.
                tree.SerializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(tree);
                tree.RegenerateTable();
                tree.SerializedObject.Update();
                Undo.CollapseUndoOperations(undoGroup);
                observer?.RefreshNodeReferenceObserver();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, tree);
                Undo.RevertAllDownToGroup(undoGroup);
                tree.RegenerateTable();
                tree.SerializedObject.Update();
                return false;
            }
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
            if (owner == null || candidate == owner)
            {
                return false;
            }

            HashSet<UUID> visited = new();
            Stack<TreeNode> pending = new();
            pending.Push(candidate);
            while (pending.Count > 0)
            {
                TreeNode current = pending.Pop();
                if (current == null || !visited.Add(current.uuid))
                {
                    continue;
                }

                if (current == owner)
                {
                    return false;
                }

                AddStructuralChildren(current, pending);
            }

            return true;
        }

        /// <summary>Adds authored structural children while excluding parent and raw references.</summary>
        private void AddStructuralChildren(TreeNode node, Stack<TreeNode> pending)
        {
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(node.GetType());
            foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
            {
                INodeReference reference = field.Get(node);
                if (field.Name == nameof(TreeNode.parent) || reference?.IsRawReference == true)
                {
                    continue;
                }

                TreeNode child = reference == null ? null : tree.GetNode(reference.UUID);
                if (child != null)
                {
                    pending.Push(child);
                }
            }

            foreach (INodeReferenceCollectionFieldAccessor collection in accessor.NodeReferenceCollections)
            {
                IList entries = collection.Get(node);
                if (entries == null)
                {
                    continue;
                }

                foreach (object entry in entries)
                {
                    if (entry is not INodeReference reference || reference.IsRawReference)
                    {
                        continue;
                    }

                    TreeNode child = tree.GetNode(reference.UUID);
                    if (child != null)
                    {
                        pending.Push(child);
                    }
                }
            }
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
                WarnInvalidProperty();
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
                WarnInvalidProperty();
                return false;
            }

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

            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            int collectionSeparator = relativePropertyPath.IndexOf(".Array.data[", StringComparison.Ordinal);
            if (collectionSeparator < 0)
            {
                foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
                {
                    if (field.Name == relativePropertyPath)
                    {
                        reference = field.Get(owner);
                        return reference != null;
                    }
                }

                return false;
            }

            string fieldName = relativePropertyPath[..collectionSeparator];
            int indexStart = collectionSeparator + ".Array.data[".Length;
            int indexEnd = relativePropertyPath.IndexOf(']', indexStart);
            if (indexEnd < 0 || !int.TryParse(relativePropertyPath[indexStart..indexEnd], out int index))
            {
                return false;
            }

            foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
            {
                if (field.Name != fieldName || field.Get(owner) is not System.Collections.IList entries ||
                    index < 0 || index >= entries.Count || entries[index] is not INodeReference entry)
                {
                    continue;
                }

                string suffix = relativePropertyPath[(indexEnd + 1)..];
                reference = suffix switch
                {
                    "" => entry,
                    ".reference" when entry is Probability.EventWeight weighted => weighted.reference,
                    ".reference" when entry is PseudoProbability.EventWeight weighted => weighted.reference,
                    _ => null,
                };
                return reference != null;
            }

            return false;
        }

        /// <summary>
        /// Resolves and validates the candidate without mutating data.
        /// </summary>
        /// <param name="choice">The dropdown choice.</param>
        /// <param name="newNode">The existing, created, or pasted root.</param>
        /// <param name="pastedNodes">The cloned clipboard nodes, if this is a paste.</param>
        /// <returns>True when the candidate is valid.</returns>
        private bool TryResolveCandidate(NodeSelectionChoice choice, out TreeNode newNode, out System.Collections.Generic.List<TreeNode> pastedNodes)
        {
            newNode = null;
            pastedNodes = null;
            switch (choice.Kind)
            {
                case NodeSelectionChoiceKind.ExistingNode:
                    if (choice.ExistingNodeUUID == UUID.Empty)
                    {
                        return true;
                    }

                    newNode = tree.GetNode(choice.ExistingNodeUUID);
                    return newNode != null && newNode is not Service;
                case NodeSelectionChoiceKind.CreateType:
                    if (choice.CreateType == null || !NodeMenuCache.IsCreatableNodeType(choice.CreateType) ||
                        !typeof(TreeNode).IsAssignableFrom(choice.CreateType) || typeof(Service).IsAssignableFrom(choice.CreateType))
                    {
                        return false;
                    }

                    newNode = NodeFactory.Create(choice.CreateType);
                    newNode.name = tree.GenerateNewNodeName(NodeMenuCache.Shared.GetDisplayName(choice.CreateType));
                    return true;
                case NodeSelectionChoiceKind.PasteRoot:
                    if (clipboard == null || !clipboard.HasSingleRootContent || clipboard.TypeMatch(typeof(Service)))
                    {
                        return false;
                    }

                    pastedNodes = clipboard.Content;
                    if (pastedNodes == null || pastedNodes.Count == 0)
                    {
                        return false;
                    }

                    foreach (TreeNode node in pastedNodes)
                    {
                        node.name = tree.GenerateNewNodeName(node.name);
                    }

                    newNode = pastedNodes[0];
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Detaches an old structural node without removing the destination occurrence.</summary>
        private void DetachNode(TreeNode node, TreeNode destinationOwner)
        {
            if (node == null)
            {
                return;
            }

            TreeNode parent = tree.GetParent(node);
            if (parent != null && (destinationOwner == null || parent.uuid != destinationOwner.uuid))
            {
                RemoveFromParent(parent, node);
            }

            SetParent(node, UUID.Empty);
        }

        /// <summary>Writes a node parent UUID through the shared SerializedObject.</summary>
        private void SetParent(TreeNode node, UUID parentUUID)
        {
            SerializedProperty parentUUIDProperty = tree.GetNodeProperty(node)?
                .FindPropertyRelative(nameof(TreeNode.parent))?
                .FindPropertyRelative(NodeReference.uuidPropertyName);
            if (parentUUIDProperty == null)
            {
                throw new InvalidOperationException($"Node {node.uuid} has no serialized parent reference.");
            }

            parentUUIDProperty.boxedValue = parentUUID;
        }

        /// <summary>Removes all serialized structural references to a node from one owner.</summary>
        private void RemoveFromParent(TreeNode owner, TreeNode node)
        {
            UUID targetUUID = node.uuid;
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(owner.GetType());
            SerializedProperty ownerProperty = tree.GetNodeProperty(owner)
                ?? throw new InvalidOperationException($"Node {owner.uuid} has no serialized property.");
            foreach (INodeReferenceFieldAccessor field in accessor.NodeReferences)
            {
                SerializedProperty fieldProperty = ownerProperty.FindPropertyRelative(field.Name);
                if (field.Name != nameof(TreeNode.parent) &&
                    NodePropertyDrawerUtility.TryGetReferenceUuidProperty(fieldProperty, out SerializedProperty fieldUUID) &&
                    fieldUUID.boxedValue is UUID uuid && uuid == targetUUID)
                {
                    fieldUUID.boxedValue = UUID.Empty;
                }
            }

            foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
            {
                SerializedProperty collection = ownerProperty.FindPropertyRelative(field.Name);
                if (collection == null || !collection.isArray)
                {
                    continue;
                }

                for (int index = collection.arraySize - 1; index >= 0; index--)
                {
                    SerializedProperty element = collection.GetArrayElementAtIndex(index);
                    if (NodePropertyDrawerUtility.TryGetReferenceUuidProperty(element, out SerializedProperty elementUUID) &&
                        elementUUID.boxedValue is UUID uuid && uuid == targetUUID)
                    {
                        collection.DeleteArrayElementAtIndex(index);
                    }
                }
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

        /// <summary>Reports a stale property without writing partial serialized state.</summary>
        private void WarnInvalidProperty()
        {
            Debug.LogWarning("NodeReference selection was cancelled because the serialized property is no longer valid.", tree);
        }
    }
}
