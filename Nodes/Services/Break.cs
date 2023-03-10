using Amlos.AI.References;
using System;

namespace Amlos.AI.Nodes
{

    [Serializable]
    [NodeTip("Break the ongoing process by given condition")]
    public sealed class Break : RepeatService
    {
        public enum ReturnType
        {
            self,
            parent,
        }

        public ReturnType returnTo;
        public NodeReference condition;

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (!@return)
            {
                return State.Failed;
            }

            // end current service first then jump
            behaviourTree.EndService(this);
            TreeNode until = parent;
            if (returnTo == ReturnType.parent) until = until?.parent;
            behaviourTree.Break(until);
            return State.Success;
        }

        public override State Execute()
        {
            return SetNextExecute(condition);
        }

        public override void Initialize()
        {
            condition = behaviourTree.References[condition];
        }
    }
}
