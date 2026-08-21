using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Accessors
{
    /// <summary>Describes one node reference reported by a member visitor.</summary>
    public readonly struct NodeReferenceVisit
    {
        /// <summary>Creates a visit record.</summary>
        public NodeReferenceVisit(TreeNode owner, string path, INodeReference reference)
        {
            Owner = owner;
            Path = path ?? string.Empty;
            Reference = reference;
            RootName = GetRootName(Path, out int index);
            Index = index;
        }

        /// <summary>Gets the node that owns the reference.</summary>
        public TreeNode Owner { get; }

        /// <summary>Gets the complete nested member path.</summary>
        public string Path { get; }

        /// <summary>Gets the discovered reference.</summary>
        public INodeReference Reference { get; }

        /// <summary>Gets the root member name of the path.</summary>
        public string RootName { get; }

        /// <summary>Gets the root collection index, or -1 for a scalar path.</summary>
        public int Index { get; }

        /// <summary>Gets whether the reference is a raw, non-owning reference.</summary>
        public bool IsRaw => Reference?.IsRawReference == true;

        private static string GetRootName(string path, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(path)) return string.Empty;

            int separator = path.IndexOf('.');
            string root = separator < 0 ? path : path.Substring(0, separator);
            int bracket = root.IndexOf('[');
            if (bracket < 0) return root;

            if (root.EndsWith("]", StringComparison.Ordinal)
                && int.TryParse(root.Substring(bracket + 1, root.Length - bracket - 2), out int parsed))
            {
                index = parsed;
            }

            return root.Substring(0, bracket);
        }
    }

    /// <summary>Collects all normal and raw node references from one node.</summary>
    public sealed class NodeReferenceCollectingVisitor : NodeMemberVisitor
    {
        private readonly TreeNode owner;
        private readonly List<NodeReferenceVisit> visits = new();

        /// <summary>Creates a collector for one node.</summary>
        /// <param name="owner">The node being visited.</param>
        public NodeReferenceCollectingVisitor(TreeNode owner)
        {
            this.owner = owner;
        }

        /// <summary>Gets collected references in descriptor traversal order.</summary>
        public IReadOnlyList<NodeReferenceVisit> Visits => visits;

        /// <inheritdoc />
        protected override void OnNodeReference(string path, INodeReference reference)
        {
            visits.Add(new NodeReferenceVisit(owner, path, reference));
        }

        /// <inheritdoc />
        protected override void OnVariableBinding(string path, IVariableBinding binding)
        {
        }
    }

    /// <summary>Remaps authored UUIDs while clearing runtime node caches.</summary>
    public sealed class NodeReferenceRemapVisitor : NodeMemberVisitor
    {
        private readonly IReadOnlyDictionary<UUID, UUID> translation;
        private readonly bool allowExternalRaw;

        /// <summary>Creates a reference remapper.</summary>
        /// <param name="translation">The authored UUID translation table.</param>
        /// <param name="allowExternalRaw">Whether raw references outside the table are retained.</param>
        public NodeReferenceRemapVisitor(IReadOnlyDictionary<UUID, UUID> translation, bool allowExternalRaw = true)
        {
            this.translation = translation ?? throw new ArgumentNullException(nameof(translation));
            this.allowExternalRaw = allowExternalRaw;
        }

        /// <inheritdoc />
        protected override void OnNodeReference(string path, INodeReference reference)
        {
            if (reference == null) return;
            if (path == nameof(TreeNode.parent))
            {
                if (translation.TryGetValue(reference.UUID, out UUID translatedParent))
                {
                    reference.UUID = translatedParent;
                }

                reference.Node = null;
                return;
            }

            if (translation.TryGetValue(reference.UUID, out UUID translated))
            {
                reference.UUID = translated;
                reference.Node = null;
                return;
            }

            if (reference.IsRawReference && allowExternalRaw)
            {
                reference.Node = null;
                return;
            }

            if (reference.UUID != UUID.Empty)
            {
                throw new InvalidOperationException(
                    $"Reference at '{path}' points outside the remapped subtree: {reference.UUID}.");
            }

            reference.Node = null;
        }

        /// <inheritdoc />
        protected override void OnVariableBinding(string path, IVariableBinding binding)
        {
        }
    }

    /// <summary>Captures destination reference UUIDs before authored state is copied.</summary>
    public sealed class DestinationReferenceSnapshotVisitor : NodeMemberVisitor
    {
        private readonly TreeNode owner;
        private readonly Dictionary<string, UUID> references = new(StringComparer.Ordinal);

        /// <summary>Captures destination references for one node.</summary>
        /// <param name="owner">The node whose destination references are captured.</param>
        public DestinationReferenceSnapshotVisitor(TreeNode owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Gets the captured UUID by full member path.</summary>
        public IReadOnlyDictionary<string, UUID> References => references;

        /// <summary>Restores all captured authored UUIDs by their complete member paths.</summary>
        /// <returns>The number of paths restored.</returns>
        public int Restore()
        {
            int restored = 0;
            foreach (KeyValuePair<string, UUID> item in references)
            {
                if (NodeReferenceStructureProvider.TrySetReferenceUuid(owner, item.Key, item.Value))
                {
                    restored++;
                }
            }

            return restored;
        }

        /// <inheritdoc />
        protected override void OnNodeReference(string path, INodeReference reference)
        {
            if (reference != null)
            {
                references[path] = reference.UUID;
            }
        }

        /// <inheritdoc />
        protected override void OnVariableBinding(string path, IVariableBinding binding)
        {
        }
    }
}
