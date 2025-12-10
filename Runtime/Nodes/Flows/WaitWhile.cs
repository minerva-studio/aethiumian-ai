using Amlos.AI.References;

namespace Amlos.AI.Nodes
{
    public class WaitWhile : Flow
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
                SetNextExecute(condition);
                return State.Yield;
            }
            else
            {
                return State.Success;
            }
        }

        public override void Initialize()
        {
            behaviourTree.GetNode(ref condition);
        }
    }
}