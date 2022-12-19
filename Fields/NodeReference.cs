using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{

    /// <summary>
    /// Node Reference class
    /// <para>
    /// Represent the reference of nodes in the behaviour tree
    /// </para>
    /// </summary>
    [Serializable]
    public class NodeReference : INodeReference, ICloneable, IEquatable<NodeReference>, IComparable<NodeReference>
    {
        public static NodeReference Empty => new NodeReference();

        [SerializeField] private UUID uuid = UUID.Empty;
        private TreeNode node;

        public bool HasEditorReference => uuid != UUID.Empty;
        public bool HasReference => node != null;
        public bool IsRawReference => false;

        public UUID UUID { get => uuid; set => uuid = value; }
        public TreeNode Node { get => node; set => node = value; }

        public NodeReference()
        {
        }
        public NodeReference(UUID uuid)
        {
            this.uuid = uuid;
        }

        public static implicit operator Service(NodeReference nodeReference)
        {
            return nodeReference.node as Service;
        }
        public static implicit operator TreeNode(NodeReference nodeReference)
        {
            return nodeReference.node;
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


        public static bool operator ==(NodeReference a, TreeNode b)
        {
            if (a is null && b is null)
            {
                return true;
            }
            if (a is null || b is null)
            {
                return false;
            }
            return a.uuid == b.uuid;
        }

        public static bool operator !=(NodeReference a, TreeNode b) => !(a == b);

        public static bool operator !=(TreeNode a, NodeReference b) => !(b == a);

        public static bool operator ==(TreeNode a, NodeReference b) => (b == a);

        public override bool Equals(object obj)
        {
            return obj is NodeReference ? base.Equals(obj) :
                (obj is TreeNode node ? this.uuid == node.uuid :
                (obj is UUID uuid ? this.uuid == uuid : false));
        }

        public bool Equals(NodeReference other)
        {
            return other is null ? uuid == UUID.Empty : uuid == other.uuid;
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
    }
}