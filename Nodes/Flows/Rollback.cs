using Amlos.AI.References;

namespace Amlos.AI.Nodes
{
    [NodeTip("Rollback current branch execution to certain state, just like Break")]
    public sealed class Rollback : Flow
    {
        public enum Stack
        {
            current,
            main
        }

        //public VariableField<int> depth = 1;

        public RawNodeReference stopAt;
        public Stack stack;
        public bool yield = true;

        private bool afterYield;

        public override State Execute()
        {
            if (afterYield)
            {
                return State.Success;
            }

            TreeNode until = GetDepth();
            bool result;
            if (!isInServiceRoutine || stack == Stack.main) result = behaviourTree.Break(until);
            else result = behaviourTree.Break(until, ServiceHead);

            if (!result) return State.Failed;

            // when executing happened on the same stack
            if ((isInServiceRoutine && stack == Stack.current) || (!isInServiceRoutine))
            {
                // no return value since now executing something else
                if (yield) return State.Yield;
                return State.NONE_RETURN;
            }

            afterYield = true;
            // no return value since now executing something else
            if (yield) return State.Yield;
            return State.Success;
        }

        private TreeNode GetDepth()
        {
            return stopAt;
        }

        public override void Initialize()
        {
            // nothing
            afterYield = false;
            behaviourTree.GetNode(ref stopAt);
        }
    }
}
