using Aethiumian.AI.References;

namespace Aethiumian.AI.Nodes
{
    public class WaitUntil : Flow
    {
        public NodeReference condition;

        public override State Execute()
        {
            if (condition is not null && condition.HasReference)
                return SetNextExecute(condition);
            return HandleException(InvalidNodeException.ReferenceIsRequired(nameof(condition), this));
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (@return)
            {
                return State.Success;
            }
            else
            {
                return State.Yield;
            }
        }

        public override void Initialize()
        {
            behaviourTree.GetNode(ref condition);
        }

        public override bool EditorCheck(BehaviourTreeData tree)
        {
            return condition.HasReference;
        }
    }
}
