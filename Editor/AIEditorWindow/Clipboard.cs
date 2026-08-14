using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Clipboard used in AI editor
    /// </summary>
    [Serializable]
    public class Clipboard
    {
        /// <summary>
        /// tree ref
        /// </summary>
        public BehaviourTreeData tree;
        /// <summary>
        /// sub tree inside the clipboard
        /// </summary>
        [SerializeReference]
        public List<TreeNode> treeNodes;
        /// <summary>
        /// uuid of the first node
        /// </summary>
        public UUID uuid;
        [SerializeField]
        private bool graphSelection;
        [SerializeField]
        private List<Vector2> graphPositions = new();

        /// <summary>
        /// subtree size inside the clipboard
        /// </summary>
        public int Count => treeNodes?.Count ?? 0;
        /// <summary>
        /// the main content (root of the subtree)
        /// </summary>
        public TreeNode Root => GetRootCopy();
        /// <summary>
        /// the root buffered (root of the subtree)
        /// </summary>
        private TreeNode RootBuffered => Count > 0 ? treeNodes[0] : null;
        /// <summary>
        /// all contents inside the clipboard
        /// </summary>
        public List<TreeNode> Content => GetContent();
        /// <summary>
        /// root node type
        /// </summary>
        public Type RootType => RootBuffered?.GetType();
        /// <summary>
        /// check whether clipboard has value
        /// </summary>
        /// <returns></returns>
        public bool HasContent
        {
            get
            {
                treeNodes.RemoveAll(x => x == null);
                return treeNodes.Count > 0;
            }
        }

        /// <summary>Gets whether the clipboard contains a detached multi-root Graph selection.</summary>
        public bool IsGraphSelection => graphSelection;

        /// <summary>Gets whether legacy commands may interpret the clipboard as one rooted subtree.</summary>
        public bool HasSingleRootContent => HasContent && (!graphSelection || Count == 1);


        public Clipboard()
        {
            Init();
        }

        /// <summary>
        /// init clipboard
        /// </summary>
        private void Init()
        {
            tree = null;
            uuid = UUID.Empty;
            treeNodes ??= new();
            treeNodes.Clear();
            graphSelection = false;
            graphPositions ??= new List<Vector2>();
            graphPositions.Clear();
        }

        /// <summary>
        /// clear clipboard
        /// </summary>
        public void Clear()
        {
            Init();
        }

        /// <summary>
        /// write clipboard entry
        /// </summary>
        /// <param name="node"></param>
        /// <param name="tree"></param>
        public void Write(TreeNode node, BehaviourTreeData tree)
        {
            graphSelection = false;
            graphPositions.Clear();
            this.tree = tree;

            if (node != null)
            {
                uuid = node.uuid;
                treeNodes = NodeFactory.DeepCloneSubTree(node, tree);
                // parent of the node is invalid now, set to empty
                treeNodes[0].parent ??= NodeReference.Empty;
                treeNodes[0].parent.UUID = UUID.Empty;
            }
        }

        /// <summary>
        /// write clipboard entry (without given node's subtree)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="tree"></param>
        public void WriteSingle(TreeNode node, BehaviourTreeData tree)
        {
            graphSelection = false;
            graphPositions.Clear();
            this.tree = tree;

            if (node != null)
            {
                uuid = node.uuid;
                TreeNode treeNode = NodeFactory.DeepClone(node);
                // clear node child references
                foreach (var item in treeNode.GetChildrenReference())
                {
                    item.UUID = UUID.Empty;
                }
                treeNodes = new List<TreeNode>() { treeNode };
                // parent of the node is invalid now, set to empty
                treeNodes[0].parent ??= NodeReference.Empty;
                treeNodes[0].parent.UUID = UUID.Empty;
            }
        }

        /// <summary>Writes an authored Graph selection while preserving only relations internal to that selection.</summary>
        /// <param name="nodes">Selected authored nodes in stable selection order.</param>
        /// <param name="positions">Graph positions corresponding to <paramref name="nodes"/>.</param>
        /// <param name="tree">Source behaviour tree.</param>
        public void WriteGraphSelection(IReadOnlyList<TreeNode> nodes, IReadOnlyList<Vector2> positions, BehaviourTreeData tree)
        {
            Init();
            if (nodes == null || positions == null || nodes.Count == 0 || nodes.Count != positions.Count || tree == null)
            {
                return;
            }

            this.tree = tree;
            graphSelection = true;
            HashSet<UUID> selected = nodes.Where(node => node != null).Select(node => node.uuid).ToHashSet();
            foreach (TreeNode source in nodes)
            {
                if (source == null || tree.GetNode(source.uuid) != source) continue;
                // Keep source UUIDs until Content performs one translation pass so every
                // selected-node relation can be translated as a coherent subgraph.
                TreeNode clone = NodeFactory.Duplicate(source);
                if (clone.parent == null || !selected.Contains(clone.parent.UUID)) clone.parent = NodeReference.Empty;
                foreach (INodeReference reference in clone.GetChildrenReference())
                {
                    if (reference != null && !selected.Contains(reference.UUID)) reference.UUID = UUID.Empty;
                }

                NodeAccessor accessor = NodeAccessorProvider.GetAccessor(clone.GetType());
                foreach (INodeReferenceCollectionFieldAccessor field in accessor.NodeReferenceCollections)
                {
                    if (field.ElementType == typeof(RawNodeReference)) continue;
                    IList entries = field.Get(clone);
                    if (entries == null) continue;
                    List<object> kept = entries.Cast<object>()
                        .Where(entry => entry is INodeReference reference && selected.Contains(reference.UUID))
                        .ToList();
                    if (entries is Array)
                    {
                        Array replacement = Array.CreateInstance(field.ElementType, kept.Count);
                        for (int index = 0; index < kept.Count; index++) replacement.SetValue(kept[index], index);
                        field.Set(clone, replacement);
                    }
                    else
                    {
                        entries.Clear();
                        foreach (object entry in kept) entries.Add(entry);
                    }
                }
                treeNodes.Add(clone);
            }

            Dictionary<UUID, TreeNode> copiedByUUID = treeNodes.ToDictionary(node => node.uuid);
            foreach (TreeNode owner in treeNodes)
            {
                foreach (INodeReference reference in owner.GetChildrenReference())
                {
                    if (reference != null && copiedByUUID.TryGetValue(reference.UUID, out TreeNode child))
                    {
                        child.parent ??= NodeReference.Empty;
                        child.parent.UUID = owner.uuid;
                    }
                }
                foreach (NodeReference reference in owner.GetServices() ?? Enumerable.Empty<NodeReference>())
                {
                    if (reference != null && copiedByUUID.TryGetValue(reference.UUID, out TreeNode service))
                    {
                        service.parent ??= NodeReference.Empty;
                        service.parent.UUID = owner.uuid;
                    }
                }
            }

            graphPositions.AddRange(positions.Take(treeNodes.Count));
            uuid = treeNodes.Count > 0 ? treeNodes[0].uuid : UUID.Empty;
        }

        /// <summary>Creates a UUID-reassigned copy of Graph selection content and its relative layout.</summary>
        public bool TryGetGraphSelection(out List<TreeNode> content, out List<Vector2> positions)
        {
            content = graphSelection && HasContent ? Content : null;
            positions = content == null ? null : new List<Vector2>(graphPositions);
            return content != null && content.Count == positions.Count;
        }


        /// <summary>
        /// clone the buffered content inside the clipboard
        /// </summary>
        /// <returns></returns>
        private List<TreeNode> GetContent()
        {
            List<TreeNode> contents = new();
            foreach (var item in treeNodes)
            {
                TreeNode clone = NodeFactory.Duplicate(item);
                contents.Add(clone);
            }
            NodeFactory.ReassignUUID(contents);
            return contents;
        }

        /// <summary>
        /// clone the buffered content inside the clipboard
        /// </summary>
        /// <returns></returns>
        private TreeNode GetRootCopy()
        {
            return NodeFactory.Duplicate(RootBuffered);
        }

        /// <summary>
        /// Check root has the same type as the given node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool TypeMatch(TreeNode node)
        {
            return HasSingleRootContent && RootType == node.GetType();
        }

        /// <summary>
        /// Check root has the same type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool TypeMatch(Type type)
        {
            if (!HasSingleRootContent) return false;
            Type rootType = RootType;
            if (rootType == null) return false;
            return rootType.IsSubclassOf(type) || rootType == type;
        }







        /// <summary>
        /// Paste clipboard content to given reference
        /// </summary>
        /// <param name="parent">The destination owner node.</param>
        /// <param name="slot">The destination single-reference slot.</param>
        /// <returns><c>true</c> when the subtree was added and connected.</returns>
        public bool PasteTo(
            BehaviourTreeData tree,
            TreeNode parent,
            INodeReferenceSingleSlot slot,
            Vector2? graphPosition = null)
        {
            if (!HasSingleRootContent || RootBuffered is Service)
            {
                EditorUtility.DisplayDialog("Pasting service node", "Cannot paste service to main tree as normal node", "OK");
                return false;
            }

            List<TreeNode> content = Content;
            TreeNode root = content[0];
            foreach (var item in content)
            {
                item.name = tree.GenerateNewNodeName(item.name);
            }

            return tree.TryAddAndSetReference(
                parent.uuid,
                slot.Name,
                -1,
                content,
                root.uuid,
                $"Paste clipboard content under {parent.name}",
                CreateGraphPositions(root.uuid, graphPosition));
        }

        /// <summary>
        /// Paste clipboard value to given node
        /// </summary>
        /// <param name="targetTree">The tree that owns the destination node.</param>
        /// <param name="node"></param>
        public void PasteValue(BehaviourTreeData targetTree, TreeNode node)
        {
            if (targetTree == null)
            {
                EditorUtility.DisplayDialog("Null Tree", "Pasting to null tree is not allowed", "OK");
                return;
            }
            if (node == null)
            {
                EditorUtility.DisplayDialog("Null Destination", $"Pasting to null is not allowed", "OK");
                return;
            }
            if (!HasContent)
            {
                EditorUtility.DisplayDialog("Empty Clipboard", $"Nothing is in clipboard", "OK");
                return;
            }
            if (!TypeMatch(node))
            {
                EditorUtility.DisplayDialog("Type mismatch", $"Pasting to  \"{node.GetType().Name}\" from type \"{RootType?.Name}\" is not allowed", "OK");
                return;
            }

            Undo.RecordObject(targetTree, $"Paste value to {node.name}");
            NodeFactory.Copy(node, Root);
            EditorUtility.SetDirty(targetTree);
        }




        public bool PasteAsLast(BehaviourTreeData tree, TreeNode owner, INodeReferenceListSlot slot) => PasteAt(tree, owner, slot, slot?.Count ?? 0);

        public bool PasteAsFirst(BehaviourTreeData tree, TreeNode owner, INodeReferenceListSlot slot) => PasteAt(tree, owner, slot, 0);

        /// <returns><c>true</c> when the subtree was added and inserted.</returns>
        public bool PasteAt(
            BehaviourTreeData tree,
            TreeNode owner,
            INodeReferenceListSlot slot,
            int index,
            Vector2? graphPosition = null)
        {
            if (tree == null)
            {
                EditorUtility.DisplayDialog("Null Tree", "Pasting to null tree is not allowed", "OK");
                return false;
            }

            if (owner == null)
            {
                EditorUtility.DisplayDialog("Null Destination", "Pasting to null node is not allowed", "OK");
                return false;
            }

            if (slot == null)
            {
                EditorUtility.DisplayDialog("Null Destination", "Pasting to null slot is not allowed", "OK");
                return false;
            }

            if (!HasSingleRootContent || RootBuffered is Service)
            {
                EditorUtility.DisplayDialog("Pasting service node", "Cannot paste service to main tree as normal node", "OK");
                return false;
            }

            List<TreeNode> content = Content;
            TreeNode root = content[0];

            foreach (var item in content)
            {
                item.name = tree.GenerateNewNodeName(item.name);
            }

            int clampedIndex = Mathf.Clamp(index, 0, slot.Count);
            return tree.TryAddAndInsertReference(
                owner.uuid,
                slot.Name,
                clampedIndex,
                content,
                root.uuid,
                $"Insert clipboard content to {owner.name}.{slot.Name} index {clampedIndex}",
                CreateGraphPositions(root.uuid, graphPosition));

            //RemoveServicesIfServiceStack(tree, owner, content);
        }

        /// <summary>Creates an optional layout snapshot containing one newly pasted root.</summary>
        private static IReadOnlyDictionary<UUID, Vector2> CreateGraphPositions(UUID rootUUID, Vector2? graphPosition)
        {
            if (!graphPosition.HasValue)
            {
                return null;
            }

            return new Dictionary<UUID, Vector2> { [rootUUID] = graphPosition.Value };
        }



        /// <summary>
        /// Builds the clipboard status line used by the toolbar and clipboard menu.
        /// </summary>
        /// <returns>The clipboard status text.</returns>
        public string GetStatusText()
        {
            if (!HasContent)
            {
                return "Clipboard is empty.";
            }

            string rootName = treeNodes[0]?.name ?? "None";
            return graphSelection
                ? $"Clipboard: {Count} selected graph node(s)"
                : $"Clipboard: {Count} node(s), root: {rootName}";
        }


        [Obsolete("This method is obsoleted since now support service in serivce")]
        private static void RemoveServicesIfServiceStack(BehaviourTreeData tree, TreeNode parent, List<TreeNode> content)
        {
            if (tree.IsServiceCall(parent))
            {
                var names = new List<string>();
                foreach (var item in content)
                {
                    if (item is not Service service) continue;
                    names.Add(item.name);
                    tree.RemoveSubTree(service);
                }
                if (names.Count > 0)
                    EditorUtility.DisplayDialog("Pasting to service", $"Service {string.Join(", ", names)} will not be copied because destination parent node is in a service stack", "ok");
            }
        }

    }
}
