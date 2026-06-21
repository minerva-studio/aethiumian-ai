using Aethiumian.AI.References;

namespace Aethiumian.AI.Nodes
{
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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
