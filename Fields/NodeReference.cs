using Minerva.Module;
using System;

namespace Amlos.AI
{
    [Serializable]
    public class RawNodeReference : NodeReference, ICloneable, IEquatable<NodeReference>
    {
        public static new RawNodeReference Empty => new RawNodeReference();

        public static implicit operator Service(RawNodeReference nodeReference)
        {
            return nodeReference.node as Service;
        }
        public static implicit operator TreeNode(RawNodeReference nodeReference)
        {
            return nodeReference.node;
        }
        public static implicit operator RawNodeReference(TreeNode node)
        {
            return node is null ? Empty : node.ToRawReference();
        }
    }

    [Serializable]
    public class NodeReference : ICloneable, IEquatable<NodeReference>, IComparable<NodeReference>
    {
        public static NodeReference Empty => new NodeReference();

        public UUID uuid = UUID.Empty;
        [NonSerialized] public TreeNode node;

        public bool HasEditorReference => uuid != UUID.Empty;
        public bool HasReference => node != null;


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
            if (a is null)
            {
                return false;
            }
            if (b is null)
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