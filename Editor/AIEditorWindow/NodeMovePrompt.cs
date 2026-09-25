using Aethiumian.AI.Nodes;
using System.Collections.Generic;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    /// <summary>Asks for authorization when a Nodes-page action changes a node's structural owner.</summary>
    internal static class NodeMovePrompt
    {
        /// <summary>
        /// Asks whether a node owned elsewhere may move to the destination. Commit immediately after authorization.
        /// </summary>
        /// <param name="tree">The tree containing the selected node.</param>
        /// <param name="nodeUUID">The existing node UUID, or empty for Create and Paste.</param>
        /// <param name="destination">The new owner, or null for Head.</param>
        /// <param name="allowMoveExisting">Whether the user authorized moving an existing ownership edge.</param>
        /// <returns><c>false</c> only when the user declines the move.</returns>
        internal static bool TryAuthorizeMove(BehaviourTreeData tree, UUID nodeUUID, TreeNode destination, out bool allowMoveExisting)
        {
            allowMoveExisting = false;
            if (tree == null || nodeUUID == UUID.Empty || tree.GetNode(nodeUUID) is not TreeNode node)
            {
                return true;
            }

            IReadOnlyList<NodeReferenceOccurrence> incoming = NodeTopologySnapshot.Create(tree.EditorNodes).GetIncoming(node);
            TreeNode owner = incoming.Count == 1 ? incoming[0].Owner : null;
            if (owner == null || owner == destination)
            {
                return true;
            }

            string message = destination == null
                ? $"This Node is connecting to {owner.name}, move to Head?"
                : $"This Node is connecting to {owner.name}, move under {destination.name} ?";
            allowMoveExisting = EditorUtility.DisplayDialog("Node has a parent already", message, "OK", "Cancel");
            return allowMoveExisting;
        }

        /// <summary>Asks whether to detach a node from its actual structural owner.</summary>
        /// <param name="node">The node being detached.</param>
        /// <param name="owner">The owner found through the authored reference.</param>
        /// <returns><c>true</c> when the user confirms detachment.</returns>
        internal static bool ConfirmDetach(TreeNode node, TreeNode owner)
        {
            return EditorUtility.DisplayDialog(
                "Node has a parent already",
                $"This Node is connecting to {owner.name}, detach it?",
                "OK",
                "Cancel");
        }
    }
}
