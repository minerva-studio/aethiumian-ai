using Aethiumian.AI.References;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Rollback the current branch execution to a referenced node")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Rollback : Flow
    {
        public RawNodeReference stopAt;
        public bool yield = true;

        public override State Execute()
        {
            TreeNode until = GetDepth();
            if (until == null || callStack == null)
            {
                return State.Failed;
            }

            bool result = callStack.Break(until);
            if (!result) return State.Failed;

            // The current stack now points at another node, so this node should not continue in-place.
            if (yield) return State.Yield;
            return State.NONE_RETURN;
        }

        private TreeNode GetDepth()
        {
            return stopAt;
        }

        public override void Initialize()
        {
            behaviourTree.GetNode(ref stopAt);
        }

        public override bool EditorCheck(BehaviourTreeData tree)
        {
            return stopAt != null && stopAt.HasReference;
        }
    }
}
