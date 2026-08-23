using System;

namespace Aethiumian.AI.Editor.Mutations
{
    /// <summary>
    /// Identifies the kind of topology location in a mutation result.
    /// </summary>
    public enum BehaviourTreeNodeLocationKind
    {
        Reference,
        Head,
        Detached,
    }

    /// <summary>
    /// Describes where a node was or is attached.
    /// </summary>
    [Serializable]
    public sealed class BehaviourTreeNodeLocation
    {
        public BehaviourTreeNodeLocationKind Kind;
        public UUID OwnerNodeId;
        public string Field;
        public int Index;
    }
}
