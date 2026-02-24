using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.References
{
    /// <summary>
    /// Node Reference class
    /// <para>
    /// Represent the reference of nodes in the behaviour tree
    /// </para>
    /// </summary>
    [Serializable]
    public class NodeReference : INodeReference, INodeConnection, ICloneable, IEquatable<NodeReference>, IEquatable<TreeNode>, IComparable<NodeReference>
    {
        public const string uuidPropertyName = nameof(uuid);

        public static NodeReference Empty => new NodeReference();

        [SerializeField] private UUID uuid = UUID.Empty;
        private TreeNode node;

        public bool HasEditorReference => uuid != UUID.Empty;
        public bool HasReference => node != null;
        public bool IsRawReference => false;

        /// <summary> Get UUID of the uuid </summary>
        public UUID UUID { get => uuid; set => uuid = value; }

        /// <summary> Get the tree node this node reference points to, only available in runtime </summary>
        public TreeNode Node { get => node; set => node = value; }



        public NodeReference()
        {
        }
        public NodeReference(UUID uuid)
        {
            this.uuid = uuid;
        }

        public override bool Equals(object obj)
        {
            return obj is NodeReference ? base.Equals(obj) :
                obj is TreeNode node ? this.uuid == node.uuid :
                obj is UUID uuid ? this.uuid == uuid : false;
        }

        public bool Equals(NodeReference other)
        {
            return other is null ? uuid == UUID.Empty : uuid == other.uuid;
        }

        public bool Equals(TreeNode other)
        {
            return uuid == other?.uuid;
        }

        public NodeReference Clone()
        {
            return new NodeReference() { node = node, uuid = uuid };
        }

        object ICloneable.Clone()
        {
            return new NodeReference() { node = node, uuid = uuid };
        }

        public override string ToString()
        {
            return uuid;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(uuid);
        }

        public int CompareTo(NodeReference other)
        {
            return uuid.CompareTo(other?.uuid);
        }

        public static implicit operator NodeReference(TreeNode node)
        {
            return node is null ? Empty : node.ToReference();
        }

        public static implicit operator UUID(NodeReference node)
        {
            return node is null ? UUID.Empty : node.uuid;
        }

        public static bool operator ==(NodeReference a, NodeReference b)
        {
            if (ReferenceEquals(a, b)) return true;
            return a is not null ? a.Equals(b) : b.Equals(a);
        }

        public static bool operator !=(NodeReference a, NodeReference b)
        {
            return !(a == b);
        }
    }

    public static class NodeReferenceExtensions
    {
        public static bool IsPointTo(this NodeReference a, TreeNode b)
        {
            if (a is null && b is null)
            {
                return true;
            }
            if (a is null || b is null)
            {
                return false;
            }
            return a.UUID == b.uuid;
        }
    }
}
