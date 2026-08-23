using System;

namespace Aethiumian.AI.Editor.Mutations
{
    /// <summary>Describes a typed node creation and attachment request.</summary>
    [Serializable]
    public sealed class BehaviourTreeAddRequest
    {
        /// <summary>Short node name or full CLR type name.</summary>
        public string Type;

        /// <summary>Optional authored node name.</summary>
        public string Name;

        /// <summary>Parent UUID; omit when creating a new Head.</summary>
        public UUID ParentNode;

        /// <summary>Node-reference field on the parent.</summary>
        public string Field;

        /// <summary>Collection insertion index, or -1 for append/scalar fields.</summary>
        public int Index = -1;
    }

    /// <summary>Describes one collection reorder request.</summary>
    [Serializable]
    public sealed class BehaviourTreeReorderRequest
    {
        /// <summary>Authored node UUID to reorder.</summary>
        public UUID NodeId;

        /// <summary>Destination index within the current owning collection.</summary>
        public int Index;
    }

    /// <summary>Describes one cross-owner node move request.</summary>
    [Serializable]
    public sealed class BehaviourTreeMoveRequest
    {
        /// <summary>Authored node UUID to move.</summary>
        public UUID NodeId;

        /// <summary>Destination parent UUID.</summary>
        public UUID TargetParent;

        /// <summary>Destination node-reference field.</summary>
        public string Field;

        /// <summary>Destination collection index, or -1 for append/scalar fields.</summary>
        public int Index = -1;
    }
}
