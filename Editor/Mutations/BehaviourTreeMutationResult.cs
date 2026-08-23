using System;

namespace Aethiumian.AI.Editor.Mutations
{
    /// <summary>Provides common transaction state for one editor mutation.</summary>
    [Serializable]
    public abstract class BehaviourTreeMutationResult
    {
        /// <summary>Whether the mutation and save completed successfully.</summary>
        public bool Success;

        /// <summary>Whether the changed asset was saved to disk.</summary>
        public bool Saved;

        /// <summary>Failure detail when <see cref="Success"/> is false.</summary>
        public string Error;

        /// <summary>Head UUID after the mutation.</summary>
        public UUID HeadNodeId;

        /// <summary>Non-fatal mutation notes; empty when the operation is fully resolved.</summary>
        public string[] Diagnostics;
    }


    /// <summary>Reports a created node and its attachment location.</summary>
    [Serializable]
    public sealed class BehaviourTreeAddResult : BehaviourTreeMutationResult
    {
        public UUID CreatedNodeId;
        public string CreatedNodeName;
        public string CreatedNodeType;
        public BehaviourTreeNodeLocation Location;
    }

    /// <summary>Reports the authored node UUIDs removed by a mutation.</summary>
    [Serializable]
    public sealed class BehaviourTreeRemoveResult : BehaviourTreeMutationResult
    {
        public UUID[] RemovedNodeIds;
    }

    /// <summary>Reports one node's source and destination topology locations.</summary>
    [Serializable]
    public sealed class BehaviourTreeRearrangeResult : BehaviourTreeMutationResult
    {
        public UUID NodeId;
        public BehaviourTreeNodeLocation Source;
        public BehaviourTreeNodeLocation Destination;
    }
}
