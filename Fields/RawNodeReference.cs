using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.References
{
    /// <summary>
    /// A raw node reference
    /// <para>
    /// Raw node reference does not count toward the connection of the behaviour tree</para>
    /// </summary>
    [Serializable]
    public class RawNodeReference : INodeReference, ICloneable
    {
        public static RawNodeReference Empty => new RawNodeReference();

        [SerializeField] private UUID uuid = UUID.Empty;
        private TreeNode node;

        public UUID UUID { get => uuid; set => uuid = value; }
        public TreeNode Node { get => node; set => node = value; }

        public bool HasEditorReference => uuid != UUID.Empty;
        public bool HasReference => node != null;

        public bool IsRawReference => true;

        public static implicit operator Service(RawNodeReference nodeReference)
        {
            return nodeReference.node as Service;
        }
        public static implicit operator TreeNode(RawNodeReference nodeReference)
        {
            return nodeReference.node;
        }

        public RawNodeReference Clone()
        {
            return new RawNodeReference() { node = node, uuid = uuid };
        }

        object ICloneable.Clone()
        {
            return Clone();
        }
    }
}