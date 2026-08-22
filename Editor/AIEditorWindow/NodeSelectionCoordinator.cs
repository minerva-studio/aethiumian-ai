using Aethiumian.AI.Nodes;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Owns the short-lived node selection popup state shared by editor surfaces.
    /// It does not resolve or mutate node data; those operations belong to NodeEditorCommandService.
    /// </summary>
    internal sealed class NodeSelectionCoordinator
    {
        private readonly AIEditorWindow editor;
        private NodeReferenceSelectionSession pendingCreate;

        /// <summary>Initializes the coordinator for one editor window.</summary>
        /// <param name="editor">The owning editor window.</param>
        internal NodeSelectionCoordinator(AIEditorWindow editor)
        {
            this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
        }

        /// <summary>Returns whether the Graph Inspector can host a deferred Create popup.</summary>
        /// <param name="expectedTree">The tree captured by the reference drawer.</param>
        internal bool CanQueueCreate(BehaviourTreeData expectedTree)
        {
            return editor
                && editor.CurrentTree == expectedTree
                && editor.window == AIEditorWindow.Window.Graph
                && editor.GraphInspectorContainer != null;
        }

        /// <summary>Queues one deferred Create popup for the next matching Inspector draw.</summary>
        /// <param name="session">The reference selection session.</param>
        internal bool QueueCreate(NodeReferenceSelectionSession session)
        {
            if (session == null || !CanQueueCreate(editor.CurrentTree))
            {
                return false;
            }

            pendingCreate = session;
            editor.GraphInspectorContainer.MarkDirtyRepaint();
            editor.Repaint();
            return true;
        }

        /// <summary>Consumes a queued Create popup when its captured property is drawn again.</summary>
        /// <param name="candidateTree">The currently drawn tree.</param>
        /// <param name="ownerUUID">The owner node UUID.</param>
        /// <param name="propertyPath">The serialized property path.</param>
        /// <param name="rawReference">Whether the reference is raw.</param>
        /// <param name="session">The matching session, if present.</param>
        internal bool TryConsumeCreate(
            BehaviourTreeData candidateTree,
            UUID ownerUUID,
            string propertyPath,
            bool rawReference,
            out NodeReferenceSelectionSession session)
        {
            session = pendingCreate;
            if (!CanQueueCreate(candidateTree)
                || session == null
                || !session.Matches(candidateTree, ownerUUID, propertyPath, rawReference))
            {
                return false;
            }

            pendingCreate = null;
            return true;
        }

        /// <summary>Clears a queued popup when the owning window is disabled.</summary>
        internal void Clear() => pendingCreate = null;

        /// <summary>Opens a selection dropdown for one destination context.</summary>
        /// <param name="context">The destination catalogue context.</param>
        /// <param name="commit">The callback receiving the mutation-free choice.</param>
        /// <param name="anchor">The IMGUI popup anchor.</param>
        /// <param name="existingNodeFilter">Optional caller-owned existing-node filter.</param>
        internal void Open(
            NodeSelectionContext context,
            Action<NodeSelectionChoice> commit,
            Rect anchor,
            Func<TreeNode, bool> existingNodeFilter = null)
        {
            if (anchor.width <= 0f || anchor.height <= 0f)
            {
                anchor = new Rect(0f, 0f, 1f, EditorGUIUtility.singleLineHeight);
            }

            NodeSelectionDropdown dropdown = new(
                editor.CurrentTree,
                editor.Clipboard,
                context,
                commit,
                existingNodeFilter,
                NodeSelectionSources.Mixed);
            dropdown.Show(anchor);
        }
    }
}
